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
        private int shadowCount = 0;
        private int unShadowCount = 0;
        private CBDRSharedData cbdr;
        protected override void Init(PipelineResources resources)
        {
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
            cullJob.shadowCount = (int*)UnsafeUtility.AddressOf(ref shadowCount);
            cullJob.unShadowCount = (int*)UnsafeUtility.AddressOf(ref unShadowCount);
            cullJob.length = indicesArray.Length - 1;
            shadowCount = 0;
            unShadowCount = 0;
            cullJobHandler = cullJob.Schedule(MPointLight.allPointLights.Count, 32);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            PipelineBaseBuffer baseBuffer;
            if (!SceneController.GetBaseBuffer(out baseBuffer)) return;
            CommandBuffer buffer = data.buffer;
            cullJobHandler.Complete();
            UnsafeUtility.ReleaseGCObject(gcHandler);
            pointLightMaterial.SetBuffer(ShaderIDs.verticesBuffer, sphereBuffer);
            //Un Shadow Point light
            cbdr.SetDatas(cam.cam, indicesArray, unShadowCount, buffer);
            buffer.BlitSRT(cam.targets.renderTargetIdentifier, pointLightMaterial, 2);
            //Shadow Point Light
            /*
            if (shadowCount > 0)
            {
                NativeArray<Vector4> positions = new NativeArray<Vector4>(shadowCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < shadowCount; i++)
                {
                    MPointLight light = MPointLight.allPointLights[cullJob.indices[i]];
                    positions[i] = new Vector4(light.position.x, light.position.y, light.position.z, light.range);
                }
                CubeFunction.UpdateLength(ref cubeBuffer, shadowCount);
                var cullShader = data.resources.pointLightFrustumCulling;
                CubeFunction.UpdateData(ref cubeBuffer, baseBuffer, cullShader, buffer, positions);
                RenderClusterOptions opts = new RenderClusterOptions
                {
                    cullingShader = cullShader,
                    command = buffer,
                    frustumPlanes = null,
                    isOrtho = false
                };
                for (int i = 0; i < shadowCount; i++)
                {
                    MPointLight light = MPointLight.allPointLights[cullJob.indices[i]];
                    SceneController.current.DrawCubeMap(light, cubeDepthMaterial, ref opts, ref cubeBuffer, i);
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
        public int* shadowCount;
        [NativeDisableUnsafePtrRestriction]
        public int* unShadowCount;
        public int length;
        public void Execute(int index)
        {
            MPointLight cube = MPointLight.allPointLights[index];
            if (PipelineFunctions.FrustumCulling(cube.position, cube.range, planes))
            {
                /*  if (cube.useShadow)
                  {
                      int last = Interlocked.Increment(ref *shadowCount) - 1;
                      indices[last] = index;
                  }
                  else
                  {
                      int last = Interlocked.Increment(ref *unShadowCount) - 1;
                      indices[length - last] = index;
                  }
                  */
                  
                if(!cube.useShadow)
                {
                    int last = Interlocked.Increment(ref *unShadowCount) - 1;
                    PointLightStruct* crt = indices + last;
                    crt->lightColor = new Vector3(cube.color.r, cube.color.g, cube.color.b);
                    crt->lightIntensity = cube.intensity;
                    crt->sphere = new Vector4(cube.position.x, cube.position.y, cube.position.z, cube.range);
                }
            }
        }
    }

}