Shader "Hidden/DeferredPointLight"
{
	SubShader
	{

CGINCLUDE
#pragma target 5.0
			#pragma multi_compile _ POINTLIGHT
			#pragma multi_compile _ SPOTLIGHT
#include "UnityCG.cginc"
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Shader_Include/Common.hlsl"
#include "CGINC/Random.cginc"
#include "CGINC/Shader_Include/BSDF_Library.hlsl"
#include "CGINC/Shader_Include/AreaLight.hlsl"
#include "CGINC/Lighting.cginc"
			Texture2D _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
			Texture2D<float4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
			Texture2D<float4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
			Texture2D<float4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;


			float4x4 _InvVP;

			struct v2fScreen
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};
			v2fScreen screenVert(appdata v)
			{
				v2fScreen o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
			}
ENDCG
		Pass
		{
			Cull off ZWrite Off ZTest Greater
			Blend one one

			CGPROGRAM

			#pragma vertex screenVert
			#pragma fragment frag

			
			float3 frag(v2fScreen i) : SV_TARGET
			{
				float2 uv = i.uv;
				float SceneDepth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
				float linearDepth = LinearEyeDepth(SceneDepth);
				float2 NDC_UV = uv * 2 - 1;
				
				//////Screen Data
				float3 AlbedoColor = _CameraGBufferTexture0.SampleLevel(sampler_CameraGBufferTexture0, uv, 0).rgb;
				float3 WorldNormal = _CameraGBufferTexture2.SampleLevel(sampler_CameraGBufferTexture2, uv, 0).rgb * 2 - 1;
				float4 SpecularColor = _CameraGBufferTexture1.SampleLevel(sampler_CameraGBufferTexture1, uv, 0);
				float Roughness = clamp(1 - SpecularColor.a, 0.02, 1);
				float4 WorldPos = mul(_InvVP, float4(NDC_UV, SceneDepth, 1));
				WorldPos /= WorldPos.w;
				return CalculateLocalLight(uv, WorldPos, linearDepth, AlbedoColor, WorldNormal, SpecularColor, Roughness);
			}
			ENDCG
		}
	}
}