using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
namespace MPipeline
{
    public struct LoadCommand
    {
        public bool isInitialized;
        public Action initFunc;
        public LoadFunction load;
    }
    public delegate bool LoadFunction(ref PipelineBaseBuffer baseBuffer, PipelineResources resources);
    public unsafe class SceneStreaming
    {
        public static NativeList<ulong> pointerContainer;
        public static LoadingCommandQueue commandQueue;
        public static bool loading = false;
        public enum State
        {
            Unloaded, Loaded, Loading
        }
        public State state;
        private NativeArray<int> indicesBuffer;
        private byte[] pointBytesArray;
        private byte[] clusterBytesArray;
        private NativeArray<ClusterMeshData> clusterBuffer;
        private NativeArray<Point> pointsBuffer;
        private NativeArray<Vector2Int> results;
        private int resultLength;
        private Action generateAsyncFunc;
        private Action deleteAsyncFunc;
        private LoadCommand deleteCommand;
        private LoadCommand generateCommand;
        string fileName;
        int length;
        public SceneStreaming(string fileName, int length)
        {
            state = State.Unloaded;
            this.fileName = fileName;
            this.length = length;
            generateAsyncFunc = GenerateAsync;
            deleteAsyncFunc = DeleteAsync;
            deleteCommand = new LoadCommand
            {
                isInitialized = false,
                initFunc = DeleteInit,
                load = DeleteRun
            };
            generateCommand = new LoadCommand
            {
                isInitialized = false,
                initFunc = GenerateInit,
                load = GenerateRun
            };
        }
        private void GenerateAsync()
        {
            ClusterMeshData* clusterData = clusterBuffer.Ptr();
            Point* verticesData = pointsBuffer.Ptr();
            byte* clusterBytes = (byte*)UnsafeUtility.AddressOf(ref clusterBytesArray[0]);
            byte* pointBytes = (byte*)UnsafeUtility.AddressOf(ref pointBytesArray[0]);
            UnsafeUtility.MemCpy(clusterData, clusterBytes, indicesBuffer.Length * sizeof(ClusterMeshData));
            UnsafeUtility.MemCpy(verticesData, pointBytes, indicesBuffer.Length * PipelineBaseBuffer.CLUSTERCLIPCOUNT * sizeof(Point));
            int* indicesPtr = indicesBuffer.Ptr();
            for (int i = 0; i < indicesBuffer.Length; ++i)
            {
                indicesPtr[i] = pointerContainer.Length;
                pointerContainer.Add((ulong)(indicesPtr + i));
            }
            pointBytesArray = null;
            clusterBytesArray = null;
            lock (commandQueue)
            {
                commandQueue.Queue(generateCommand);
            }
        }

