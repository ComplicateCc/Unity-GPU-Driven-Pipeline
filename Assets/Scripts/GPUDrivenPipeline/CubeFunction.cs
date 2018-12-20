using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
namespace MPipeline
{
    public unsafe static class CubeFunction
    {
        const int initLength = 10;
        public const int GetFrustumPlane = 0;
        public const int RunFrustumCull = 1;
        public const int ClearCluster = 2;
        public const float spreadLengthRate = 1.2f;
        public static void Init(ref CubeCullingBuffer buffer)
        {
            buffer.currentLength = initLength;
            buffer.planes = new ComputeBuffer(initLength * 6, sizeof(Vector4));
            buffer.lightPositionBuffer = new ComputeBuffer(initLength, sizeof(Vector4));
            buffer.indirectDrawBuffer = new ComputeBuffer(initLength * 5, sizeof(int), ComputeBufferType.IndirectArguments);
        }

        public static void UpdateLength(ref CubeCullingBuffer buffer, int targetLength)
        {
            if (targetLength <= buffer.currentLength) return;
            buffer.currentLength = (int)(buffer.currentLength * spreadLengthRate);
            buffer.currentLength = Mathf.Max(buffer.currentLength, targetLength);
            buffer.indirectDrawBuffer.Dispose();
            buffer.planes.Dispose();
            buffer.lightPositionBuffer.Dispose();
            buffer.planes = new ComputeBuffer(buffer.currentLength * 6, sizeof(Vector4));
            buffer.lightPositionBuffer = new ComputeBuffer(buffer.currentLength, sizeof(Vector4));
            buffer.indirectDrawBuffer = new ComputeBuffer(buffer.currentLength * 5, sizeof(int), ComputeBufferType.IndirectArguments);
        }

        public static void UpdateData(ref CubeCullingBuffer buffer, PipelineBaseBuffer baseBuffer, ComputeShader shader, CommandBuffer cb, NativeArray<Vector4> positions)
        {
            cb.SetComputeBufferParam(shader, ClearCluster, ShaderIDs.instanceCountBuffer, buffer.indirectDrawBuffer);
            cb.SetComputeBufferParam(shader, RunFrustumCull, ShaderIDs.instanceCountBuffer, buffer.indirectDrawBuffer);
            cb.SetComputeBufferParam(shader, RunFrustumCull, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
            cb.SetComputeBufferParam(shader, RunFrustumCull, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            cb.SetComputeBufferParam(shader, RunFrustumCull, ShaderIDs.planes, buffer.planes);
            cb.SetComputeBufferParam(shader, GetFrustumPlane, ShaderIDs.planes, buffer.planes);
            cb.SetComputeBufferParam(shader, GetFrustumPlane, ShaderIDs.lightPositionBuffer, buffer.lightPositionBuffer);
            int targetLength = positions.Length;
            buffer.lightPositionBuffer.SetData(positions);
            ComputeShaderUtility.Dispatch(shader, cb, ClearCluster, targetLength, 64);
            ComputeShaderUtility.Dispatch(shader, cb, GetFrustumPlane, targetLength, 16);
        }
       
        public static void Dispose(ref CubeCullingBuffer buffer)
        {
            buffer.indirectDrawBuffer.Dispose();
            buffer.planes.Dispose();
            buffer.lightPositionBuffer.Dispose();
        }
    }

    public struct CubeCullingBuffer
    {
        public ComputeBuffer planes;
        public ComputeBuffer lightPositionBuffer;
        public ComputeBuffer indirectDrawBuffer;
        public RenderTexture renderTarget;
        public int currentLength;
    }
}