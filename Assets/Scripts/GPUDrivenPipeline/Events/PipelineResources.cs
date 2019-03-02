using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;
using System;
namespace MPipeline
{
    public abstract class EventsCollection
    {
        public PipelineEvent[] GetAllEvents()
        {
            FieldInfo[] infos = GetType().GetFields();
            PipelineEvent[] events = new PipelineEvent[infos.Length];
            for (int i = 0; i < events.Length; ++i)
            {
                events[i] = infos[i].GetValue(this) as PipelineEvent;
            }
            return events;
        }
    }
    public class TargetPathAttribute : Attribute
    {
        public PipelineResources.CameraRenderingPath path { get; private set; }
        public TargetPathAttribute(PipelineResources.CameraRenderingPath renderingPath)
        {
            path = renderingPath;
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
            GPUDeferred, Bake
        }
        [Serializable]
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
        public PipelineEvent[][] allEvents { get; private set; }
        public void SetRenderingPath()
        {
            FieldInfo[] infos = events.GetType().GetFields();
            List<Pair<int, EventsCollection>> allCollection = new List<Pair<int, EventsCollection>>();
            foreach (var i in infos)
            {
                TargetPathAttribute target = i.GetCustomAttribute<TargetPathAttribute>();
                if(target != null)
                {
                    EventsCollection collection = i.GetValue(events) as EventsCollection;
                    if(collection != null)
                    {
                        allCollection.Add(new Pair<int, EventsCollection>((int)target.path, collection));
                    }
                }
            }
            int maximum = -1;
            foreach(var i in allCollection)
            {
                if (i.key > maximum)
                    maximum = i.key;
            }
            allEvents = new PipelineEvent[maximum + 1][];
            foreach(var i in allCollection)
            {
                allEvents[i.key] = i.value.GetAllEvents();
            }
        }
        public AllEvents events = new AllEvents();
    }
}