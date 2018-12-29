#ifndef __TERRAIN_INCLUDE__
#define __TERRAIN_INCLUDE__
struct TerrainVertex
{
    float2 uv;
    int2 vertexIndex;
    float2 localPos;
};

struct TerrainPanel
{
    float3 extent;
    float3 position;
    int4 textureIndex;
    int heightMapIndex;
};

uint _CullingPlaneCount;
shared float4 planes[6];
float PlaneTest(float3 position, float3 extent){
    float result = 1;
    for(uint i = 0; i < _CullingPlaneCount; ++i)
    {
        float4 plane = planes[i];
        float3 absNormal = abs(plane.xyz);
        result *= ((dot(position, plane.xyz) - dot(absNormal, extent)) < -plane.w) ? 1.0 : 0.0;
    }
    return result;
}
#endif