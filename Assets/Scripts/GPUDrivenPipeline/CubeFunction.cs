using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
namespace MPipeline
{
    public unsafe struct CubeCullingBuffer
    {
        public const int RunFrustumCull = 0;
        public const int Clear = 1;
        public ComputeBuffer indirectDrawBuffer;
        public RenderTexture renderTarget;
        public CubemapViewProjMatrix* vpMatrices;
        private Vector4[] planes;
        private ComputeShader shader;
        public void Init(ComputeShader shader)
        {
            this.shader = shader;
            renderTarget = null;
            vpMatrices = null;
            planes = new Vector4[6];
            indirectDrawBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            NativeArray<int> ind = new NativeArray<int>(5, Allocator.Temp);
            ind[0] = PipelineBaseBuffer.CLUSTERVERTEXCOUNT;
            indirectDrawBuffer.SetData(ind);
            ind.Dispose();
        }
        public void Dispose()
        { 
           indirectDrawBuffer.Dispose();
        }

        public void StartCull(PipelineBaseBuffer baseBuffer, CommandBuffer cb, float4* planesNative)
        {
            UnsafeUtility.MemCpy(planes.Ptr(), planesNative, sizeof(Vector4) * 6);
            cb.SetComputeBufferParam(shader, RunFrustumCull, ShaderIDs.instanceCountBuffer, indirectDrawBuffer);
            cb.SetComputeBufferParam(shader, RunFrustumCull, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            cb.SetComputeBufferParam(shader, RunFrustumCull, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            cb.SetComputeVectorArrayParam(shader, ShaderIDs.planes, planes);
            cb.SetComputeBufferParam(shader, Clear, ShaderIDs.instanceCountBuffer, indirectDrawBuffer);
            cb.DispatchCompute(shader, Clear, 1, 1, 1);
            ComputeShaderUtility.Dispatch(shader, cb, RunFrustumCull, baseBuffer.clusterCount, 64);
        }
    }

    public unsafe struct CubemapViewProjMatrix
    {
        public Matrix4x4 forwardView;
        public Matrix4x4 backView;
        public Matrix4x4 upView;
        public Matrix4x4 downView;
        public Matrix4x4 rightView;
        public Matrix4x4 leftView;
        public Matrix4x4 projMat;
        [NativeDisableUnsafePtrRestriction]
        public float4* frustumPlanes;
    }
}