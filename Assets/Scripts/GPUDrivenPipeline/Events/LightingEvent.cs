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
using UnityEngine.Experimental.Rendering;
namespace MPipeline
{
    [PipelineEvent(true, true)]
    public unsafe class LightingEvent : PipelineEvent
    {
        #region DIR_LIGHT
        private Material shadMaskMaterial;
        private static int[] _Count = new int[2];
        private Matrix4x4[] cascadeShadowMapVP = new Matrix4x4[4];
        private Vector4[] shadowFrustumVP = new Vector4[6];
        private CBDRSharedData cbdr;
        #endregion
        #region POINT_LIGHT
        private Material pointLightMaterial;
        private Material cubeDepthMaterial;
        private ComputeBuffer sphereBuffer;
        private RenderSpotShadowCommand spotBuffer;
        private NativeList<CubemapViewProjMatrix> cubemapVPMatrices;
        private NativeArray<PointLightStruct> pointLightArray;
        private NativeArray<SpotLight> spotLightArray;
        private NativeList<SpotLightMatrix> spotLightMatrices;
        private List<Light> addMLightCommandList = new List<Light>(30);
        private List<Light> allLights = new List<Light>(30);
        private JobHandle lightingHandle;
        #endregion
        protected override void Init(PipelineResources resources)
        {
            cbdr = PipelineSharedData.Get(renderPath, resources, (a) => new CBDRSharedData(a));
            shadMaskMaterial = new Material(resources.shadowMaskShader);
            for (int i = 0; i < cascadeShadowMapVP.Length; ++i)
            {
                cascadeShadowMapVP[i] = Matrix4x4.identity;
            }
            pointLightMaterial = new Material(resources.pointLightShader);
            cubeDepthMaterial = new Material(resources.cubeDepthShader);
            Vector3[] vertices = resources.sphereMesh.vertices;
            int[] triangle = resources.sphereMesh.triangles;
            NativeArray<Vector3> allVertices = new NativeArray<Vector3>(triangle.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < allVertices.Length; ++i)
            {
                allVertices[i] = vertices[triangle[i]];
            }
            sphereBuffer = new ComputeBuffer(allVertices.Length, sizeof(Vector3));
            sphereBuffer.SetData(allVertices);
            allVertices.Dispose();
            spotBuffer = new RenderSpotShadowCommand();
            spotBuffer.Init(resources.spotLightDepthShader);
        }

