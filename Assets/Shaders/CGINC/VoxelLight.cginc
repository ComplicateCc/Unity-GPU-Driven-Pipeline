#ifndef __VOXELLIGHT_INCLUDE__
#define __VOXELLIGHT_INCLUDE__

#define XRES 32
#define YRES 16
#define ZRES 64
#define VOXELSIZE uint3(XRES, YRES, ZRES)
#define MAXLIGHTPERCLUSTER 8
            struct PointLight{
                float3 lightColor;
                float lightIntensity;
                float4 sphere;
                int shadowIndex;
            };
float3 _CameraForward;
float3 _CameraNearPos;
float3 _CameraFarPos;
uint _PointLightCount;

inline uint GetIndex(uint3 id, const uint3 size){
    const uint3 multiValue = uint3(1, size.x, size.x * size.y) * (MAXLIGHTPERCLUSTER + 1);
    return dot(id, multiValue);
}
Texture2D<float4> _RandomTex; SamplerState sampler_RandomTex;
float4 _RandomNumber;
float4 _RandomWeight;

inline float2 getSeed(float2 uv)
{
    return float2(dot(_RandomTex.Sample(sampler_RandomTex, float2(uv.x, 0)), _RandomWeight), dot(_RandomTex.Sample(sampler_RandomTex, float2(uv.y, 0)), _RandomWeight));
}

inline float rand(float3 co){
    return frac(sin(dot(co,_RandomNumber.xyz)) * _RandomNumber.w);
}

#endif