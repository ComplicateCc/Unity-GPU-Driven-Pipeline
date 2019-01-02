using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using System;
using Random = Unity.Mathematics.Random;
namespace MPipeline
{
    public struct RenderClusterOptions
    {
        public Vector4[] frustumPlanes;
        public CommandBuffer command;
        public bool isOrtho;
        public bool isClusterEnabled;
        public bool isTerrainEnabled;
        public ComputeShader cullingShader;
        public ComputeShader terrainCompute;
    }

    public struct HizOptions
    {
        public HizOcclusionData hizData;
        public HizDepth hizDepth;
        public Material linearLODMaterial;
        public RenderTargetIdentifier currentDepthTex;
        public Vector3 currentCameraUpVec;
    }
    [Serializable]
    public unsafe class SceneController
    {
        public struct SceneCommonData
        {
            public struct TextureIdentifier
            {
                public int usedCount;
                public int belonged;
            }
            public Material clusterMaterial;
            public Material terrainMaterial;
            public Dictionary<string, TextureIdentifier> texDict;
            public NativeList<int> avaiableTexs;
            public NativeList<int> avaiableProperties;
            public Material copyTextureMat;
            public ComputeBuffer texCopyBuffer;
            public ComputeBuffer propertyBuffer;
            public RenderTexture texArray;
            public TerrainDrawStreaming terrainDrawStreaming;
            public int GetIndex(string guid, out bool alreadyContained)
            {
                if (string.IsNullOrEmpty(guid))
                {
                    alreadyContained = false;
                    return -1;
                }
                if (texDict.ContainsKey(guid) && texDict[guid].usedCount > 0)
                {
                    TextureIdentifier ident = texDict[guid];
                    ident.usedCount++;
                    texDict[guid] = ident;
                    alreadyContained = true;
                    return ident.belonged;
                }
                else
                {
                    TextureIdentifier ident;
                    ident.usedCount = 1;
                    if (avaiableTexs.Length <= 0)
                    {
                        throw new Exception("No available texture lefted!");
                    }
                    ident.belonged = avaiableTexs[avaiableTexs.Length - 1];

                    avaiableTexs.RemoveLast();
                    texDict[guid] = ident;
                    alreadyContained = false;
                    return ident.belonged;
                }
            }
            public void RemoveTex(string guid)
            {
                if (texDict.ContainsKey(guid))
                {
                    TextureIdentifier ident = texDict[guid];
                    ident.usedCount--;
                    if (ident.usedCount <= 0)
                    {
                        texDict.Remove(guid);
                        avaiableTexs.Add(ident.belonged);
                    }
                    else
                    {
                        texDict[guid] = ident;
                    }
                }
            }
            public NativeArray<uint> GetPropertyIndex(int count)
            {
                if (count > avaiableProperties.Length) throw new Exception("Property pool is gone!");
                NativeArray<uint> properties = new NativeArray<uint>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                uint* ptr = properties.Ptr();
                int last = avaiableProperties.Length - 1;
                for (int i = 0; i < count; ++i)
                {
                    ptr[i] = (uint)avaiableProperties[last - i];
                }
                avaiableProperties.RemoveLast(count);
                return properties;
            }
            public void RemoveProperty(NativeArray<uint> pool)
            {
                uint* ptr = pool.Ptr();
                for (int i = 0; i < pool.Length; ++i)
                {
                    avaiableProperties.Add((int)ptr[i]);
                }
                pool.Dispose();
            }
        }
        public static SceneCommonData commonData;
        public static SceneController current;
        public PipelineBaseBuffer baseBuffer { get; private set; }
        public string mapResources = "SceneManager";
        private ClusterMatResources clusterResources;
        private List<SceneStreaming> allScenes;
        public NativeList<ulong> pointerContainer;
        public LoadingCommandQueue commandQueue;
        private MonoBehaviour behavior;
        [Header("Material Settings:")]
        public int resolution = 1024;
        public int texArrayCapacity = 50;
        public int propertyCapacity = 500;
        public void Awake(MonoBehaviour behavior)
        {
            current = this;
            addList = new NativeList<ulong>(10, Allocator.Persistent);
            this.behavior = behavior;
            baseBuffer = new PipelineBaseBuffer();
            clusterResources = Resources.Load<ClusterMatResources>("MapMat/" + mapResources);
            int clusterCount = 0;
            allScenes = new List<SceneStreaming>(clusterResources.clusterProperties.Count);
            foreach (var i in clusterResources.clusterProperties)
            {
                clusterCount += i.clusterCount;
                allScenes.Add(new SceneStreaming(i));
            }
            PipelineFunctions.InitBaseBuffer(baseBuffer, clusterResources, mapResources, clusterCount);
            pointerContainer = new NativeList<ulong>(clusterCount, Allocator.Persistent);
            commandQueue = new LoadingCommandQueue();
            RenderTextureDescriptor desc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGB32,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2DArray,
                enableRandomWrite = false,
                height = resolution,
                width = resolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                vrUsage = VRTextureUsage.None,
                volumeDepth = texArrayCapacity,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false
            };
            commonData = new SceneCommonData
            {
                texDict = new Dictionary<string, SceneCommonData.TextureIdentifier>(),
                avaiableProperties = new NativeList<int>(propertyCapacity, Allocator.Persistent),
                avaiableTexs = new NativeList<int>(texArrayCapacity, Allocator.Persistent),
                texCopyBuffer = new ComputeBuffer(resolution * resolution, sizeof(int)),
                propertyBuffer = new ComputeBuffer(propertyCapacity, sizeof(PropertyValue)),
                copyTextureMat = new Material(RenderPipeline.current.resources.copyShader),
                texArray = new RenderTexture(desc),
                clusterMaterial = new Material(RenderPipeline.current.resources.clusterRenderShader),
                terrainMaterial = new Material(RenderPipeline.current.resources.terrainShader),
                terrainDrawStreaming = new TerrainDrawStreaming(100, 16, RenderPipeline.current.resources.terrainCompute)
            };
            commonData.clusterMaterial.SetBuffer(ShaderIDs._PropertiesBuffer, commonData.propertyBuffer);
            commonData.clusterMaterial.SetTexture(ShaderIDs._MainTex, commonData.texArray);
            commonData.copyTextureMat.SetVector("_TextureSize", new Vector4(resolution, resolution));
            commonData.copyTextureMat.SetBuffer("_TextureBuffer", commonData.texCopyBuffer);
            for (int i = 0; i < propertyCapacity; ++i)
            {
                commonData.avaiableProperties.Add(i);
            }
            for (int i = 0; i < texArrayCapacity; ++i)
            {
                commonData.avaiableTexs.Add(i);
            }
            testNodeArray = new NativeList<ulong>(terrainTransforms.Length, Allocator.Persistent);
            foreach (var i in terrainTransforms)
            {
                TerrainQuadTree.QuadTreeNode* testNode = (TerrainQuadTree.QuadTreeNode*)UnsafeUtility.Malloc(sizeof(TerrainQuadTree.QuadTreeNode), 16, Allocator.Persistent);
                testNode->listPosition = -1;
                ref TerrainPanel panel = ref testNode->panel;
                if (i.localScale.x > 1.1f)
                    panel.edgeFlag = 0;
                else
                    panel.edgeFlag = 15;
                panel.extent = i.localScale * 0.5f;
                panel.position = i.position;
                panel.textureIndex = 0;
                panel.heightMapIndex = 0;
                testNodeArray.Add((ulong)testNode);
            }
        }

