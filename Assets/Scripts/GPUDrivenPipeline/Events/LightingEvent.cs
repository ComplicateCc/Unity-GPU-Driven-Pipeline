using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using System.Threading;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Collections.Concurrent;

using static Unity.Mathematics.math;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Lighting")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class LightingEvent : PipelineEvent
    {
        
        #region DIR_LIGHT
        private static int[] _Count = new int[2];
        private Matrix4x4[] cascadeShadowMapVP = new Matrix4x4[4];
        private Vector4[] shadowFrustumVP = new Vector4[6];
        public CBDRSharedData cbdr;
        [System.NonSerialized]
        private VolumetricLightEvent volumetricEvent;
        #endregion
        #region POINT_LIGHT
        private Material cubeDepthMaterial;
        private RenderSpotShadowCommand spotBuffer;
        private NativeList<CubemapViewProjMatrix> cubemapVPMatrices;
        private NativeArray<PointLightStruct> pointLightArray;
        private NativeArray<SpotLight> spotLightArray;
        private NativeList<SpotLightMatrix> spotLightMatrices;
        private List<Light> addMLightCommandList = new List<Light>(30);
        private List<Light> allLights = new List<Light>(30);
        private JobHandle lightingHandle;
        private JobHandle csmHandle;
        private CascadeShadowmap csmStruct;
        private StaticFit staticFit;
        private float* clipDistances;
        private float4x4* cascadeWorldToCamera;
        private float4x4* cascadeProjection;
        public override bool CheckProperty()
        {
            if (!cbdr.CheckAvailiable())
            {
                try
                {
                    cbdr.Dispose();
                }
                catch { }
                return false;
            }
            return cubeDepthMaterial != null;
        }
        #endregion
        protected override void Init(PipelineResources resources)
        {
            
            cbdr = new CBDRSharedData(resources);
            volumetricEvent = RenderPipeline.GetEvent<VolumetricLightEvent>(renderingPath);
            for (int i = 0; i < cascadeShadowMapVP.Length; ++i)
            {
                cascadeShadowMapVP[i] = Matrix4x4.identity;
            }
            cubeDepthMaterial = new Material(resources.shaders.cubeDepthShader);
            spotBuffer = new RenderSpotShadowCommand();
            spotBuffer.Init(resources.shaders.spotLightDepthShader);

        }

        protected override void Dispose()
        {
            DestroyImmediate(cubeDepthMaterial);
            spotBuffer.Dispose();
            cbdr.Dispose();
        }
        private static StaticFit DirectionalShadowStaticFit(Camera cam, SunLight sunlight, float* outClipDistance)
        {
            StaticFit staticFit;
            staticFit.resolution = sunlight.resolution;
            staticFit.mainCamTrans = cam;
            staticFit.frustumCorners = new NativeArray<float3>((SunLight.CASCADELEVELCOUNT + 1) * 4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            outClipDistance[0] = cam.nearClipPlane;
            outClipDistance[1] = sunlight.firstLevelDistance;
            outClipDistance[2] = sunlight.secondLevelDistance;
            outClipDistance[3] = sunlight.thirdLevelDistance;
            outClipDistance[4] = sunlight.farestDistance;
            return staticFit;
        }
        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            var visLights = data.cullResults.visibleLights;
            LightFilter.allVisibleLight = visLights.Ptr();
            allLights.Clear();
            cbdr.UpdateFroxel(volumetricEvent && volumetricEvent.Enabled);
            foreach (var i in visLights)
            {
                allLights.Add(i.light);
            }
            addMLightCommandList.Clear();
            LightFilter.allMLightCommandList = addMLightCommandList;
            pointLightArray = new NativeArray<PointLightStruct>(visLights.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            spotLightArray = new NativeArray<SpotLight>(visLights.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            cubemapVPMatrices = new NativeList<CubemapViewProjMatrix>(CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT, Allocator.Temp);
            spotLightMatrices = new NativeList<SpotLightMatrix>(CBDRSharedData.MAXIMUMSPOTLIGHTCOUNT, Allocator.Temp);
            LightFilter.allLights = allLights;
            LightFilter.pointLightArray = pointLightArray;
            LightFilter.spotLightArray = spotLightArray;
            LightFilter.cubemapVPMatrices = cubemapVPMatrices;
            LightFilter.spotLightMatrices = spotLightMatrices;
            lightingHandle = (new LightFilter()).Schedule(allLights.Count, 1);
            if (SunLight.current != null && SunLight.current.enabled && SunLight.current.enableShadow)
            {
                clipDistances = (float*)UnsafeUtility.Malloc(SunLight.CASCADECLIPSIZE * sizeof(float), 16, Allocator.Temp);
                staticFit = DirectionalShadowStaticFit(cam.cam, SunLight.current, clipDistances);
                cascadeWorldToCamera = (float4x4*)UnsafeUtility.Malloc(SunLight.CASCADELEVELCOUNT * sizeof(float4x4), 16, Allocator.Temp);
                cascadeProjection = (float4x4*)UnsafeUtility.Malloc(SunLight.CASCADELEVELCOUNT * sizeof(float4x4), 16, Allocator.Temp);
                PipelineFunctions.GetfrustumCorners(clipDistances, SunLight.CASCADELEVELCOUNT + 1, cam.cam, staticFit.frustumCorners.Ptr());
                csmStruct = new CascadeShadowmap
                {
                    cascadeShadowmapVPs = (float4x4*)cascadeShadowMapVP.Ptr(),
                    cascadeProjection = cascadeProjection,
                    cascadeWorldToCamera = cascadeWorldToCamera,
                    orthoCam = (OrthoCam*)UnsafeUtility.AddressOf(ref SunLight.current.shadCam),
                    farClipPlane = SunLight.current.farestZ,
                    frustumCorners = staticFit.frustumCorners.Ptr(),
                    resolution = staticFit.resolution,
                    isD3D = GraphicsUtility.platformIsD3D
                };
                csmHandle = csmStruct.ScheduleRefBurst(SunLight.CASCADELEVELCOUNT, 1);
            }
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            DirLight(cam, ref data);
            PointLight(cam, ref data);
            LightFilter.Clear();
        }
        private void DirLight(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (SunLight.current == null || !SunLight.current.enabled)
            {
                return;
            }
            cbdr.lightFlag |= 0b100;
            if (SunLight.current.enableShadow)
                cbdr.lightFlag |= 0b010;
            CommandBuffer buffer = data.buffer;
            int pass;
            if (SunLight.current.enableShadow)
            {
                RenderClusterOptions opts = new RenderClusterOptions
                {
                    frustumPlanes = shadowFrustumVP,
                    command = buffer,
                    cullingShader = data.resources.shaders.gpuFrustumCulling,
                };
                buffer.SetGlobalVector(ShaderIDs._NormalBiases, SunLight.current.normalBias);   //Only Depth
                buffer.SetGlobalVector(ShaderIDs._ShadowDisableDistance, new Vector4(SunLight.current.firstLevelDistance,
                    SunLight.current.secondLevelDistance,
                    SunLight.current.thirdLevelDistance,
                    SunLight.current.farestDistance));//Only Mask
                buffer.SetGlobalVector(ShaderIDs._SoftParam, SunLight.current.cascadeSoftValue / SunLight.current.resolution);
                csmHandle.Complete();
                SceneController.DrawDirectionalShadow(cam, ref staticFit, ref data, ref opts, clipDistances, cascadeWorldToCamera, cascadeProjection);
                buffer.SetGlobalMatrixArray(ShaderIDs._ShadowMapVPs, cascadeShadowMapVP);
                buffer.SetGlobalTexture(ShaderIDs._DirShadowMap, SunLight.current.shadowmapTexture);
                cbdr.dirLightShadowmap = SunLight.current.shadowmapTexture;
                staticFit.frustumCorners.Dispose();
                pass = 0;
            }
            else
            {
                pass = 1;
            }
            buffer.SetGlobalVector(ShaderIDs._DirLightFinalColor, SunLight.current.light.color * SunLight.current.light.intensity);
            buffer.SetGlobalVector(ShaderIDs._DirLightPos, -(Vector3)SunLight.current.shadCam.forward);
        }
        private void PointLight(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            VoxelLightCommonData(buffer, cam.cam);
            ClearDispatch(buffer);
            lightingHandle.Complete();
            foreach (var i in addMLightCommandList)
            {
                MLight.AddMLight(i);
            }
            addMLightCommandList.Clear();
            cbdr.pointshadowCount = cubemapVPMatrices.Length;
            if (LightFilter.pointLightCount > 0)
            {
                if (cubemapVPMatrices.Length > 0)
                {
                    var cullShader = data.resources.shaders.gpuFrustumCulling;
                    buffer.SetGlobalTexture(ShaderIDs._CubeShadowMapArray, cbdr.cubeArrayMap);
                    NativeArray<VisibleLight> allLights = data.cullResults.visibleLights;
                    PointLightStruct* pointLightPtr = pointLightArray.Ptr();
                    for (int i = 0; i < cubemapVPMatrices.Length; ++i)
                    {
                        ref CubemapViewProjMatrix vpMatrices = ref cubemapVPMatrices[i];
                        int2 lightIndex = vpMatrices.index;
                        Light lt = allLights[lightIndex.y].light;
                        MLight light = MUnsafeUtility.GetObject<MLight>(vpMatrices.mLightPtr);
                        if (light.useShadowCache)
                        {
                            light.UpdateShadowCacheType(true);
                            if (light.updateShadowCache)
                            {
                                light.updateShadowCache = false;
                                SceneController.DrawPointLight(light, ref pointLightPtr[lightIndex.x], cubeDepthMaterial, buffer, cullShader, i, ref data, cubemapVPMatrices.unsafePtr, cbdr.cubeArrayMap, cam.inverseRender);
                                int offset = i * 6;
                                for (int a = 0; a < 6; ++a)
                                {
                                    buffer.CopyTexture(cbdr.cubeArrayMap, offset + a, light.shadowMap, a);
                                }
                            }
                            else
                            {
                                PipelineFunctions.CopyToCubeMap(cbdr.cubeArrayMap, light.shadowMap, buffer, i);
                            }
                        }
                        else
                        {
                            SceneController.DrawPointLight(light, ref pointLightPtr[lightIndex.x], cubeDepthMaterial, buffer, cullShader, i, ref data, cubemapVPMatrices.unsafePtr, cbdr.cubeArrayMap, cam.inverseRender);
                        }

                        //TODO
                        //Multi frame shadowmap
                    }
                }
                SetPointLightBuffer(pointLightArray, LightFilter.pointLightCount, buffer);
                buffer.EnableShaderKeyword("POINTLIGHT");
                cbdr.lightFlag |= 1;
            }
            else
            {
                buffer.DisableShaderKeyword("POINTLIGHT");
            }
            cbdr.spotShadowCount = spotLightMatrices.Length;
            if (LightFilter.spotLightCount > 0)
            {
                if (spotLightMatrices.Length > 0)
                {
                    SpotLight* allSpotLightPtr = spotLightArray.Ptr();
                    buffer.SetGlobalTexture(ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
                    spotBuffer.renderTarget = cbdr.spotArrayMap;
                    spotBuffer.shadowMatrices = spotLightMatrices.unsafePtr;
                    NativeArray<VisibleLight> allLights = data.cullResults.visibleLights;
                    for (int i = 0; i < spotLightMatrices.Length; ++i)
                    {
                        ref SpotLightMatrix vpMatrices = ref spotLightMatrices[i];
                        int2 index = vpMatrices.index;
                        MLight mlight = MUnsafeUtility.GetObject<MLight>(vpMatrices.mLightPtr);
                        ref SpotLight spot = ref allSpotLightPtr[index.x];
                        if (mlight.useShadowCache)
                        {
                            mlight.UpdateShadowCacheType(false);
                            if (mlight.updateShadowCache)
                            {
                                mlight.updateShadowCache = false;
                                SceneController.DrawSpotLight(buffer, data.resources.shaders.gpuFrustumCulling, ref data, mlight.shadowCam, ref spot, ref spotBuffer, cam.inverseRender);
                                buffer.CopyTexture(cbdr.spotArrayMap, spot.shadowIndex, mlight.shadowMap, 0);
                            }
                            else
                            {
                                ref SpotLightMatrix spotLightMatrix = ref spotBuffer.shadowMatrices[spot.shadowIndex];
                                spot.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;
                                buffer.CopyTexture(mlight.shadowMap, 0, cbdr.spotArrayMap, spot.shadowIndex);
                            }
                        }
                        else
                        {
                            SceneController.DrawSpotLight(buffer, data.resources.shaders.gpuFrustumCulling, ref data, mlight.shadowCam, ref spot, ref spotBuffer, cam.inverseRender);
                        }
                    }
                }
                SetSpotLightBuffer(spotLightArray, LightFilter.spotLightCount, buffer);
                buffer.EnableShaderKeyword("SPOTLIGHT");
                cbdr.lightFlag |= 0b1000;
            }
            else
            {
                buffer.DisableShaderKeyword("SPOTLIGHT");
            }
            VoxelLightCalculate(buffer, cam.cam);
        }
        private void VoxelLightCommonData(CommandBuffer buffer, Camera cam)
        {
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.SetXYPlaneKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.SetZPlaneKernel, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            Transform camTrans = cam.transform;
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraFarPos, camTrans.position + cam.farClipPlane * camTrans.forward);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraNearPos, camTrans.position + cam.nearClipPlane * camTrans.forward);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraForward, camTrans.forward);
            buffer.SetGlobalVector(ShaderIDs._CameraClipDistance, new Vector4(cam.nearClipPlane, cam.farClipPlane - cam.nearClipPlane));
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetXYPlaneKernel, 1, 1, 1);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetZPlaneKernel, 1, 1, 1);
        }

        private void SetPointLightBuffer(NativeArray<PointLightStruct> pointLightArray, int pointLightLength, CommandBuffer buffer)
        {
            CBDRSharedData.ResizeBuffer(ref cbdr.allPointLightBuffer, pointLightLength);
            cbdr.allPointLightBuffer.SetData(pointLightArray, 0, 0, pointLightLength);
            int tbdrPointKernel = cbdr.TBDRPointKernel;
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeTextureParam(cbdrShader, tbdrPointKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, tbdrPointKernel, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeTextureParam(cbdrShader, tbdrPointKernel, ShaderIDs._TilePointLightList, cbdr.pointTileLightList);
            if (cbdr.useFroxel) buffer.SetComputeTextureParam(cbdrShader, tbdrPointKernel, ShaderIDs._FroxelPointTileLightList, cbdr.froxelpointTileLightList);
            buffer.DispatchCompute(cbdr.cbdrShader, tbdrPointKernel, 1, 1, pointLightLength);
        }

        private void SetSpotLightBuffer(NativeArray<SpotLight> spotLightArray, int spotLightLength, CommandBuffer buffer)
        {
            CBDRSharedData.ResizeBuffer(ref cbdr.allSpotLightBuffer, spotLightLength);
            ComputeShader cbdrShader = cbdr.cbdrShader;
            int tbdrSpotKernel = cbdr.TBDRSpotKernel;
            cbdr.allSpotLightBuffer.SetData(spotLightArray, 0, 0, spotLightLength);
            buffer.SetComputeTextureParam(cbdrShader, tbdrSpotKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, tbdrSpotKernel, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeTextureParam(cbdrShader, tbdrSpotKernel, ShaderIDs._TileSpotLightList, cbdr.spotTileLightList);
            if (cbdr.useFroxel) buffer.SetComputeTextureParam(cbdrShader, tbdrSpotKernel, ShaderIDs._FroxelSpotTileLightList, cbdr.froxelSpotTileLightList);
            buffer.DispatchCompute(cbdr.cbdrShader, tbdrSpotKernel, 1, 1, spotLightLength);
        }

        private void ClearDispatch(CommandBuffer buffer)
        {
            int clearKernel = cbdr.ClearKernel;
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeTextureParam(cbdrShader, clearKernel, ShaderIDs._TilePointLightList, cbdr.pointTileLightList);
            buffer.SetComputeTextureParam(cbdrShader, clearKernel, ShaderIDs._TileSpotLightList, cbdr.spotTileLightList);
            if (cbdr.useFroxel)
            {
                buffer.SetComputeTextureParam(cbdrShader, clearKernel, ShaderIDs._FroxelPointTileLightList, cbdr.froxelpointTileLightList);
                buffer.SetComputeTextureParam(cbdrShader, clearKernel, ShaderIDs._FroxelSpotTileLightList, cbdr.froxelSpotTileLightList);
            }
            buffer.DispatchCompute(cbdrShader, clearKernel, 1, 1, 1);
        }

        private void VoxelLightCalculate(CommandBuffer buffer, Camera cam)
        {
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._TilePointLightList, cbdr.pointTileLightList);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._TileSpotLightList, cbdr.spotTileLightList);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._SpotLightIndexBuffer, cbdr.spotlightIndexBuffer);
            if (cbdr.useFroxel)
            {
                Transform camTrans = cam.transform;
                float3 inPoint = camTrans.position + camTrans.forward * cbdr.availiableDistance;
                float3 normal = camTrans.forward;
                float4 plane = new float4(normal, -dot(normal, inPoint));
                buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._FroxelPlane, plane);
            }
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.DeferredCBDR, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._SpotLightIndexBuffer, cbdr.spotlightIndexBuffer);
        }

        [Unity.Burst.BurstCompile]
        public unsafe struct CascadeShadowmap : IJobParallelFor
        {
            public int resolution;
            public float farClipPlane;
            [NativeDisableUnsafePtrRestriction]
            public OrthoCam* orthoCam;
            [NativeDisableUnsafePtrRestriction]
            public float4x4* cascadeShadowmapVPs;
            [NativeDisableUnsafePtrRestriction]
            public float3* frustumCorners;
            [NativeDisableUnsafePtrRestriction]
            public float4x4* cascadeWorldToCamera;
            [NativeDisableUnsafePtrRestriction]
            public float4x4* cascadeProjection;
            public bool isD3D;
            public void Execute(int index)
            {
                OrthoCam shadCam = new OrthoCam
                {
                    forward = orthoCam->forward,
                    up = orthoCam->up,
                    right = orthoCam->right
                };
                float range = 0;
                double3 averagePos = double3(0, 0, 0);
                int frustumStartPos = index * 4;
                for (int i = 0; i < 8; ++i)
                {
                    averagePos += frustumCorners[i + frustumStartPos];
                }
                averagePos /= 8;
                for (int i = 0; i < 8; ++i)
                {
                    double dist = distance(averagePos, frustumCorners[i + frustumStartPos]);
                    if (range < dist)
                    {
                        range = (float)dist;
                    }
                }
                shadCam.size = range;
                float3 targetPosition = (float3)averagePos - shadCam.forward * farClipPlane * 0.5f;
                shadCam.nearClipPlane = 0;
                shadCam.farClipPlane = farClipPlane;
                ref float4x4 shadowVP = ref cascadeShadowmapVPs[index];
                float4x4 invShadowVP = inverse(shadowVP);

                float4 ndcPos = mul(shadowVP, new float4(targetPosition, 1));
                ndcPos /= ndcPos.w;
                float2 uv = new float2(ndcPos.x, ndcPos.y) * 0.5f + new float2(0.5f, 0.5f);
                uv.x = (int)(uv.x * resolution + 0.5);
                uv.y = (int)(uv.y * resolution + 0.5);
                uv /= resolution;
                uv = uv * 2f - 1;
                ndcPos = new float4(uv.x, uv.y, ndcPos.z, 1);
                float4 targetPos_4 = mul(invShadowVP, ndcPos);
                targetPosition = targetPos_4.xyz / targetPos_4.w;
                shadCam.position = targetPosition;
                shadCam.UpdateProjectionMatrix();
                shadCam.UpdateTRSMatrix();
                shadowVP = mul(GraphicsUtility.GetGPUProjectionMatrix(shadCam.projectionMatrix, false, isD3D), shadCam.worldToCameraMatrix);
                cascadeWorldToCamera[index] = shadCam.worldToCameraMatrix;
                cascadeProjection[index] = shadCam.projectionMatrix;
            }
        }

        public unsafe struct LightFilter : IJobParallelFor
        {
            public static VisibleLight* allVisibleLight;
            public static List<Light> allLights;
            public static List<Light> allMLightCommandList;
            public static NativeList<CubemapViewProjMatrix> cubemapVPMatrices;
            public static NativeArray<PointLightStruct> pointLightArray;
            public static NativeArray<SpotLight> spotLightArray;
            public static NativeList<SpotLightMatrix> spotLightMatrices;
            public static int pointLightCount = 0;
            public static int spotLightCount = 0;
            public static void Clear()
            {
                pointLightCount = 0;
                spotLightCount = 0;
                allLights = null;
            }
            public static void CalculateCubemapMatrix(PointLightStruct* allLights, CubemapViewProjMatrix* allMatrix, int index)
            {
                ref CubemapViewProjMatrix cube = ref allMatrix[index];
                int2 shadowIndex = cube.index;
                PointLightStruct str = allLights[shadowIndex.x];
                PerspCam cam = new PerspCam();
                cam.aspect = 1;
                cam.farClipPlane = str.sphere.w;
                cam.nearClipPlane = 0.3f;
                cam.position = str.sphere.xyz;
                cam.fov = 90f;
                //Forward
                cam.right = float3(1, 0, 0);
                cam.up = float3(0, 1, 0);
                cam.forward = float3(0, 0, 1);
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();
                float4x4 proj = GraphicsUtility.GetGPUProjectionMatrix(cam.projectionMatrix, true);
                cube.forwardProjView = mul(proj, cam.worldToCameraMatrix);
                //Back
                cam.right = float3(-1, 0, 0);
                cam.up = float3(0, 1, 0);
                cam.forward = float3(0, 0, -1);
                cam.UpdateTRSMatrix();

                cube.backProjView = mul(proj, cam.worldToCameraMatrix);
                //Up
                cam.right = float3(-1, 0, 0);
                cam.up = float3(0, 0, 1);
                cam.forward = float3(0, 1, 0);
                cam.UpdateTRSMatrix();
                cube.upProjView = mul(proj, cam.worldToCameraMatrix);
                //Down
                cam.right = float3(-1, 0, 0);
                cam.up = float3(0, 0, -1);
                cam.forward = float3(0, -1, 0);
                cam.UpdateTRSMatrix();
                cube.downProjView = mul(proj, cam.worldToCameraMatrix);
                //Right
                cam.up = float3(0, 1, 0);
                cam.right = float3(0, 0, -1);
                cam.forward = float3(1, 0, 0);
                cam.UpdateTRSMatrix();
                cube.rightProjView = mul(proj, cam.worldToCameraMatrix);
                //Left
                cam.up = float3(0, 1, 0);
                cam.right = float3(0, 0, 1);
                cam.forward = float3(-1, 0, 0);
                cam.UpdateTRSMatrix();
                cube.leftProjView = mul(proj, cam.worldToCameraMatrix);
                NativeArray<float4> frustumArray = new NativeArray<float4>(6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                cube.frustumPlanes = frustumArray.Ptr();
                cube.frustumPlanes[0] = VectorUtility.GetPlane(float3(0, 1, 0), str.sphere.xyz + float3(0, str.sphere.w, 0));
                cube.frustumPlanes[1] = VectorUtility.GetPlane(float3(0, -1, 0), str.sphere.xyz + float3(0, -str.sphere.w, 0));
                cube.frustumPlanes[2] = VectorUtility.GetPlane(float3(1, 0, 0), str.sphere.xyz + float3(str.sphere.w, 0, 0));
                cube.frustumPlanes[3] = VectorUtility.GetPlane(float3(-1, 0, 0), str.sphere.xyz + float3(-str.sphere.w, 0, 0));
                cube.frustumPlanes[4] = VectorUtility.GetPlane(float3(0, 0, 1), str.sphere.xyz + float3(0, 0, str.sphere.w));
                cube.frustumPlanes[5] = VectorUtility.GetPlane(float3(0, 0, -1), str.sphere.xyz + float3(0, 0, -str.sphere.w));
            }
            public static void CalculatePersMatrix(SpotLight* allLights, SpotLightMatrix* projectionMatrices, int index)
            {
                ref SpotLightMatrix matrices = ref projectionMatrices[index];
                int2 shadowIndex = matrices.index;
                ref SpotLight lit = ref allLights[shadowIndex.x];
                PerspCam cam = new PerspCam
                {
                    aspect = 1,
                    farClipPlane = lit.lightCone.height,
                    fov = lit.angle * 2 * Mathf.Rad2Deg,
                    nearClipPlane = Mathf.Max(0.3f, allLights->nearClip)
                };
                cam.UpdateViewMatrix(lit.vpMatrix);
                cam.UpdateProjectionMatrix();
                matrices.projectionMatrix = cam.projectionMatrix;
                matrices.worldToCamera = cam.worldToCameraMatrix;
            }
            public void Execute(int index)
            {
                PointLightStruct* indStr = pointLightArray.Ptr();
                SpotLight* spotStr = spotLightArray.Ptr();
                VisibleLight i = allVisibleLight[index];
                MLight mlight;
                switch (i.lightType)
                {
                    case LightType.Point:
                        if (!MLight.GetPointLight(allLights[index], out mlight))
                        {
                            lock (allMLightCommandList)
                            {
                                allMLightCommandList.Add(allLights[index]);
                            }
                            break;
                        }
                        int currentPointCount = Interlocked.Increment(ref pointLightCount) - 1;
                        PointLightStruct* currentPtr = indStr + currentPointCount;
                        Color col = i.finalColor;
                        currentPtr->lightColor = new float3(col.r, col.g, col.b) / (4 * Mathf.PI);
                        currentPtr->sphere = i.localToWorldMatrix.GetColumn(3);
                        currentPtr->sphere.w = i.range;
                        if (mlight.useShadow)
                        {
                            currentPtr->shadowIndex = cubemapVPMatrices.ConcurrentAdd(new CubemapViewProjMatrix
                            {
                                index = new int2(currentPointCount, index),
                                mLightPtr = MUnsafeUtility.GetManagedPtr(mlight)
                            });
                        }
                        else
                        {
                            currentPtr->shadowIndex = -1;
                        }
                        if (currentPtr->shadowIndex >= 0)
                        {
                            CalculateCubemapMatrix(indStr, cubemapVPMatrices.unsafePtr, currentPtr->shadowIndex);
                        }

                        break;
                    case LightType.Spot:
                        if (!MLight.GetPointLight(allLights[index], out mlight))
                        {
                            lock (allMLightCommandList)
                            {
                                allMLightCommandList.Add(allLights[index]);
                            }
                            break;
                        }
                        int currentSpotCount = Interlocked.Increment(ref spotLightCount) - 1;
                        SpotLight* currentSpot = spotStr + currentSpotCount;
                        Color spotCol = i.finalColor;
                        currentSpot->lightColor = new float3(spotCol.r, spotCol.g, spotCol.b) / (4 * Mathf.PI);
                        float deg = Mathf.Deg2Rad * i.spotAngle * 0.5f;
                        currentSpot->lightCone = new Cone((Vector3)i.localToWorldMatrix.GetColumn(3), i.range, normalize((Vector3)i.localToWorldMatrix.GetColumn(2)), deg);
                        currentSpot->angle = deg;
                        currentSpot->lightRight = normalize((Vector3)i.localToWorldMatrix.GetColumn(1));
                        currentSpot->smallAngle = Mathf.Deg2Rad * mlight.smallSpotAngle * 0.5f;
                        currentSpot->nearClip = mlight.spotNearClip;
                        if (mlight.useShadow)
                        {
                            currentSpot->vpMatrix = i.localToWorldMatrix;
                            currentSpot->shadowIndex = spotLightMatrices.ConcurrentAdd(new SpotLightMatrix
                            {
                                index = new int2(currentSpotCount, index),
                                mLightPtr = MUnsafeUtility.GetManagedPtr(mlight)
                            });
                        }
                        else
                        {
                            currentSpot->shadowIndex = -1;
                        }
                        if (currentSpot->shadowIndex >= 0)
                        {
                            CalculatePersMatrix(spotStr, spotLightMatrices.unsafePtr, currentSpot->shadowIndex);
                        }
                        break;

                }
            }
        }
    }
}