Shader "Unlit/Reflection"
{

CGINCLUDE
#include "UnityCG.cginc"
#include "CGINC/VoxelLight.cginc"
#define _CameraDepthTexture _
#include "UnityDeferredLibrary.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardBRDF.cginc"
#include "UnityPBSLighting.cginc"
#include "CGINC/Reflection.cginc"
#pragma multi_compile ___ UNITY_HDR_ON
#pragma target 5.0
#undef _CameraDepthTexture

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
                float3 normal = normalize(_CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv).xyz * 2 - 1);
                float occlusion = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv).w;
                float3 eyeVec = normalize(worldPos.xyz - _WorldSpaceCameraPos);
                float3 finalColor = 0;
                float4 specular = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
                float lod = (1 - specular.w) * 9.998;

                
                half oneMinusReflectivity = 1 - SpecularStrength(specular.xyz);
                [loop]
                for(int a = 1; a < target; ++a)
                {
                    int currentIndex = _ReflectionIndices[index + a];
                    ReflectionData data = _ReflectionData[currentIndex];
                    float3 leftDown = data.position - data.maxExtent;
                    float3 cubemapUV = (worldPos.xyz - leftDown) / (data.maxExtent * 2);
                    if(abs(dot(cubemapUV - saturate(cubemapUV), 1)) > 1e-13) continue;
                    UnityGIInput d;
                    d.worldPos = worldPos.xyz;
                    d.worldViewDir = -eyeVec;
                    d.probeHDR[0] = data.hdr;
                    if(data.boxProjection > 0)
                    {
                        d.probePosition[0]  = float4(data.position, 1);
                        d.boxMin[0].xyz     = leftDown;
                        d.boxMax[0].xyz     = (data.position + data.maxExtent);
                    }
                    Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(specular.w, d.worldViewDir, normal, specular.xyz);
                    //TODO
                    //data has been defined in Reflection.cginc
                    UnityLight light;
                    light.color = half3(0, 0, 0);
                    light.dir = half3(0, 1, 0);
                    UnityIndirect ind;
                    ind.diffuse = 0;
                    ind.specular = MPipelineGI_IndirectSpecular(d, occlusion, g, data, currentIndex, lod);
                    half3 rgb = BRDF1_Unity_PBS (0, specular.xyz, oneMinusReflectivity, specular.w, normal, -eyeVec, light, ind).rgb;
                    float3 distanceToMin = saturate((abs(worldPos.xyz - data.position) - data.minExtent) / data.blendDistance);
                    finalColor = lerp(rgb, finalColor, max(distanceToMin.x, max(distanceToMin.y, distanceToMin.z)));
                }
                return finalColor;
            }
            ENDCG
        }
    }
}
