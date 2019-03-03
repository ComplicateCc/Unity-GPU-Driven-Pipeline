using System;
using UnityEngine;

namespace MPipeline
{
    [Serializable]
    public class GPUDeferred : EventsCollection
    {
        public PropertySetEvent propertySet;
        public GeometryEvent geometry;
        public AOEvents ambientOcclusion;
        public LightingEvent lighting;
        public ReflectionEvent reflection;
        public SkyboxEvent skybox;
        public VolumetricLightEvent volumetric;
        public TemporalAAEvent temporalAA;
        public TransEvent transparent;
        public FinalPostEvent postEffects;
    }

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
        public Shader indirectDepthShader;
        public Shader HizLodShader;
        public Shader motionVectorShader;
        public Shader shadowMaskShader;
        public Shader reflectionShader;
        public Shader linearDepthShader;
        public Shader pointLightShader;
        public Shader cubeDepthShader;
        public Shader clusterRenderShader;
        public Shader volumetricShader;
        public Shader terrainShader;
        public Shader spotLightDepthShader;
        public Shader gtaoShader;
        public Mesh occluderMesh;
        public Mesh sphereMesh;
    }

    [Serializable]
    public class BakeInEditor : EventsCollection
    {
        public PropertySetEvent propertySet;
        public GeometryEvent geometry;
        public LightingEvent lighting;
        public SkyboxEvent skybox;
    }

    [Serializable]
    public struct AllEvents
    {
        [TargetPath(PipelineResources.CameraRenderingPath.GPUDeferred)]
        public GPUDeferred gpuDeferred;
        [TargetPath(PipelineResources.CameraRenderingPath.Bake)]
        public BakeInEditor baker;
    }
}
