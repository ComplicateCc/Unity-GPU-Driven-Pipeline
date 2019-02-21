#ifndef REFLECTION
#define REFLECTION
#define MAXIMUM_PROBE 8
    int DownDimension(int3 coord, const int2 xysize)
    {
        int3 multi = (xysize.y * xysize.x, xysize.x, 1);
        return dot(coord.zyx, multi);
    }

    int3 UpDimension(int coord, const int2 xysize)
    {
        int xy = (xysize.x * xysize.y);
        return int3(coord % xysize.x, (coord % xy) / xysize.x, coord / xy);
    }
    struct ReflectionData
    {
        float3x3 localToWorld;
        float3 position;
        float3 extent;
        float4 hdr;
        float blendDistance;
        int importance;
        int boxProjection;
    };
#endif