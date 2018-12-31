using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public class PipelineResources : ScriptableObject
    {
        public ComputeShader cbdrShader;
        public ComputeShader gpuFrustumCulling;
        public ComputeShader gpuSkin;
        public ComputeShader streamingShader;
        public ComputeShader pointLightFrustumCulling;
        public ComputeShader terrainCompute;
        public Shader copyShader;
        public Shader taaShader;
        public Shader indirectDepthShader;
        public Shader HizLodShader;
        public Shader spotlightShader;
        public Shader motionVectorShader;
        public Shader shadowMaskShader;
        public Shader reflectionShader;
        public Shader linearDepthShader;
        public Shader pointLightShader;
        public Shader cubeDepthShader;
        public Shader clusterRenderShader;
        public Shader volumetricShader;
        public Shader terrainShader;
        public Mesh occluderMesh;
        public Mesh sphereMesh;
    }
}