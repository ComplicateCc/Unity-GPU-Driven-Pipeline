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
        float3 minExtent;
        float3 maxExtent;
        float4 hdr;
        float blendDistance;
        int boxProjection;
    };

#ifndef COMPUTE_SHADER
inline half3 MPipelineGI_IndirectSpecular(UnityGIInput data, half occlusion, Unity_GlossyEnvironmentData glossIn, ReflectionData reflData, int currentIndex, float lod)
{
    half3 specular;
    half3 originalReflUVW = 0;
    if(reflData.boxProjection > 0)
    {
        // we will tweak reflUVW in glossIn directly (as we pass it to Unity_GlossyEnvironment twice for probe0 and probe1), so keep original to pass into BoxProjectedCubemapDirection
        originalReflUVW = glossIn.reflUVW;
        glossIn.reflUVW = BoxProjectedCubemapDirection (originalReflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
    }
    float3 env0 = GetColor(currentIndex, glossIn.reflUVW, lod);
    /*
        #ifdef UNITY_SPECCUBE_BLENDING
            const float kBlendFactor = 0.99999;
            float blendLerp = data.boxMin[0].w;
            UNITY_BRANCH
            if (blendLerp < kBlendFactor)
            {
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                    glossIn.reflUVW = BoxProjectedCubemapDirection (originalReflUVW, data.worldPos, data.probePosition[1], data.boxMin[1], data.boxMax[1]);
                #endif

                half3 env1 = Unity_GlossyEnvironment (UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1,unity_SpecCube0), data.probeHDR[1], glossIn);
                specular = lerp(env1, env0, blendLerp);
            }
            else
            {
                specular = env0;
            }
        #else
            specular = env0;
        #endif
        */
        specular = env0;
    return specular * occlusion;
}
#endif
#endif