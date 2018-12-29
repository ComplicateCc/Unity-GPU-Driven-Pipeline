using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
namespace MPipeline
{
    public unsafe class SceneStreaming
    {
        public struct TextureInfos
        {
            public int index;
            public string texGUID;
            public string texType;
            public NativeArray<Color32> array;
        }
        public static bool loading = false;
        public enum State
        {
            Unloaded, Loaded, Loading
        }
        public State state;
        private NativeArray<int> indicesBuffer;
        private NativeArray<CullBox> clusterBuffer;
        private NativeArray<Point> pointsBuffer;
        private NativeArray<Vector2Int> results;
        private NativeArray<uint> propertiesPool;
        private int resultLength;
        private static Action<object> generateAsyncFunc = (obj) =>
        {
            SceneStreaming str = obj as SceneStreaming;
            str.GenerateAsync();
        };
        private static Action<object> deleteAsyncFunc = (obj) =>
        {
            SceneStreaming str = obj as SceneStreaming;
            str.DeleteAsync();
        };
        ClusterProperty property;
        private List<TextureInfos> allTextureDatas;
        public SceneStreaming(ClusterProperty property)
        {
            state = State.Unloaded;
            this.property = property;
            allTextureDatas = new List<TextureInfos>();
        }
        static string[] allStrings = new string[3];
        public void GenerateAsync()
        {
            clusterBuffer = new NativeArray<CullBox>(property.clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pointsBuffer = new NativeArray<Point>(property.clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indicesBuffer = new NativeArray<int>(property.clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeList<ulong> pointerContainer = SceneController.current.pointerContainer;
            pointerContainer.AddCapacityTo(pointerContainer.Length + indicesBuffer.Length);
            CullBox* clusterData = clusterBuffer.Ptr();
            Point* verticesData = pointsBuffer.Ptr();
            const string infosPath = "Assets/BinaryData/MapInfos/";
            const string pointsPath = "Assets/BinaryData/MapPoints/";
            MStringBuilder sb = new MStringBuilder(pointsPath.Length + property.name.Length + ".txt".Length);
            allStrings[0] = infosPath;
            allStrings[1] = property.name;
            allStrings[2] = ".txt";
            sb.Combine(allStrings);
            using (BinaryReader reader = new BinaryReader(File.Open(sb.str, FileMode.Open)))
            {
                byte[] bytes = reader.ReadBytes((int)reader.BaseStream.Length);
                fixed (byte* b = bytes)
                {
                    UnsafeUtility.MemCpy(clusterData, b, bytes.Length);
                }
            }
            allStrings[0] = pointsPath;
            sb.Combine(allStrings);
            using (BinaryReader reader = new BinaryReader(File.Open(sb.str, FileMode.Open)))
            {
                byte[] bytes = reader.ReadBytes((int)reader.BaseStream.Length);
                fixed (byte* b = bytes)
                {
                    UnsafeUtility.MemCpy(verticesData, b, bytes.Length);
                }
            }
            int* indicesPtr = indicesBuffer.Ptr();
            LoadingCommandQueue commandQueue = SceneController.current.commandQueue;
            for (int i = 0; i < indicesBuffer.Length; ++i)
            {
                indicesPtr[i] = pointerContainer.Length;
                pointerContainer.Add((ulong)(indicesPtr + i));
            }
            LoadTextures();
            propertiesPool = SceneController.commonData.GetPropertyIndex(property.properties.Length);
            uint* poolPtr = propertiesPool.Ptr();
            for (int i = 0; i < pointsBuffer.Length; ++i)
            {
                verticesData[i].objIndex = poolPtr[verticesData[i].objIndex];
            }
            lock (commandQueue)
            {
                commandQueue.Queue(GenerateRun());
            }
        }
        static readonly int PROPERTYVALUESIZE = sizeof(PropertyValue);
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
                LoadingThread.AddCommand(generateAsyncFunc, this);
            }
        }
        //TODO
        public void LoadTextures()
        {
            PropertyValue[] values = property.properties;
            if (property.texPaths.Length > 3)
            {
                Debug.LogError("Scene: " + property.name + "'s texture type count is larger than 3! That is illegal!");
                return;
            }
            for (int i = 0; i < values.Length; ++i)
            {
                ref PropertyValue value = ref values[i];
                int* indexPtr = (int*)UnsafeUtility.AddressOf(ref value.textureIndex);
                for (int a = 0; a < property.texPaths.Length; ++a)
                {
                    string texName = property.texPaths[a].instancingIDs[i];
                    MStringBuilder sb = new MStringBuilder(texName.Length + 50);
                    allStrings[0] = "Assets/BinaryData/Textures/";
                    allStrings[1] = texName;
                    allStrings[2] = ".txt";
                    sb.Combine(allStrings);
                    string texType = property.texPaths[a].texName;
                    bool alreadyContained;
                    indexPtr[a] = SceneController.commonData.GetIndex(texName, out alreadyContained);
                    if (indexPtr[a] >= 0)
                    {
                        if (alreadyContained)
                        {
                            allTextureDatas.Add(new TextureInfos
                            {
                                array = new NativeArray<Color32>(),
                                index = indexPtr[a],
                                texGUID = texName,
                                texType = texType
                            });
                        }
                        using (BinaryReader reader = new BinaryReader(File.Open(sb.str, FileMode.Open)))
                        {
                            byte[] bytes = reader.ReadBytes((int)reader.BaseStream.Length);
                            NativeArray<Color32> allColors = new NativeArray<Color32>(SceneController.current.resolution * SceneController.current.resolution, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                            fixed (byte* source = bytes)
                            {
                                UnsafeUtility.MemCpy(allColors.GetUnsafePtr(), source, Mathf.Min(allColors.Length * sizeof(Color32), bytes.Length));
                            }
                            allTextureDatas.Add(new TextureInfos
                            {
                                array = allColors,
                                index = indexPtr[a],
                                texGUID = texName,
                                texType = texType
                            });
                        }
                    }
                }
            }
        }

        public void UnloadTextures()
        {
            foreach (var i in allTextureDatas)
            {
                SceneController.commonData.RemoveTex(i.texGUID);
            }
            allTextureDatas.Clear();
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
                LoadingThread.AddCommand(deleteAsyncFunc, this);
            }
        }

        public void DeleteAsync()
        {
            ref NativeList<ulong> pointerContainer = ref SceneController.current.pointerContainer;
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
                    while (pointerContainer[currentIndex] == 0)
                    {
                        currentIndex--;
                        if (currentIndex < 0)
                            goto FINALIZE;
                    }
                    
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
            LoadingCommandQueue commandQueue = SceneController.current.commandQueue;
            SceneController.commonData.RemoveProperty(propertiesPool);
            lock (commandQueue)
            {
                commandQueue.Queue(DeleteRun());
            }
        }

        #region MainThreadCommand
        private const int MAXIMUMINTCOUNT = 5000;
        private const int MAXIMUMVERTCOUNT = 100;

        private IEnumerator DeleteRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.current.baseBuffer;
            ComputeBuffer indexBuffer = new ComputeBuffer(results.Length, 8);//sizeof(Vector2Int)
            int currentCount = 0;
            int targetCount;
            while ((targetCount = currentCount + MAXIMUMINTCOUNT) < resultLength)
            {
                indexBuffer.SetData(results, currentCount, currentCount, MAXIMUMINTCOUNT);
                currentCount = targetCount;
                yield return null;
            }
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
            UnloadTextures();
            baseBuffer.clusterCount -= indicesBuffer.Length;
            indexBuffer.Dispose();
            results.Dispose();
            indicesBuffer.Dispose();
            loading = false;
            state = State.Unloaded;
        }

