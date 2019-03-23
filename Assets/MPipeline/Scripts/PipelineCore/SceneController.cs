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
    [Serializable]
    public unsafe static class SceneController
    {
        public struct DrawSceneSettings
        {
            public RenderClusterOptions clusterOptions;
            public Camera targetCam;
            public RenderQueueRange renderRange;
            public string passName;
            public CullingOptions flag;
            public PerObjectData configure;
            public Material clusterMat;
        }
        public static bool gpurpEnabled { get; private set; }
        private static bool singletonReady = false;
        private static PipelineResources resources;
        public static PipelineBaseBuffer baseBuffer { get; private set; }
        private static ClusterMatResources clusterResources;
        private static List<SceneStreaming> allScenes;
        public static NativeList<ulong> pointerContainer;
        public static NativeList<ulong> addList;
        private static Dictionary<int, ComputeBuffer> allTempBuffers = new Dictionary<int, ComputeBuffer>(11);
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
        public static ComputeBuffer GetTempPropertyBuffer(int length, int stride)
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
        public static void Awake(PipelineResources resources,  string mapResources)
        {
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
            
        }

        public static void Dispose()
        {
            singletonReady = false;
            PipelineFunctions.Dispose(baseBuffer);
            pointerContainer.Dispose();
            addList.Dispose();
            var values = allTempBuffers.Values;
            foreach (var i in values)
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
            FilteringSettings renderSettings = new FilteringSettings();
            renderSettings.renderQueueRange = RenderQueueRange.opaque;
            renderSettings.layerMask = cam.cullingMask;
            renderSettings.renderingLayerMask = (uint)cam.cullingMask;
            SortingSettings sortSettings = new SortingSettings(cam);
            sortSettings.criteria = SortingCriteria.CommonOpaque;
            DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("GBuffer"), sortSettings)
            {
                enableDynamicBatching = true,
                enableInstancing = false,
                perObjectData = UnityEngine.Rendering.PerObjectData.MotionVectors | UnityEngine.Rendering.PerObjectData.Lightmaps
            };
            data.context.DrawRenderers(data.cullResults, ref dsettings, ref renderSettings);
        }
        public static void DrawCluster(ref RenderClusterOptions options, ref RenderTargets targets, ref PipelineCommandData data, Camera cam)
        {
            data.buffer.SetRenderTarget(targets.gbufferIdentifier, targets.depthBuffer);
            data.buffer.ClearRenderTarget(true, true, Color.black);
            RenderScene(ref data, cam);

        }
        public static void DrawSpotLight(CommandBuffer buffer, int mask, ComputeShader cullingShader, ref PipelineCommandData data, Camera currentCam, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand, bool inverseRender)
        {
            ref SpotLightMatrix spotLightMatrix = ref spotcommand.shadowMatrices[spotLights.shadowIndex];
            spotLights.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;
            buffer.SetInvertCulling(true);
            currentCam.orthographic = false;
            currentCam.fieldOfView = spotLights.angle;
            currentCam.nearClipPlane = spotLights.nearClip;
            currentCam.farClipPlane = spotLights.lightCone.height;
            currentCam.cullingMatrix = spotLightMatrix.projectionMatrix * currentCam.worldToCameraMatrix;
            buffer.SetRenderTarget(spotcommand.renderTarget, 0, CubemapFace.Unknown, spotLights.shadowIndex);
            buffer.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            buffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, true) * spotLightMatrix.worldToCamera);
            currentCam.TryGetCullingParameters(out data.cullParams);
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
            FilteringSettings renderSettings = new FilteringSettings()
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = mask,
                renderingLayerMask = (uint)mask
            };
            DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("SpotLightPass"), new SortingSettings { criteria = SortingCriteria.None })
            {
                enableDynamicBatching = true,
                enableInstancing = false,
                perObjectData = UnityEngine.Rendering.PerObjectData.None
            };
            data.cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
            CullingResults results = data.context.Cull(ref data.cullParams);

            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            buffer.SetInvertCulling(inverseRender);
        }

        public static void DrawDirectionalShadow(PipelineCamera cam, int mask, ref StaticFit staticFit, ref PipelineCommandData data, ref RenderClusterOptions opts, float* clipDistances, OrthoCam* camCoords, Matrix4x4[] shadowVPs)
        {
            SunLight sunLight = SunLight.current;
            if (gpurpEnabled)
            {
                opts.command.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                opts.command.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            }
            opts.command.SetInvertCulling(true);
            Camera currentCam = cam.cam;
            float bias = sunLight.bias / currentCam.farClipPlane;
            opts.command.SetGlobalFloat(ShaderIDs._ShadowOffset, bias);
            for (int pass = 0; pass < SunLight.CASCADELEVELCOUNT; ++pass)
            {
                float4* vec = (float4*)opts.frustumPlanes.Ptr();
                ref OrthoCam orthoCam = ref camCoords[pass];
                SunLight.shadowCam.cullingMatrix = shadowVPs[pass];
                SunLight.shadowCam.orthographicSize = orthoCam.size;
                SunLight.shadowCam.nearClipPlane = orthoCam.nearClipPlane;
                SunLight.shadowCam.farClipPlane = orthoCam.farClipPlane;
                Transform tr = SunLight.shadowCam.transform;
                tr.position = orthoCam.position;
                tr.up = orthoCam.up;
                tr.right = orthoCam.right;
                tr.forward = orthoCam.forward;
                if (!SunLight.shadowCam.TryGetCullingParameters(out data.cullParams))
                    return;
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = data.cullParams.GetCullingPlane(i);
                    vec[i] = -float4(p.normal, p.distance);
                }
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(sunLight, ref orthoCam.projectionMatrix, ref orthoCam.worldToCameraMatrix, opts.command, pass, out vpMatrix);
                if (gpurpEnabled)
                {
                    PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, opts.frustumPlanes, opts.command);
                    PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, opts.command);
                    opts.command.DrawProceduralIndirect(Matrix4x4.identity, sunLight.shadowDepthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
                }
                data.ExecuteCommandBuffer();
                FilteringSettings renderSettings = new FilteringSettings()
                {
                    renderQueueRange = RenderQueueRange.opaque,
                    layerMask = mask,
                    renderingLayerMask = (uint)mask,
                    excludeMotionVectorObjects = true
                };
                SortingSettings sorting = new SortingSettings(SunLight.shadowCam);
                sorting.criteria = SortingCriteria.CommonOpaque;
                DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("DirectionalLight"), sorting)
                {
                    enableDynamicBatching = true,
                    enableInstancing = false,
                    perObjectData = UnityEngine.Rendering.PerObjectData.None
                };
                data.cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
                CullingResults results = data.context.Cull(ref data.cullParams);

                data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            }
            opts.command.SetInvertCulling(cam.inverseRender);
        }

        public static void DrawPointLight(MLight lit, int mask, ref PointLightStruct light, Material depthMaterial, CommandBuffer cb, ComputeShader cullingShader, int offset, ref PipelineCommandData data, CubemapViewProjMatrix* vpMatrixArray, RenderTexture renderTarget, bool inverseRender)
        {
            ref CubemapViewProjMatrix vpMatrices = ref vpMatrixArray[offset];
            cb.SetGlobalVector(ShaderIDs._LightPos, light.sphere);
            cb.SetInvertCulling(true);
            FilteringSettings renderSettings = new FilteringSettings()
            {
                renderQueueRange = RenderQueueRange.opaque,
                layerMask = mask,
                renderingLayerMask = (uint)mask
            };
            DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("PointLightPass"), new SortingSettings { criteria = SortingCriteria.None })
            {
                enableDynamicBatching = true,
                enableInstancing = false,
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
            };
            //X
            int depthSlice = offset * 6;
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 1);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.rightProjView);
            data.ExecuteCommandBuffer();
            float size = light.sphere.w;
            lit.shadowCam.orthographic = true;
            lit.shadowCam.nearClipPlane = -size;
            lit.shadowCam.farClipPlane = size;
            lit.shadowCam.orthographicSize = size;
            lit.shadowCam.cullingMatrix = Matrix4x4.Ortho(-size, size, -size, size, -size, size) * lit.shadowCam.worldToCameraMatrix;
            lit.shadowCam.TryGetCullingParameters(out data.cullParams);
            data.cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
          
            CullingResults results = data.context.Cull(ref data.cullParams);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, vpMatrices.frustumPlanes, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, cb);
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            //-X
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.leftProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);

            //Y
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 3);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.upProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);

            //-Y
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 2);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.downProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);

            //Z
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 5);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.forwardProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);

            //-Z
            cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, depthSlice + 4);
            cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
            cb.SetGlobalMatrix(ShaderIDs._VP, vpMatrices.backProjView);
            if (gpurpEnabled)
            {
                cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
            }
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            cb.SetInvertCulling(inverseRender);
        }
    }
}