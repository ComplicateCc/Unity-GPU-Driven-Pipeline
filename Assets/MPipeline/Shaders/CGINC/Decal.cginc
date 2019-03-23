#ifndef __DECAL_INCLUDE__
#define __DECAL_INCLUDE__
#define MAX_DECAL_PER_CLUSTER 16
struct DecalData
{
    float3x3 rotation;
    float3 position;
    float3 extent;
    uint texIndex;
};
#endif