Shader "Hidden/ShadowMask_GPURP"
{
	SubShader
	{

CGINCLUDE
#pragma target 5.0
#define _CameraDepthTexture __
#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardBRDF.cginc"
#undef _CameraDepthTexture
#define UNITY_PASS_DEFERRED
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};
			//16 for mobile
			//32 for console
			//64 for PC
			//#define SAMPLECOUNT 16
			//#define SAMPLECOUNT 32
			#define SAMPLECOUNT 64
static const half2 DirPoissonDisks[64] =
{
	half2 (0.1187053, 0.7951565),
	half2 (0.1173675, 0.6087878),
	half2 (-0.09958518, 0.7248842),
	half2 (0.4259812, 0.6152718),
	half2 (0.3723574, 0.8892787),
	half2 (-0.02289676, 0.9972908),
	half2 (-0.08234791, 0.5048386),
	half2 (0.1821235, 0.9673787),
	half2 (-0.2137264, 0.9011746),
	half2 (0.3115066, 0.4205415),
	half2 (0.1216329, 0.383266),
	half2 (0.5948939, 0.7594361),
	half2 (0.7576465, 0.5336417),
	half2 (-0.521125, 0.7599803),
	half2 (-0.2923127, 0.6545699),
	half2 (0.6782473, 0.22385),
	half2 (-0.3077152, 0.4697627),
	half2 (0.4484913, 0.2619455),
	half2 (-0.5308799, 0.4998215),
	half2 (-0.7379634, 0.5304936),
	half2 (0.02613133, 0.1764302),
	half2 (-0.1461073, 0.3047384),
	half2 (-0.8451027, 0.3249073),
	half2 (-0.4507707, 0.2101997),
	half2 (-0.6137282, 0.3283674),
	half2 (-0.2385868, 0.08716244),
	half2 (0.3386548, 0.01528411),
	half2 (-0.04230833, -0.1494652),
	half2 (0.167115, -0.1098648),
	half2 (-0.525606, 0.01572019),
	half2 (-0.7966855, 0.1318727),
	half2 (0.5704287, 0.4778273),
	half2 (-0.9516637, 0.002725032),
	half2 (-0.7068223, -0.1572321),
	half2 (0.2173306, -0.3494083),
	half2 (0.06100426, -0.4492816),
	half2 (0.2333982, 0.2247189),
	half2 (0.07270987, -0.6396734),
	half2 (0.4670808, -0.2324669),
	half2 (0.3729528, -0.512625),
	half2 (0.5675077, -0.4054544),
	half2 (-0.3691984, -0.128435),
	half2 (0.8752473, 0.2256988),
	half2 (-0.2680127, -0.4684393),
	half2 (-0.1177551, -0.7205751),
	half2 (-0.1270121, -0.3105424),
	half2 (0.5595394, -0.06309237),
	half2 (-0.9299136, -0.1870008),
	half2 (0.974674, 0.03677348),
	half2 (0.7726735, -0.06944724),
	half2 (-0.4995361, -0.3663749),
	half2 (0.6474168, -0.2315787),
	half2 (0.1911449, -0.8858921),
	half2 (0.3671001, -0.7970535),
	half2 (-0.6970353, -0.4449432),
	half2 (-0.417599, -0.7189326),
	half2 (-0.5584748, -0.6026504),
	half2 (-0.02624448, -0.9141423),
	half2 (0.565636, -0.6585149),
	half2 (-0.874976, -0.3997879),
	half2 (0.9177843, -0.2110524),
	half2 (0.8156927, -0.3969557),
	half2 (-0.2833054, -0.8395444),
	half2 (0.799141, -0.5886372)
};
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
			}
			float4 _SoftParam;
			float4x4 _InvVP;
			float4x4 _ShadowMapVPs[4];
			float4 _ShadowDisableDistance;
			float3 _DirLightPos;
			Texture2DArray<float> _DirShadowMap; SamplerState sampler_DirShadowMap;
			Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
			Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
			Texture2D<half4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
			Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
			float3 _DirLightFinalColor;
			#define RANDOM(seed) cos(sin(seed * half2(54.135764, 77.468761) + half2(631.543147, 57.4687)) * half2(657.387478, 86.1653) + half2(65.15686, 15.3574563))
			float GetShadow(inout float4 worldPos, float depth, half2 screenUV)
			{
				worldPos /= worldPos.w;
				float eyeDistance = LinearEyeDepth(depth);
				half4 eyeRange = eyeDistance < _ShadowDisableDistance;
				eyeRange.yzw -= eyeRange.xyz;
				float zAxisUV = dot(eyeRange, half4(0, 1, 2, 3));
				float4x4 vpMat = _ShadowMapVPs[zAxisUV];
				float4 shadowPos = mul(vpMat, worldPos);
				half2 shadowUV = shadowPos.xy;
				shadowUV = shadowUV * 0.5 + 0.5;
				half softValue = dot(_SoftParam, eyeRange);

				#if UNITY_REVERSED_Z
				float dist = 1 - shadowPos.z;
				#else
				float dist = shadowPos.z;
				#endif
				half2 seed = (_ScreenParams.yx * screenUV.yx + screenUV.xy) * _ScreenParams.xy + _Time.zw;
				float atten = 0;
				for(int i = 0; i < SAMPLECOUNT; ++i)
				{
					seed = RANDOM(seed + DirPoissonDisks[i]).yx;
					half2 dir = DirPoissonDisks[i] + seed;
					atten += dist < _DirShadowMap.Sample(sampler_DirShadowMap, half3(shadowUV + dir * softValue, zAxisUV));
				}
				atten /= SAMPLECOUNT;
				float fadeDistance = saturate( (_ShadowDisableDistance.w - eyeDistance) / (_ShadowDisableDistance.w * 0.05));
				atten = lerp(1, atten, fadeDistance);
				return atten;
			}