        private IEnumerator GenerateRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.current.baseBuffer;
            int targetCount;
            int currentCount = 0;
            while ((targetCount = currentCount + MAXIMUMVERTCOUNT) < clusterBuffer.Length)
            {
                baseBuffer.clusterBuffer.SetData(clusterBuffer, currentCount, currentCount + baseBuffer.clusterCount, MAXIMUMVERTCOUNT);
                baseBuffer.verticesBuffer.SetData(pointsBuffer, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, MAXIMUMVERTCOUNT * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
                currentCount = targetCount;
                yield return null;
            }
            //TODO
            baseBuffer.clusterBuffer.SetData(clusterBuffer, currentCount, currentCount + baseBuffer.clusterCount, clusterBuffer.Length - currentCount);
            baseBuffer.verticesBuffer.SetData(pointsBuffer, currentCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (currentCount + baseBuffer.clusterCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT, (clusterBuffer.Length - currentCount) * PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            baseBuffer.clusterCount += clusterBuffer.Length;
            clusterBuffer.Dispose();
            pointsBuffer.Dispose();
            loading = false;
            state = State.Loaded;
            yield return null;
            foreach (var i in allTextureDatas)
            {
                if (i.array.IsCreated)
                {
                    SceneController.commonData.texCopyBuffer.SetData(i.array);
                    RenderTexture rt = SceneController.commonData.texArray;
                    Graphics.SetRenderTarget(rt, 0, CubemapFace.Unknown, i.index);
                    int pass = i.texType == "_MainTex" ? 0 : 1;
                    SceneController.commonData.copyTextureMat.SetPass(pass);
                    Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
                    i.array.Dispose();
                    yield return null;
                }
            }
            ComputeShader copyShader = resources.gpuFrustumCulling;
            //TODO
            //Load Property
            const int loadPropertyKernel = 6;
            ComputeBuffer currentPropertyBuffer = new ComputeBuffer(property.properties.Length, PROPERTYVALUESIZE);
            currentPropertyBuffer.SetData(property.properties);
            ComputeBuffer propertyIndexBuffer = new ComputeBuffer(propertiesPool.Length, 4);
            propertyIndexBuffer.SetData(propertiesPool);
            copyShader.SetBuffer(loadPropertyKernel, ShaderIDs._PropertiesBuffer, SceneController.commonData.propertyBuffer);
            copyShader.SetBuffer(loadPropertyKernel, ShaderIDs._TempPropBuffer, currentPropertyBuffer);
            copyShader.SetBuffer(loadPropertyKernel, ShaderIDs._IndexBuffer, propertyIndexBuffer);
            ComputeShaderUtility.Dispatch(copyShader, loadPropertyKernel, propertiesPool.Length, 64);
            currentPropertyBuffer.Dispose();
            propertyIndexBuffer.Dispose();
        }
        #endregion
    }
}