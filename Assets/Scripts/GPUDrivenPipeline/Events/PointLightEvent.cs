using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using System;

namespace MPipeline
{
    [PipelineEvent(true, true)]
    public unsafe class PointLightEvent : PipelineEvent
    {
        private ulong gcHandler;
        private MPointLightEvent cullJob;
        private JobHandle cullJobHandler;
        private Material pointLightMaterial;
        private Material cubeDepthMaterial;
        private ComputeBuffer sphereBuffer;
        private NativeArray<PointLightStruct> indicesArray;
        private CubeCullingBuffer cubeBuffer;
        private int lightCount = 0;
        private CBDRSharedData cbdr;
        private NativeList<int> shadowList;
        protected override void Init(PipelineResources resources)
        {
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
            cbdr = PipelineSharedData.Get(renderPath, resources, (res) => new CBDRSharedData(res));
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            cullJob.planes = (Vector4*)UnsafeUtility.PinGCArrayAndGetDataAddress(data.arrayCollection.frustumPlanes, out gcHandler);
            indicesArray = new NativeArray<PointLightStruct>(MPointLight.allPointLights.Count, Allocator.Temp);
            cullJob.indices = indicesArray.Ptr();
            cullJob.lightCount = (int*)UnsafeUtility.AddressOf(ref lightCount);
            shadowList.Clear();
            cullJob.shadowList = shadowList;
            lightCount = 0;
            cullJobHandler = cullJob.Schedule(MPointLight.allPointLights.Count, 32);
        }
        static readonly int _CubeShadowMapArray = Shader.PropertyToID("_CubeShadowMapArray");
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            PipelineBaseBuffer baseBuffer;
            if (!SceneController.GetBaseBuffer(out baseBuffer)) return;
            CommandBuffer buffer = data.buffer;
            cullJobHandler.Complete();
            UnsafeUtility.ReleaseGCObject(gcHandler);
            pointLightMaterial.SetBuffer(ShaderIDs.verticesBuffer, sphereBuffer);
            VoxelLightCommonData(buffer, cam.cam);
            if (lightCount > 0)
            {
                if (shadowList.Length > 0)
                {
                    RenderTexture cubeArray = RenderTexture.GetTemporary(new RenderTextureDescriptor
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
                    cam.temporalRT.Add(cubeArray);  //Let Pipeline dispose this
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
                    cubeBuffer.renderTarget = cubeArray;
                    for (int i = 0; i < shadowList.Length; ++i)
                    {
                        MPointLight light = MPointLight.allPointLights[shadowList[i]];
                        SceneController.current.DrawCubeMap(light, cubeDepthMaterial, ref opts, ref cubeBuffer, i);
                    }
                    buffer.SetGlobalTexture(_CubeShadowMapArray, cubeArray);
                }
                VoxelPointLight(indicesArray, lightCount, buffer);
                buffer.BlitSRT(cam.targets.renderTargetIdentifier, pointLightMaterial, 2);
                cbdr.pointLightEnabled = true;
            }else
                cbdr.pointLightEnabled = false;
            indicesArray.Dispose();
            data.ExecuteCommandBuffer();
        }

        private void VoxelLightCommonData(CommandBuffer buffer, Camera cam)
        {
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.SetXYPlaneKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.SetZPlaneKernel, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            Transform camTrans = cam.transform;
            buffer.SetGlobalVector(ShaderIDs._CameraFarPos, camTrans.position + cam.farClipPlane * camTrans.forward);
            buffer.SetGlobalVector(ShaderIDs._CameraNearPos, camTrans.position + cam.nearClipPlane * camTrans.forward);
            buffer.SetGlobalVector(ShaderIDs._CameraClipDistance, new Vector4(cam.nearClipPlane, cam.farClipPlane - cam.nearClipPlane));
            buffer.SetGlobalVector(ShaderIDs._CameraForward, camTrans.forward);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetXYPlaneKernel, 1, 1, 1);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetZPlaneKernel, 1, 1, 1);
        }

        public void VoxelPointLight(NativeArray<PointLightStruct> arr, int length, CommandBuffer buffer)
        {
            CBDRSharedData.ResizeBuffer(cbdr.allPointLightBuffer, length);
            ComputeShader cbdrShader = cbdr.cbdrShader;
            const int PointLightKernel = CBDRSharedData.PointLightKernel;
            cbdr.allPointLightBuffer.SetData(arr, 0, 0, length);
            buffer.SetGlobalInt(ShaderIDs._PointLightCount, length);
            buffer.SetComputeTextureParam(cbdrShader, PointLightKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, PointLightKernel, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, PointLightKernel, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(cbdrShader, PointLightKernel, ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._PointLightIndexBuffer, cbdr.pointlightIndexBuffer);
            buffer.DispatchCompute(cbdrShader, PointLightKernel, 1, 1, CBDRSharedData.ZRES);
        }

        protected override void Dispose()
        {
            shadowList.Dispose();
            Destroy(pointLightMaterial);
            Destroy(cubeDepthMaterial);
            sphereBuffer.Dispose();
            CubeFunction.Dispose(ref cubeBuffer);
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