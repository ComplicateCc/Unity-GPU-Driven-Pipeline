#ifndef LIGHTBAKER
#define LIGHTBAKER
#define RESOLUTION 128
half4 Decode(uint value)
{
    uint4 values = 0;
    values.x = value & 255;
    value >>= 8;
    values.y = value & 255;
    value >>= 8;
    values.z = value & 255;
    value >>= 8;
    values.w = value & 255;
    return values / 255.0;
}
uint Encode(half4 value)
{
    uint4 v = value * 255;
    uint result = 0;
    result |= v.w & 255;
    result <<= 8;
    result |= v.z & 255;
    result <<= 8;
    result |= v.y & 255;
    result <<= 8;
    result |= v.x & 255;
    return result;
}
static const float Pi = 3.141592654;
static const float CosineA0 = Pi;
static const float CosineA1 = (2.0 * Pi) / 3.0;
static const float CosineA2 = Pi * 0.25;

struct SH9
{
    float c[9];
};

struct SHColor
{
    float3 c[9];
};

SH9 SHCosineLobe(float3 normal)
{
    SH9 sh;
    sh.c[0] = 0.282095;
    sh.c[1] = 0.488603 * normal.x;
    sh.c[2] = 0.488603 * normal.z;
    sh.c[3] = 0.488603 * normal.y;
    sh.c[4] = 1.092548 * normal.x*normal.z;
    sh.c[5] = 1.092548 * normal.y*normal.z;
    sh.c[6] = 0.315392 * normal.y*normal.x;
    sh.c[7] = 1.092548 * normal.z * normal.z - 0.315392;
    sh.c[8] = 0.546274 * (normal.x*normal.x - normal.y*normal.y);

    return sh;
}

#define GETCOEFF(normal)\
float Y00     = 0.282095;\
float Y11     = 0.488603 * normal.x;\
float Y10     = 0.488603 * normal.z;\
float Y1_1    = 0.488603 * normal.y;\
float Y21     = 1.092548 * normal.x*normal.z;\
float Y2_1    = 1.092548 * normal.y*normal.z;\
float Y2_2    = 1.092548 * normal.y*normal.x;\
float Y20     = 0.946176 * normal.z * normal.z - 0.315392;\
float Y22     = 0.546274 * (normal.x*normal.x - normal.y*normal.y);

float3 DirFromCube(uint face, float2 uv){
    float3 dir = float3(1, uv * 2 - 1);
    switch(face){
        case 1:
        dir = dir.xzy * float3(1, -1, -1);
        break;
        case 0:
        dir = dir.xzy * float3(-1, -1, 1);
        break;
        case 2:
        dir = dir.yxz;
        break;
        case 3:
        dir = dir.yxz * float3(1, -1, -1);
        break;
        case 5:
        dir = dir.yzx * float3(1, -1, 1);
        break;
        case 4:
        dir = dir.yzx * -1;
        break;
    }
    return normalize(dir);
}


    int DownDimension(int3 coord, int2 xysize)
    {
        int3 multi = int3(xysize.y * xysize.x, xysize.x, 1);
        return dot(coord.zyx, multi);
    }

    int3 UpDimension(int coord, int2 xysize)
    {
        int xy = xysize.x * xysize.y;
        return int3(coord % xysize.x, (coord % xy) / xysize.x, coord / xy);
    }


#endif