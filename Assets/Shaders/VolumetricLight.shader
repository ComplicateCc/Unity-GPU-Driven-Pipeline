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

#define MARCHSTEP 0.0078125
#pragma multi_compile _ DIRLIGHT
#pragma multi_compile _ DIRLIGHTSHADOW
#pragma multi_compile _ POINTLIGHT
float4x4 _InvVP;
float4x4 _ShadowMapVPs[4];
float4 _ShadowDisableDistance;
float3 _DirLightPos;
float2 _CameraClipDistance; //X: Near Y: Far - Near
float3 _DirLightFinalColor;
TextureCubeArray<half> _CubeShadowMapArray; SamplerState sampler_CubeShadowMapArray;

StructuredBuffer<float3> verticesBuffer;
StructuredBuffer<PointLight> _AllPointLight;
StructuredBuffer<uint> _PointLightIndexBuffer;
Texture2DArray<float> _DirShadowMap; SamplerState sampler_DirShadowMap;
Texture2D<float> _DownSampledDepth; SamplerState sampler_DownSampledDepth;

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
				float fadeDistance = saturate( (_ShadowDisableDistance.w - eyeDistance) / (_ShadowDisableDistance.w * 0.05));
				atten = lerp(1, atten, fadeDistance);
				return atten;
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

 Pass
        {
            Cull off ZWrite off ZTest Always
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            
            half4 frag(v2fScreen i) : SV_TARGET
            {
                float2 uv = i.uv;
                float sceneDepth = _DownSampledDepth.Sample(sampler_DownSampledDepth, uv);
                float2 projCoord = uv * 2 - 1;
                float4 worldPosition = mul(_InvVP, float4(projCoord, sceneDepth, 1));
                #if UNITY_REVERSED_Z
                float4 worldNearPos = mul(_InvVP, float4(projCoord, 1, 1));
                #else
                float4 worldNearPos = mul(_InvVP, float4(projCoord, 0, 1));
                #endif
                worldPosition /= worldPosition.w;
                worldNearPos /= worldNearPos.w;
                float linearDepth = LinearEyeDepth(sceneDepth);
                uint2 xyVox = (uint2)(uv * float2(XRES, YRES));
                uint curZ = -1;
                uint2 ind;
                float3 color = 0;
                float2 randomSeed = getSeed(i.uv);
                [loop]
                for(float aa = MARCHSTEP; aa < 1; aa += MARCHSTEP)
                {
                    float4 currentPos = lerp(float4(worldNearPos.xyz, _CameraClipDistance.x), float4(worldPosition.xyz, linearDepth), aa + MARCHSTEP * rand(float3(randomSeed, aa)));//xyz: worldPos w: linearDepth
                    #if DIRLIGHT
                    #if DIRLIGHTSHADOW
                    color += _DirLightFinalColor * GetHardShadow(currentPos.xyz, currentPos.w);
                    #else
                    color += _DirLightFinalColor;
                    #endif
                    #endif
                    
                    #if POINTLIGHT
                    float rate = saturate((currentPos.w - _CameraClipDistance.x) / _CameraClipDistance.y);
                    uint newZ = (uint)(rate * ZRES);
                    if(curZ != newZ){
                        curZ = newZ;
                        uint sb = GetIndex(uint3(xyVox, newZ), VOXELSIZE);
                        ind = uint2(sb + 1, _PointLightIndexBuffer[sb]);
                    }
                    for(uint c = ind.x; c < ind.y; c++)
                    {
                        PointLight pt = _AllPointLight[_PointLightIndexBuffer[c]];
                        float3 currentCol = pt.lightColor * saturate(1 - distance(currentPos.xyz , pt.sphere.xyz) / pt.sphere.w);
                        if(pt.shadowIndex >= 0){
                            float3 lightDir = pt.sphere.xyz - currentPos.xyz;
                            half lenOfLightDir = length(lightDir);
                            half shadowDist = _CubeShadowMapArray.Sample(sampler_CubeShadowMapArray, float4(lightDir * float3(-1,-1,1), pt.shadowIndex));
                            half lightDist = lenOfLightDir / pt.sphere.w;
                            currentCol *= lightDist <= shadowDist;
                        }
                        //TODO
                        //color integration
                        color += currentCol;
                    }
                    #endif
                }
                color *= MARCHSTEP;
                return float4(color, 1);
            }
            ENDCG
        }
        //Pass 1: Down Sampler
        Pass
        {
            Cull off ZWrite off ZTest Always
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            Texture2D<float> _OriginMap; SamplerState sampler_OriginMap;
            float4 _OriginMap_TexelSize;
            #define Samp(uv) (_OriginMap.Sample(sampler_OriginMap, uv))
            float frag(v2fScreen i) : SV_TARGET
            {
                float2 offset = _OriginMap_TexelSize.xy * 0.5;
                float4 values = float4(Samp(i.uv + offset), Samp(i.uv + offset * float2(1, -1)), Samp(i.uv + offset * float2(-1, 1)), Samp(i.uv + offset * -1));
                #if UNITY_REVERSED_Z
                float nearPoint = max(values.x, values.y);
                nearPoint = max(nearPoint, values.z);
                return max(nearPoint, values.w);
                #else
                float nearPoint = min(values.x, values.y);
                nearPoint = min(nearPoint, values.z);
                return min(nearPoint, values.w);
                #endif
            }
            ENDCG
        }

        Pass
        {
            Cull off ZWrite off ZTest Always
            Blend srcAlpha OneminusSrcAlpha
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            Texture2D<half3> _VolumeTex; SamplerState sampler_VolumeTex;
            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            half4 frag(v2fScreen i) : SV_TARGET
            {
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv);
                float4 worldPos = mul(_InvVP, float4(i.uv * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                float dist = distance(_WorldSpaceCameraPos, worldPos.xyz);
                return float4(_VolumeTex.Sample(sampler_VolumeTex, i.uv), saturate(1 - exp(-dist * 0.05)));
            }
            ENDCG
        }
    }
}