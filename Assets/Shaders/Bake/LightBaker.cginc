#ifndef LIGHTBAKER
#define LIGHTBAKER
#define RESOLUTION 32
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
#define POSITIVEXCUBE(dir) dir = float3(1, uv * 2 - 1); dir = dir.xzy * float3(1, -1, -1); dir = normalize(dir);
#define NEGATIVEXCUBE(dir) dir = float3(1, uv * 2 - 1); dir = dir.xzy * float3(-1, -1, 1); dir = normalize(dir);
#define POSITIVEYCUBE(dir) dir = float3(1, uv * 2 - 1); dir = dir.yxz; dir = normalize(dir);
#define NEGATIVEYCUBE(dir) dir = float3(1, uv * 2 - 1); dir = dir.yxz * float3(1, -1, -1); dir = normalize(dir);
#define POSITIVEZCUBE(dir) dir = float3(1, uv * 2 - 1); dir = dir.yzx * float3(1, -1, 1); dir = normalize(dir);
#define NEGATIVEZCUBE(dir) dir = float3(1, uv * 2 - 1); dir = dir.yzx * -1; dir = normalize(dir);
#endif