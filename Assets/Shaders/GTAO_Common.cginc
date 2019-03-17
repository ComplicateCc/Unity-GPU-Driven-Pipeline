#include "UnityCG.cginc"
#include "CGINC/Shader_Include/Include_HLSL.hlsl"
#include "CGINC/Random.cginc"

#define KERNEL_RADIUS 8
int _AO_MultiBounce;
float _AO_DirSampler, _AO_SliceSampler, _AO_Intensity, _AO_Radius, _AO_Power, _AO_Sharpeness, _AO_TemporalScale, _AO_TemporalResponse, _AO_HalfProjScale, _AO_TemporalOffsets, _AO_TemporalDirections;
float2 _AO_FadeParams;
float4	_AO_UVToView, _AO_RT_TexelSize, _AO_FadeValues;
float4x4	_WorldToCameraMatrix, _CameraToWorldMatrix, _ProjectionMatrix, _LastFrameViewProjectionMatrix, _View_ProjectionMatrix, _Inverse_View_ProjectionMatrix;
sampler2D _CameraGBufferTexture0, _CameraGBufferTexture1, _CameraGBufferTexture2, _CameraMotionVectorsTexture, _CameraDepthTexture, _BentNormal_Texture, _GTAO_Texture, _GTAO_Spatial_Texture, _PrevRT, _CurrRT;
struct VertexInput
{
	float4 vertex : POSITION;
	float4 uv : TEXCOORD0;
};

struct PixelInput
{
	float4 vertex : SV_POSITION;
	float4 uv : TEXCOORD0;
};

PixelInput vert(VertexInput v)
{
	PixelInput o;
	o.vertex = v.vertex;
	o.uv = v.uv;
	return o;
}

//---//---//----//----//-------//----//----//----//-----//----//-----//----//----MultiBounce & ReflectionOcclusion//---//---//----//----//-------//----//----//----//-----//----//-----//----//----
inline float ApproximateConeConeIntersection(float ArcLength0, float ArcLength1, float AngleBetweenCones)
{
	float AngleDifference = abs(ArcLength0 - ArcLength1);

	float Intersection = smoothstep(0, 1, 1 - saturate((AngleBetweenCones - AngleDifference) / (ArcLength0 + ArcLength1 - AngleDifference)));

	return Intersection;
}

inline float ReflectionOcclusion(float3 BentNormal, float3 ReflectionVector, float Roughness, float OcclusionStrength)
{
	float BentNormalLength = length(BentNormal);
	float ReflectionConeAngle = max(Roughness, 0.1) * PI;
	float UnoccludedAngle = BentNormalLength * PI * OcclusionStrength;

	float AngleBetween = acos(dot(BentNormal, ReflectionVector) / max(BentNormalLength, 0.001));
	float ReflectionOcclusion = ApproximateConeConeIntersection(ReflectionConeAngle, UnoccludedAngle, AngleBetween);
	ReflectionOcclusion = lerp(0, ReflectionOcclusion, saturate((UnoccludedAngle - 0.1) / 0.2));
	return ReflectionOcclusion;
}

inline float ReflectionOcclusion_Approch(float NoV, float Roughness, float AO)
{
	return saturate(pow(NoV + AO, Roughness * Roughness) - 1 + AO);
}

inline float3 MultiBounce(float AO, float3 Albedo)
{
	float3 A = 2 * Albedo - 0.33;
	float3 B = -4.8 * Albedo + 0.64;
	float3 C = 2.75 * Albedo + 0.69;
	return max(AO, ((AO * A + B) * AO + C) * AO);
}


//---//---//----//----//-------//----//----//----//-----//----//-----//----//----BilateralBlur//---//---//----//----//-------//----//----//----//-----//----//-----//----//----
inline void FetchAoAndDepth(float2 uv, inout float ao, inout float depth) {
	float2 aod = tex2Dlod(_GTAO_Texture, float4(uv, 0, 0)).rga;
	ao = aod.r;
	depth = aod.g;
}

inline float CrossBilateralWeight(float r, float d, float d0) {
	const float BlurSigma = (float)KERNEL_RADIUS * 0.5;
	const float BlurFalloff = 1 / (2 * BlurSigma * BlurSigma);

    float dz = (d0 - d) * _ProjectionParams.z * _AO_Sharpeness;
	return exp2(-r * r * BlurFalloff - dz * dz);
}

inline void ProcessSample(float2 aoz, float r, float d0, inout float totalAO, inout float totalW) {
	float w = CrossBilateralWeight(r, d0, aoz.y);
	totalW += w;
	totalAO += w * aoz.x;
}

inline void ProcessRadius(float2 uv0, float2 deltaUV, float d0, inout float totalAO, inout float totalW) {
	float ao, z;
	float2 uv;
	float r = 1;

	UNITY_UNROLL
	for (; r <= KERNEL_RADIUS / 2; r += 1) {
		uv = uv0 + r * deltaUV;
		FetchAoAndDepth(uv, ao, z);
		ProcessSample(float2(ao, z), r, d0, totalAO, totalW);
	}

	UNITY_UNROLL
	for (; r <= KERNEL_RADIUS; r += 2) {
		uv = uv0 + (r + 0.5) * deltaUV;
		FetchAoAndDepth(uv, ao, z);
		ProcessSample(float2(ao, z), r, d0, totalAO, totalW);
	}
		
}

