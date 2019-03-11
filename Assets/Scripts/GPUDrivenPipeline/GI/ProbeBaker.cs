
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
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline
{
#if UNITY_EDITOR
    public unsafe sealed class ProbeBaker : MonoBehaviour
    {
        public int3 probeCount = new int3(10, 10, 10);
        public float considerRange = 5;
        public PipelineResources resources;
        public bool isRendered { get; private set; }
        public IrradianceResources saveTarget;
        public string volumeName;
        private PipelineCamera targetCamera;
        private const int RESOLUTION = 128;
        private CommandBuffer cbuffer;
        private ComputeBuffer coeffTemp;
        private ComputeBuffer coeff;

        private RenderTexture[] coeffTextures;
        private bool isRendering = false;
        private int indexInList;
        private void Init()
        {
            isRendered = false;
            cbuffer = new CommandBuffer();

            coeffTextures = new RenderTexture[7];
            for (int i = 0; i < 7; ++i)
            {
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
            if (!targetCamera)
            {
                targetCamera = transform.GetComponentInChildren<PipelineCamera>();
                if (!targetCamera)
                {
                    GameObject go = new GameObject("Bake Camera", typeof(Camera), typeof(PipelineCamera));
                    targetCamera = go.GetComponent<PipelineCamera>();
                    targetCamera.cam = go.GetComponent<Camera>();
                    go.transform.SetParent(transform);
                }
                targetCamera.cam.enabled = false;
                targetCamera.enabled = false;
            }
            targetCamera.inverseRender = false;
            targetCamera.renderingPath = PipelineResources.CameraRenderingPath.Bake;
        }
        private void Dispose()
        {
            DestroyImmediate(targetCamera.gameObject);
            targetCamera = null;
            cbuffer.Dispose();
            coeff.Dispose();
            coeffTemp.Dispose();
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
            persp.up = float3(0, 1, 0);
            persp.right = float3(0, 0, -1);
            persp.forward = float3(1, 0, 0);
            persp.UpdateTRSMatrix();
            allmat[1] = persp.worldToCameraMatrix;
            //-X
            persp.up = float3(0, 1, 0);
            persp.right = float3(0, 0, 1);
            persp.forward = float3(-1, 0, 0);
            persp.UpdateTRSMatrix();
            allmat[0] = persp.worldToCameraMatrix;
            //Y
            persp.right = float3(-1, 0, 0);
            persp.up = float3(0, 0, 1);
            persp.forward = float3(0, 1, 0);
            persp.UpdateTRSMatrix();
            allmat[2] = persp.worldToCameraMatrix;
            //-Y
            persp.right = float3(-1, 0, 0);
            persp.up = float3(0, 0, -1);
            persp.forward = float3(0, -1, 0);
            persp.UpdateTRSMatrix();
            allmat[3] = persp.worldToCameraMatrix;
            //Z
            persp.right = float3(1, 0, 0);
            persp.up = float3(0, 1, 0);
            persp.forward = float3(0, 0, 1);
            persp.UpdateTRSMatrix();
            allmat[5] = persp.worldToCameraMatrix;
            //-Z
            persp.right = float3(-1, 0, 0);
            persp.up = float3(0, 1, 0);
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
            for (int i = 0; i < 6; ++i)
            {
                projection[i] = persp.projectionMatrix;
            }
            RenderPipeline.AddRenderingMissionInEditor(worldToCameras, projection, targetCamera, texArray, tempTex, cbuffer);
        }
        static readonly int _ShadowmapForCubemap = Shader.PropertyToID("_ShadowmapForCubemap");
        private OrthoCam shadowCam;
        [EasyButtons.Button]
        public void Cubemap()
        {
            StartCoroutine(RunCubemap());
        }
        private void BakeCubemap()
        {

            PerspCam persp = new PerspCam();
            persp.aspect = 1;
            persp.farClipPlane = considerRange;
            persp.nearClipPlane = 0.1f;
            persp.fov = 90f;
            NativeList<float4x4> worldToCameras = new NativeList<float4x4>(6, 6, Allocator.TempJob);
            NativeList<float4x4> projection = new NativeList<float4x4>(6, 6, Allocator.TempJob);
            GetMatrix(worldToCameras.unsafePtr, ref persp, transform.position);
            persp.UpdateProjectionMatrix();
            for (int i = 0; i < 6; ++i)
            {
                projection[i] = persp.projectionMatrix;
            }
            RenderTexture rt = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 16,
                dimension = TextureDimension.Tex2DArray,
                enableRandomWrite = false,
                height = 128,
                width = 128,
                volumeDepth = 6,
                msaaSamples = 1,
                useMipMap = true
            });
            rt.filterMode = FilterMode.Trilinear;
            rt.Create();
            RenderTexture tempRT = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 16,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = false,
                height = 128,
                width = 128,
                volumeDepth = 1,
                msaaSamples = 1
            });
            cbuffer.Clear();
            cbuffer.SetGlobalTexture("_Cubemap", rt);
            cbuffer.GenerateMips(rt);
            RenderPipeline.AddRenderingMissionInEditor(worldToCameras, projection, targetCamera, rt, tempRT, cbuffer);
        }
        private IEnumerator RunCubemap()
        {
            Init();
            BakeCubemap();
            yield return null;
            yield return null;
            Dispose();
        }

        [EasyButtons.Button]
        public void BakeProbe()
        {
            Init();
            if (!Application.isPlaying)
            {
                Debug.LogError("Has to be baked in runtime!");
                return;
            }
            if (!saveTarget)
            {
                Debug.LogError("Save Target is empty!");
                return;
            }
            bool alreadyContained = false;
            for (int i = 0; i < saveTarget.allVolume.Count; ++i)
            {
                var a = saveTarget.allVolume[i];
                if (a.volumeName == volumeName)
                {
                    File.Delete("Assets/BinaryData/Irradiance/" + volumeName + ".mpipe");
                    alreadyContained = true;
                    indexInList = i;
                    break;
                }
            }
            if (!alreadyContained)
            {
                indexInList = saveTarget.allVolume.Count;
                saveTarget.allVolume.Add(new IrradianceResources.Volume());
            }
            if (isRendering) return;
            isRendering = true;
            StartCoroutine(BakeLightmap());
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
                useMipMap = true,
                volumeDepth = 6,
                vrUsage = VRTextureUsage.None
            };

            RenderTexture rt = RenderTexture.GetTemporary(texArrayDescriptor); rt.filterMode = FilterMode.Trilinear;
            rt.Create();
            texArrayDescriptor.volumeDepth = 1;
            texArrayDescriptor.dimension = TextureDimension.Tex2D;
            RenderTexture tempRT = RenderTexture.GetTemporary(texArrayDescriptor);
            tempRT.Create();
            ComputeShader shader = resources.shaders.probeCoeffShader;
            Action<CommandBuffer> func = (cb) =>
            {
                cb.SetComputeBufferParam(shader, 0, "_CoeffTemp", coeffTemp);
                cb.SetComputeBufferParam(shader, 1, "_CoeffTemp", coeffTemp);
                cb.SetComputeBufferParam(shader, 1, "_Coeff", coeff);
                cb.SetComputeTextureParam(shader, 0, "_SourceCubemap", rt);
                cb.SetGlobalVector("_Tex3DSize", new Vector4(probeCount.x + 0.01f, probeCount.y + 0.01f, probeCount.z + 0.01f));
                cb.SetGlobalVector("_SHSize", transform.localScale);
                cb.SetGlobalVector("_LeftDownBack", transform.position - transform.localScale * 0.5f);
            };
            RenderPipeline.ExecuteBufferAtFrameEnding(func);
            yield return null;
            yield return null;
            int target = probeCount.x * probeCount.y * probeCount.z;
            for (int x = 0; x < probeCount.x; ++x)
            {
                for (int y = 0; y < probeCount.y; ++y)
                {
                    for (int z = 0; z < probeCount.z; ++z)
                    {
                        BakeMap(int3(x, y, z), rt, tempRT);
                        cbuffer.GenerateMips(rt);
                        cbuffer.SetComputeIntParam(shader, "_OffsetIndex", PipelineFunctions.DownDimension(int3(x, y, z), probeCount.xy));
                        cbuffer.DispatchCompute(shader, 0, RESOLUTION / 32, RESOLUTION / 32, 6);
                        cbuffer.DispatchCompute(shader, 1, 1, 1, 1);
                        yield return null;
                    }
                }
            }
            isRendering = false;
            yield return null;
            isRendered = true;
            byte[] byteArray = new byte[coeff.count * coeff.stride];
            coeff.GetData(byteArray);
            string path = "Assets/BinaryData/Irradiance/" + volumeName + ".mpipe";
            File.WriteAllBytes(path, byteArray);
            float4x4 localToWorld = transform.localToWorldMatrix;
            IrradianceResources.Volume volume = new IrradianceResources.Volume
            {
                position = transform.position,
                localToWorld = float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz),
                resolution = (uint3)probeCount,
                volumeName = volumeName,
                path = path
            };
            Debug.Log(volume.volumeName);
            saveTarget.allVolume[indexInList] = volume;
            EditorUtility.SetDirty(saveTarget);
            RenderTexture.ReleaseTemporary(rt);
            RenderTexture.ReleaseTemporary(tempRT);
            yield return null;
            Dispose();
        }
    }
#endif
}
