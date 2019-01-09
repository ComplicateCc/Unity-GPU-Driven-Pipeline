using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using System;
using System.Runtime.CompilerServices;
namespace MPipeline
{
    public struct RenderClusterOptions
    {
        public Vector4[] frustumPlanes;
        public CommandBuffer command;
        public bool isOrtho;
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
    public unsafe abstract class SceneController
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
        protected static SceneController onlySRP = new SceneControllerWithOnlySRP();
        protected static SceneControllerWithGPURPEnabled gpurp = null;
        public static SceneController current;
        public static void SetSingleton()
        {
            if (gpurp != null && gpurp.baseBuffer.clusterCount > 0) current = gpurp;
            else current = onlySRP;
        }
        public abstract void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets, ref PipelineCommandData data, Camera cam);
        public abstract void DrawSpotLight(ref RenderClusterOptions options, ref PipelineCommandData data, Camera currentCam, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand, Texture shadowCache);
        public abstract void DrawClusterOccDoubleCheck(ref RenderClusterOptions options, ref HizOptions hizOpts, ref RenderTargets rendTargets, ref PipelineCommandData data, Camera cam);
        public abstract void DrawDirectionalShadow(Camera currentCam, ref PipelineCommandData data, ref RenderClusterOptions opts, ref ShadowmapSettings settings, ref ShadowMapComponent shadMap, Matrix4x4[] cascadeShadowMapVP);
        public abstract void DrawCubeMap(MLight lit, Light light, Material depthMaterial, ref RenderClusterOptions opts, ref CubeCullingBuffer buffer, int offset, RenderTexture targetCopyTex, ref PipelineCommandData data);
    }
    [Serializable]
    public unsafe class SceneControllerWithGPURPEnabled : SceneController
    {
        private PipelineResources resources;
        public PipelineBaseBuffer baseBuffer { get; private set; }
        public string mapResources = "SceneManager";
        private ClusterMatResources clusterResources;
        private List<SceneStreaming> allScenes;
        public NativeList<ulong> pointerContainer;
        public LoadingCommandQueue commandQueue;
        [Header("Material Settings:")]
        public int resolution = 1024;
        public int texArrayCapacity = 50;
        public int propertyCapacity = 500;
        public void Awake(PipelineResources resources)
        {
            this.resources = resources;
            gpurp = this;
            addList = new NativeList<ulong>(10, Allocator.Persistent);
            baseBuffer = new PipelineBaseBuffer();
            clusterResources = Resources.Load<ClusterMatResources>("MapMat/" + mapResources);
            int clusterCount = 0;
            allScenes = new List<SceneStreaming>(clusterResources.clusterProperties.Count);
            foreach (var i in clusterResources.clusterProperties)
            {
                clusterCount += i.clusterCount;
                allScenes.Add(new SceneStreaming(i, this));
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
                copyTextureMat = new Material(resources.copyShader),
                texArray = new RenderTexture(desc),
                clusterMaterial = new Material(resources.clusterRenderShader),
                terrainMaterial = new Material(resources.terrainShader),
                terrainDrawStreaming = new TerrainDrawStreaming(100, 16, resources.terrainCompute)
            };

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateCopyMat()
        {
            commonData.copyTextureMat.SetVector(ShaderIDs._TextureSize, new Vector4(resolution, resolution));
            commonData.copyTextureMat.SetBuffer(ShaderIDs._TextureBuffer, commonData.texCopyBuffer);
        }
        public void TransformMapPosition(int startPos)
        {
            if (baseBuffer.clusterCount - startPos <= 0) return;
            resources.gpuFrustumCulling.SetInt(ShaderIDs._OffsetIndex, startPos);
            resources.gpuFrustumCulling.SetBuffer(PipelineBaseBuffer.MoveVertex, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            resources.gpuFrustumCulling.SetBuffer(PipelineBaseBuffer.MoveCluster, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            resources.gpuFrustumCulling.Dispatch(PipelineBaseBuffer.MoveVertex, baseBuffer.clusterCount - startPos, 1, 1);
            ComputeShaderUtility.Dispatch(resources.gpuFrustumCulling, PipelineBaseBuffer.MoveCluster, baseBuffer.clusterCount - startPos, 64);
        }
        public void Dispose()
        {
            gpurp = null;
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
            UnityEngine.Object.DestroyImmediate(commonData.terrainMaterial);
            UnityEngine.Object.DestroyImmediate(commonData.copyTextureMat);
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
        public void Update(MonoBehaviour behavior)
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
        private bool GetBaseBuffer(out PipelineBaseBuffer result)
        {
            result = baseBuffer;
            return result.clusterCount > 0;
        }
        public override void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets, ref PipelineCommandData data, Camera cam)
        {

            options.command.SetGlobalBuffer(ShaderIDs._PropertiesBuffer, commonData.propertyBuffer);
            options.command.SetGlobalTexture(ShaderIDs._MainTex, commonData.texArray);
            PipelineFunctions.SetBaseBuffer(baseBuffer, options.cullingShader, options.frustumPlanes, options.command);
            PipelineFunctions.RunCullDispatching(baseBuffer, options.cullingShader, options.isOrtho, options.command);
            PipelineFunctions.RenderProceduralCommand(baseBuffer, commonData.clusterMaterial, options.command);
            options.command.DispatchCompute(options.cullingShader, 1, 1, 1, 1);
            data.ExecuteCommandBuffer();
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = cam.cullingMask
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("GBuffer"));
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.CommonOpaque,
                sortMode = DrawRendererSortMode.Perspective,
                cameraPosition = cam.transform.position
            };
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.PerObjectMotionVectors;
            data.context.DrawRenderers(data.cullResults.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            //TODO
        }
        public override void DrawSpotLight(ref RenderClusterOptions options, ref PipelineCommandData data, Camera currentCam, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand, Texture shadowCache)
        {
            ref SpotLightMatrix spotLightMatrix = ref spotcommand.shadowMatrices[spotLights.shadowIndex];
            spotLights.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;
            options.command.SetRenderTarget(spotcommand.renderTarget, 0, CubemapFace.Unknown, spotLights.shadowIndex);
            options.command.CopyTexture(spotcommand.renderTarget, spotLights.shadowIndex, shadowCache, 0);
            options.command.ClearRenderTarget(true, true, new Color(5, 1, 1, 1));
            options.command.SetGlobalVector(ShaderIDs._LightPos, (Vector3)spotLights.lightCone.vertex);
            options.command.SetGlobalFloat(ShaderIDs._LightRadius, spotLights.lightCone.height);
            options.command.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, true) * spotLightMatrix.worldToCamera);

            PipelineFunctions.SetBaseBuffer(baseBuffer, options.cullingShader, spotcommand.GetCullingPlane(spotLightMatrix.frustumPlanes), options.command);
            PipelineFunctions.RunCullDispatching(baseBuffer, options.cullingShader, false, options.command);
            PipelineFunctions.RenderProceduralCommand(baseBuffer, spotcommand.clusterShadowMaterial, options.command);
            options.command.DispatchCompute(options.cullingShader, 1, 1, 1, 1);

            data.ExecuteCommandBuffer();
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = currentCam.cullingMask
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("SpotLightPass"));
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.CommonOpaque,
                sortMode = DrawRendererSortMode.Perspective,
                cameraPosition = spotLights.lightCone.vertex
            };
            CullResults.GetCullingParameters(currentCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

        }

        public override void DrawClusterOccDoubleCheck(ref RenderClusterOptions options, ref HizOptions hizOpts, ref RenderTargets rendTargets, ref PipelineCommandData data, Camera cam)
        {
            CommandBuffer buffer = options.command;
            ComputeShader gpuFrustumShader = options.cullingShader;

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
            //TODO 绘制其他物体
            data.ExecuteCommandBuffer();
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = cam.cullingMask
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("GBuffer"));
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.CommonOpaque,
                sortMode = DrawRendererSortMode.Perspective,
                cameraPosition = cam.transform.position
            };
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.PerObjectMotionVectors;
            data.context.DrawRenderers(data.cullResults.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            //TODO
            buffer.Blit(hizOpts.currentDepthTex, hizOpts.hizData.historyDepth, hizOpts.linearLODMaterial, 0);
            hizOpts.hizDepth.GetMipMap(hizOpts.hizData.historyDepth, buffer);

            //使用新数据进行二次剔除
            PipelineFunctions.OcclusionRecheck(baseBuffer, gpuFrustumShader, buffer, hizOpts.hizData);
            //绘制二次剔除结果
            buffer.SetRenderTarget(rendTargets.gbufferIdentifier, rendTargets.depthIdentifier);
            PipelineFunctions.DrawRecheckCullResult(baseBuffer, commonData.clusterMaterial, buffer);
            buffer.Blit(hizOpts.currentDepthTex, hizOpts.hizData.historyDepth, hizOpts.linearLODMaterial, 0);
            hizOpts.hizDepth.GetMipMap(hizOpts.hizData.historyDepth, buffer);

        }
        public override void DrawDirectionalShadow(Camera currentCam, ref PipelineCommandData data, ref RenderClusterOptions opts, ref ShadowmapSettings settings, ref ShadowMapComponent shadMap, Matrix4x4[] cascadeShadowMapVP)
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
                PipelineFunctions.SetShadowCameraPositionStaticFit(ref staticFit, ref shadMap.shadCam, pass, (float4x4*)cascadeShadowMapVP.Ptr());
                Vector4* vec = opts.frustumPlanes.Ptr();
                PipelineFunctions.GetCullingPlanes((float4*)vec, shadMap.shadCam.size, shadMap.shadCam.nearClipPlane, shadMap.shadCam.farClipPlane,
                                                    shadMap.shadCam.up, shadMap.shadCam.right, shadMap.shadCam.forward, shadMap.shadCam.position);
                PipelineFunctions.SetBaseBuffer(baseBuffer, gpuFrustumShader, opts.frustumPlanes, opts.command);
                PipelineFunctions.RunCullDispatching(baseBuffer, gpuFrustumShader, true, opts.command);
                float* biasList = (float*)UnsafeUtility.AddressOf(ref settings.bias);
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(ref shadMap, opts.command, biasList[pass] / currentCam.farClipPlane, pass, out vpMatrix);
                opts.command.DrawProceduralIndirect(Matrix4x4.identity, shadMap.shadowDepthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
                opts.command.DispatchCompute(gpuFrustumShader, 1, 1, 1, 1);
                data.ExecuteCommandBuffer();
                FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
                {
                    renderQueueRange = RenderQueueRange.opaque,
                    layerMask = currentCam.cullingMask
                };
                data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("DirectionalLight"));
                data.defaultDrawSettings.flags = DrawRendererFlags.EnableDynamicBatching;
                data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
                data.defaultDrawSettings.sorting = new DrawRendererSortSettings
                {
                    flags = SortFlags.CommonOpaque,
                    worldToCameraMatrix = shadMap.shadCam.worldToCameraMatrix,
                    sortMode = DrawRendererSortMode.Orthographic
                };
                shadMap.cameraComponent.worldToCameraMatrix = shadMap.shadCam.worldToCameraMatrix;
                shadMap.cameraComponent.projectionMatrix = shadMap.shadCam.projectionMatrix;
                CullResults.GetCullingParameters(shadMap.cameraComponent, out data.cullParams);
                data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
                CullResults results = CullResults.Cull(ref data.cullParams, data.context);
                data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
                data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            }
        }
        public override void DrawCubeMap(MLight lit, Light light, Material depthMaterial, ref RenderClusterOptions opts, ref CubeCullingBuffer buffer, int offset, RenderTexture targetCopyTex, ref PipelineCommandData data)
        {
            CommandBuffer cb = opts.command;
            ComputeShader shader = opts.cullingShader;
            ComputeShaderUtility.Dispatch(shader, cb, CubeCullingBuffer.RunFrustumCull, baseBuffer.clusterCount, 64);
            Vector3 position = lit.transform.position;
            cb.SetGlobalVector(ShaderIDs._LightPos, new Vector4(position.x, position.y, position.z, light.range));
            cb.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            cb.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            ref CubemapViewProjMatrix vpMatrices = ref buffer.vpMatrices[offset];
            buffer.StartCull(baseBuffer, cb, vpMatrices.frustumPlanes);
            Matrix4x4 projMat = GL.GetGPUProjectionMatrix(vpMatrices.projMat, true);
            lit.shadowCam.projectionMatrix = projMat;
            lit.shadowCam.worldToCameraMatrix = vpMatrices.forwardView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            for (int i = 0; i < data.cullParams.cullingPlaneCount; ++i)
            {
                ref float4 vec = ref vpMatrices.frustumPlanes[i];
                data.cullParams.SetCullingPlane(i, new Plane(-vec.xyz, -vec.w));
            }
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("PointLightPass"));
            data.defaultDrawSettings.flags = DrawRendererFlags.EnableDynamicBatching;
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.None,
            };
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            //Forward
            int depthSlice = offset * 6;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 5);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.forwardView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, 0);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 5, targetCopyTex, 5);
            //Back
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.backView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, 0);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 4, targetCopyTex, 4);
            //Up
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.upView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, 0);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 2, targetCopyTex, 2);
            //Down
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.downView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, 0);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 3, targetCopyTex, 3);
            //Right
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.rightView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, 0);
            cb.CopyTexture(buffer.renderTarget, depthSlice, targetCopyTex, 0);
            //Left
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.leftView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, buffer.indirectDrawBuffer, 0);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 1, targetCopyTex, 1);
        }
    }
    public unsafe class SceneControllerWithOnlySRP : SceneController
    {
        public override void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets, ref PipelineCommandData data, Camera cam)
        {
            data.ExecuteCommandBuffer();
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = cam.cullingMask
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("GBuffer"));
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.CommonOpaque,
                sortMode = DrawRendererSortMode.Perspective,
                cameraPosition = cam.transform.position
            };
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.PerObjectMotionVectors;
            data.context.DrawRenderers(data.cullResults.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
        }
        public override void DrawSpotLight(ref RenderClusterOptions options, ref PipelineCommandData data, Camera currentCam, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand, Texture shadowCache)
        {
            ref SpotLightMatrix spotLightMatrix = ref spotcommand.shadowMatrices[spotLights.shadowIndex];
            spotLights.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;
            options.command.SetRenderTarget(spotcommand.renderTarget, 0, CubemapFace.Unknown, spotLights.shadowIndex);
            options.command.CopyTexture(spotcommand.renderTarget, spotLights.shadowIndex, shadowCache, 0);
            options.command.ClearRenderTarget(true, true, new Color(5, 1, 1, 1));
            options.command.SetGlobalVector(ShaderIDs._LightPos, (Vector3)spotLights.lightCone.vertex);
            options.command.SetGlobalFloat(ShaderIDs._LightRadius, spotLights.lightCone.height);
            options.command.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, true) * spotLightMatrix.worldToCamera);
            data.ExecuteCommandBuffer();
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = currentCam.cullingMask
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("SpotLightPass"));
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.CommonOpaque,
                sortMode = DrawRendererSortMode.Perspective,
                cameraPosition = spotLights.lightCone.vertex
            };

            CullResults.GetCullingParameters(currentCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

        }
        public override void DrawClusterOccDoubleCheck(ref RenderClusterOptions options, ref HizOptions hizOpts, ref RenderTargets rendTargets, ref PipelineCommandData data, Camera cam)
        {
            DrawCluster(ref options, ref rendTargets, ref data, cam);
        }
        public override void DrawDirectionalShadow(Camera currentCam, ref PipelineCommandData data, ref RenderClusterOptions opts, ref ShadowmapSettings settings, ref ShadowMapComponent shadMap, Matrix4x4[] cascadeShadowMapVP)
        {
            const int CASCADELEVELCOUNT = 4;
            const int CASCADECLIPSIZE = (CASCADELEVELCOUNT + 1) * sizeof(float);
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
            for (int pass = 0; pass < CASCADELEVELCOUNT; ++pass)
            {
                Vector2 farClipDistance = new Vector2(clipDistances[pass], clipDistances[pass + 1]);
                PipelineFunctions.GetfrustumCorners(farClipDistance, ref shadMap, currentCam);
                // PipelineFunctions.SetShadowCameraPositionCloseFit(ref shadMap, ref settings);
                PipelineFunctions.SetShadowCameraPositionStaticFit(ref staticFit, ref shadMap.shadCam, pass, (float4x4*)cascadeShadowMapVP.Ptr());
                Vector4* vec = opts.frustumPlanes.Ptr();
                PipelineFunctions.GetCullingPlanes((float4*)vec, shadMap.shadCam.size, shadMap.shadCam.nearClipPlane, shadMap.shadCam.farClipPlane,
                                                    shadMap.shadCam.up, shadMap.shadCam.right, shadMap.shadCam.forward, shadMap.shadCam.position);
                float* biasList = (float*)UnsafeUtility.AddressOf(ref settings.bias);
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(ref shadMap, opts.command, biasList[pass] / currentCam.farClipPlane, pass, out vpMatrix);
                data.ExecuteCommandBuffer();
                FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
                {
                    renderQueueRange = RenderQueueRange.opaque,
                    layerMask = currentCam.cullingMask
                };
                data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("DirectionalLight"));
                data.defaultDrawSettings.flags = DrawRendererFlags.EnableDynamicBatching;
                data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
                data.defaultDrawSettings.sorting = new DrawRendererSortSettings
                {
                    flags = SortFlags.CommonOpaque,
                    worldToCameraMatrix = shadMap.shadCam.worldToCameraMatrix,
                    sortMode = DrawRendererSortMode.Orthographic
                };

                shadMap.cameraComponent.worldToCameraMatrix = shadMap.shadCam.worldToCameraMatrix;
                shadMap.cameraComponent.projectionMatrix = shadMap.shadCam.projectionMatrix;
                CullResults.GetCullingParameters(shadMap.cameraComponent, out data.cullParams);
                data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
                CullResults results = CullResults.Cull(ref data.cullParams, data.context);
                data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
                data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            }
        }
        public override void DrawCubeMap(MLight lit, Light light, Material depthMaterial, ref RenderClusterOptions opts, ref CubeCullingBuffer buffer, int offset, RenderTexture targetCopyTex, ref PipelineCommandData data)
        {
            CommandBuffer cb = opts.command;
            Vector3 position = lit.transform.position;
            cb.SetGlobalVector(ShaderIDs._LightPos, new Vector4(position.x, position.y, position.z, light.range));
            ref CubemapViewProjMatrix vpMatrices = ref buffer.vpMatrices[offset];

            Matrix4x4 projMat = GL.GetGPUProjectionMatrix(vpMatrices.projMat, true);
            lit.shadowCam.projectionMatrix = projMat;
            lit.shadowCam.worldToCameraMatrix = vpMatrices.forwardView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            for (int i = 0; i < data.cullParams.cullingPlaneCount; ++i)
            {
                ref float4 vec = ref vpMatrices.frustumPlanes[i];
                data.cullParams.SetCullingPlane(i, new Plane(-vec.xyz, -vec.w));
            }
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("PointLightPass"));
            data.defaultDrawSettings.flags = DrawRendererFlags.EnableDynamicBatching;
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.None
            };

            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            //Forward
            int depthSlice = offset * 6;
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 5);
            cb.ClearRenderTarget(true, true, Color.white);

            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.forwardView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 5, targetCopyTex, 5);
            //Back
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.backView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 4, targetCopyTex, 4);
            //Up
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.upView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 2, targetCopyTex, 2);
            //Down
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.downView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 3, targetCopyTex, 3);
            //Right
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.rightView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(buffer.renderTarget, depthSlice, targetCopyTex, 0);
            //Left
            cb.SetRenderTarget(buffer.renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, Color.white);
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.leftView);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(buffer.renderTarget, depthSlice + 1, targetCopyTex, 1);
        }
    }
}