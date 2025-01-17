﻿#include "GTAO_Common.cginc"

//////Resolve Pass
void ResolveGTAO_frag(PixelInput IN, out float2 AO : SV_Target0, out float3 BentNormal : SV_Target1)
{
	float2 uv = IN.uv.xy;

	float Depth = 0;
	float4 GT_Details = GTAO(uv, (int)_AO_DirSampler, (int)_AO_SliceSampler, Depth);

	AO = float2(GT_Details.a, Depth);
	BentNormal = mul((float3x3)_CameraToWorldMatrix, float3(GT_Details.rg, -GT_Details.b));
} 

//////Spatial filter
float2 SpatialGTAO_X_frag(PixelInput IN) : SV_Target
{
	float2 uv = IN.uv.xy;
	float2 AO = BilateralBlur(uv, float2(1 / _ScreenParams.x, 0));
	return AO;
} 

float2 SpatialGTAO_Y_frag(PixelInput IN) : SV_Target
{
	float2 uv = IN.uv.xy;
	float2 AO = BilateralBlur(uv, float2(0, 1 / _ScreenParams.y));

	//////Reflection Occlusion
	float3 bentNormal = tex2D(_BentNormal_Texture, uv).rgb;
	float3 worldNormal = normalize(tex2D(_CameraGBufferTexture2, uv).rgb * 2 - 1);
	float4 Specular = tex2D(_CameraGBufferTexture1, uv);
	float Roughness = 1 - Specular.a;

	float Depth = tex2D(_DownSampledDepthTexture, uv).r;
	float4 worldPos = mul(_Inverse_View_ProjectionMatrix, float4(float3(uv * 2 - 1, Depth), 1));
	worldPos.xyz /= worldPos.w;

	float3 viewDir= normalize(worldPos.xyz - _WorldSpaceCameraPos.rgb);
	float3 reflectionDir = reflect(viewDir, worldNormal);
	float GTRO = ReflectionOcclusion(bentNormal, reflectionDir, Roughness, 0.5);

	return lerp(1, float2(AO.r, GTRO), _AO_Intensity);
} 

//////Temporal filter
float4 TemporalGTAO_frag(PixelInput IN) : SV_Target
{
	float2 uv = IN.uv.xy; 
	float2 velocity = tex2D(_CameraMotionVectorsTexture, uv);
	float2 prevUV = uv - velocity;
	float4 filterColor = 0;
	float4 minColor, maxColor;
	ResolverAABB(_GTAO_Spatial_Texture, 0, 0, _AO_TemporalScale, uv, _AO_RT_TexelSize.zw, minColor, maxColor, filterColor);

	float4 currColor = filterColor;
	if(abs(dot(prevUV - saturate(prevUV), 1)) > 1e-5) return currColor;
	float4 lastColor = tex2D(_PrevRT, prevUV);
	lastColor = clamp(lastColor, minColor, maxColor);
	
	float weight = clamp(_AO_TemporalResponse, 0, 0.98);

	float4 temporalColor = lerp(currColor, lastColor, weight);
	return temporalColor;
}