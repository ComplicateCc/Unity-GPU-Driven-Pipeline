using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [PipelineEvent(true, true)]
    public unsafe class ReflectionEvent : PipelineEvent
    {
        private static readonly int _ReflectionProbe = Shader.PropertyToID("_ReflectionProbe");
        private Material reflectMaterial;
        private ulong gcHandler;
        private ReflectionCullJob cullJob;
        private JobHandle cullJobHandler;
        private MaterialPropertyBlock block;

        protected override void Init(PipelineResources resources)
        {
            reflectMaterial = new Material(resources.reflectionShader);
            block = new MaterialPropertyBlock();
        }

        protected override void Dispose()
        {
            Destroy(reflectMaterial);
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            cullJob.planes = (Vector4*)UnsafeUtility.PinGCArrayAndGetDataAddress(data.frustumPlanes, out gcHandler);
            cullJob.resultIndices = new NativeList<int>(ReflectionCube.allCubes.Count, Allocator.Temp);
            cullJobHandler = cullJob.Schedule(ReflectionCube.allCubes.Count, 32);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            buffer.SetRenderTarget(cam.targets.renderTargetIdentifier, cam.targets.depthIdentifier);
            cullJobHandler.Complete();
            UnsafeUtility.ReleaseGCObject(gcHandler);
            //Use Result Indices
            foreach(int i in cullJob.resultIndices)
            {
                ReflectionCube cube = ReflectionCube.allCubes[i];
                block.SetTexture(_ReflectionProbe, cube.reflectionCube);
                buffer.DrawMesh(GraphicsUtility.cubeMesh, cube.localToWorld, reflectMaterial, 0, 0, block);
            }
            //TODO
            cullJob.resultIndices.Dispose();
            data.ExecuteCommandBuffer();
        }
    }

    public unsafe struct ReflectionCullJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction]
        public Vector4* planes;
        public NativeList<int> resultIndices;
        public void Execute(int index)
        {
            ReflectionCube cube = ReflectionCube.allCubes[index];
            if (PipelineFunctions.FrustumCulling(ref cube.localToWorld, new Vector3(0.5f, 0.5f, 0.5f), planes))
            {
#if UNITY_EDITOR        //For debugging
                if (!resultIndices.ConcurrentAdd(index))
                {
                    Debug.LogError("Reflection culling Out of range");
                }
#else
                resultIndices.ConcurrentAdd(index);
#endif
            }
        }
    }
}