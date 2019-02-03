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
        public string path = "Assets/Test.txt";
        public PipelineResources resources;
        private const int RESOLUTION = 32;
        private CommandBuffer cbuffer;
        private ComputeBuffer coeffTemp;
        private ComputeBuffer coeff;
        private RenderTexture rt;
        private NativeList<int> _CoeffIDs;
        private RenderTexture[] coeffTextures;
        private bool isRendering = false;
        public RenderTexture shadowmap;
        public UnityEngine.UI.Text testText;
        private void OnEnable()
        {
            cbuffer = new CommandBuffer();
            _CoeffIDs = new NativeList<int>(7, Allocator.Persistent);
            coeffTextures = new RenderTexture[7];
            string str = "_CoeffTexture0";
            for (int i = 0; i < 7; ++i)
            {
                fixed (char* chr = str)
                {
                    chr[13] = (char)(i + 48);
                }
                _CoeffIDs.Add(Shader.PropertyToID(str));
                coeffTextures[i] = new RenderTexture(new RenderTextureDescriptor
                {
                    autoGenerateMips = false,
                    bindMS = false,
                    colorFormat = RenderTextureFormat.ARGBHalf,
                    depthBufferBits = 0,
                    dimension = TextureDimension.Tex3D,
                    enableRandomWrite = true,
                    height = probeCount.y,
                    width = probeCount.x,
                    volumeDepth = probeCount.z,
                    memoryless = RenderTextureMemoryless.None,
                    msaaSamples = 1,
                    shadowSamplingMode = ShadowSamplingMode.None,
                    sRGB = false,
                    useMipMap = false,
                    vrUsage = VRTextureUsage.None
                });
                coeffTextures[i].Create();
            }
            rt = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGB32,
                depthBufferBits = 16,
                dimension = TextureDimension.Tex2DArray,
                enableRandomWrite = false,
                height = RESOLUTION,
                width = RESOLUTION,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = 6,
                vrUsage = VRTextureUsage.None
            });
            coeffTemp = new ComputeBuffer(9, 12);
            coeff = new ComputeBuffer(probeCount.x * probeCount.y * probeCount.z * 9, 12);
            rt.Create();
        }
        private void OnDisable()
        {
            cbuffer.Dispose();
            coeff.Dispose();
            coeffTemp.Dispose();
            DestroyImmediate(rt);
            _CoeffIDs.Dispose();
            foreach (var i in coeffTextures)
            {
                DestroyImmediate(i);
            }
            Shader.DisableKeyword("ENABLESH");
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
            SceneController.GICubeCull(position, considerRange, cbuffer, resources.shaders.gpuFrustumCulling);
            SceneController.DrawGIBuffer(rt, float4(position, considerRange), resources.shaders.gpuFrustumCulling, cbuffer);
        }
        static readonly int _ShadowmapForCubemap = Shader.PropertyToID("_ShadowmapForCubemap");
        private OrthoCam shadowCam;
        private Matrix4x4 shadowVP;
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
                height = 4096,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = 1,
                vrUsage = VRTextureUsage.None,
                width = 4096
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
                shadowmap = new RenderTexture(desc);
                cbuffer.SetGlobalTexture(_ShadowmapForCubemap, shadowmap);
                SceneController.DrawSunShadowForCubemap(allVert, shadowmap, SunLight.current, cbuffer, out shadowCam, resources.shaders.gpuFrustumCulling);
                shadowVP = GL.GetGPUProjectionMatrix(shadowCam.projectionMatrix, false) * (Matrix4x4)shadowCam.worldToCameraMatrix;
                return true;
            }
            else
            {
                shadowmap = null;
                return false;
            }
        }

        [EasyButtons.Button]
        public void BakeProbe()
        {
            if (isRendering) return;
            isRendering = true;
            StartCoroutine(BakeLightmap());
        }
        public IEnumerator BakeLightmap()
        {
            ComputeShader shader = resources.shaders.probeCoeffShader;
            cbuffer.SetComputeBufferParam(shader, 0, "_CoeffTemp", coeffTemp);
            cbuffer.SetComputeBufferParam(shader, 1, "_CoeffTemp", coeffTemp);
            cbuffer.SetComputeBufferParam(shader, 1, "_Coeff", coeff);
            cbuffer.SetComputeBufferParam(shader, 2, "_Coeff", coeff);
            cbuffer.SetComputeTextureParam(shader, 0, "_SourceCubemap", rt);
            for (int i = 0; i < 7; ++i)
            {
                cbuffer.SetGlobalTexture(_CoeffIDs[i], coeffTextures[i]);
                cbuffer.SetComputeTextureParam(shader, 2, _CoeffIDs[i], coeffTextures[i]);
            }
            cbuffer.SetGlobalVector("_Tex3DSize", new Vector4(probeCount.x + 0.01f, probeCount.y + 0.01f, probeCount.z + 0.01f));
            cbuffer.SetGlobalVector("_SHSize", transform.localScale);
            cbuffer.SetGlobalVector("_LeftDownBack", transform.position - transform.localScale * 0.5f);
            cbuffer.EnableShaderKeyword("ENABLESH");
            bool useShadow = CalculateShadowmap(transform.position, (float3)transform.lossyScale * 0.5f + float3(considerRange));
            Debug.Log(useShadow);
            if (useShadow)
                cbuffer.EnableShaderKeyword("EnableShadow");
            else
                cbuffer.DisableShaderKeyword("EnableShadow");
            int count = 0;
            int target = probeCount.x * probeCount.y * probeCount.z;
            for (int x = 0; x < probeCount.x; ++x)
            {
                for (int y = 0; y < probeCount.y; ++y)
                {
                    for (int z = 0; z < probeCount.z; ++z)
                    {

                        cbuffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, shadowVP);
                        BakeMap(int3(x, y, z));
                        cbuffer.SetComputeIntParam(shader, "_OffsetIndex", PipelineFunctions.DownDimension(int3(x, y, z), probeCount.xy));
                        cbuffer.DispatchCompute(shader, 0, 1, 1, 6);
                        cbuffer.DispatchCompute(shader, 1, 1, 1, 1);
                        testText.text = count.ToString() + " " + target.ToString();
                        count++;
                        if (count % 50 == 0)
                        {
                            RenderPipeline.ExecuteBufferAtFrameEnding(cbuffer);
                            yield return null;
                        }
                    }
                }
            }
            ComputeShaderUtility.Dispatch(shader, cbuffer, 2, probeCount.x * probeCount.y * probeCount.z, 64);
            RenderPipeline.ExecuteBufferAtFrameEnding(cbuffer);
            if (shadowmap != null)
            {
                Destroy(shadowmap);
                shadowmap = null;
            }
            isRendering = false;

        }
    }

}