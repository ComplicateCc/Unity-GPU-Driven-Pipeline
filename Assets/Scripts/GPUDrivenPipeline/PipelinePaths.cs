using System;
using UnityEngine;
using System.Collections.Generic;
namespace MPipeline
{
    [Serializable]
    public struct PipelineShaders
    {
        public ComputeShader cbdrShader;
        public ComputeShader gpuFrustumCulling;
        public ComputeShader gpuSkin;
        public ComputeShader streamingShader;
        public ComputeShader pointLightFrustumCulling;
        public ComputeShader terrainCompute;
        public ComputeShader volumetricScattering;
        public ComputeShader probeCoeffShader;
        public ComputeShader texCopyShader;
        public ComputeShader reflectionCullingShader;
        public Shader taaShader;
        public Shader ssrShader;
        public Shader indirectDepthShader;
        public Shader HizLodShader;
        public Shader motionVectorShader;
        public Shader shadowMaskShader;
        public Shader reflectionShader;
        public Shader linearDepthShader;
        public Shader cubeDepthShader;
        public Shader clusterRenderShader;
        public Shader volumetricShader;
        public Shader terrainShader;
        public Shader spotLightDepthShader;
        public Shader gtaoShader;
        public Shader uberShader;
        public Shader irradianceVolumeShader;
        public Mesh occluderMesh;
        public Mesh sphereMesh;
    }

    public static class AllEvents
    {
        public static readonly Type[] gpuDeferredType =
        {
       typeof(PropertySetEvent),
       typeof(GeometryEvent),
       typeof(AOEvents),
       typeof(LightingEvent),
       typeof(ReflectionEvent),
       typeof(SkyboxEvent),
       typeof(VolumetricLightEvent),
       typeof(UberEvents),
       typeof(TemporalAAEvent),
       typeof(TransEvent),
       typeof(FinalPostEvent)
        };

        public static readonly Type[] bakeType =
        {
        typeof(PropertySetEvent),
        typeof(GeometryEvent),
        typeof(LightingEvent),
        typeof(SkyboxEvent),
        typeof(UberEvents)
        };

        public static List<Pair<int, Type[]>> GetAllPath()
        {
            List<Pair<int, Type[]>> list = new List<Pair<int, Type[]>>();
            list.Add(new Pair<int, Type[]>((int)PipelineResources.CameraRenderingPath.GPUDeferred, gpuDeferredType));
            list.Add(new Pair<int, Type[]>((int)PipelineResources.CameraRenderingPath.Bake, bakeType));
            return list;
        }
    }
}