        protected override void Dispose()
        {
            DestroyImmediate(shadMaskMaterial);
            DestroyImmediate(pointLightMaterial);
            DestroyImmediate(cubeDepthMaterial);
            sphereBuffer.Dispose();
            spotBuffer.Dispose();
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            LightFilter.allVisibleLight = data.cullResults.visibleLights;
            allLights.Clear();
            foreach(var i in LightFilter.allVisibleLight)
            {
                allLights.Add(i.light);
            }
            addMLightCommandList.Clear();
            LightFilter.allMLightCommandList = addMLightCommandList;
            pointLightArray = new NativeArray<PointLightStruct>(LightFilter.allVisibleLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            spotLightArray = new NativeArray<SpotLight>(LightFilter.allVisibleLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            cubemapVPMatrices = new NativeList<CubemapViewProjMatrix>(CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT, Allocator.Temp);
            spotLightMatrices = new NativeList<SpotLightMatrix>(CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT, Allocator.Temp);
            LightFilter.allLights = allLights;
            LightFilter.pointLightArray = pointLightArray;
            LightFilter.spotLightArray = spotLightArray;
            LightFilter.cubemapVPMatrices = cubemapVPMatrices;
            LightFilter.spotLightMatrices = spotLightMatrices;
            lightingHandle = (new LightFilter()).Schedule(allLights.Count, 1);
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
                    cullingShader = data.resources.gpuFrustumCulling,
                    isOrtho = true
                };
                buffer.SetGlobalVector(ShaderIDs._NormalBiases, SunLight.current.normalBias);   //Only Depth
                buffer.SetGlobalVector(ShaderIDs._ShadowDisableDistance, new Vector4(SunLight.current.firstLevelDistance,
                    SunLight.current.secondLevelDistance,
                    SunLight.current.thirdLevelDistance,
                    SunLight.current.farestDistance));//Only Mask
                buffer.SetGlobalVector(ShaderIDs._SoftParam, SunLight.current.cascadeSoftValue / SunLight.current.resolution);
                SceneController.DrawDirectionalShadow(cam.cam, ref data, ref opts, SunLight.current, cascadeShadowMapVP);
                buffer.SetGlobalMatrixArray(ShaderIDs._ShadowMapVPs, cascadeShadowMapVP);
                buffer.SetGlobalTexture(ShaderIDs._DirShadowMap, SunLight.current.shadowmapTexture);
                cbdr.dirLightShadowmap = SunLight.current.shadowmapTexture;
                pass = 0;
            }
            else
            {
                pass = 1;
            }
            buffer.SetGlobalVector(ShaderIDs._DirLightFinalColor, SunLight.current.light.color * SunLight.current.light.intensity);
            buffer.SetGlobalVector(ShaderIDs._DirLightPos, -(Vector3)SunLight.current.shadCam.forward);
            buffer.SetRenderTarget(cam.targets.renderTargetIdentifier, cam.targets.depthIdentifier);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, shadMaskMaterial, 0, pass);
        }
        private void PointLight(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            pointLightMaterial.SetBuffer(ShaderIDs.verticesBuffer, sphereBuffer);
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
                    var cullShader = data.resources.gpuFrustumCulling;
                    buffer.SetGlobalTexture(ShaderIDs._CubeShadowMapArray, cbdr.cubeArrayMap);
                    RenderClusterOptions opts = new RenderClusterOptions
                    {
                        cullingShader = cullShader,
                        command = buffer,
                        frustumPlanes = null,
                        isOrtho = false
                    };
                    List<VisibleLight> allLights = data.cullResults.visibleLights;
                    PointLightStruct* pointLightPtr = pointLightArray.Ptr();
                    for (int i = 0; i < cubemapVPMatrices.Length; ++i)
                    {
                        ref CubemapViewProjMatrix vpMatrices = ref cubemapVPMatrices[i];
                        int2 lightIndex = vpMatrices.index;
                        Light lt = allLights[lightIndex.y].light;
                        MLight light = MUnsafeUtility.GetObject<MLight>(vpMatrices.mLightPtr);
                        if (light.UpdateFrame(Time.frameCount))
                        {
                            light.UpdateShadowCacheType(true);
                            SceneController.DrawPointLight(light, ref pointLightPtr[lightIndex.x], cubeDepthMaterial, ref opts, i, light.shadowMap, ref data, cubemapVPMatrices.unsafePtr, cbdr.cubeArrayMap);
                        }
                        else
                        {
                            PipelineFunctions.CopyToCubeMap(cbdr.cubeArrayMap, light.shadowMap, buffer, i);
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
                    RenderClusterOptions opts = new RenderClusterOptions
                    {
                        cullingShader = data.resources.gpuFrustumCulling,
                        command = buffer,
                        frustumPlanes = null,
                        isOrtho = false,
                    };
                    SpotLight* allSpotLightPtr = spotLightArray.Ptr();
                    buffer.SetGlobalTexture(ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
                    spotBuffer.renderTarget = cbdr.spotArrayMap;
                    spotBuffer.shadowMatrices = spotLightMatrices.unsafePtr;
                    List<VisibleLight> allLights = data.cullResults.visibleLights;
                    for (int i = 0; i < spotLightMatrices.Length; ++i)
                    {
                        ref SpotLightMatrix vpMatrices = ref spotLightMatrices[i];
                        int2 index = vpMatrices.index;
                        MLight mlight = MUnsafeUtility.GetObject<MLight>(vpMatrices.mLightPtr);
                        mlight.UpdateShadowCacheType(false);
                        ref SpotLight spot = ref allSpotLightPtr[index.x];
                        if (mlight.UpdateFrame(Time.frameCount))
                        {
                            SceneController.DrawSpotLight(ref opts, ref data, mlight.shadowCam, ref spot, ref spotBuffer);
                            buffer.CopyTexture(cbdr.spotArrayMap, spot.shadowIndex, mlight.shadowMap, 0);
                        }
                        else
                        {
                            ref SpotLightMatrix spotLightMatrix = ref spotBuffer.shadowMatrices[spot.shadowIndex];
                            spot.vpMatrix = GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, false) * spotLightMatrix.worldToCamera;
                            buffer.CopyTexture(mlight.shadowMap, 0, cbdr.spotArrayMap, spot.shadowIndex);
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
            buffer.BlitSRT(cam.targets.renderTargetIdentifier, pointLightMaterial, 0);
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

        public unsafe struct LightFilter : IJobParallelFor
        {
            public static List<VisibleLight> allVisibleLight;
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
                allVisibleLight = null;
                allMLightCommandList = null;
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
                cam.forward = Vector3.forward;
                cam.up = Vector3.down;
                cam.right = Vector3.left;
                cam.UpdateTRSMatrix();

                cube.forwardView = cam.worldToCameraMatrix;
                //Back
                cam.forward = Vector3.back;
                cam.up = Vector3.down;
                cam.right = Vector3.right;
                cam.UpdateTRSMatrix();

                cube.backView = cam.worldToCameraMatrix;
                //Up
                cam.forward = Vector3.up;
                cam.up = Vector3.back;
                cam.right = Vector3.right;
                cam.UpdateTRSMatrix();
                cube.upView = cam.worldToCameraMatrix;
                //Down
                cam.forward = Vector3.down;
                cam.up = Vector3.forward;
                cam.right = Vector3.right;
                cam.UpdateTRSMatrix();
                cube.downView = cam.worldToCameraMatrix;
                //Right
                cam.forward = Vector3.right;
                cam.up = Vector3.down;
                cam.right = Vector3.forward;
                cam.UpdateTRSMatrix();
                cube.rightView = cam.worldToCameraMatrix;
                //Left
                cam.forward = Vector3.left;
                cam.up = Vector3.down;
                cam.right = Vector3.back;
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();
                cube.projMat = cam.projectionMatrix;
                cube.leftView = cam.worldToCameraMatrix;
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
                    aspect = lit.aspect,
                    farClipPlane = lit.lightCone.height,
                    fov = lit.angle * 2 * Mathf.Rad2Deg,
                    nearClipPlane = 0.3f
                };
                cam.UpdateViewMatrix(lit.vpMatrix);
                cam.UpdateProjectionMatrix();
                matrices.projectionMatrix = cam.projectionMatrix;
                matrices.worldToCamera = cam.worldToCameraMatrix;
            }
            public void Execute(int index)
            {
                const float LUMENRATE = (4 * Mathf.PI);
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
                        currentPtr->lightColor = new float3(col.r, col.g, col.b) / LUMENRATE;
                        currentPtr->sphere = i.localToWorld.GetColumn(3);
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
                        if(currentPtr->shadowIndex >= 0)
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
                        currentSpot->lightColor = new float3(spotCol.r, spotCol.g, spotCol.b) / LUMENRATE;
                        float deg = Mathf.Deg2Rad * i.spotAngle * 0.5f;
                        currentSpot->lightCone = new Cone((Vector3)i.localToWorld.GetColumn(3), i.range, normalize((Vector3)i.localToWorld.GetColumn(2)), deg);
                        currentSpot->angle = deg;
                        currentSpot->aspect = mlight.aspect;
                        currentSpot->lightRight = normalize((Vector3)i.localToWorld.GetColumn(1));
                        currentSpot->smallAngle = Mathf.Deg2Rad * mlight.smallSpotAngle * 0.5f;
                        currentSpot->nearClip = mlight.spotNearClip;
                        if (mlight.useShadow)
                        {
                            currentSpot->vpMatrix = i.localToWorld;
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