        public void OnDestroy()
        {
            if (current != this) return;
            current = null;
            PipelineFunctions.Dispose(baseBuffer);
            pointerContainer.Dispose();
            commandQueue = null;
            commonData.avaiableProperties.Dispose();
            commonData.avaiableTexs.Dispose();
            commonData.texCopyBuffer.Dispose();
            commonData.propertyBuffer.Dispose();
            commonData.texDict.Clear();
            commonData.texArray.Release();
            commonData.terrainDrawStreaming.Dispose();
            UnityEngine.Object.Destroy(commonData.terrainMaterial);
            UnityEngine.Object.Destroy(commonData.copyTextureMat);
            foreach (var i in testNodeArray)
            {
                UnsafeUtility.Free((void*)i, Allocator.Persistent);
            }
            testNodeArray.Dispose();
            addList.Dispose();
        }
        //Press number load scene
        public Transform[] terrainTransforms;
        public NativeList<ulong> addList;
        public NativeList<ulong> testNodeArray;
        public void Update()
        {
            /* int value;
             if (int.TryParse(Input.inputString, out value) && value < testNodeArray.Length)
             {
                 Random rd = new Random((uint)Guid.NewGuid().GetHashCode());
                 addList.Add(testNodeArray[value]);
                 TerrainQuadTree.QuadTreeNode* node = (TerrainQuadTree.QuadTreeNode*)testNodeArray[value];
                 if (node->listPosition < 0)
                 {
                     NativeArray<float> heightMap = new NativeArray<float>(commonData.terrainDrawStreaming.heightMapSize, Allocator.Temp);
                     for(int i = 0; i < heightMap.Length; ++i)
                     {
                         heightMap[i] = (float)(rd.NextDouble() * 0.2);
                     }
                     commonData.terrainDrawStreaming.AddQuadTrees(addList, heightMap);

                 }
                 else
                 {
                     commonData.terrainDrawStreaming.RemoveQuadTrees(addList);
                 }
                 addList.Clear();
             }
             */
            int value;
             if (int.TryParse(Input.inputString, out value))
             {
                 if (value < allScenes.Count)
                 {
                     SceneStreaming str = allScenes[value];
                     if (str.state == SceneStreaming.State.Loaded)
                         behavior.StartCoroutine(str.Delete());
                     else if (str.state == SceneStreaming.State.Unloaded)
                         behavior.StartCoroutine(str.Generate());
                 }
             }

        }
        public static bool GetBaseBuffer(out PipelineBaseBuffer result)
        {
            if (current == null)
            {
                result = null;
                return false;
            }
            result = current.baseBuffer;
            return result.clusterCount > 0;
        }

