using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
namespace MPipeline
{
    public struct PipelineEventsCollection
    {
        public List<PipelineEvent> preEvents;
        public List<PipelineEvent> postEvents;
        public List<PipelineEvent> allEvents;
    }
    public unsafe class PipelineResources : ScriptableObject
    {
        public abstract class EventsCollection
        {
            public PipelineEventsCollection GetAllEvents()
            {
                FieldInfo[] infos = GetType().GetFields();
                PipelineEventsCollection collect = new PipelineEventsCollection
                {
                    postEvents = new List<PipelineEvent>(infos.Length),
                    preEvents = new List<PipelineEvent>(infos.Length),
                    allEvents = new List<PipelineEvent>(infos.Length)
                };

                for (int i = 0; i < infos.Length; ++i)
                {
                    PipelineEvent evt = infos[i].GetValue(this) as PipelineEvent;
                    evt.GetDomainName();
                    if(evt.postEnable)
                    {
                        collect.postEvents.Add(evt);
                    }
                    if(evt.preEnable)
                    {
                        collect.preEvents.Add(evt);
                    }
                    collect.allEvents.Add(evt);
                }
                return collect;
            }
        }
        [System.Serializable]
        public class GPURPEvents : EventsCollection
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
            public ComputeShader lightmapShader;
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
        [SerializeField]
        private GPURPEvents gpurpEvents = new GPURPEvents();
        public Dictionary<RenderPipeline.CameraRenderingPath, PipelineEventsCollection> GetAllEvents()
        {
            Dictionary<RenderPipeline.CameraRenderingPath, PipelineEventsCollection> result = new Dictionary<RenderPipeline.CameraRenderingPath, PipelineEventsCollection>();
            PipelineEventsCollection evts = gpurpEvents.GetAllEvents();
            result.Add(RenderPipeline.CameraRenderingPath.GPUDeferred, evts);
            return result;
        }
    }

}