using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
namespace MPipeline
{
    public unsafe sealed class SceneStreaming
    {
        public struct TextureInfos
        {
            public int index;
            public string texGUID;
            public string texType;
            public NativeArray<Color32> array;
        }
        public struct LightmapInfos
        {
            public int index;
            public int size;
            public string texGUID;
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
        private List<LightmapInfos> allLightmapDatas;
        public SceneStreaming(ClusterProperty property)
        {
            state = State.Unloaded;
            this.property = property;
            allTextureDatas = new List<TextureInfos>();
            allLightmapDatas = new List<LightmapInfos>();
        }
        static string[] allStrings = new string[3];
        private static byte[] bytesArray = new byte[8192];
        private static byte[] GetByteArray(int length)
        {
            if (bytesArray == null || bytesArray.Length < length)
            {
                bytesArray = new byte[length];
            }
            return bytesArray;
        }
        public void GenerateAsync(bool listCommand = true)
        {
            clusterBuffer = new NativeArray<CullBox>(property.clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            pointsBuffer = new NativeArray<Point>(property.clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indicesBuffer = new NativeArray<int>(property.clusterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeList<ulong> pointerContainer = SceneController.pointerContainer;
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
            // FileStream fileStream = new FileStream(sb.str, FileMode.Open, FileAccess.Read);
            using (FileStream reader = new FileStream(sb.str, FileMode.Open, FileAccess.Read))
            {
                int length = (int)reader.Length;
                byte[] bytes = GetByteArray(length);
                reader.Read(bytes, 0, length);
                fixed (byte* b = bytes)
                {
                    UnsafeUtility.MemCpy(clusterData, b, length);
                }
            }
            allStrings[0] = pointsPath;
            sb.Combine(allStrings);
            using (FileStream reader = new FileStream(sb.str, FileMode.Open, FileAccess.Read))
            {
                int length = (int)reader.Length;
                byte[] bytes = GetByteArray(length);
                reader.Read(bytes, 0, length);
                fixed (byte* b = bytes)
                {
                    UnsafeUtility.MemCpy(verticesData, b, length);
                }
            }
            int* indicesPtr = indicesBuffer.Ptr();
            LoadingCommandQueue commandQueue = SceneController.commandQueue;
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
                Point* pt = verticesData + i;
                pt->objIndex = poolPtr[pt->objIndex];
                pt->lightmapIndex = allLightmapDatas[pt->lightmapIndex].index;
            }
            if (listCommand)
            {
                lock (commandQueue)
                {
                    commandQueue.Queue(GenerateRun());
                }
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
            MStringBuilder sb = new MStringBuilder(150);
            allStrings[0] = "Assets/BinaryData/Lightmaps/";
            allStrings[2] = ".txt";
            for (int i = 0; i < property.lightmapGUIDs.Length; ++i)
            {
                LightmapPaths lightmapGUID = property.lightmapGUIDs[i];
                allStrings[1] = lightmapGUID.name;
                sb.Combine(allStrings);
                bool alreadyContained;
                int index = SceneController.commonData.GetLightmapIndex(lightmapGUID.name, out alreadyContained);
                if (index >= 0)
                {
                    if (alreadyContained)
                    {
                        allLightmapDatas.Add(new LightmapInfos
                        {
                            index = index,
                            texGUID = lightmapGUID.name,
                            size = 0
                        });
                    }
                    else
                    {
                        using (FileStream reader = new FileStream(sb.str, FileMode.Open, FileAccess.Read))
                        {
                            NativeArray<Color32> color;
                            int length = (int)reader.Length;
                            byte[] bytes = GetByteArray(length);
                            reader.Read(bytes, 0, length);
                            color = new NativeArray<Color32>(length / sizeof(Color32), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                            fixed (byte* source = bytes)
                            {
                                UnsafeUtility.MemCpy(color.GetUnsafePtr(), source, Mathf.Min(color.Length * sizeof(Color32), length));
                            }
                            allLightmapDatas.Add(new LightmapInfos
                            {
                                array = color,
                                index = index,
                                texGUID = lightmapGUID.name,
                                size = lightmapGUID.size
                            });
                        }
                    }
                }
            }
            for (int i = 0; i < values.Length; ++i)
            {
                ref PropertyValue value = ref values[i];
                int* indexPtr = (int*)UnsafeUtility.AddressOf(ref value.textureIndex);
                for (int a = 0; a < property.texPaths.Length; ++a)
                {
                    string texName = property.texPaths[a].instancingIDs[i];
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
                        else
                        {
                            using (FileStream reader = new FileStream(sb.str, FileMode.Open, FileAccess.Read))
                            {
                                int length = (int)reader.Length;
                                byte[] bytes = GetByteArray(length);
                                reader.Read(bytes, 0, length);
                                int res = SceneController.resolution;
                                NativeArray<Color32> allColors = new NativeArray<Color32>(res * res, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                                fixed (byte* source = bytes)
                                {
                                    UnsafeUtility.MemCpy(allColors.GetUnsafePtr(), source, Mathf.Min(allColors.Length * sizeof(Color32), length));
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
        }

        public void UnloadTextures()
        {
            foreach (var i in allTextureDatas)
            {
                SceneController.commonData.RemoveTex(i.texGUID);
            }
            foreach (var i in allLightmapDatas)
            {
                SceneController.commonData.RemoveTex(i.texGUID);
            }
            allTextureDatas.Clear();
            allLightmapDatas.Clear();
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

        public void DeleteInEditor()
        {
            results = new NativeArray<Vector2Int>(indicesBuffer.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            resultLength = 0;
            DeleteAsync(false);
            IEnumerator syncFunc = DeleteRun();
            while (syncFunc.MoveNext()) ;
        }

        public void GenerateInEditor()
        {
            GenerateAsync(false);
            IEnumerator syncFunc = GenerateRun();
            while (syncFunc.MoveNext()) ;
        }

        public void DeleteAsync(bool listCommand = true)
        {
            ref NativeList<ulong> pointerContainer = ref SceneController.pointerContainer;
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
            LoadingCommandQueue commandQueue = SceneController.commandQueue;
            SceneController.commonData.RemoveProperty(propertiesPool);
            if (listCommand)
            {
                lock (commandQueue)
                {
                    commandQueue.Queue(DeleteRun());
                }
            }
        }

        #region MainThreadCommand
        private const int MAXIMUMINTCOUNT = 5000;
        private const int MAXIMUMVERTCOUNT = 100;

        private IEnumerator DeleteRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
            ComputeBuffer indexBuffer = SceneController.commonData.GetTempPropertyBuffer(results.Length, 8);//sizeof(Vector2Int)
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
                ComputeShader shader = resources.shaders.streamingShader;
                shader.SetBuffer(0, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
                shader.SetBuffer(1, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                shader.SetBuffer(0, ShaderIDs._IndexBuffer, indexBuffer);
                shader.SetBuffer(1, ShaderIDs._IndexBuffer, indexBuffer);
                ComputeShaderUtility.Dispatch(shader, 0, resultLength, 256);
                shader.Dispatch(1, resultLength, 1, 1);
            }
            UnloadTextures();
            baseBuffer.clusterCount -= indicesBuffer.Length;
            results.Dispose();
            indicesBuffer.Dispose();
            loading = false;
            state = State.Unloaded;
        }

        private IEnumerator GenerateRun()
        {
            PipelineResources resources = RenderPipeline.current.resources;
            PipelineBaseBuffer baseBuffer = SceneController.baseBuffer;
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
            int clusterCount = clusterBuffer.Length;
            clusterBuffer.Dispose();
            pointsBuffer.Dispose();
            loading = false;
            state = State.Loaded;
            yield return null;
            ComputeShader texCopyShader = resources.shaders.texCopyShader;
            foreach (var i in allTextureDatas)
            {
                if (i.array.IsCreated)
                {
                    SceneController.commonData.texCopyBuffer.SetData(i.array);
                    RenderTexture rt = SceneController.commonData.texArray;
                    texCopyShader.SetBuffer(3, ShaderIDs._Buffer, SceneController.commonData.texCopyBuffer);
                    texCopyShader.SetInt(ShaderIDs._Width, rt.width);
                    texCopyShader.SetInt(ShaderIDs._OffsetIndex, i.index);
                    texCopyShader.SetInt(ShaderIDs._Scale, 1);
                    texCopyShader.SetTexture(3, ShaderIDs._OutputTex, rt);
                    texCopyShader.Dispatch(3, rt.width / 8, rt.height / 8, 1);
                    i.array.Dispose();
                    yield return null;
                }
            }
            foreach (var i in allLightmapDatas)
            {
                if (i.array.IsCreated)
                {
                    const int unitSize = 1024 * 1024;
                    int length = i.array.Length / unitSize;
                    for (int a = 0; a < length; ++a)
                    {
                        SceneController.commonData.lightmapCopyBuffer.SetData(i.array, a * unitSize, a * unitSize, Mathf.Min(unitSize, i.array.Length - a * unitSize));
                        yield return null;
                    }
                    RenderTexture rt = SceneController.commonData.lightmapArray;
                    texCopyShader.SetBuffer(1, ShaderIDs._Buffer, SceneController.commonData.lightmapCopyBuffer);
                    texCopyShader.SetInt(ShaderIDs._Width, i.size);
                    texCopyShader.SetInt(ShaderIDs._OffsetIndex, i.index);
                    texCopyShader.SetTexture(1, ShaderIDs._OutputTex, rt);
                    texCopyShader.SetInt(ShaderIDs._Scale, rt.width / i.size);
                    texCopyShader.Dispatch(1, rt.width / 8, rt.height / 8, 1);
                    i.array.Dispose();
                    yield return null;
                }
            }
            ComputeShader copyShader = resources.shaders.gpuFrustumCulling;
            //TODO
            //Load Property
            const int loadPropertyKernel = 6;
            ComputeBuffer currentPropertyBuffer = SceneController.commonData.GetTempPropertyBuffer(property.properties.Length, PROPERTYVALUESIZE);
            currentPropertyBuffer.SetData(property.properties);
            ComputeBuffer propertyIndexBuffer = SceneController.commonData.GetTempPropertyBuffer(propertiesPool.Length, 4);
            propertyIndexBuffer.SetData(propertiesPool);
            copyShader.SetBuffer(loadPropertyKernel, ShaderIDs._PropertiesBuffer, SceneController.commonData.propertyBuffer);
            copyShader.SetBuffer(loadPropertyKernel, ShaderIDs._TempPropBuffer, currentPropertyBuffer);
            copyShader.SetBuffer(loadPropertyKernel, ShaderIDs._IndexBuffer, propertyIndexBuffer);
            ComputeShaderUtility.Dispatch(copyShader, loadPropertyKernel, propertiesPool.Length, 64);
            baseBuffer.clusterCount += clusterCount;
        }
        #endregion
    }
}