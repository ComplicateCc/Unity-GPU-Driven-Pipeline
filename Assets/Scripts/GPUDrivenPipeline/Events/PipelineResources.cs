using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe class PipelineResources : RenderPipelineAsset
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
        private static Dictionary<CameraRenderingPath, PipelineEvent[]> presetDict = new Dictionary<CameraRenderingPath, PipelineEvent[]>();
        public Dictionary<CameraRenderingPath, PipelineEvent[]> renderingPaths
        {
            get { return presetDict; }
        }
        public void SetRenderingPath()
        {
            presetDict.Clear();
            presetDict.Add(CameraRenderingPath.GPUDeferred, gpurpEvents);
            //Add New Events Here
        }
    }
}