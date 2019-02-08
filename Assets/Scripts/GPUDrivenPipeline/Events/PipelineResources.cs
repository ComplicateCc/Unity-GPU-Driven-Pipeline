using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using System;
using UnityEngine.Experimental.Rendering;
namespace MPipeline
{
    public unsafe class PipelineResources : RenderPipelineAsset
    {
        protected override IRenderPipeline InternalCreatePipeline()
        {
            return new MPipeline.RenderPipeline(this);
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
        public PipelineEvent[] gpurpEvents;
        private static Dictionary<CameraRenderingPath, Func<PipelineResources, PipelineEvent[]>> presetDict = null;
        public static Dictionary<CameraRenderingPath, Func<PipelineResources, PipelineEvent[]>> GetEventsDict()
        {
            if (presetDict != null) return presetDict;
            presetDict = new Dictionary<CameraRenderingPath, Func<PipelineResources, PipelineEvent[]>>();
            presetDict.Add(CameraRenderingPath.GPUDeferred, (res) => res.gpurpEvents);
            return presetDict;
        }
    }
}