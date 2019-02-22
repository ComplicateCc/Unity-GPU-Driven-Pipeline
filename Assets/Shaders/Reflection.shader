Shader "Unlit/Reflection"
{

CGINCLUDE
#include "UnityCG.cginc"
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Reflection.cginc"
#pragma target 5.0
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
            float4x4 _InvVP;    //Inverse View Project Matrix
            float3 _Size;
            TextureCube<half4> _ReflectionProbe; SamplerState sampler_ReflectionProbe;
            Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;       //RGB Diffuse A AO
            Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;       //RGB Specular A Smoothness
            Texture2D<half3> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;       //RGB Normal
            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            
            float2 _CameraClipDistance; //X: Near Y: Far - Near
            StructuredBuffer<uint> _ReflectionIndices;
            StructuredBuffer<ReflectionData> _ReflectionData;
ENDCG
    SubShader
    {
                    Cull off ZWrite off ZTest Always
            Blend one one
        Tags { "RenderType"="Opaque" }
        LOD 100
//Pass 0 Regular Projection
        Pass
        {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            float3 frag (v2f i) : SV_Target
            {
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
                float4 worldPos = mul(_InvVP, float4(i.uv * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                float rate = saturate((LinearEyeDepth(depth) - _CameraClipDistance.x) / _CameraClipDistance.y);
                float3 uv = float3(i.uv, rate);
                uint3 intUV = uv * float3(XRES, YRES, ZRES);
                int index = DownDimension(intUV, uint2(XRES, YRES), MAXIMUM_PROBE + 1);
                int target = _ReflectionIndices[index];
                float3 normal = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv).xyz * 2 - 1;
                float3 finalColor = 0;
                float4 specular = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
                float importance = 0;
                [loop]
                for(int a = 1; a < target; ++a)
                {
                    int currentIndex = _ReflectionIndices[index + a];
                    ReflectionData data = _ReflectionData[currentIndex];
                    float3 leftDown = data.position - data.extent;
                    float3 cubemapUV = (worldPos.xyz - leftDown) / (data.extent * 2);
                    if(abs(dot(cubemapUV - saturate(cubemapUV), 1)) > 0.001) continue;
                    //TODO
                    //data has been defined in Reflection.cginc
                    importance += data.importance;
                    float3 color = GetColor(currentIndex, normal,(1 - specular.w) * 9.998);
                    color *= data.importance;
                    finalColor += color;
                }
                if(importance < 1e-4) importance = 1;
                finalColor /= importance;
                return finalColor;
            }
            ENDCG
        }
    }
}
