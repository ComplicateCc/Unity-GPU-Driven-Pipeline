#ifndef __VOXELLIGHT_INCLUDE__
#define __VOXELLIGHT_INCLUDE__

#define XRES 16
#define YRES 8
#define ZRES 64
#define VOXELZ 64

static const uint3 _ScreenSize = uint3(160, 90, 256);


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
float3 _NearFarClip; //x: farClip / availiable distance y: nearclip / availiable distance z: nearClip

float4 _Screen_TexelSize;

inline uint GetIndex(uint3 id, const uint3 size){
    const uint3 multiValue = uint3(1, size.x, size.x * size.y) * (MAXLIGHTPERCLUSTER + 1);
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

inline float getRandomFloat(uint index)
{
    uint value = wang_hash(_RandomBuffer[index]);
    _RandomBuffer[index] = value;
    return value * (1.0 / 4294967296.0);
}

inline float2 getRandomFloat2(uint index)
{
    uint2 value = 0;
    value.x = wang_hash(_RandomBuffer[index]);
    value.y = wang_hash(value.x);
    _RandomBuffer[index] = value.y;
    return value * (1.0 / 4294967296.0);
}

inline float3 getRandomFloat3(uint index)
{
    uint3 value = 0;
    value.x = wang_hash(_RandomBuffer[index]);
    value.y = wang_hash(value.x);
    value.z = wang_hash(value.y);
    _RandomBuffer[index] = value.z;
    return value * (1.0 / 4294967296.0);
}

#endif