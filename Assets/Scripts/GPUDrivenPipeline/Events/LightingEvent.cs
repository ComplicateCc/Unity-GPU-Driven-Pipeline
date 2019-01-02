using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
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
        private MPointLightEvent cullJob;
        private JobHandle cullJobHandler;
        private Material pointLightMaterial;
        private Material cubeDepthMaterial;
        private ComputeBuffer sphereBuffer;
        private NativeArray<PointLightStruct> indicesArray;
        private CubeCullingBuffer cubeBuffer;
        private int lightCount = 0;
        private NativeList<int> shadowList;
        #endregion
        public Shader debugShader;
        private Material debugMat;
        protected override void Init(PipelineResources resources)
        {
            debugMat = new Material(debugShader);
            cbdr = PipelineSharedData.Get(renderPath, resources, (a) => new CBDRSharedData(a));
            shadMaskMaterial = new Material(resources.shadowMaskShader);
            for (int i = 0; i < cascadeShadowMapVP.Length; ++i)
            {
                cascadeShadowMapVP[i] = Matrix4x4.identity;
            }

            shadowList = new NativeList<int>(50, Allocator.Persistent);
            cubeBuffer = new CubeCullingBuffer();
            CubeFunction.Init(ref cubeBuffer);
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

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            lightCount = 0;
            cullJob.planes = (Vector4*)UnsafeUtility.PinGCArrayAndGetDataAddress(data.arrayCollection.frustumPlanes, out gcHandler);
            indicesArray = new NativeArray<PointLightStruct>(MPointLight.allPointLights.Count, Allocator.Temp);
            cullJob.indices = indicesArray.Ptr();
            cullJob.lightCount = (int*)UnsafeUtility.AddressOf(ref lightCount);
            shadowList.Clear();
            cullJob.shadowList = shadowList;
            cullJobHandler = cullJob.Schedule(MPointLight.allPointLights.Count, 32);
        }

        protected override void Dispose()
        {
            Destroy(shadMaskMaterial);
            shadowList.Dispose();
            Destroy(pointLightMaterial);
            Destroy(cubeDepthMaterial);
            sphereBuffer.Dispose();
            CubeFunction.Dispose(ref cubeBuffer);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            DirLight(cam, ref data);
            PointLight(cam, ref data);
            data.ExecuteCommandBuffer();
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
                SceneController.current.DrawDirectionalShadow(cam.cam, ref opts, ref SunLight.current.settings, ref SunLight.shadMap, cascadeShadowMapVP);
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
            cullJobHandler.Complete();
            UnsafeUtility.ReleaseGCObject(gcHandler);
            pointLightMaterial.SetBuffer(ShaderIDs.verticesBuffer, sphereBuffer);
            VoxelLightCommonData(buffer, cam.cam);
            if (lightCount > 0)
            {
                if (shadowList.Length > 0)
                {
                    RenderTexture shadowArray = RenderTexture.GetTemporary(new RenderTextureDescriptor
                    {
                        autoGenerateMips = false,
                        bindMS = false,
                        colorFormat = RenderTextureFormat.RHalf,
                        depthBufferBits = 16,
                        dimension = TextureDimension.CubeArray,
                        volumeDepth = shadowList.Length * 6,
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
                    buffer.SetGlobalTexture(ShaderIDs._CubeShadowMapArray, shadowArray);

                    cam.temporalRT.Add(shadowArray);
                    NativeArray<Vector4> positions = new NativeArray<Vector4>(shadowList.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < shadowList.Length; ++i)
                    {
                        MPointLight light = MPointLight.allPointLights[shadowList[i]];
                        positions[i] = new Vector4(light.position.x, light.position.y, light.position.z, light.range);
                    }

                    CubeFunction.UpdateLength(ref cubeBuffer, shadowList.Length);
                    var cullShader = data.resources.pointLightFrustumCulling;
                    CubeFunction.UpdateData(ref cubeBuffer, baseBuffer, cullShader, buffer, positions);
                    RenderClusterOptions opts = new RenderClusterOptions
                    {
                        cullingShader = cullShader,
                        command = buffer,
                        frustumPlanes = null,
                        isOrtho = false
                    };
                    cubeBuffer.renderTarget = shadowArray;
                    cbdr.cubemapShadowArray = shadowArray;
                    for (int i = 0; i < shadowList.Length; ++i)
                    {
                        MPointLight light = MPointLight.allPointLights[shadowList[i]];
                        SceneController.current.DrawCubeMap(light, cubeDepthMaterial, ref opts, ref cubeBuffer, i, light.shadowMap);
                        //TODO
                        //Multi frame shadowmap
                    }
                }
                VoxelLightCalculate(indicesArray, lightCount, buffer, cam.cam);
              //  buffer.BlitSRT(cbdr.tileLightList, cam.targets.renderTargetIdentifier, debugMat, 0);
                buffer.BlitSRT(cam.targets.renderTargetIdentifier, pointLightMaterial, 2);
                cbdr.lightFlag |= 1;
            }
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

        private void VoxelLightCalculate(NativeArray<PointLightStruct> arr, int length, CommandBuffer buffer, Camera cam)
        {
            CBDRSharedData.ResizeBuffer(ref cbdr.allPointLightBuffer, length);
            ComputeShader cbdrShader = cbdr.cbdrShader;
            int tbdrKernel = cbdr.TBDRKernel;
            int clearKernel = cbdr.ClearKernel;
            cbdr.allPointLightBuffer.SetData(arr, 0, 0, length);
            //TBDR

            buffer.SetComputeTextureParam(cbdrShader, tbdrKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, tbdrKernel, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeTextureParam(cbdrShader, tbdrKernel, ShaderIDs._TileLightList, cbdr.tileLightList);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._TileLightList, cbdr.tileLightList);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DeferredCBDR, ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.SetComputeTextureParam(cbdrShader, clearKernel, ShaderIDs._TileLightList, cbdr.tileLightList);
            if (cbdr.useFroxel)
            {
                Transform camTrans = cam.transform;
                float3 inPoint = camTrans.position + camTrans.forward * cbdr.availiableDistance;
                float3 normal = camTrans.forward;
                float4 plane = new float4(normal, -math.dot(normal, inPoint));
                buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._FroxelPlane, plane);
                buffer.SetComputeTextureParam(cbdrShader, tbdrKernel, ShaderIDs._FroxelTileLightList, cbdr.froxelTileLightList);
                buffer.SetComputeTextureParam(cbdrShader, clearKernel, ShaderIDs._FroxelTileLightList, cbdr.froxelTileLightList);
            }
            buffer.DispatchCompute(cbdrShader, clearKernel, 1, 1, 1);
            buffer.DispatchCompute(cbdrShader, tbdrKernel, 1, 1, length);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.DeferredCBDR, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);

        }

    }

    public unsafe struct MPointLightEvent : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction]
        public Vector4* planes;
        [NativeDisableUnsafePtrRestriction]
        public PointLightStruct* indices;
        [NativeDisableUnsafePtrRestriction]
        public int* lightCount;
        public NativeList<int> shadowList;
        public void Execute(int index)
        {
            MPointLight cube = MPointLight.allPointLights[index];
            if (PipelineFunctions.FrustumCulling(cube.position, cube.range, planes))
            {
                int last = Interlocked.Increment(ref *lightCount) - 1;
                PointLightStruct* crt = indices + last;
                crt->lightColor = new Vector3(cube.color.r, cube.color.g, cube.color.b);
                crt->lightIntensity = cube.intensity;
                crt->sphere = new Vector4(cube.position.x, cube.position.y, cube.position.z, cube.range);
                crt->shadowIndex = cube.useShadow ? shadowList.ConcurrentAdd(index, RenderPipeline.current) : -1;
            }
        }
    }
}