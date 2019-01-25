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
        public static void Awake(PipelineResources resources, int resolution, int texArrayCapacity, int propertyCapacity, string mapResources)
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
                copyTextureMat = new Material(resources.shaders.copyShader),
                texArray = new RenderTexture(desc),
                clusterMaterial = new Material(resources.shaders.clusterRenderShader),
                terrainMaterial = new Material(resources.shaders.terrainShader),
                terrainDrawStreaming = new TerrainDrawStreaming(100, 16, resources.shaders.terrainCompute)
            };
            commonData.texArray.wrapMode = TextureWrapMode.Repeat;

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
        public static void UpdateCopyMat()
        {
            commonData.copyTextureMat.SetVector(ShaderIDs._TextureSize, new Vector4(resolution, resolution));
            commonData.copyTextureMat.SetBuffer(ShaderIDs._TextureBuffer, commonData.texCopyBuffer);
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
        public static void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets, ref PipelineCommandData data, Camera cam)
        {
            if (gpurpEnabled)
            {
                options.command.SetGlobalBuffer(ShaderIDs._PropertiesBuffer, commonData.propertyBuffer);
                options.command.SetGlobalTexture(ShaderIDs._MainTex, commonData.texArray);
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
        private static StaticFit DirectionalShadowStaticFit(Camera cam, SunLight sunlight, float* outClipDistance)
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
        public static void DrawDirectionalShadow(Camera currentCam, ref PipelineCommandData data, ref RenderClusterOptions opts, SunLight sunLight, Matrix4x4[] cascadeShadowMapVP)
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
            float bias = sunLight.bias / currentCam.farClipPlane;
            opts.command.SetGlobalFloat(ShaderIDs._ShadowOffset, bias);
            for (int pass = 0; pass < CASCADELEVELCOUNT; ++pass)
            {
                PipelineFunctions.SetShadowCameraPositionStaticFit(ref staticFit, ref sunLight.shadCam, pass, (float4x4*)cascadeShadowMapVP.Ptr());
                float4* vec = (float4*)opts.frustumPlanes.Ptr();
                SunLight.shadowCam.worldToCameraMatrix = sunLight.shadCam.worldToCameraMatrix;
                SunLight.shadowCam.projectionMatrix = sunLight.shadCam.projectionMatrix;
                CullResults.GetCullingParameters(SunLight.shadowCam, out data.cullParams);
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = data.cullParams.GetCullingPlane(i);
                    vec[i] = -float4(p.normal, p.distance);
                }
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(ref sunLight, opts.command, pass, out vpMatrix);
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
        }

        public static void DrawPointLight(MLight lit, ref PointLightStruct light, Material depthMaterial, CommandBuffer cb, ComputeShader cullingShader, int offset, RenderTexture targetCopyTex, ref PipelineCommandData data, CubemapViewProjMatrix* vpMatrixArray, RenderTexture renderTarget)
        {
            ref CubemapViewProjMatrix vpMatrices = ref vpMatrixArray[offset];
            cb.SetGlobalVector(ShaderIDs._LightPos, light.sphere);
            Matrix4x4 projMat = GL.GetGPUProjectionMatrix(vpMatrices.projMat, true);
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
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.forwardView);
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
            cb.CopyTexture(renderTarget, depthSlice + 5, targetCopyTex, 5);
            //Back
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.backView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(renderTarget, depthSlice + 4, targetCopyTex, 4);
            //Up
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.upView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(renderTarget, depthSlice + 2, targetCopyTex, 2);
            //Down
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.downView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(renderTarget, depthSlice + 3, targetCopyTex, 3);
            //Right
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.rightView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(renderTarget, depthSlice, targetCopyTex, 0);
            //Left
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, projMat * vpMatrices.leftView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results.visibleRenderers, ref data.defaultDrawSettings, renderSettings);
            cb.CopyTexture(renderTarget, depthSlice + 1, targetCopyTex, 1);
        }
        public static void DrawGIBuffer(RenderTexture targetRT, float4 renderCube, ComputeShader cullingshader, CommandBuffer buffer)
        {
            if (!gpurpEnabled) return;
            PerspCam perspCam = new PerspCam();
            perspCam.aspect = 1;
            perspCam.farClipPlane = renderCube.w;
            perspCam.nearClipPlane = 0.1f;
            perspCam.fov = 90;
            perspCam.position = renderCube.xyz;
            perspCam.UpdateProjectionMatrix();
            float3x3* coords = stackalloc float3x3[]
            {
                float3x3(float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1)),
                float3x3(float3(0, 0, -1), float3(0, 1, 0), float3(1, 0, 0)),
                float3x3(float3(-1, 0, 0), float3(0, 1, 0), float3(0, 0, -1)),
                float3x3(float3(0, 0, 1), float3(0, 1, 0), float3(-1, 0, 0)),
                float3x3(float3(1, 0, 0), float3(0, 0, -1), float3(0, 1, 0)),
                float3x3(float3(1, 0, 0), float3(0, 0, 1), float3(0, -1, 0))
            };
            CubemapFace* faces = stackalloc CubemapFace[]
            {
                CubemapFace.PositiveZ,
                CubemapFace.PositiveX,
                CubemapFace.NegativeZ,
                CubemapFace.NegativeX,
                CubemapFace.PositiveY,
                CubemapFace.NegativeY
            };
            float4* cullingPlanes = stackalloc float4[]
            {
                VectorUtility.GetPlane(float3(0, 0, 1), perspCam.position + float3(0, 0, perspCam.farClipPlane)),
                VectorUtility.GetPlane(float3(0, 0, -1), perspCam.position - float3(0, 0, perspCam.farClipPlane)),
                VectorUtility.GetPlane(float3(0, 1, 0), perspCam.position + float3(0, perspCam.farClipPlane, 0)),
                VectorUtility.GetPlane(float3(0, -1, 0), perspCam.position - float3(0, perspCam.farClipPlane, 0)),
                VectorUtility.GetPlane(float3(1, 0, 0), perspCam.position + float3(perspCam.farClipPlane, 0, 0)),
                VectorUtility.GetPlane(float3(-1, 0, 0), perspCam.position - float3(perspCam.farClipPlane, 0, 0))
            };
            PipelineFunctions.SetBaseBuffer(baseBuffer, cullingshader, cullingPlanes, buffer);
            PipelineFunctions.RunCullDispatching(baseBuffer, cullingshader, buffer);
            for (int i = 0; i < 6; ++i)
            {
                float3x3 c = coords[i];
                perspCam.right = c.c0;
                perspCam.up = c.c1;
                perspCam.forward = c.c2;
                perspCam.UpdateTRSMatrix();
                buffer.SetGlobalMatrix(ShaderIDs._VP, GL.GetGPUProjectionMatrix(perspCam.projectionMatrix, true) * (Matrix4x4)perspCam.worldToCameraMatrix);
                buffer.SetRenderTarget(targetRT, 0, faces[i], 0);
                buffer.ClearRenderTarget(true, true, Color.black);
                buffer.DrawProceduralIndirect(Matrix4x4.identity, commonData.clusterMaterial, 1, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
        }
        public static void DrawSunShadowForCubemap(float3* cubeVertex, int renderTarget, SunLight sunLight, CommandBuffer buffer, out OrthoCam camData, ComputeShader cullingShader)
        {
            camData = new OrthoCam();
            Transform tr = SunLight.current.transform;
            camData.nearClipPlane = -500;
            camData.forward = tr.forward;
            camData.right = tr.right;
            camData.up = tr.up;
            float4x4 localToWorld = float4x4(float4(camData.right, 0), float4(camData.up, 0), float4(camData.forward, 0), float4(0, 0, 0, 1));
            float3* localPoses = stackalloc float3[8];
            for (int i = 0; i < 8; ++i)
            {
                localPoses[i] = mul(localToWorld, float4(cubeVertex[i], 1)).xyz;
            }
            int2 rightMostPoint = 0;
            int2 upMostPoint = 0;
            int2 forwardMostPoint = 0;
            for (int i = 1; i < 8; i++)
            {
                float3 p = localPoses[i];
                if (p.x < localPoses[rightMostPoint.x].x)
                {
                    rightMostPoint.x = i;
                }
                if (p.x > localPoses[rightMostPoint.y].x)
                {
                    rightMostPoint.y = i;
                }
                if (p.y < localPoses[upMostPoint.x].y)
                {
                    upMostPoint.x = i;
                }
                if (p.y > localPoses[upMostPoint.y].y)
                {
                    upMostPoint.y = i;
                }
                if (p.z < localPoses[forwardMostPoint.x].z)
                {
                    forwardMostPoint.x = i;
                }
                if (p.z > localPoses[forwardMostPoint.y].z)
                {
                    forwardMostPoint.y = i;
                }
            }
            float4 center = float4((localPoses[rightMostPoint.x].x + localPoses[rightMostPoint.y].x) * 0.5f, (localPoses[upMostPoint.x].y + localPoses[upMostPoint.y].y) * 0.5f, (localPoses[forwardMostPoint.x].z + localPoses[forwardMostPoint.y].z) * 0.5f, 1);
            center = mul(center, localToWorld);
            camData.position = center.xyz;
            float width = localPoses[rightMostPoint.y].x - localPoses[rightMostPoint.x].x;
            float up = localPoses[upMostPoint.y].y - localPoses[upMostPoint.x].y;
            float length = localPoses[forwardMostPoint.y].z - localPoses[forwardMostPoint.x].z;
            camData.size = max(width, up) * 0.5f;
            camData.farClipPlane = length * 0.5f;
            camData.UpdateTRSMatrix();
            camData.UpdateProjectionMatrix();
            float4* planes = stackalloc float4[6];
            PipelineFunctions.GetOrthoCullingPlanes(ref camData, planes);
            PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, planes, buffer);
            PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, buffer);
            buffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(camData.projectionMatrix, true) * (Matrix4x4)camData.worldToCameraMatrix);
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            buffer.SetRenderTarget(renderTarget);
            buffer.ClearRenderTarget(true, true, Color.white);
            buffer.DrawProceduralIndirect(Matrix4x4.identity, sunLight.shadowDepthMaterial, 1, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
        }
    }
}