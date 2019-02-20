using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using System;
using static Unity.Mathematics.math;
using System.Runtime.CompilerServices;
namespace MPipeline
{
    public struct RenderClusterOptions
    {
        public Vector4[] frustumPlanes;
        public CommandBuffer command;
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
    public unsafe static class SceneController
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
            public Dictionary<String, TextureIdentifier> texDict;
            public Dictionary<String, TextureIdentifier> lightmapDict;
            public Dictionary<int, ComputeBuffer> allTempBuffers;
            public NativeList<int> avaiableTexs;
            public NativeList<int> avaiableProperties;
            public NativeList<int> avaiableLightmap;
            public ComputeBuffer texCopyBuffer;
            public ComputeBuffer lightmapCopyBuffer;
            public ComputeBuffer propertyBuffer;
            public RenderTexture texArray;
            public RenderTexture lightmapArray;
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
            public int GetLightmapIndex(string guid, out bool alreadyContained)
            {
                if (lightmapDict.ContainsKey(guid) && lightmapDict[guid].usedCount > 0)
                {
                    TextureIdentifier ident = lightmapDict[guid];
                    ident.usedCount++;
                    lightmapDict[guid] = ident;
                    alreadyContained = true;
                    return ident.belonged;
                }
                else
                {
                    TextureIdentifier ident;
                    ident.usedCount = 1;
                    if (avaiableLightmap.Length <= 0)
                    {
                        throw new Exception("No available lightmap lefted!");
                    }
                    ident.belonged = avaiableLightmap[avaiableLightmap.Length - 1];

                    avaiableLightmap.RemoveLast();
                    lightmapDict[guid] = ident;
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
            public ComputeBuffer GetTempPropertyBuffer(int length, int stride)
            {
                ComputeBuffer target;
                if (allTempBuffers.TryGetValue(stride, out target))
                {
                    if (target == null) Debug.Log("Null");
                    if (target.count < length)
                    {
                        target.Dispose();
                        target = new ComputeBuffer(length, stride);
                        allTempBuffers[stride] = target;
                    }
                    return target;
                }
                else
                {
                    target = new ComputeBuffer(length, stride);
                    allTempBuffers[stride] = target;
                    return target;
                }
            }
            public void RemoveLightmap(string guid)
            {
                if (lightmapDict.ContainsKey(guid))
                {
                    TextureIdentifier ident = lightmapDict[guid];
                    ident.usedCount--;
                    if (ident.usedCount <= 0)
                    {
                        lightmapDict.Remove(guid);
                        avaiableLightmap.Add(ident.belonged);
                    }
                    else
                    {
                        lightmapDict[guid] = ident;
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
        public static SceneCommonData commonData;
        public static bool gpurpEnabled { get; private set; }
        private static bool singletonReady = false;
        private static PipelineResources resources;
        public static PipelineBaseBuffer baseBuffer { get; private set; }
        private static ClusterMatResources clusterResources;
        private static List<SceneStreaming> allScenes;
        public static NativeList<ulong> pointerContainer;
        public static LoadingCommandQueue commandQueue;
        public static NativeList<ulong> addList;
        public static int resolution { get; private set; }
        public static void SetState()
        {
            if (singletonReady && baseBuffer.clusterCount > 0)
            {
                gpurpEnabled = true;
            }
            else
            {
                gpurpEnabled = false;
            }
        }
        public static void Awake(PipelineResources resources, int resolution, int texArrayCapacity, int lightmapResolution, int lightmapCapacity, int propertyCapacity, string mapResources)
        {
            SceneController.resolution = resolution;
            singletonReady = true;
            SceneController.resources = resources;
            addList = new NativeList<ulong>(10, Allocator.Persistent);
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
                enableRandomWrite = true,
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
                texDict = new Dictionary<string, SceneCommonData.TextureIdentifier>(texArrayCapacity),
                lightmapDict = new Dictionary<string, SceneCommonData.TextureIdentifier>(lightmapCapacity),
                avaiableProperties = new NativeList<int>(propertyCapacity, Allocator.Persistent),
                avaiableTexs = new NativeList<int>(texArrayCapacity, Allocator.Persistent),
                avaiableLightmap = new NativeList<int>(lightmapCapacity, Allocator.Persistent),
                texCopyBuffer = new ComputeBuffer(resolution * resolution, sizeof(int)),
                lightmapCopyBuffer = new ComputeBuffer(lightmapResolution * lightmapResolution, sizeof(int)),
                propertyBuffer = new ComputeBuffer(propertyCapacity, sizeof(PropertyValue)),
                texArray = new RenderTexture(desc),
                clusterMaterial = new Material(resources.shaders.clusterRenderShader),
                terrainMaterial = new Material(resources.shaders.terrainShader),
                terrainDrawStreaming = new TerrainDrawStreaming(100, 16, resources.shaders.terrainCompute),
                allTempBuffers = new Dictionary<int, ComputeBuffer>(11)
            };
            commonData.texArray.wrapMode = TextureWrapMode.Repeat;
            desc.volumeDepth = lightmapCapacity;
            desc.width = lightmapResolution;
            desc.height = lightmapResolution;
            desc.colorFormat = RenderTextureFormat.RGB111110Float;
            commonData.lightmapArray = new RenderTexture(desc);
            for (int i = 0; i < propertyCapacity; ++i)
            {
                commonData.avaiableProperties.Add(i);
            }
            for (int i = 0; i < texArrayCapacity; ++i)
            {
                commonData.avaiableTexs.Add(i);
            }
            for (int i = 0; i < lightmapCapacity; ++i)
            {
                commonData.avaiableLightmap.Add(i);
            }
            commonData.lightmapArray.Create();
            commonData.texArray.Create();
        }

        public static void TransformMapPosition(int startPos)
        {
            if (baseBuffer.clusterCount - startPos <= 0) return;
            resources.shaders.gpuFrustumCulling.SetInt(ShaderIDs._OffsetIndex, startPos);
            resources.shaders.gpuFrustumCulling.SetBuffer(PipelineBaseBuffer.MoveVertex, ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            resources.shaders.gpuFrustumCulling.SetBuffer(PipelineBaseBuffer.MoveCluster, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            resources.shaders.gpuFrustumCulling.Dispatch(PipelineBaseBuffer.MoveVertex, baseBuffer.clusterCount - startPos, 1, 1);
            ComputeShaderUtility.Dispatch(resources.shaders.gpuFrustumCulling, PipelineBaseBuffer.MoveCluster, baseBuffer.clusterCount - startPos, 64);
        }

        public static void Dispose()
        {
            singletonReady = false;
            PipelineFunctions.Dispose(baseBuffer);
            pointerContainer.Dispose();
            commandQueue = null;
            commonData.avaiableProperties.Dispose();
            commonData.avaiableTexs.Dispose();
            commonData.avaiableLightmap.Dispose();
            commonData.texCopyBuffer.Dispose();
            commonData.lightmapCopyBuffer.Dispose();
            commonData.propertyBuffer.Dispose();
            commonData.texDict.Clear();
            commonData.lightmapDict.Clear();
            commonData.terrainDrawStreaming.Dispose();
            UnityEngine.Object.DestroyImmediate(commonData.terrainMaterial);
            UnityEngine.Object.DestroyImmediate(commonData.terrainMaterial);
            UnityEngine.Object.DestroyImmediate(commonData.lightmapArray);
            UnityEngine.Object.DestroyImmediate(commonData.texArray);
            addList.Dispose();
            foreach (var i in commonData.allTempBuffers.Values)
            {
                i.Dispose();
            }
        }
        //Press number load scene

        public static void Update(MonoBehaviour behavior)
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
        private static bool GetBaseBuffer(out PipelineBaseBuffer result)
        {
            result = baseBuffer;
            return result.clusterCount > 0;
        }

        private static void RenderScene(ref PipelineCommandData data, Camera cam)
        {
            data.ExecuteCommandBuffer();
            FilterRenderersSettings renderSettings = new FilterRenderersSettings(true);
            renderSettings.renderQueueRange = RenderQueueRange.opaque;
            renderSettings.layerMask = cam.cullingMask;
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
        public static void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets, ref PipelineCommandData data, Camera cam)
        {
            if (gpurpEnabled)
            {
                options.command.SetGlobalBuffer(ShaderIDs._PropertiesBuffer, commonData.propertyBuffer);
                options.command.SetGlobalTexture(ShaderIDs._MainTex, commonData.texArray);
                options.command.SetGlobalTexture(ShaderIDs._LightMap, commonData.lightmapArray);
                PipelineFunctions.SetBaseBuffer(baseBuffer, options.cullingShader, options.frustumPlanes, options.command);
                PipelineFunctions.RunCullDispatching(baseBuffer, options.cullingShader, options.command);
                PipelineFunctions.RenderProceduralCommand(baseBuffer, commonData.clusterMaterial, options.command);
            }
            RenderScene(ref data, cam);

        }
        public static void DrawSpotLight(CommandBuffer buffer, ComputeShader cullingShader, ref PipelineCommandData data, Camera currentCam, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand)
        {
            ref SpotLightMatrix spotLightMatrix = ref spotcommand.shadowMatrices[spotLights.shadowIndex];
            spotLights.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;
            buffer.SetInvertCulling(true);
            currentCam.worldToCameraMatrix = spotLightMatrix.worldToCamera;
            currentCam.projectionMatrix = spotLightMatrix.projectionMatrix;
            buffer.SetRenderTarget(spotcommand.renderTarget, 0, CubemapFace.Unknown, spotLights.shadowIndex);
            buffer.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            buffer.SetGlobalVector(ShaderIDs._LightPos, (Vector3)spotLights.lightCone.vertex);
            buffer.SetGlobalFloat(ShaderIDs._LightRadius, spotLights.lightCone.height);
            buffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, true) * spotLightMatrix.worldToCamera);
            CullResults.GetCullingParameters(currentCam, out data.cullParams);
            if (gpurpEnabled)
            {
                float4* frustumPlanes = stackalloc float4[6];
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = data.cullParams.GetCullingPlane(i);
                    frustumPlanes[i] = new float4(-p.normal, -p.distance);
                }
                PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, frustumPlanes, buffer);
                PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, buffer);
                PipelineFunctions.RenderProceduralCommand(baseBuffer, spotcommand.clusterShadowMaterial, buffer);
            }

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

            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive | CullFlag.DisablePerObjectCulling;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            buffer.SetInvertCulling(false);
        }

        public static void DrawClusterOccDoubleCheck(ref RenderClusterOptions options, ref HizOptions hizOpts, ref RenderTargets rendTargets, ref PipelineCommandData data, Camera cam)
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
options.frustumPlanes);
            //First Draw
            buffer.SetGlobalBuffer(ShaderIDs._PropertiesBuffer, commonData.propertyBuffer);
            buffer.SetGlobalTexture(ShaderIDs._MainTex, commonData.texArray);
            buffer.SetGlobalTexture(ShaderIDs._LightMap, commonData.lightmapArray);
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