inline float2 BilateralBlur(float2 uv0, float2 deltaUV)
{
	float totalAO, depth;
	FetchAoAndDepth(uv0, totalAO, depth);
	float totalW = 1;
		
	ProcessRadius(uv0, -deltaUV, depth, totalAO, totalW);
	ProcessRadius(uv0, deltaUV, depth, totalAO, totalW);

	totalAO /= totalW;
	return float2(totalAO, depth);
}


//---//---//----//----//-------//----//----//----//-----//----//-----//----//----GTAO//---//---//----//----//-------//----//----//----//-----//----//-----//----//----
inline float ComputeDistanceFade(const float distance)
{
	return saturate(max(0, distance - _AO_FadeParams.x) * _AO_FadeParams.y);
}

inline float3 GetPosition(float2 uv)
{
	float depth = tex2Dlod(_CameraDepthTexture, float4(uv, 0, 0)).r; 
	float viewDepth = LinearEyeDepth(depth);
	return float3((uv * _AO_UVToView.xy + _AO_UVToView.zw) * viewDepth, viewDepth);
}

inline float3 GetNormal(float2 uv)
{
	float3 Normal = tex2D(_CameraGBufferTexture2, uv).rgb * 2 - 1; 
	float3 view_Normal = normalize(mul((float3x3) _WorldToCameraMatrix, Normal));

	return float3(view_Normal.xy, -view_Normal.z);
}

inline float GTAO_Offsets(float2 uv)
{
	int2 position = (int2)(uv * _AO_RT_TexelSize.zw);
	return 0.25 * (float)((position.y - position.x) & 3);
}

float IntegrateArc_UniformWeight(float2 h)
{
	float2 Arc = 1 - cos(h);
	return Arc.x + Arc.y;
}

float IntegrateArc_CosWeight(float2 h, float n)
{
    float2 Arc = -cos(2 * h - n) + cos(n) + 2 * h * sin(n);
    return 0.25 * (Arc.x + Arc.y);
}

float4 GTAO(float2 uv, int NumCircle, int NumSlice, inout float Depth)
{
	float3 vPos = GetPosition(uv);
	float3 viewNormal = GetNormal(uv);
	float3 viewDir = normalize(0 - vPos);

	float2 radius_thickness = lerp(float2(_AO_Radius, 1), _AO_FadeValues.yw, ComputeDistanceFade(vPos.b).xx);
	float radius = radius_thickness.x;
	float thickness = radius_thickness.y;

	float stepRadius = max(min((radius * _AO_HalfProjScale) / vPos.b, 512), (float)NumSlice);
	stepRadius /= ((float)NumSlice + 1);
	float2 rand = cellNoise(uv) * 0.25 + 0.25;
	float noiseOffset = GTAO_Offsets(uv) ;
	float noiseDirection = rand.x + rand.y;

	float initialRayStep = frac(noiseOffset + _AO_TemporalOffsets);

	float Occlusion, angle, bentAngle, wallDarkeningCorrection, projLength, n, cos_n;
	float2 slideDir_TexelSize, h, H, falloff, uvOffset, dsdt, dsdtLength;
	float3 sliceDir, ds, dt, planeNormal, tangent, projectedNormal, BentNormal;
	float4 uvSlice;

	UNITY_LOOP
	for (int i = 0; i < NumCircle; i++)
	{
		angle = (i + noiseDirection + _AO_TemporalDirections) * (UNITY_PI / (float)NumCircle);
		sliceDir = float3(float2(cos(angle), sin(angle)), 0);
		slideDir_TexelSize = sliceDir.xy * _AO_RT_TexelSize.xy;
		h = -1;

		UNITY_LOOP
		for (int j = 0; j < NumSlice; j++)
		{
			uvOffset = slideDir_TexelSize * max(stepRadius * (j + initialRayStep), 1 + j);
			uvSlice = uv.xyxy + float4(uvOffset.xy, -uvOffset);

			ds = GetPosition(uvSlice.xy) - vPos;
			dt = GetPosition(uvSlice.zw) - vPos;

			dsdt = float2(dot(ds, ds), dot(dt, dt));
			dsdtLength = rsqrt(dsdt);

			falloff = saturate(dsdt.xy * (2 / pow2(radius)));

			H = float2(dot(ds, viewDir), dot(dt, viewDir)) * dsdtLength;
			h.xy = (H.xy > h.xy) ? lerp(H, h, falloff) : lerp(H.xy, h.xy, thickness);
		}

		planeNormal = normalize(cross(sliceDir, viewDir));
		tangent = cross(viewDir, planeNormal);
		projectedNormal = viewNormal - planeNormal * dot(viewNormal, planeNormal);
		projLength = length(projectedNormal);

		cos_n = clamp(dot(normalize(projectedNormal), viewDir), -1, 1);
		n = -sign(dot(projectedNormal, tangent)) * acos(cos_n);

		h = acos(clamp(h, -1, 1));
		h.x = n + max(-h.x - n, -UNITY_HALF_PI);
		h.y = n + min(h.y - n, UNITY_HALF_PI);

		bentAngle = (h.x + h.y) * 0.5;

		BentNormal += viewDir * cos(bentAngle) - tangent * sin(bentAngle);
		Occlusion += projLength * IntegrateArc_CosWeight(h, n); 			
		//Occlusion += projLength * IntegrateArc_UniformWeight(h);			
	}

	BentNormal = normalize(normalize(BentNormal) - viewDir * 0.5);
	Occlusion = saturate(pow(Occlusion / (float)NumCircle, _AO_Power));
	Depth = vPos.b;

	return float4(BentNormal, Occlusion);
}
