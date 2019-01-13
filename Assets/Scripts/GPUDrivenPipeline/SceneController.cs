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
    public unsafe sealed class SceneController
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
        public struct DrawSceneSettings
        {
            public RenderClusterOptions clusterOptions;
            public Camera targetCam;
            public RenderQueueRange renderRange;
            public string passName;
            public CullFlag flag;
            public RendererConfiguration configure;
            public Material clusterMat;
        }
        private const int CASCADELEVELCOUNT = 4;
        private const int CASCADECLIPSIZE = CASCADELEVELCOUNT + 1;
        public static SceneCommonData commonData;
        public static SceneController current = new SceneController();
        public static bool gpurpEnabled = false;
        private static bool singletonReady = false;
        private PipelineResources resources;
        public PipelineBaseBuffer baseBuffer { get; private set; }
        private ClusterMatResources clusterResources;
        private List<SceneStreaming> allScenes;
        public NativeList<ulong> pointerContainer;
        public LoadingCommandQueue commandQueue;
        public NativeList<ulong> addList;
        public int resolution { get; private set; }
        public static void SetState()
        {
            if (singletonReady && current.baseBuffer.clusterCount > 0)
            {
                gpurpEnabled = true;
            }
            else
            {
                gpurpEnabled = false;
            }
        }
        private SceneController()
        {
        }
        public void Awake(PipelineResources resources, int resolution, int texArrayCapacity, int propertyCapacity, string mapResources)
        {
            this.resolution = resolution;
            singletonReady = true;
            this.resources = resources;
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
            singletonReady = false;
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
            addList.Dispose();
        }
        //Press number load scene
        
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
        private void ClusterCullDraw(ref RenderClusterOptions options, Material mat)
        {
            PipelineFunctions.SetBaseBuffer(baseBuffer, options.cullingShader, options.frustumPlanes, options.command);
            PipelineFunctions.RunCullDispatching(baseBuffer, options.cullingShader, options.isOrtho, options.command);
            PipelineFunctions.RenderProceduralCommand(baseBuffer, mat, options.command);
        }
        private void RenderScene(ref PipelineCommandData data, Camera cam)
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
        public void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets, ref PipelineCommandData data, Camera cam)
        {
            if (gpurpEnabled)
            {
                options.command.SetGlobalBuffer(ShaderIDs._PropertiesBuffer, commonData.propertyBuffer);
                options.command.SetGlobalTexture(ShaderIDs._MainTex, commonData.texArray);
                ClusterCullDraw(ref options, commonData.clusterMaterial);
            }
            RenderScene(ref data, cam);

        }
        public void DrawSpotLight(ref RenderClusterOptions options, ref PipelineCommandData data, Camera currentCam, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand)
        {
            ref SpotLightMatrix spotLightMatrix = ref spotcommand.shadowMatrices[spotLights.shadowIndex];
            spotLights.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;

            currentCam.worldToCameraMatrix = spotLightMatrix.worldToCamera;
            currentCam.projectionMatrix = spotLightMatrix.projectionMatrix;
            options.command.SetRenderTarget(spotcommand.renderTarget, 0, CubemapFace.Unknown, spotLights.shadowIndex);
            options.command.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            options.command.SetGlobalVector(ShaderIDs._LightPos, (Vector3)spotLights.lightCone.vertex);
            options.command.SetGlobalFloat(ShaderIDs._LightRadius, spotLights.lightCone.height);
            options.command.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, true) * spotLightMatrix.worldToCamera);

            options.isOrtho = false;
            if (gpurpEnabled)
                ClusterCullDraw(ref options, spotcommand.clusterShadowMaterial);

            data.ExecuteCommandBuffer();
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = currentCam.cullingMask
            };
            data.defaultDrawSettings.SetShaderPassName(0, new ShaderPassName("SpotLightPass"));
            data.defaultDrawSettings.sorting = new DrawRendererSortSettings
            {
                flags = SortFlags.None
            };
            CullResults.GetCullingParameters(currentCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive | CullFlag.DisablePerObjectCulling;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
        }

        public void DrawClusterOccDoubleCheck(ref RenderClusterOptions options, ref HizOptions hizOpts, ref RenderTargets rendTargets, ref PipelineCommandData data, Camera cam)
        {
            if (!gpurpEnabled)
            {
                RenderScene(ref data, cam);
                return;
            }
            CommandBuffer buffer = options.command;
            ComputeShader gpuFrustumShader = options.cullingShader;

            PipelineFunctions.ClearOcclusionData(baseBuffer, buffer, gpuFrustumShader);
            PipelineFunctions.UpdateOcclusionBuffer(
baseBuffer, gpuFrustumShader,
buffer,
hizOpts.hizData,
options.frustumPlanes,
options.isOrtho);
            //First Draw
            buffer.SetGlobalBuffer(ShaderIDs._PropertiesBuffer, commonData.propertyBuffer);
            buffer.SetGlobalTexture(ShaderIDs._MainTex, commonData.texArray);
            PipelineFunctions.DrawLastFrameCullResult(baseBuffer, buffer, commonData.clusterMaterial);
            //更新Vector，Depth Mip Map
            hizOpts.hizData.lastFrameCameraUp = hizOpts.currentCameraUpVec;
            //TODO Draw others
            RenderScene(ref data, cam);
            //TODO
            buffer.Blit(hizOpts.currentDepthTex, hizOpts.hizData.historyDepth, hizOpts.linearLODMaterial, 0);
            hizOpts.hizDepth.GetMipMap(hizOpts.hizData.historyDepth, buffer);
            //double check
            PipelineFunctions.OcclusionRecheck(baseBuffer, gpuFrustumShader, buffer, hizOpts.hizData);
            //double draw
            buffer.SetRenderTarget(rendTargets.gbufferIdentifier, rendTargets.depthIdentifier);
            PipelineFunctions.DrawRecheckCullResult(baseBuffer, commonData.clusterMaterial, buffer);
            buffer.Blit(hizOpts.currentDepthTex, hizOpts.hizData.historyDepth, hizOpts.linearLODMaterial, 0);
            hizOpts.hizDepth.GetMipMap(hizOpts.hizData.historyDepth, buffer);
        }
        private StaticFit DirectionalShadowStaticFit(Camera cam, SunLight sunlight, float* outClipDistance)
        {
            StaticFit staticFit;
            staticFit.resolution = sunlight.resolution;
            staticFit.mainCamTrans = cam;
            staticFit.frustumCorners = new NativeArray<float3>((CASCADELEVELCOUNT + 1) * 4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            outClipDistance[0] = sunlight.shadCam.nearClipPlane;
            outClipDistance[1] = sunlight.firstLevelDistance;
            outClipDistance[2] = sunlight.secondLevelDistance;
            outClipDistance[3] = sunlight.thirdLevelDistance;
            outClipDistance[4] = sunlight.farestDistance;
            return staticFit;
        }
        public void DrawDirectionalShadow(Camera currentCam, ref PipelineCommandData data, ref RenderClusterOptions opts, SunLight sunLight, Matrix4x4[] cascadeShadowMapVP)
        {
            float* clipDistances = stackalloc float[CASCADECLIPSIZE];
            StaticFit staticFit = DirectionalShadowStaticFit(currentCam, sunLight, clipDistances);
            //   PipelineFunctions.GetfrustumCorners(farClipDistance, ref shadMap, currentCam);
            PipelineFunctions.GetfrustumCorners(clipDistances, CASCADELEVELCOUNT + 1, currentCam, (float3*)staticFit.frustumCorners.GetUnsafePtr());
            if (gpurpEnabled)
            {
                opts.command.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                opts.command.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            }
            for (int pass = 0; pass < CASCADELEVELCOUNT; ++pass)
            {
                PipelineFunctions.SetShadowCameraPositionStaticFit(ref staticFit, ref sunLight.shadCam, pass, (float4x4*)cascadeShadowMapVP.Ptr());
                Vector4* vec = opts.frustumPlanes.Ptr();
                PipelineFunctions.GetCullingPlanes((float4*)vec, sunLight.shadCam.size, sunLight.shadCam.nearClipPlane, sunLight.shadCam.farClipPlane,
                                                    sunLight.shadCam.up, sunLight.shadCam.right, sunLight.shadCam.forward, sunLight.shadCam.position);
                float* biasList = (float*)UnsafeUtility.AddressOf(ref sunLight.bias);
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(ref sunLight, opts.command, biasList[pass] / currentCam.farClipPlane, pass, out vpMatrix);
                if (gpurpEnabled)
                {
                    PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, opts.frustumPlanes, opts.command);
                    PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, true, opts.command);
                    opts.command.DrawProceduralIndirect(Matrix4x4.identity, sunLight.shadowDepthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
                }
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
                    worldToCameraMatrix = sunLight.shadCam.worldToCameraMatrix,
                    sortMode = DrawRendererSortMode.Orthographic
                };
                sunLight.cameraComponent.worldToCameraMatrix = sunLight.shadCam.worldToCameraMatrix;
                sunLight.cameraComponent.projectionMatrix = sunLight.shadCam.projectionMatrix;
                CullResults.GetCullingParameters(sunLight.cameraComponent, out data.cullParams);
                data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
                CullResults results = CullResults.Cull(ref data.cullParams, data.context);
                data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
                data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            }
        }

        public void DrawCubeMap(MLight lit, ref PointLightStruct light, Material depthMaterial, ref RenderClusterOptions opts, int offset, RenderTexture targetCopyTex, ref PipelineCommandData data, CubemapViewProjMatrix* vpMatrixArray, RenderTexture renderTarget)
        {
            CommandBuffer cb = opts.command;
            ref CubemapViewProjMatrix vpMatrices = ref vpMatrixArray[offset];
            cb.SetGlobalVector(ShaderIDs._LightPos, light.sphere);
            Matrix4x4 projMat = GL.GetGPUProjectionMatrix(vpMatrices.projMat, true);
            lit.shadowCam.projectionMatrix = vpMatrices.projMat;
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

            //Forward
            int depthSlice = offset * 6;
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 5);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.forwardView);
            data.ExecuteCommandBuffer();
            lit.shadowCam.worldToCameraMatrix = vpMatrices.forwardView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive | CullFlag.DisablePerObjectCulling;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, vpMatrices.frustumPlanes, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, false, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            cb.CopyTexture(renderTarget, depthSlice + 5, targetCopyTex, 5);
            //Back
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.backView);
            data.ExecuteCommandBuffer();
            lit.shadowCam.worldToCameraMatrix = vpMatrices.backView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            results = CullResults.Cull(ref data.cullParams, data.context);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, vpMatrices.frustumPlanes + 6, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, false, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            cb.CopyTexture(renderTarget, depthSlice + 4, targetCopyTex, 4);
            //Up
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.upView);
            data.ExecuteCommandBuffer();
            lit.shadowCam.worldToCameraMatrix = vpMatrices.upView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            results = CullResults.Cull(ref data.cullParams, data.context);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, vpMatrices.frustumPlanes + 12, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, false, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            cb.CopyTexture(renderTarget, depthSlice + 2, targetCopyTex, 2);
            //Down
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.downView);
            data.ExecuteCommandBuffer();
            lit.shadowCam.worldToCameraMatrix = vpMatrices.downView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            results = CullResults.Cull(ref data.cullParams, data.context);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, vpMatrices.frustumPlanes + 18, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, false, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            cb.CopyTexture(renderTarget, depthSlice + 3, targetCopyTex, 3);
            //Right
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.rightView);
            data.ExecuteCommandBuffer();
            lit.shadowCam.worldToCameraMatrix = vpMatrices.rightView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            results = CullResults.Cull(ref data.cullParams, data.context);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, vpMatrices.frustumPlanes + 24, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, false, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            cb.CopyTexture(renderTarget, depthSlice, targetCopyTex, 0);
            //Left
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.leftView);
            data.ExecuteCommandBuffer();
            lit.shadowCam.worldToCameraMatrix = vpMatrices.leftView;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
            results = CullResults.Cull(ref data.cullParams, data.context);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, vpMatrices.frustumPlanes + 30, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, false, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(renderTarget, depthSlice + 1, targetCopyTex, 1);
        }

    }
}