        //TODO
        //Use some clever resources loading system
        public IEnumerator Generate()
        {
            if (state == State.Unloaded)
            {
                state = State.Loading;
                while (loading)
                {
                    yield return null;
                }
                loading = true;
                clusterBuffer = new NativeArray<ClusterMeshData>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                pointsBuffer = new NativeArray<Point>(length * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                indicesBuffer = new NativeArray<int>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                ResourceRequest clusterRequest = Resources.LoadAsync<TextAsset>("MapInfos/" + fileName);
                ResourceRequest pointsRequest = Resources.LoadAsync<TextAsset>("MapPoints/" + fileName);
                yield return clusterRequest;
                yield return pointsRequest;
                pointBytesArray = ((TextAsset)pointsRequest.asset).bytes;
                clusterBytesArray = ((TextAsset)clusterRequest.asset).bytes;
                Resources.UnloadAsset(pointsRequest.asset);
                Resources.UnloadAsset(clusterRequest.asset);
                pointerContainer.AddCapacityTo(pointerContainer.Length + indicesBuffer.Length);
                LoadingThread.AddCommand(generateAsyncFunc);
            }
        }

        public IEnumerator Delete()
        {
            if (state == State.Loaded)
            {
                state = State.Loading;
                while (loading)
                {
                    yield return null;
                }
                loading = true;
                results = new NativeArray<Vector2Int>(indicesBuffer.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                resultLength = 0;
                LoadingThread.AddCommand(deleteAsyncFunc);
            }
        }

        private void DeleteAsync()
        {
            int targetListLength = pointerContainer.Length - indicesBuffer.Length;
            int* indicesPtr = indicesBuffer.Ptr();
            int currentIndex = pointerContainer.Length - 1;
            for (int i = 0; i < indicesBuffer.Length; ++i)
            {
                if (indicesPtr[i] >= targetListLength)
                {
                    indicesPtr[i] = -1;
                    pointerContainer[indicesPtr[i]] = 0;
                }
            }

            for (int i = 0; i < indicesBuffer.Length; ++i)
            {
                int index = indicesPtr[i];
                if (index >= 0)
                {
                    while (currentIndex >= 0 && pointerContainer[currentIndex] == 0)
                    {
                        currentIndex--;
                    }
                    if (currentIndex < 0)
                        goto FINALIZE;
                    Vector2Int value = new Vector2Int(index, currentIndex);
                    currentIndex--;
                    results[resultLength] = value;
                    pointerContainer[value.x] = pointerContainer[value.y];
                    *(int*)pointerContainer[value.x] = value.x;
                    resultLength += 1;
                }
            }
            FINALIZE:
            pointerContainer.RemoveLast(indicesBuffer.Length);
            lock (commandQueue)
            {
                commandQueue.Queue(deleteCommand);
            }
        }

        #region MainThreadCommand
        private ComputeBuffer indexBuffer;
        private int currentCount;
        private const int MAXIMUMINTCOUNT = 2000;
        private const int MAXIMUMVERTCOUNT = 30;
        private void DeleteInit()
        {
            indexBuffer = new ComputeBuffer(results.Length, sizeof(Vector2Int));
            currentCount = 0;
        }

        private bool DeleteRun(ref PipelineBaseBuffer baseBuffer, PipelineResources resources)
        {
            int targetCount = currentCount + MAXIMUMINTCOUNT;
            if (targetCount >= resultLength)
            {
                if (resultLength > 0)
                {
                    indexBuffer.SetData(results, currentCount, currentCount, resultLength - currentCount);
                    ComputeShader shader = resources.streamingShader;
                    shader.SetBuffer(0, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
                    shader.SetBuffer(1, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                    shader.SetBuffer(0, ShaderIDs._IndexBuffer, indexBuffer);
                    shader.SetBuffer(1, ShaderIDs._IndexBuffer, indexBuffer);
                    ComputeShaderUtility.Dispatch(shader, 0, resultLength, 256);
                    shader.Dispatch(1, resultLength, 1, 1);
                }
                baseBuffer.clusterCount -= indicesBuffer.Length;
                indexBuffer.Dispose();
                results.Dispose();
                indicesBuffer.Dispose();
                loading = false;
                state = State.Unloaded;
                return true;
            }
            else
            {
                indexBuffer.SetData(results, currentCount, currentCount, MAXIMUMINTCOUNT);
                currentCount = targetCount;
                return false;
            }
        }

        private void GenerateInit()
        {
            currentCount = 0;
        }

        private bool GenerateRun(ref PipelineBaseBuffer baseBuffer, PipelineResources resources)
        {
            int targetCount = currentCount + MAXIMUMVERTCOUNT;
            if (targetCount >= clusterBuffer.Length)
            {
                baseBuffer.clusterBuffer.SetData(clusterBuffer, currentCount, currentCount + baseBuffer.clusterCount, clusterBuffer.Length - currentCount);
                baseBuffer.verticesBuffer.SetData(pointsBuffer, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (clusterBuffer.Length - currentCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
                baseBuffer.clusterCount += clusterBuffer.Length;
                clusterBuffer.Dispose();
                pointsBuffer.Dispose();
                loading = false;
                state = State.Loaded;
                return true;
            }
            else
            {
                baseBuffer.clusterBuffer.SetData(clusterBuffer, currentCount, currentCount + baseBuffer.clusterCount, MAXIMUMVERTCOUNT);
                baseBuffer.verticesBuffer.SetData(pointsBuffer, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
                currentCount = targetCount;
                return false;
            }
        }
        #endregion
    }
}