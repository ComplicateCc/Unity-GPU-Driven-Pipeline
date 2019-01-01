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

Texture3D<half4> _VolumeTex; SamplerState sampler_VolumeTex;
Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
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
        pass
        {
            Cull off ZWrite off ZTest Always
            Blend srcAlpha oneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float4 frag(v2fScreen i) : SV_TARGET
            {
                float linearDepth = min(_MaxDistance, LinearEyeDepth(_CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv)));
                const float step = 4.0 / _MarchStep;
                float3 color = 0;
                for(float aa = 0; aa < 1; aa += step)
                {
                    float currentDepth = lerp(_CameraClipDistance.x, linearDepth, aa ) / _MaxDistance;
                    color += _VolumeTex.Sample(sampler_VolumeTex, float3(i.uv, currentDepth)).xyz;
                }
                return float4(color / _MarchStep, saturate(linearDepth * 0.03));
            }
            ENDCG
        }
    }
}