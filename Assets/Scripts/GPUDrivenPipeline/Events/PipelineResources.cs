using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;
namespace MPipeline
{ 
    public abstract class EventsCollection
    {
        public PipelineEvent[] GetAllEvents()
        {
            FieldInfo[] infos = GetType().GetFields();
            PipelineEvent[] events = new PipelineEvent[infos.Length];
            for(int i = 0; i < events.Length; ++i)
            {
                events[i] = infos[i].GetValue(this) as PipelineEvent;
            }
            return events;
        }
    }
    public unsafe sealed class PipelineResources : RenderPipelineAsset
    {
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
        {
            return new RenderPipeline(this);
        }
        public enum CameraRenderingPath
        {
            Unlit = 0, Forward = 1, GPUDeferred = 2
        }
        [System.Serializable]
        public struct Shaders
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
        public Shaders shaders = new Shaders();
        [System.Serializable]
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
            public FinalPostEvent postEffects;
        }
        public GPUDeferred gpuDeferred;
        private Dictionary<CameraRenderingPath, PipelineEvent[]> presetDict = new Dictionary<CameraRenderingPath, PipelineEvent[]>();
        public Dictionary<CameraRenderingPath, PipelineEvent[]> renderingPaths
        {
            get { return presetDict; }
        }
        public void SetRenderingPath()
        {
            presetDict.Clear();
            presetDict.Add(CameraRenderingPath.GPUDeferred, gpuDeferred.GetAllEvents());
            //Add New Events Here
        }
    }
}