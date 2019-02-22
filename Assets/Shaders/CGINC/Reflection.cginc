#ifndef REFLECTION
#define REFLECTION
#define MAXIMUM_PROBE 8
    int DownDimension(uint3 id, const uint2 size, const int multiply){
        const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
        return dot(id, multiValue);
    }

    struct ReflectionData
    {
        float3 position;
        float3 extent;
        float4 hdr;
        float blendDistance;
        int importance;
        int boxProjection;
    };
#endif