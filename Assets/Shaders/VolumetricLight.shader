Shader "Hidden/VolumetricLight"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

CGINCLUDE
#pragma target 5.0
#include "CGINC/VoxelLight.cginc"
#include "UnityCG.cginc"

#pragma multi_compile _ DIRLIGHT
#pragma multi_compile _ DIRLIGHTSHADOW
#pragma multi_compile _ POINTLIGHT
float4x4 _InvVP;
float4x4 _ShadowMapVPs[4];
float4 _ShadowDisableDistance;
float3 _DirLightPos;
float2 _CameraClipDistance; //X: Near Y: Far - Near
float3 _DirLightFinalColor;
float _MaxDistance;
uint _MarchStep;
float2 _ScreenSize;

TextureCubeArray<half> _CubeShadowMapArray; SamplerState sampler_CubeShadowMapArray;
Texture2D<float> _MainTex; SamplerState sampler_MainTex;
StructuredBuffer<PointLight> _AllPointLight;
StructuredBuffer<uint> _PointLightIndexBuffer;
Texture2DArray<float> _DirShadowMap; SamplerState sampler_DirShadowMap;
Texture2D<half> _DownSampledDepth; SamplerState sampler_DownSampledDepth;   float4 _DownSampledDepth_TexelSize;
Texture3D<half3> _VolumeTex;
Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;

			float GetHardShadow(float3 worldPos, float eyeDistance)
			{
				float4 eyeRange = eyeDistance < _ShadowDisableDistance;
				eyeRange.yzw -= eyeRange.xyz;
				float zAxisUV = dot(eyeRange, float4(0, 1, 2, 3));
				float4x4 vpMat = _ShadowMapVPs[zAxisUV];
				float4 shadowPos = mul(vpMat, float4(worldPos, 1));
				half2 shadowUV = shadowPos.xy / shadowPos.w;
				shadowUV = shadowUV * 0.5 + 0.5;
				#if UNITY_REVERSED_Z
				float dist = 1 - shadowPos.z;
				#else
				float dist = shadowPos.z;
				#endif
				float atten = dist < _DirShadowMap.Sample(sampler_DirShadowMap, half3(shadowUV, zAxisUV));
				return atten;
			}

            inline float EyeDepthToProj(float lin)
            {
                return (1/lin - _ZBufferParams.w) / _ZBufferParams.z;
            }

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
            v2fScreen screenVert (appdata v)
            {
                v2fScreen o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

ENDCG
//Pass 0: Down Sampler
        Pass
        {
            Cull off ZWrite off ZTest Always
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float4 _MainTex_TexelSize;
            #define Samp(uv) (_MainTex.Sample(sampler_MainTex, uv))
            float frag(v2fScreen i) : SV_TARGET
            {
                float2 offset = _MainTex_TexelSize.xy * 0.5;
                float4 depth = float4(Samp(i.uv + offset), Samp(i.uv + offset * float2(1, -1)), Samp(i.uv + offset * float2(-1, 1)), Samp(i.uv + offset * -1));
                #if UNITY_REVERSED_Z
                float maxDepth =  min(min(depth.x, depth.y), min(depth.z, depth.w));
                #else
			    float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));
                #endif
                return maxDepth;
            }
            ENDCG
        }
//Pass 1: Down Sample and To linear
        Pass
        {
            Cull off ZWrite off ZTest Always
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float4 _MainTex_TexelSize;
            #define Samp(uv) (_MainTex.Sample(sampler_MainTex, uv))
            float frag(v2fScreen i) : SV_TARGET
            {
                float2 offset = _MainTex_TexelSize.xy * 0.5;
                float4 depth = float4(Samp(i.uv + offset), Samp(i.uv + offset * float2(1, -1)), Samp(i.uv + offset * float2(-1, 1)), Samp(i.uv + offset * -1));
			    #if UNITY_REVERSED_Z
                float maxDepth =  min(min(depth.x, depth.y), min(depth.z, depth.w));
                #else
			    float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));
                #endif
                return min(_MaxDistance, LinearEyeDepth(maxDepth));
            }
            ENDCG
        }

        //Pass 2: Accumulate
        pass
        {
            Cull off ZWrite off ZTest Always
            Blend srcAlpha oneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float4 frag(v2fScreen i) : SV_TARGET
            {
                uint2 ind = i.uv * _ScreenSize;
                float3 color = 0;
                for(int a = 0; a < _MarchStep; ++a)
                {
                    color += _VolumeTex[uint3(ind, a)];
                }
                color /= _MarchStep;
                return float4(color, LinearEyeDepth(_CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv)) * 0.03);
            }
            ENDCG
        }
    }
}