#ifndef __VOXELLIGHT_INCLUDE__
#define __VOXELLIGHT_INCLUDE__

#define XRES 32
#define YRES 16
#define ZRES 256
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

#endif