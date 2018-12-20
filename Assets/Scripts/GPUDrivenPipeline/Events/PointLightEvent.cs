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
            cbdr = PipelineSharedData.Get(renderPath, resources, (res) => new CBDRSharedData(res, 1024));
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
        public RenderTexture cubeArray;
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            PipelineBaseBuffer baseBuffer;
            if (cubeArray != null)
            {
                RenderTexture.ReleaseTemporary(cubeArray);
                cubeArray = null;
            }
            if (!SceneController.GetBaseBuffer(out baseBuffer)) return;
            CommandBuffer buffer = data.buffer;
            cullJobHandler.Complete();
            UnsafeUtility.ReleaseGCObject(gcHandler);
            pointLightMaterial.SetBuffer(ShaderIDs.verticesBuffer, sphereBuffer);
            //Un Shadow Point light
            if (lightCount > 0)
            {
                if(shadowList.Length > 0)
                {
                   cubeArray = RenderTexture.GetTemporary(new RenderTextureDescriptor
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

                }
                buffer.SetGlobalTexture("_CubeShadowMapArray", cubeArray);
                cbdr.SetDatas(cam.cam, indicesArray, lightCount, buffer);
                buffer.BlitSRT(cam.targets.renderTargetIdentifier, pointLightMaterial, 2);
            }
            //Shadow Point Light
            /*
            if (shadowCount > 0)
            {
                
                
                
                for (int i = 0; i < shadowCount; i++)
                {
                    MPointLight light = MPointLight.allPointLights[cullJob.indices[i]];
                    
                    buffer.SetRenderTarget(cam.targets.renderTargetIdentifier, cam.targets.depthIdentifier);
                    buffer.SetGlobalVector(ShaderIDs._LightColor, light.color);
                    buffer.SetGlobalVector(ShaderIDs._LightPos, positions[i]);
                    buffer.SetGlobalFloat(ShaderIDs._LightIntensity, light.intensity);
                    buffer.SetGlobalTexture(ShaderIDs._CubeShadowMap, light.shadowmapTexture);
                    buffer.DrawProcedural(Matrix4x4.identity, pointLightMaterial, 1, MeshTopology.Triangles, sphereBuffer.count);
                }
                positions.Dispose();
            }*/
            indicesArray.Dispose();
            data.ExecuteCommandBuffer();
        }

        protected override void Dispose()
        {
            shadowList.Dispose();
            Destroy(pointLightMaterial);
            Destroy(cubeDepthMaterial);
            sphereBuffer.Dispose();
            CubeFunction.Dispose(ref cubeBuffer);
            cbdr.Dispose();
            PipelineSharedData.Remove<CBDRSharedData>(renderPath);
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