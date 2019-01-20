using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe class ProbeBaker : MonoBehaviour
    {
        public int3 probeCount = new int3(10, 10, 10);
        public float considerRange = 5;
        public ComputeShader decodeShader;
        public string path = "Assets/Test.txt";
        public PipelineResources resources;
        private RenderBuffer[][] renderBuffers;
        private RenderTexture[][] prepareRenderTexture;
        private const int RESOLUTION = 128;
        private ComputeBuffer resultbuffer;
        private uint2[] colorResults;
        private void Awake()
        {
            prepareRenderTexture = new RenderTexture[6][];
            renderBuffers = new RenderBuffer[6][];
            for(int i = 0; i < 6; ++i)
            {
                prepareRenderTexture[i] = new RenderTexture[2];
                prepareRenderTexture[i][0] = new RenderTexture(RESOLUTION, RESOLUTION, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                prepareRenderTexture[i][1] = new RenderTexture(RESOLUTION, RESOLUTION, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                renderBuffers[i] = new RenderBuffer[2];
                renderBuffers[i][0] = prepareRenderTexture[i][0].colorBuffer;
                renderBuffers[i][1] = prepareRenderTexture[i][1].colorBuffer;
            }
            resultbuffer = new ComputeBuffer(6 * RESOLUTION * RESOLUTION, sizeof(uint2));
            colorResults = new uint2[6 * RESOLUTION * RESOLUTION];
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
        private void BakeMap(int3 index)
        {
            RenderTexture depthBuffer = RenderTexture.GetTemporary(RESOLUTION, RESOLUTION, 32, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
            float3 left = transform.position - transform.lossyScale * 0.5f;
            float3 right = transform.position + transform.lossyScale * 0.5f;
            float3 position = lerp(left, right, ((float3)index + 0.5f) / probeCount);
            SceneController.DrawGIBuffer(renderBuffers, depthBuffer.depthBuffer, float4(position, considerRange), resources.gpuFrustumCulling);
            for(int i = 0; i < 6; ++i)
            {
                decodeShader.SetBuffer(0, "_ResultBuffer", resultbuffer);
                decodeShader.SetTexture(0, "_GBuffer0", prepareRenderTexture[i][0]);
                decodeShader.SetTexture(0, "_GBuffer1", prepareRenderTexture[i][1]);
                decodeShader.SetInt(ShaderIDs._OffsetIndex, i * RESOLUTION * RESOLUTION);
                decodeShader.Dispatch(0, RESOLUTION / 32, RESOLUTION / 32, 1);
            }
            RenderTexture.ReleaseTemporary(depthBuffer);
            resultbuffer.GetData(colorResults);
        }
        [EasyButtons.Button]
        public void BakeAll()
        {
            if (!SceneController.gpurpEnabled)
            {
                Debug.LogError("GPU Rendering Pipeline has not Started! Mission Abort!");
                return;
            }
            NativeArray<uint2> allColorResults = new NativeArray<uint2>(probeCount.x * probeCount.y * probeCount.z * colorResults.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int count = 0;
            for(int x = 0; x < probeCount.x; ++x)
            {
                for(int y = 0; y < probeCount.y; ++y)
                {
                    for(int z = 0; z < probeCount.z; ++z)
                    {
                        BakeMap(int3(x, y, z));
                        UnsafeUtility.MemCpy(allColorResults.GetUnsafePtr(), colorResults.Ptr(), colorResults.Length * sizeof(uint2));
                        count += colorResults.Length;
                    }
                }
            }
            TextureUtility.SaveData<uint2>(allColorResults.Ptr(), allColorResults.Length, path);
        }
    }

}