        public static void DrawDirectionalShadow(Camera currentCam, ref StaticFit staticFit, ref PipelineCommandData data, ref RenderClusterOptions opts, float* clipDistances, float4x4* worldToCamMatrices, float4x4* projectionMatrices)
        {
            SunLight sunLight = SunLight.current;
            if (gpurpEnabled)
            {
                opts.command.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                opts.command.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            }
            opts.command.SetInvertCulling(true);
            float bias = sunLight.bias / currentCam.farClipPlane;
            opts.command.SetGlobalFloat(ShaderIDs._ShadowOffset, bias);
            for (int pass = 0; pass < SunLight.CASCADELEVELCOUNT; ++pass)
            {
                float4* vec = (float4*)opts.frustumPlanes.Ptr();
                SunLight.shadowCam.worldToCameraMatrix = worldToCamMatrices[pass];
                SunLight.shadowCam.projectionMatrix = projectionMatrices[pass];
                if (!CullResults.GetCullingParameters(SunLight.shadowCam, out data.cullParams))
                    return;
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = data.cullParams.GetCullingPlane(i);
                    vec[i] = -float4(p.normal, p.distance);
                }
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(sunLight, ref projectionMatrices[pass], ref worldToCamMatrices[pass], opts.command, pass, out vpMatrix);
                if (gpurpEnabled)
                {
                    PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, opts.frustumPlanes, opts.command);
                    PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, opts.command);
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
                    flags = SortFlags.None
                };
                data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive;
                CullResults results = CullResults.Cull(ref data.cullParams, data.context);
                data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;
                data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            }
            opts.command.SetInvertCulling(false);
        }

        public static void DrawPointLight(MLight lit, ref PointLightStruct light, Material depthMaterial, CommandBuffer cb, ComputeShader cullingShader, int offset, ref PipelineCommandData data, CubemapViewProjMatrix* vpMatrixArray, RenderTexture renderTarget)
        {
            ref CubemapViewProjMatrix vpMatrices = ref vpMatrixArray[offset];
            cb.SetGlobalVector(ShaderIDs._LightPos, light.sphere);
            cb.SetInvertCulling(true);
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
            data.defaultDrawSettings.rendererConfiguration = RendererConfiguration.None;

            //Forward
            int depthSlice = offset * 6;
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 5);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.forwardProjView);
            data.ExecuteCommandBuffer();

            lit.shadowCam.orthographic = true;
            lit.shadowCam.nearClipPlane = -light.sphere.w;
            lit.shadowCam.farClipPlane = light.sphere.w;
            lit.shadowCam.orthographicSize = light.sphere.w;
            lit.shadowCam.aspect = 1;
            CullResults.GetCullingParameters(lit.shadowCam, out data.cullParams);
            data.cullParams.cullingFlags = CullFlag.ForceEvenIfCameraIsNotActive | CullFlag.DisablePerObjectCulling;
            CullResults results = CullResults.Cull(ref data.cullParams, data.context);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, vpMatrices.frustumPlanes, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            //Back
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.backProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            //Up
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.upProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            //Down
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.downProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            //Right
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.rightProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);

            //Left
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.leftProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.SetInvertCulling(false);
        }
        public static void GICubeCull(float3 position, float extent, CommandBuffer buffer, ComputeShader cullingshader)
        {
            float4* cullingPlanes = stackalloc float4[]
            {
                VectorUtility.GetPlane(float3(0, 0, 1), position + float3(0, 0, extent)),
                VectorUtility.GetPlane(float3(0, 0, -1), position - float3(0, 0, extent)),
                VectorUtility.GetPlane(float3(0, 1, 0), position + float3(0, extent, 0)),
                VectorUtility.GetPlane(float3(0, -1, 0), position - float3(0, extent, 0)),
                VectorUtility.GetPlane(float3(1, 0, 0), position + float3(extent, 0, 0)),
                VectorUtility.GetPlane(float3(-1, 0, 0), position - float3(extent, 0, 0))
            };
            PipelineFunctions.SetBaseBuffer(baseBuffer, cullingshader, cullingPlanes, buffer);
            PipelineFunctions.RunCullDispatching(baseBuffer, cullingshader, buffer);
        }
        public static void DrawGIBuffer(RenderTexture targetRT, float4 renderCube, ComputeShader cullingshader, CommandBuffer buffer)
        {
            void GetMatrix(float4x4* allmat, ref PerspCam persp, float3 position)
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
            if (!gpurpEnabled) return;
            PerspCam perspCam = new PerspCam();
            perspCam.aspect = 1;
            perspCam.farClipPlane = renderCube.w;
            perspCam.nearClipPlane = 0.05f;
            perspCam.fov = 90;
            perspCam.position = renderCube.xyz;
            perspCam.UpdateProjectionMatrix();
            float4x4* worldToCamMatrix = stackalloc float4x4[6];
            GetMatrix(worldToCamMatrix, ref perspCam, renderCube.xyz);
            for (int i = 0; i < 6; ++i)
            {
                buffer.SetGlobalMatrix(ShaderIDs._VP, GL.GetGPUProjectionMatrix(perspCam.projectionMatrix, true) * (Matrix4x4)worldToCamMatrix[i]);
                buffer.SetRenderTarget(targetRT, 0, CubemapFace.Unknown, i);
                buffer.ClearRenderTarget(true, true, Color.black);
                buffer.DrawProceduralIndirect(Matrix4x4.identity, commonData.clusterMaterial, 1, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
        }
       
    }
}