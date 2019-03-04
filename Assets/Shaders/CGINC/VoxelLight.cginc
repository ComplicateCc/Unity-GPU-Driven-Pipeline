#ifndef __VOXELLIGHT_INCLUDE__
#define __VOXELLIGHT_INCLUDE__

#define XRES 32
#define YRES 16
#define ZRES 256
#define VOXELZ 64
#define MAXLIGHTPERCLUSTER 16
#define MAXPOINTLIGHTPERTILE 64
#define MAXSPOTLIGHTPERTILE 64
#define FROXELMAXPOINTLIGHTPERTILE 32
#define FROXELMAXSPOTLIGHTPERTILE 32
#define MAXFOGVOLUMEPERTILE 16

static const uint3 _ScreenSize = uint3(160, 90, 256);
#include "CGINC/Plane.cginc"

#define VOXELSIZE uint3(XRES, YRES, ZRES)


            struct PointLight{
                float3 lightColor;
                float4 sphere;
                int shadowIndex;
            };
            struct SpotLight
            {
                float3 lightColor;
                Cone lightCone;
                float angle;
                float4x4 vpMatrix;
                float smallAngle;
                float nearClip;
                float3 lightRight;
                int shadowIndex;
            };

            struct FogVolume
            {
                float3x3 localToWorld;
                float4x4 worldToLocal;
                float3 position;
                float3 extent;
                float targetVolume;
            };
float3 _CameraForward;
float3 _CameraNearPos;
float3 _CameraFarPos;
float3 _NearFarClip; //x: farClip / availiable distance y: nearclip / availiable distance z: nearClip

inline uint GetIndex(uint3 id, const uint3 size, const int multiply){
    const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
    return dot(id, multiValue);
}

RWStructuredBuffer<uint> _RandomBuffer;
#define NEXTSTATE(state) state ^= state << 13; state ^= state >> 17; state ^= state << 5;
inline uint wang_hash(uint seed)
{
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);
    return seed;
}

inline float3 getRandomFloat3(uint index)
{
    uint3 value = 0;
    value.x = wang_hash(_RandomBuffer[index]);
    value.y = wang_hash(value.x);
    value.z = wang_hash(value.y); 
    return value * (1.0 / 4294967296.0);
}

inline float3 updateRandomFloat3(uint index)
{
    uint3 value = 0;
    value.x = wang_hash(_RandomBuffer[index]);
    value.y = wang_hash(value.x);
    value.z = wang_hash(value.y);
    _RandomBuffer[index] = value.z;
    return value * (1.0 / 4294967296.0);
}
#endif