ENDCG

		Pass
		{
		Cull Off ZWrite Off ZTest Greater
		Blend one one
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag (v2f i) : SV_Target
			{
				half4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
    			half4 gbuffer1 = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
    			half4 gbuffer2 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv);
				float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
				float4 wpos = mul(_InvVP, float4(i.uv * 2 - 1, depth, 1));
				float atten = GetShadow(wpos, depth, i.uv);
				UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);
				float3 eyeVec = normalize(wpos.xyz - _WorldSpaceCameraPos);
				half oneMinusReflectivity = 1 - SpecularStrength(data.specularColor.rgb);
    			UnityIndirect ind;
    			UNITY_INITIALIZE_OUTPUT(UnityIndirect, ind);
    			ind.diffuse = 0;
    			ind.specular = 0;
				UnityLight light;
				light.dir = _DirLightPos;
				light.color = _DirLightFinalColor * atten;
				return UNITY_BRDF_PBS (data.diffuseColor, data.specularColor, oneMinusReflectivity, data.smoothness, data.normalWorld, -eyeVec, light, ind);
			}
			ENDCG
		}

		Pass
		{
		Cull Off ZWrite Off ZTest Greater
		Blend one one
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag (v2f i) : SV_Target
			{
				half4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
    			half4 gbuffer1 = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
    			half4 gbuffer2 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv);
				float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
				float4 wpos = mul(_InvVP, float4(i.uv * 2 - 1, depth, 1));
				wpos /= wpos.w;
				UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);
				float3 eyeVec = normalize(wpos.xyz - _WorldSpaceCameraPos);
				half oneMinusReflectivity = 1 - SpecularStrength(data.specularColor.rgb);
    			UnityIndirect ind;
    			UNITY_INITIALIZE_OUTPUT(UnityIndirect, ind);
    			ind.diffuse = 0;
    			ind.specular = 0;
				UnityLight light;
				light.dir = _DirLightPos;
				light.color = _DirLightFinalColor;
				return UNITY_BRDF_PBS (data.diffuseColor, data.specularColor, oneMinusReflectivity, data.smoothness, data.normalWorld, -eyeVec, light, ind);
			}
			ENDCG
		}
	}
}
