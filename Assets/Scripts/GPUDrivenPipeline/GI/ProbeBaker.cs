using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe class ProbeBaker : MonoBehaviour
    {
        public int3 probeCount = new int3(10, 10, 10);
        public float considerRange = 5;
        public ComputeShader decodeShader;
        public string path = "Assets/Test.txt";
        public PipelineResources resources;
        private const int RESOLUTION = 128;
        private CommandBuffer cbuffer;
        public RenderTexture rt;
        private void Awake()
        {
            cbuffer = new CommandBuffer();
            rt = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGB32,
                depthBufferBits = 16,
                dimension = TextureDimension.Cube,
                enableRandomWrite = false,
                height = RESOLUTION,
                width = RESOLUTION,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = 1,
                vrUsage = VRTextureUsage.None
            });
            rt.Create();
        }
        private void OnDestroy()
        {
            cbuffer.Dispose();
            rt.Release();
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
        private void BakeMap(int3 index)
        {
            float3 left = transform.position - transform.lossyScale * 0.5f;
            float3 right = transform.position + transform.lossyScale * 0.5f;
            float3 position = lerp(left, right, ((float3)index + 0.5f) / probeCount);
            if (CalculateShadowmap(transform.position, (float3)transform.lossyScale * 0.5f + float3(considerRange)))
            {
                cbuffer.EnableShaderKeyword("EnableShadow");
            }
            else
            {
                cbuffer.DisableShaderKeyword("EnableShadow");
            }
            SceneController.DrawGIBuffer(rt, float4(position, considerRange), resources.shaders.gpuFrustumCulling, cbuffer);
        }
        static readonly int _ShadowmapForCubemap = Shader.PropertyToID("_ShadowmapForCubemap");
        private OrthoCam shadowCam;
        private bool CalculateShadowmap(float3 center, float3 extent)
        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RHalf,
                depthBufferBits = 16,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = false,
                height = 2048,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = 1,
                vrUsage = VRTextureUsage.None,
                width = 2048
            };
            
            float3* allVert = stackalloc float3[]
            {
                center + extent,
                center + float3(extent.x, -extent.y, extent.z),
                center + float3(extent.x, extent.y, -extent.z),
                center + float3(extent.x, -extent.y, -extent.z),
                center + float3(-extent.x, extent.y, extent.z),
                center + float3(-extent.x, -extent.y, extent.z),
                center + float3(-extent.x, extent.y, -extent.z),
                center - extent
            };
            if (SunLight.current && SunLight.current.enableShadow)
            {
                cbuffer.GetTemporaryRT(_ShadowmapForCubemap, desc);
                SceneController.DrawSunShadowForCubemap(allVert, _ShadowmapForCubemap, SunLight.current, cbuffer, out shadowCam, resources.shaders.gpuFrustumCulling);
                cbuffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(shadowCam.projectionMatrix, false) * (Matrix4x4)shadowCam.worldToCameraMatrix);
                return true;
            }
            else
            {
                return false;
            }
        }
        [EasyButtons.Button]
        public void DebugTry()
        {
            if(!SceneController.gpurpEnabled)
            {
                Debug.Log("Cluster Rendering not enabled!");
                return;
            }
            BakeMap(0);
            RenderPipeline.ExecuteBufferAtFrameEnding(cbuffer);
        }
    }

}