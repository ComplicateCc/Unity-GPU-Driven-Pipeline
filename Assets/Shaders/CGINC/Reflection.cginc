#ifndef REFLECTION
#define REFLECTION
#define MAXIMUM_PROBE 8
    int DownDimension(uint3 id, const uint2 size, const int multiply){
        const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
        return dot(id, multiValue);
    }
    TextureCube<float3> _ReflectionCubeMap0; SamplerState sampler_ReflectionCubeMap0;
    TextureCube<float3> _ReflectionCubeMap1; SamplerState sampler_ReflectionCubeMap1;
    TextureCube<float3> _ReflectionCubeMap2; SamplerState sampler_ReflectionCubeMap2;
    TextureCube<float3> _ReflectionCubeMap3; SamplerState sampler_ReflectionCubeMap3;
    TextureCube<float3> _ReflectionCubeMap4; SamplerState sampler_ReflectionCubeMap4;
    TextureCube<float3> _ReflectionCubeMap5; SamplerState sampler_ReflectionCubeMap5;
    TextureCube<float3> _ReflectionCubeMap6; SamplerState sampler_ReflectionCubeMap6;
    TextureCube<float3> _ReflectionCubeMap7; SamplerState sampler_ReflectionCubeMap7;
    float3 GetColor(int index, float3 normal, float lod)
    {
        switch(index)
        {
            case 0:
            return _ReflectionCubeMap0.SampleLevel(sampler_ReflectionCubeMap0, normal, lod);
            case 1:
            return _ReflectionCubeMap1.SampleLevel(sampler_ReflectionCubeMap1, normal, lod);
            case 2:
            return _ReflectionCubeMap2.SampleLevel(sampler_ReflectionCubeMap2, normal, lod);
            case 3:
            return _ReflectionCubeMap3.SampleLevel(sampler_ReflectionCubeMap3, normal, lod);
            case 4:
            return _ReflectionCubeMap4.SampleLevel(sampler_ReflectionCubeMap4, normal, lod);
            case 5:
            return _ReflectionCubeMap5.SampleLevel(sampler_ReflectionCubeMap5, normal, lod);
            case 6:
            return _ReflectionCubeMap6.SampleLevel(sampler_ReflectionCubeMap6, normal, lod);
            default:
            return _ReflectionCubeMap7.SampleLevel(sampler_ReflectionCubeMap7, normal, lod);
        }
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