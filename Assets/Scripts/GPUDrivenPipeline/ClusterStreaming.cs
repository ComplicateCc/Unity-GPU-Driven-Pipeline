using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe struct ClusterStreaming
    {
        public TextAsset clusterText;
        public TextAsset pointText;
        public int length;
        public IEnumerator LoadTextAssets(string clusterPath, string pointPath, int length, List<ClusterStreaming> finishedList = null)
        {
            this.length = length;
            Application.backgroundLoadingPriority = ThreadPriority.Normal;
            var clusterRequest = Resources.LoadAsync<TextAsset>(clusterPath);
            var pointRequest = Resources.LoadAsync<TextAsset>(pointPath);
            yield return clusterRequest;
            yield return pointRequest;
            clusterText = clusterRequest.asset as TextAsset;
            pointText = pointRequest.asset as TextAsset;
            if (finishedList != null)
                finishedList.Add(this);
        }

        public void Unload()
        {
            Resources.UnloadAsset(clusterText);
            Resources.UnloadAsset(pointText);
        }
    }

    public unsafe struct ComputeList
    {
        public ComputeBuffer verticesBuffer;
        public ComputeBuffer clusterBuffer;
        public int* length;
    }

    public unsafe static class ClusterStreamingUtility
    {
        public static void GetData(byte[] clusterBytes, byte[] pointBytes, int length, out NativeArray<ClusterMeshData> clusterDatas, out NativeArray<Point> pointDatas)
        {
            clusterDatas = new NativeArray<ClusterMeshData>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            pointDatas = new NativeArray<Point>(length * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            fixed (byte* bt = &clusterBytes[0])
            {
                UnsafeUtility.MemCpy(clusterDatas.GetUnsafePtr(), bt, clusterBytes.Length);
            }
            fixed (byte* bt = &pointBytes[0])
            {
                UnsafeUtility.MemCpy(pointDatas.GetUnsafePtr(), bt, pointBytes.Length);
            }
        }
        private static readonly int[] _RemoveVar = new int[2];
        public static void LoadData(ref PipelineBaseBuffer baseBuffer, NativeArray<ClusterMeshData> clusterData, NativeArray<Point> pointData)
        {
            baseBuffer.clusterBuffer.SetData(clusterData, 0, baseBuffer.clusterCount, clusterData.Length);
            baseBuffer.verticesBuffer.SetData(pointData, 0, baseBuffer.clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, pointData.Length);
            baseBuffer.clusterCount += clusterData.Length;
            clusterData.Dispose();
            pointData.Dispose();
        }

        public static void LoadAll(ref PipelineBaseBuffer baseBuffer, MonoBehaviour currentMonoBehaviour, List<ClusterMatResources.ClusterProperty> properties, List<ClusterStreaming> clusterStreaming)
        {
            foreach (var i in properties)
            {
                ClusterStreaming streaming = new ClusterStreaming();
                currentMonoBehaviour.StartCoroutine(streaming.LoadTextAssets("MapInfos/" + i.name, "MapPoints/" + i.name, i.clusterCount, clusterStreaming));
            }
        }

        public static void RemoveElements(ref ComputeList list, CommandBuffer buffer, ComputeShader shader, int startPos, int range)
        {
            int dispatchLength = *list.length - (startPos + range);
            _RemoveVar[0] = startPos;
            _RemoveVar[1] = range + startPos;
            shader.SetInts(ShaderIDs._RemoveVar, _RemoveVar);
            shader.SetBuffer(0, ShaderIDs.verticesBuffer, list.verticesBuffer);
            shader.SetBuffer(1, ShaderIDs.clusterBuffer, list.clusterBuffer);
            *list.length -= range;
            if (dispatchLength > 0)
            {
                shader.Dispatch(0, dispatchLength, 1, 1);
                ComputeShaderUtility.Dispatch(shader, buffer, 1, dispatchLength, 256);
            }
        }
    }
}
