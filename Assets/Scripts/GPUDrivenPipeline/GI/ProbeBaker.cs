using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using System;
namespace MPipeline
{
    public unsafe sealed class ProbeBaker : MonoBehaviour
    {
        public static List<ProbeBaker> allBakers = new List<ProbeBaker>();
        public int3 probeCount = new int3(10, 10, 10);
        public float considerRange = 5;
        public string path = "Assets/Test.txt";
        public PipelineResources resources;
        public bool isRendered { get; private set; }
        private PipelineCamera targetCamera;
        private const int RESOLUTION = 128;
        private CommandBuffer cbuffer;
        private ComputeBuffer coeffTemp;
        private ComputeBuffer coeff;
        private NativeList<int> _CoeffIDs;
        private RenderTexture[] coeffTextures;
        private bool isRendering = false;
        private int index;
        private void OnEnable()
        {
            isRendered = false;
            index = allBakers.Count;
            allBakers.Add(this);
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
            coeffTemp = new ComputeBuffer(9, 12);
            coeff = new ComputeBuffer(probeCount.x * probeCount.y * probeCount.z * 9, 12);
            if(!targetCamera)
            {
                targetCamera = transform.GetComponentInChildren<PipelineCamera>();
                if(!targetCamera)
                {
                    GameObject go = new GameObject("Bake Camera", typeof(Camera), typeof(PipelineCamera));
                    targetCamera = go.GetComponent<PipelineCamera>();
                    targetCamera.cam = go.GetComponent<Camera>();
                    go.transform.SetParent(transform);
                }
                targetCamera.enabled = false;
            }
        }
        private void OnDisable()
        {
            DestroyImmediate(targetCamera.gameObject);
            targetCamera = null;
            allBakers[index] = allBakers[allBakers.Count - 1];
            allBakers[index].index = index;
            allBakers.RemoveAt(allBakers.Count - 1);
            cbuffer.Dispose();
            coeff.Dispose();
            coeffTemp.Dispose();
            _CoeffIDs.Dispose();
            foreach (var i in coeffTextures)
            {
                Destroy(i);
            }
            isRendered = false;

        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
        private static void GetMatrix(float4x4* allmat, ref PerspCam persp, float3 position)
        {
            persp.position = position;
            //X
            persp.up = float3(0, -1, 0);
            persp.right = float3(0, 0, -1);
            persp.forward = float3(1, 0, 0);
            persp.UpdateTRSMatrix();
            allmat[1] = persp.worldToCameraMatrix;
            //-X
            persp.up = float3(0, -1, 0);
            persp.right = float3(0, 0, 1);
            persp.forward = float3(-1, 0, 0);
            persp.UpdateTRSMatrix();
            allmat[0] = persp.worldToCameraMatrix;
            //Y
            persp.right = float3(-1, 0, 0);
            persp.up = float3(0, 0, -1);
            persp.forward = float3(0, 1, 0);
            persp.UpdateTRSMatrix();
            allmat[2] = persp.worldToCameraMatrix;
            //-Y
            persp.right = float3(-1, 0, 0);
            persp.up = float3(0, 0, 1);
            persp.forward = float3(0, -1, 0);
            persp.UpdateTRSMatrix();
            allmat[3] = persp.worldToCameraMatrix;
            //Z
            persp.right = float3(1, 0, 0);
            persp.up = float3(0, -1, 0);
            persp.forward = float3(0, 0, 1);
            persp.UpdateTRSMatrix();
            allmat[5] = persp.worldToCameraMatrix;
            //-Z
            persp.right = float3(-1, 0, 0);
            persp.up = float3(0, -1, 0);
            persp.forward = float3(0, 0, -1);
            persp.UpdateTRSMatrix();
            allmat[4] = persp.worldToCameraMatrix;
        }
        private void BakeMap(int3 index, RenderTexture texArray, RenderTexture tempTex)
        {
            float3 left = transform.position - transform.lossyScale * 0.5f;
            float3 right = transform.position + transform.lossyScale * 0.5f;
            float3 position = lerp(left, right, ((float3)index + 0.5f) / probeCount);
            PerspCam persp = new PerspCam();
            persp.aspect = 1;
            persp.farClipPlane = considerRange;
            persp.nearClipPlane = 0.1f;
            persp.fov = 90f;
            NativeList<float4x4> worldToCameras = new NativeList<float4x4>(6, 6, Allocator.TempJob);
            NativeList<float4x4> projection = new NativeList<float4x4>(6, 6, Allocator.TempJob);
            GetMatrix(worldToCameras.unsafePtr, ref persp, position);
            persp.UpdateProjectionMatrix();
            for(int i = 0; i < 6; ++i)
            {
                projection[i] = persp.projectionMatrix;
            }
            RenderPipeline.AddRenderingMissionInEditor(worldToCameras, projection, targetCamera, texArray, tempTex, cbuffer);
        }
        static readonly int _ShadowmapForCubemap = Shader.PropertyToID("_ShadowmapForCubemap");
        private OrthoCam shadowCam;


        [EasyButtons.Button]
        public void BakeProbe()
        {
            if (isRendering) return;
            isRendering = true;
            StartCoroutine(BakeLightmap());
        }
        public void SetCoeffTextures(CommandBuffer buffer, ComputeShader shader, int targetPass)
        {
            for (int i = 0; i < _CoeffIDs.Length; ++i)
            {
                buffer.SetComputeTextureParam(shader, targetPass, _CoeffIDs[i], coeffTextures[i]);
            }
            buffer.SetGlobalVector("_SHSize", transform.localScale);
            buffer.SetGlobalVector("_LeftDownBack", transform.position - transform.localScale * 0.5f);
        }
        public IEnumerator BakeLightmap()
        {
            RenderTextureDescriptor texArrayDescriptor = new RenderTextureDescriptor
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
            };
            RenderTexture rt = RenderTexture.GetTemporary(texArrayDescriptor);
            texArrayDescriptor.volumeDepth = 1;
            texArrayDescriptor.dimension = TextureDimension.Tex2D;
            RenderTexture tempRT = RenderTexture.GetTemporary(texArrayDescriptor);
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
            RenderPipeline.ExecuteBufferAtFrameEnding(cbuffer);
            yield return null;
            int target = probeCount.x * probeCount.y * probeCount.z;
            int count = 0;
            for (int x = 0; x < probeCount.x; ++x)
            {
                for (int y = 0; y < probeCount.y; ++y)
                {
                    for (int z = 0; z < probeCount.z; ++z)
                    {
                        BakeMap(int3(x, y, z), rt, tempRT);
                        cbuffer.SetComputeIntParam(shader, "_OffsetIndex", PipelineFunctions.DownDimension(int3(x, y, z), probeCount.xy));
                        cbuffer.DispatchCompute(shader, 0, RESOLUTION / 32, RESOLUTION / 32, 6);
                        cbuffer.DispatchCompute(shader, 1, 1, 1, 1);
                        count++;
                        yield return null;
                    }
                }
            }
            yield return null;
            ComputeShaderUtility.Dispatch(shader, cbuffer, 2, probeCount.x * probeCount.y * probeCount.z, 64);
            RenderPipeline.ExecuteBufferAtFrameEnding(cbuffer);
            Debug.Log("Finished");
            isRendering = false;
            yield return null;
            isRendered = true;
            yield return null;
            RenderTexture.ReleaseTemporary(rt);
            RenderTexture.ReleaseTemporary(tempRT);
        }
    }

}