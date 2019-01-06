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
        private ulong gcHandler;
        private Material pointLightMaterial;
        private Material cubeDepthMaterial;
        private ComputeBuffer sphereBuffer;
        private NativeArray<PointLightStruct> pointLightArray;
        private NativeArray<SpotLight> spotLightArray;
        private CubeCullingBuffer cubeBuffer;
        private int pointLightCount = 0;
        private int spotLightCount = 0;
        private List<MPointLight> shadowList;
        private NativeList<int> shadowIndicesForJobs;
        private NativeArray<CubemapViewProjMatrix> vpMatrices;
        private JobHandle vpMatricesJobHandle;
        #endregion
        protected override void Init(PipelineResources resources)
        {
            cbdr = PipelineSharedData.Get(renderPath, resources, (a) => new CBDRSharedData(a));
            shadMaskMaterial = new Material(resources.shadowMaskShader);
            for (int i = 0; i < cascadeShadowMapVP.Length; ++i)
            {
                cascadeShadowMapVP[i] = Matrix4x4.identity;
            }

            shadowList = new List<MPointLight>(20);
            cubeBuffer = new CubeCullingBuffer();
            cubeBuffer.Init(resources.pointLightFrustumCulling);
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

        }

        protected override void Dispose()
        {
            Destroy(shadMaskMaterial);
            Destroy(pointLightMaterial);
            Destroy(cubeDepthMaterial);
            sphereBuffer.Dispose();
            cubeBuffer.Dispose();
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            shadowList.Clear();
            List<VisibleLight> allLight = data.cullResults.visibleLights;
            pointLightArray = new NativeArray<PointLightStruct>(allLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            spotLightArray = new NativeArray<SpotLight>(allLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            shadowIndicesForJobs = new NativeList<int>(allLight.Count, Allocator.Temp);
            PointLightStruct* indStr = pointLightArray.Ptr();
            SpotLight* spotStr = spotLightArray.Ptr();
            pointLightCount = 0;
            spotLightCount = 0;
            foreach (var i in allLight)
            {
                Light lit = i.light;
                switch (i.lightType)
                {
                    case LightType.Point:
                        PointLightStruct* currentPtr = indStr + pointLightCount;
                        Color col = lit.color;
                        currentPtr->lightColor = new float3(col.r, col.g, col.b);
                        currentPtr->lightIntensity = lit.intensity;
                        currentPtr->sphere = i.localToWorld.GetColumn(3);
                        currentPtr->sphere.w = i.range;
                        if (lit.shadows != LightShadows.None)
                        {
                            shadowIndicesForJobs.Add(pointLightCount);
                            currentPtr->shadowIndex = shadowList.Count;
                            shadowList.Add(MPointLight.GetPointLight(lit));
                        }
                        else
                        {
                            currentPtr->shadowIndex = -1;
                        }
                        pointLightCount++;
                        break;
                    case LightType.Spot:
                        SpotLight* currentSpot = spotStr + spotLightCount;
                        Color spotCol = lit.color;
                        currentSpot->lightColor = new float3(spotCol.r, spotCol.g, spotCol.b);
                        currentSpot->lightIntensity = lit.intensity;
                        float deg = Mathf.Deg2Rad * i.spotAngle * 0.5f;
                        currentSpot->lightCone = new Cone((Vector3)i.localToWorld.GetColumn(3), i.range, (Vector3)i.localToWorld.GetColumn(2), deg);
                        currentSpot->angle = deg;
                        //TODO: add shadow here
                        spotLightCount++;
                        break;
                }
            }
            vpMatrices = new NativeArray<CubemapViewProjMatrix>(shadowIndicesForJobs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            vpMatricesJobHandle = new GetCubeMapMatrix
            {
                allLights = pointLightArray.Ptr(),
                allMatrix = vpMatrices.Ptr(),
                shadowIndex = shadowIndicesForJobs.unsafePtr
            }.Schedule(vpMatrices.Length, 1);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            DirLight(cam, ref data);
            PointLight(cam, ref data);
        }
        private void DirLight(PipelineCamera cam, ref PipelineCommandData data)
        {
            PipelineBaseBuffer baseBuffer;
            if (SunLight.current == null || !SunLight.current.enabled || !SceneController.GetBaseBuffer(out baseBuffer))
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
                ref ShadowmapSettings settings = ref SunLight.current.settings;
                buffer.SetGlobalVector(ShaderIDs._NormalBiases, settings.normalBias);   //Only Depth
                buffer.SetGlobalVector(ShaderIDs._ShadowDisableDistance, new Vector4(settings.firstLevelDistance, settings.secondLevelDistance, settings.thirdLevelDistance, settings.farestDistance));//Only Mask
                buffer.SetGlobalVector(ShaderIDs._SoftParam, settings.cascadeSoftValue / settings.resolution);
                SceneController.current.DrawDirectionalShadow(cam.cam, ref data, ref opts, ref SunLight.current.settings, ref SunLight.shadMap, cascadeShadowMapVP);
                buffer.SetGlobalMatrixArray(ShaderIDs._ShadowMapVPs, cascadeShadowMapVP);
                buffer.SetGlobalTexture(ShaderIDs._DirShadowMap, SunLight.shadMap.shadowmapTexture);
                cbdr.dirLightShadowmap = SunLight.shadMap.shadowmapTexture;
                pass = 0;
            }
            else
            {
                pass = 1;
            }
            buffer.SetGlobalVector(ShaderIDs._DirLightFinalColor, SunLight.shadMap.light.color * SunLight.shadMap.light.intensity);
            buffer.SetGlobalVector(ShaderIDs._DirLightPos, -SunLight.shadMap.shadCam.forward);
            buffer.SetRenderTarget(cam.targets.renderTargetIdentifier, cam.targets.depthIdentifier);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, shadMaskMaterial, 0, pass);
        }
        private void PointLight(PipelineCamera cam, ref PipelineCommandData data)
        {
            PipelineBaseBuffer baseBuffer;
            if (!SceneController.GetBaseBuffer(out baseBuffer))
            {
                return;
            }
            CommandBuffer buffer = data.buffer;
            UnsafeUtility.ReleaseGCObject(gcHandler);
            pointLightMaterial.SetBuffer(ShaderIDs.verticesBuffer, sphereBuffer);
            VoxelLightCommonData(buffer, cam.cam);
            ClearDispatch(buffer);
            if (pointLightCount > 0)
            {
                if (shadowList.Count > 0)
                {
                    RenderTexture shadowArray = RenderTexture.GetTemporary(new RenderTextureDescriptor
                    {
                        autoGenerateMips = false,
                        bindMS = false,
                        colorFormat = RenderTextureFormat.RHalf,
                        depthBufferBits = 16,
                        dimension = TextureDimension.CubeArray,
                        volumeDepth = shadowList.Count * 6,
                        enableRandomWrite = false,
                        height = 1024,
                        width = 1024,
                        memoryless = RenderTextureMemoryless.None,
                        msaaSamples = 1,
                        shadowSamplingMode = ShadowSamplingMode.None,
                        sRGB = false,
                        useMipMap = false,
                        vrUsage = VRTextureUsage.None
                    });
                    shadowArray.filterMode = FilterMode.Point;
                    buffer.SetGlobalTexture(ShaderIDs._CubeShadowMapArray, shadowArray);
                    cam.temporalRT.Add(shadowArray);
                    var cullShader = data.resources.pointLightFrustumCulling;
                    RenderClusterOptions opts = new RenderClusterOptions
                    {
                        cullingShader = cullShader,
                        command = buffer,
                        frustumPlanes = null,
                        isOrtho = false
                    };
                    vpMatricesJobHandle.Complete();
                    cubeBuffer.vpMatrices = vpMatrices.Ptr();
                    cubeBuffer.renderTarget = shadowArray;
                    cbdr.cubemapShadowArray = shadowArray;
                    for (int i = 0; i < shadowList.Count; ++i)
                    {
                        MPointLight light = shadowList[i];
                        //     if (light.frameCount < 0)
                        //   {
                        SceneController.current.DrawCubeMap(light, cubeDepthMaterial, ref opts, ref cubeBuffer, i, light.shadowMap, ref data, baseBuffer);
                        light.frameCount = 10000;
                        // }
                        //else
                        // {
                        //   SceneController.current.CopyToCubeMap(shadowArray, light.shadowMap, buffer, i);
                        //}

                        //TODO
                        //Multi frame shadowmap
                    }
                }
                SetPointLightBuffer(pointLightArray, pointLightCount, buffer);
                buffer.EnableShaderKeyword("POINTLIGHT");
                cbdr.lightFlag |= 1;
            }
            else
            {
                buffer.DisableShaderKeyword("POINTLIGHT");
            }
            if (spotLightCount > 0)
            {
                SetSpotLightBuffer(spotLightArray, spotLightCount, buffer);
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
                float4 plane = new float4(normal, -math.dot(normal, inPoint));
                buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._FroxelPlane, plane);
            }
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.DeferredCBDR, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._SpotLightIndexBuffer, cbdr.spotlightIndexBuffer);
        }

        public unsafe struct GetCubeMapMatrix : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public PointLightStruct* allLights;
            [NativeDisableUnsafePtrRestriction]
            public int* shadowIndex;
            [NativeDisableUnsafePtrRestriction]
            public CubemapViewProjMatrix* allMatrix;
            float4 GetPlane(float3 normal, float3 inPoint)
            {
                return new float4(normal, -math.dot(normal, inPoint));
            }
            public void Execute(int index)
            {
                ref PointLightStruct str = ref allLights[shadowIndex[index]];
                ref CubemapViewProjMatrix cube = ref allMatrix[index];
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
                cam.UpdateProjectionMatrix();
                cube.forwardProj = cam.projectionMatrix;
                cube.forwardView = cam.worldToCameraMatrix;
                //Back
                cam.forward = Vector3.back;
                cam.up = Vector3.down;
                cam.right = Vector3.right;
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();
                cube.backProj = cam.projectionMatrix;
                cube.backView = cam.worldToCameraMatrix;
                //Up
                cam.forward = Vector3.up;
                cam.up = Vector3.back;
                cam.right = Vector3.right;
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();

                cube.upProj = cam.projectionMatrix;
                cube.upView = cam.worldToCameraMatrix;
                //Down
                cam.forward = Vector3.down;
                cam.up = Vector3.forward;
                cam.right = Vector3.right;
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();

                cube.downProj = cam.projectionMatrix;
                cube.downView = cam.worldToCameraMatrix;
                //Right
                cam.forward = Vector3.right;
                cam.up = Vector3.down;
                cam.right = Vector3.forward;
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();

                cube.rightProj = cam.projectionMatrix;
                cube.rightView = cam.worldToCameraMatrix;
                //Left
                cam.forward = Vector3.left;
                cam.up = Vector3.down;
                cam.right = Vector3.back;
                cam.UpdateTRSMatrix();
                cam.UpdateProjectionMatrix();

                cube.leftProj = cam.projectionMatrix;
                cube.leftView = cam.worldToCameraMatrix;
                NativeArray<float4> vec = new NativeArray<float4>(6, Allocator.Temp);
                cube.frustumPlanes = vec.Ptr();
                float3 camPos = cam.position;
                cube.frustumPlanes[0] = GetPlane(new float3(0, 1, 0), camPos + new float3(0, str.sphere.w, 0));
                cube.frustumPlanes[1] = GetPlane(new float3(0, -1, 0), camPos + new float3(0, -str.sphere.w, 0));
                cube.frustumPlanes[2] = GetPlane(new float3(1, 0, 0), camPos + new float3(str.sphere.w, 0, 0));
                cube.frustumPlanes[3] = GetPlane(new float3(-1, 0, 0), camPos + new float3(-str.sphere.w, 0, 0));
                cube.frustumPlanes[4] = GetPlane(new float3(0, 0, 1), camPos + new float3(0, 0, str.sphere.w));
                cube.frustumPlanes[5] = GetPlane(new float3(0, 0, -1), camPos + new float3(0, 0, -str.sphere.w));
            }
        }
    }
}