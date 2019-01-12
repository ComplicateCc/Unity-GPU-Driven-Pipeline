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
        private ulong gcHandler;
        private Material pointLightMaterial;
        private Material cubeDepthMaterial;
        private ComputeBuffer sphereBuffer;
        private RenderSpotShadowCommand spotBuffer;
        private int pointLightCount = 0;
        private int spotLightCount = 0;
        private NativeList<int2> pointLightIndices;//int2.x: shadow index  int2.y: all visible light index
        private NativeList<int2> spotLightIndices;
        private NativeArray<CubemapViewProjMatrix> cubemapVPMatrices;
        private NativeArray<PointLightStruct> pointLightArray;
        private NativeArray<SpotLight> spotLightArray;
        public NativeArray<SpotLightMatrix> spotLightMatrices;
        private JobHandle vpMatricesJobHandle;
        private JobHandle spotLightJobHandle;
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
            List<VisibleLight> allLight = data.cullResults.visibleLights;
            pointLightArray = new NativeArray<PointLightStruct>(allLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            spotLightArray = new NativeArray<SpotLight>(allLight.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            pointLightIndices = new NativeList<int2>(allLight.Count, Allocator.Temp);
            spotLightIndices = new NativeList<int2>(allLight.Count, Allocator.Temp);
            PointLightStruct* indStr = pointLightArray.Ptr();
            SpotLight* spotStr = spotLightArray.Ptr();
            pointLightCount = 0;
            spotLightCount = 0;
            for (int index = 0; index < allLight.Count; ++index)
            {
                VisibleLight i = allLight[index];
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
                        if (lit.shadows != LightShadows.None && pointLightIndices.Length < CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT)
                        {
                            currentPtr->shadowIndex = pointLightIndices.Length;
                            pointLightIndices.Add(new int2(pointLightCount, index));
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
                        if (lit.shadows != LightShadows.None && spotLightIndices.Length < CBDRSharedData.MAXIMUMPOINTLIGHTCOUNT)
                        {
                            currentSpot->shadowIndex = spotLightIndices.Length;
                            currentSpot->vpMatrix = i.localToWorld;
                            spotLightIndices.Add(new int2(spotLightCount, index));
                        }
                        else
                        {
                            currentSpot->shadowIndex = -1;
                        }
                        spotLightCount++;
                        break;
                }
            }
            cubemapVPMatrices = new NativeArray<CubemapViewProjMatrix>(pointLightIndices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            vpMatricesJobHandle = new GetCubeMapMatrix
            {
                allLights = pointLightArray.Ptr(),
                allMatrix = cubemapVPMatrices.Ptr(),
                shadowIndex = pointLightIndices.unsafePtr
            }.Schedule(cubemapVPMatrices.Length, 1);
            spotLightMatrices = new NativeArray<SpotLightMatrix>(spotLightIndices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            spotLightJobHandle = new GetPerspMatrix
            {
                allLights = spotLightArray.Ptr(),
                projectionMatrices = spotLightMatrices.Ptr(),
                shadowIndex = spotLightIndices.unsafePtr
            }.Schedule(spotLightMatrices.Length, 1);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            DirLight(cam, ref data);
            PointLight(cam, ref data);
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
                SceneController.current.DrawDirectionalShadow(cam.cam, ref data, ref opts, SunLight.current, cascadeShadowMapVP);
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
            UnsafeUtility.ReleaseGCObject(gcHandler);
            pointLightMaterial.SetBuffer(ShaderIDs.verticesBuffer, sphereBuffer);
            VoxelLightCommonData(buffer, cam.cam);
            ClearDispatch(buffer);
            if (pointLightCount > 0)
            {
                if (pointLightIndices.Length > 0)
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
                    vpMatricesJobHandle.Complete();
                    List<VisibleLight> allLights = data.cullResults.visibleLights;
                    PointLightStruct* pointLightPtr = pointLightArray.Ptr();
                    for (int i = 0; i < pointLightIndices.Length; ++i)
                    {
                        int2 lightIndex = pointLightIndices[i];
                        Light lt = allLights[lightIndex.y].light;
                        MLight light = MLight.GetPointLight(lt);
                        if (light.updateShadowmap)
                        {
                            light.UpdateShadowCacheType(true);
                            SceneController.current.DrawCubeMap(light, ref pointLightPtr[lightIndex.x], cubeDepthMaterial, ref opts, i, light.shadowMap, ref data, cubemapVPMatrices.Ptr(), cbdr.cubeArrayMap);
                        }
                        else
                        {
                            PipelineFunctions.CopyToCubeMap(cbdr.cubeArrayMap, light.shadowMap, buffer, i);
                        }

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
                if (spotLightIndices.Length > 0)
                {
                    RenderClusterOptions opts = new RenderClusterOptions
                    {
                        cullingShader = data.resources.gpuFrustumCulling,
                        command = buffer,
                        frustumPlanes = null,
                        isOrtho = false,
                    };
                    spotLightJobHandle.Complete();
                    SpotLight* allSpotLightPtr = spotLightArray.Ptr();
                    buffer.SetGlobalTexture(ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
                    spotBuffer.renderTarget = cbdr.spotArrayMap;
                    spotBuffer.shadowMatrices = spotLightMatrices.Ptr();
                    List<VisibleLight> allLights = data.cullResults.visibleLights;
                    for (int i = 0; i < spotLightIndices.Length; ++i)
                    {
                        int2 index = spotLightIndices[i];
                        MLight mlight = MLight.GetPointLight(allLights[spotLightIndices[i].y].light);
                        mlight.UpdateShadowCacheType(false);
                        ref SpotLight spot = ref allSpotLightPtr[index.x];
                        if (mlight.updateShadowmap)
                        {
                            SceneController.current.DrawSpotLight(ref opts, ref data, mlight.shadowCam, ref spot, ref spotBuffer);
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
                float4 plane = new float4(normal, -dot(normal, inPoint));
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
            public int2* shadowIndex;
            [NativeDisableUnsafePtrRestriction]
            public CubemapViewProjMatrix* allMatrix;
            float4 GetPlane(float3 normal, float3 inPoint)
            {
                return new float4(normal, -math.dot(normal, inPoint));
            }
            public void Execute(int index)
            {
                PointLightStruct str = allLights[shadowIndex[index].x];
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
                NativeArray<float4> vec = new NativeArray<float4>(36, Allocator.Temp);
                cube.frustumPlanes = vec.Ptr();
                float3 camPos = cam.position;
                float3* allEdgePos = stackalloc float3[]
                {
                    camPos + float3(1, 1, -1) * str.sphere.w,    //Right, up, back
                    camPos + float3(1, 1, 1) * str.sphere.w,     //Right , up, forward
                    camPos + float3(1, -1, -1) * str.sphere.w,   //right, down, back
                    camPos + float3(1, -1, 1) * str.sphere.w,    //Right, down, forward
                    camPos + float3(-1, 1, -1) * str.sphere.w,   //Left, up, back
                    camPos + float3(-1, 1, 1) * str.sphere.w,    //Left, up, forward
                    camPos + float3(-1, -1, -1) * str.sphere.w,  //Left, down, back
                    camPos + float3(-1, -1, 1) * str.sphere.w   //Left, down, forward
                };
                void GetPlanes(int4 indices, float4* ptr, float3 dir)
                {
                    ptr[0] = VectorUtility.GetPlane(allEdgePos[indices.x], allEdgePos[indices.y], camPos);
                    ptr[1] = VectorUtility.GetPlane(allEdgePos[indices.y], allEdgePos[indices.z], camPos);
                    ptr[2] = VectorUtility.GetPlane(allEdgePos[indices.z], allEdgePos[indices.w], camPos);
                    ptr[3] = VectorUtility.GetPlane(allEdgePos[indices.w], allEdgePos[indices.x], camPos);
                    ptr[4] = float4(-dir, dot(camPos, dir));
                    ptr[5] = float4(dir, -dot(camPos + dir * str.sphere.w, dir));
                }
                GetPlanes(new int4(7, 5, 0, 2), cube.frustumPlanes, float3(0, 0, 1));
                GetPlanes(new int4(2, 0, 4, 6), cube.frustumPlanes + 6, float3(0, 0, -1));
                GetPlanes(new int4(5, 4, 0, 1), cube.frustumPlanes + 12, float3(0, 1, 0));
                GetPlanes(new int4(6, 7, 3, 2), cube.frustumPlanes + 18, float3(0, -1, 0));
                GetPlanes(new int4(3, 1, 0, 2), cube.frustumPlanes + 24, float3(1, 0, 0));
                GetPlanes(new int4(6, 4, 5, 7), cube.frustumPlanes + 30, float3(-1, 0, 0));
            }
        }
        public unsafe struct GetPerspMatrix : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public SpotLight* allLights;
            [NativeDisableUnsafePtrRestriction]
            public SpotLightMatrix* projectionMatrices;
            [NativeDisableUnsafePtrRestriction]
            public int2* shadowIndex;
            public void Execute(int index)
            {
                ref SpotLight lit = ref allLights[shadowIndex[index].x];
                ref SpotLightMatrix matrices = ref projectionMatrices[index];
                PerspCam cam = new PerspCam
                {
                    aspect = 1,
                    farClipPlane = lit.lightCone.height,
                    fov = lit.angle * 2 * Mathf.Rad2Deg,
                    nearClipPlane = 0.3f
                };
                cam.UpdateViewMatrix(lit.vpMatrix);
                cam.UpdateProjectionMatrix();
                matrices.projectionMatrix = cam.projectionMatrix;
                matrices.worldToCamera = cam.worldToCameraMatrix;
            }
        }
    }
}