#ifndef __DECALSHADING_INCLUDE__
#define __DECALSHADING_INCLUDE__
#include "Decal.cginc"
#include "VoxelLight.cginc"
StructuredBuffer<uint> _DecalCountBuffer;
StructuredBuffer<DecalData> _DecalBuffer;
Texture2D<float4> _DecalAtlas; SamplerState sampler_DecalAtlas;
void CalculateDecal(float2 uv, float linearDepth, float3 worldPos, out float4 color)
{
    float zdepth = ((linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y);
    uint3 clusterValue = (uint3)(float3(uv, zdepth) * uint3(XRES, YRES, ZRES));

    uint startIndex = From3DTo1D(clusterValue, uint2(XRES, YRES)) * (MAX_DECAL_PER_CLUSTER + 1);
    uint count = _DecalCountBuffer[startIndex];
    color = 0;
    [loop]
    for(uint i = 1; i < count; ++i)
    {
        DecalData data = _DecalBuffer[_DecalCountBuffer[i + startIndex]];
        float3 localPos = worldPos - data.position;
        localPos = mul(localPos, data.rotation);
        float2 lp = localPos.xz + 0.5;
        if(dot(abs(lp - saturate(lp)), 1) > 1e-5) continue;
        float2 uv = lerp(data.startUV, data.endUV, lp);
        color = _DecalAtlas.Sample(sampler_DecalAtlas, uv);
        return;
    }
}
#endif