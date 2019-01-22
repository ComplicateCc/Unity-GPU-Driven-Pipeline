using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public class PipelineResources : ScriptableObject
    {
        [System.Serializable]
        public class GPURPEvents
        {
            public PropertySetEvent propertyEvent;
            public GeometryEvent geometryEvent;
            public LightingEvent lightingEvent;
            public SkyboxEvent skyboxEvent;
            public VolumetricLightEvent volumetricEvent;
            public FinalPostEvent postEvent;
            public TemporalAAEvent temporalEvent;
        }
        [System.Serializable]
        public class Shaders
        {
            public ComputeShader cbdrShader;
            public ComputeShader gpuFrustumCulling;
            public ComputeShader gpuSkin;
            public ComputeShader streamingShader;
            public ComputeShader pointLightFrustumCulling;
            public ComputeShader terrainCompute;
            public ComputeShader volumetricScattering;
            public Shader copyShader;
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
            public Mesh occluderMesh;
            public Mesh sphereMesh;
        }
        public Shaders shaders = new Shaders();
        public GPURPEvents gpurpEvents = new GPURPEvents();
    }
}