        #region DrawFunctions
        public void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets)
        {
            if (options.isClusterEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, options.cullingShader, options.frustumPlanes, options.command);
                PipelineFunctions.RunCullDispatching(baseBuffer, options.cullingShader, options.isOrtho, options.command);
                PipelineFunctions.RenderProceduralCommand(baseBuffer, commonData.clusterMaterial, options.command);
                options.command.DispatchCompute(options.cullingShader, 1, 1, 1, 1);
            }
            //TODO 绘制其他物体
            if (options.isTerrainEnabled)
            {
                commonData.terrainDrawStreaming.DrawTerrain(ref options, commonData.terrainMaterial, targets.renderTargetIdentifier, targets.depthIdentifier);
            }
            //TODO
        }

        public void DrawClusterOccSingleCheck(ref RenderClusterOptions options, ref HizOptions hizOpts, ref RenderTargets targets)
        {
            CommandBuffer buffer = options.command;
            if (options.isClusterEnabled)
            {
                buffer.SetComputeVectorParam(options.cullingShader, ShaderIDs._CameraUpVector, hizOpts.hizData.lastFrameCameraUp);
                buffer.SetComputeBufferParam(options.cullingShader, 5, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
                buffer.SetComputeTextureParam(options.cullingShader, 5, ShaderIDs._HizDepthTex, hizOpts.hizData.historyDepth);
                buffer.SetComputeVectorArrayParam(options.cullingShader, ShaderIDs.planes, options.frustumPlanes);
                buffer.SetComputeBufferParam(options.cullingShader, 5, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                buffer.SetComputeBufferParam(options.cullingShader, 5, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
                buffer.SetComputeBufferParam(options.cullingShader, PipelineBaseBuffer.ClearClusterKernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
                hizOpts.hizData.lastFrameCameraUp = hizOpts.currentCameraUpVec;
                hizOpts.hizData.lastFrameCameraUp = hizOpts.currentCameraUpVec;
                buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                PipelineFunctions.RenderProceduralCommand(baseBuffer, commonData.clusterMaterial, buffer);
                buffer.DispatchCompute(options.cullingShader, PipelineBaseBuffer.ClearClusterKernel, 1, 1, 1);
            }
            //TODO 绘制其他物体

            //TODO
            buffer.Blit(hizOpts.currentDepthTex, hizOpts.hizData.historyDepth, hizOpts.linearLODMaterial, 0);
            hizOpts.hizDepth.GetMipMap(hizOpts.hizData.historyDepth, buffer);
        }

        public void DrawClusterOccDoubleCheck(ref RenderClusterOptions options, ref HizOptions hizOpts, ref RenderTargets rendTargets)
        {
            CommandBuffer buffer = options.command;
            ComputeShader gpuFrustumShader = options.cullingShader;
            if (options.isClusterEnabled)
            {
                PipelineFunctions.UpdateOcclusionBuffer(
    baseBuffer, gpuFrustumShader,
    buffer,
    hizOpts.hizData,
    options.frustumPlanes,
    options.isOrtho);
                //绘制第一次剔除结果
                PipelineFunctions.DrawLastFrameCullResult(baseBuffer, buffer, commonData.clusterMaterial);
                //更新Vector，Depth Mip Map
                hizOpts.hizData.lastFrameCameraUp = hizOpts.currentCameraUpVec;
                PipelineFunctions.ClearOcclusionData(baseBuffer, buffer, gpuFrustumShader);
            }
            //TODO 绘制其他物体

            //TODO
            buffer.Blit(hizOpts.currentDepthTex, hizOpts.hizData.historyDepth, hizOpts.linearLODMaterial, 0);
            hizOpts.hizDepth.GetMipMap(hizOpts.hizData.historyDepth, buffer);
            if (options.isClusterEnabled)
            {
                //使用新数据进行二次剔除
                PipelineFunctions.OcclusionRecheck(baseBuffer, gpuFrustumShader, buffer, hizOpts.hizData);
                //绘制二次剔除结果
                buffer.SetRenderTarget(rendTargets.gbufferIdentifier, rendTargets.depthIdentifier);
                PipelineFunctions.DrawRecheckCullResult(baseBuffer, commonData.clusterMaterial, buffer);
                buffer.Blit(hizOpts.currentDepthTex, hizOpts.hizData.historyDepth, hizOpts.linearLODMaterial, 0);
                hizOpts.hizDepth.GetMipMap(hizOpts.hizData.historyDepth, buffer);
            }
        }

        public void DrawDirectionalShadow(Camera currentCam, ref RenderClusterOptions opts, ref ShadowmapSettings settings, ref ShadowMapComponent shadMap, Matrix4x4[] cascadeShadowMapVP)
        {
            const int CASCADELEVELCOUNT = 4;
            const int CASCADECLIPSIZE = (CASCADELEVELCOUNT + 1) * sizeof(float);
            ComputeShader gpuFrustumShader = opts.cullingShader;
            StaticFit staticFit;
            staticFit.resolution = settings.resolution;
            staticFit.mainCamTrans = currentCam;
            staticFit.frustumCorners = shadMap.frustumCorners;

            float* clipDistances = stackalloc float[CASCADECLIPSIZE];
            clipDistances[0] = shadMap.shadCam.nearClipPlane;
            clipDistances[1] = settings.firstLevelDistance;
            clipDistances[2] = settings.secondLevelDistance;
            clipDistances[3] = settings.thirdLevelDistance;
            clipDistances[4] = settings.farestDistance;
            opts.command.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            opts.command.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            for (int pass = 0; pass < CASCADELEVELCOUNT; ++pass)
            {
                Vector2 farClipDistance = new Vector2(clipDistances[pass], clipDistances[pass + 1]);
                PipelineFunctions.GetfrustumCorners(farClipDistance, ref shadMap, currentCam);
                // PipelineFunctions.SetShadowCameraPositionCloseFit(ref shadMap, ref settings);
                Matrix4x4 invpVPMatrix;
                PipelineFunctions.SetShadowCameraPositionStaticFit(ref staticFit, ref shadMap.shadCam, pass, cascadeShadowMapVP, out invpVPMatrix);
                PipelineFunctions.GetCullingPlanes(ref invpVPMatrix, opts.frustumPlanes);
                PipelineFunctions.SetBaseBuffer(baseBuffer, gpuFrustumShader, opts.frustumPlanes, opts.command);
                PipelineFunctions.RunCullDispatching(baseBuffer, gpuFrustumShader, true, opts.command);
                float* biasList = (float*)UnsafeUtility.AddressOf(ref settings.bias);
                PipelineFunctions.UpdateCascadeState(ref shadMap, opts.command, biasList[pass] / currentCam.farClipPlane, pass);
                opts.command.DrawProceduralIndirect(Matrix4x4.identity, shadMap.shadowDepthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
                opts.command.DispatchCompute(gpuFrustumShader, 1, 1, 1, 1);
            }
        }

        public void DrawCubeMap(MPointLight lit, Material depthMaterial, ref RenderClusterOptions opts, ref CubeCullingBuffer buffer, int offset)
        {
            CommandBuffer cb = opts.command;
            ComputeShader shader = opts.cullingShader;
            cb.SetComputeIntParam(shader, ShaderIDs._LightOffset, offset);
            ComputeShaderUtility.Dispatch(shader, cb, CubeFunction.RunFrustumCull, baseBuffer.clusterCount, 64);
            PerspCam cam = new PerspCam();
            cam.aspect = 1;
            cam.farClipPlane = lit.range;
            cam.nearClipPlane = 0.3f;
            cam.position = lit.position;
            cam.fov = 90f;
            Matrix4x4 vpMatrix;
            cb.SetGlobalVector(ShaderIDs._LightPos, new Vector4(lit.position.x, lit.position.y, lit.position.z, lit.range));
            cb.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            cb.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            //Forward
            cam.forward = Vector3.forward;
            cam.up = Vector3.down;
            cam.right = Vector3.left;
            cam.position = lit.position;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            int depthSlice = offset * 6;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 5);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            offset = offset * 20;
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            //Back
            cam.forward = Vector3.back;
            cam.up = Vector3.down;
            cam.right = Vector3.right;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            //Up
            cam.forward = Vector3.up;
            cam.up = Vector3.back;
            cam.right = Vector3.right;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            //Down
            cam.forward = Vector3.down;
            cam.up = Vector3.forward;
            cam.right = Vector3.right;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            //Right
            cam.forward = Vector3.right;
            cam.up = Vector3.down;
            cam.right = Vector3.forward;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            //Left
            cam.forward = Vector3.left;
            cam.up = Vector3.down;
            cam.right = Vector3.back;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
        }
        public void DrawCubeMap(MPointLight lit, Material depthMaterial, ref RenderClusterOptions opts, ref CubeCullingBuffer buffer, int offset, RenderTexture targetCopyTex)
        {
            CommandBuffer cb = opts.command;
            ComputeShader shader = opts.cullingShader;
            cb.SetComputeIntParam(shader, ShaderIDs._LightOffset, offset);
            ComputeShaderUtility.Dispatch(shader, cb, CubeFunction.RunFrustumCull, baseBuffer.clusterCount, 64);
            PerspCam cam = new PerspCam();
            cam.aspect = 1;
            cam.farClipPlane = lit.range;
            cam.nearClipPlane = 0.3f;
            cam.position = lit.position;
            cam.fov = 90f;
            Matrix4x4 vpMatrix;
            cb.SetGlobalVector(ShaderIDs._LightPos, new Vector4(lit.position.x, lit.position.y, lit.position.z, lit.range));
            cb.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            cb.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            //Forward
            cam.forward = Vector3.forward;
            cam.up = Vector3.down;
            cam.right = Vector3.left;
            cam.position = lit.position;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            int depthSlice = offset * 6;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 5);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            offset = offset * 20;
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 5, targetCopyTex, 5);
            //Back
            cam.forward = Vector3.back;
            cam.up = Vector3.down;
            cam.right = Vector3.right;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 4, targetCopyTex, 4);
            //Up
            cam.forward = Vector3.up;
            cam.up = Vector3.back;
            cam.right = Vector3.right;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 2, targetCopyTex, 2);
            //Down
            cam.forward = Vector3.down;
            cam.up = Vector3.forward;
            cam.right = Vector3.right;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 3, targetCopyTex, 3);
            //Right
            cam.forward = Vector3.right;
            cam.up = Vector3.down;
            cam.right = Vector3.forward;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            cb.CopyTexture(buffer.renderTarget, depthSlice, targetCopyTex, 0);
            //Left
            cam.forward = Vector3.left;
            cam.up = Vector3.down;
            cam.right = Vector3.back;
            cam.UpdateTRSMatrix();
            cam.UpdateProjectionMatrix();
            vpMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrix);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, offset);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 1, targetCopyTex, 1);
        }
        #endregion

    }
}