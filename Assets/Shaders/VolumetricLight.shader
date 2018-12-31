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
float _MarchStep;

TextureCubeArray<half> _CubeShadowMapArray; SamplerState sampler_CubeShadowMapArray;

StructuredBuffer<PointLight> _AllPointLight;
StructuredBuffer<uint> _PointLightIndexBuffer;
Texture2DArray<float> _DirShadowMap; SamplerState sampler_DirShadowMap;
Texture2D<half> _DownSampledDepth; SamplerState sampler_DownSampledDepth;   float4 _DownSampledDepth_TexelSize;
RWTexture3D<half3> _VolumeTex; 

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
//Pass 0: Raymarch
 Pass
        {
            Cull off ZWrite off ZTest Always
            Blend zero one
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float _MaxDistance;
            
            half4 frag(v2fScreen i) : SV_TARGET
            {
                float2 randomSeed = getSeed(i.uv);
                float2 uv = i.uv;
                //TODO
                //Transform magic number
                float sceneDepth = _DownSampledDepth.Sample(sampler_DownSampledDepth, uv);
                float linearDepth = min(_MaxDistance, LinearEyeDepth(sceneDepth));
                float2 projCoord = uv * 2 - 1;
                #if UNITY_REVERSED_Z
                float4 worldNearPos = mul(_InvVP, float4(projCoord, 1, 1));
                #else
                float4 worldNearPos = mul(_InvVP, float4(projCoord, 0, 1));
                #endif
                worldNearPos /= worldNearPos.w;
                float4 targetWorldPos = mul(_InvVP, float4(projCoord, EyeDepthToProj(linearDepth), 1));
                targetWorldPos /= targetWorldPos.w;
                uint2 xyVox = (uint2)(i.uv * float2(XRES, YRES));
                uint curZ = -1;
                uint2 ind;
                uint voxelStep = 0;
                const float step = 1 / _MarchStep;
                [loop]
                for(float aa = 0; aa < 1; aa += step)
                {
                    float3 color = 0;//TODO: Ambient Light
                    float4 currentPos = lerp(float4(worldNearPos.xyz, _CameraClipDistance.x), float4(targetWorldPos.xyz, linearDepth), aa + step * rand(float3(randomSeed, aa)));//xyz: worldPos w: linearDepth
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
                        color += currentCol;
                    }
                    #endif
                    _VolumeTex[uint3(i.vertex.xy, voxelStep)] = color;
                    voxelStep++;
                }
                return 0;
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
            Texture2D<float> _MainTex; SamplerState sampler_MainTex;
            float4 _MainTex_TexelSize;
            #define Samp(uv) (_MainTex.Sample(sampler_MainTex, uv))
            float frag(v2fScreen i) : SV_TARGET
            {
                float2 offset = _MainTex_TexelSize.xy * 0.5;
                float4 depth = float4(Samp(i.uv + offset), Samp(i.uv + offset * float2(1, -1)), Samp(i.uv + offset * float2(-1, 1)), Samp(i.uv + offset * -1));
			    float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));
                return maxDepth;
            }
            ENDCG
        }

        //Pass 2: Accumulate
        pass
        {
            Cull off ZWrite off ZTest Always
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float3 frag(v2fScreen i) : SV_TARGET
            {
                float3 color = 0;
                for(int a = 0; a < _MarchStep; ++a)
                {
                    color += _VolumeTex[uint3(i.vertex.xy, a)];
                }
                color /= _MarchStep;
                return color;
            }
            ENDCG
        }
    }
}