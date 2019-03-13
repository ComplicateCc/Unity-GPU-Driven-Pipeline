using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe class OccluderDrawer : MonoBehaviour
    {
        public static OccluderDrawer current { get; private set; }
        public Mesh occluderMesh;
        public ComputeShader shader;
        public const int cull_Kernel = 0;
        public const int clear_Kernel = 1;
        public List<Transform> allRenderers = new List<Transform>();
        private ComputeBuffer clusterBuffer;
        private ComputeBuffer verticesBuffer;
        private ComputeBuffer instanceCountBuffer;
        private ComputeBuffer resultBuffer;
        void Awake()
        {
            current = this;
            clusterBuffer = new ComputeBuffer(allRenderers.Count, sizeof(float3x4));
            NativeArray<float3x4> clusterArray = new NativeArray<float3x4>(allRenderers.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for(int i = 0; i < clusterArray.Length; ++i)
            {
                Matrix4x4 mat = allRenderers[i].localToWorldMatrix;
                clusterArray[i] = float3x4((Vector3)mat.GetColumn(0), (Vector3)mat.GetColumn(1), (Vector3)mat.GetColumn(2), (Vector3)mat.GetColumn(3));
            }
            clusterBuffer.SetData(clusterArray);
            clusterArray.Dispose();
            resultBuffer = new ComputeBuffer(allRenderers.Count, sizeof(int));
            instanceCountBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            Vector3[] vertices = occluderMesh.vertices;
            int[] tris = occluderMesh.triangles;
            verticesBuffer = new ComputeBuffer(tris.Length, sizeof(float3));
            NativeArray<float3> verts = new NativeArray<float3>(tris.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for(int i = 0; i < verts.Length; ++i)
            {
                verts[i] = vertices[tris[i]];
            }
            NativeArray<uint> args = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            args[0] = (uint)verts.Length;
            instanceCountBuffer.SetData(args);
            verticesBuffer.SetData(verts);
            args.Dispose();
            verts.Dispose();
        }

        private void OnDestroy()
        {
            current = null;
        }

        public void Drawer(CommandBuffer cb, Material mat, Vector4[] frustumPlanes)
        {
            cb.SetComputeVectorArrayParam(shader, ShaderIDs.planes, frustumPlanes);
            cb.SetComputeBufferParam(shader, cull_Kernel, ShaderIDs.instanceCountBuffer, instanceCountBuffer);
            cb.SetComputeBufferParam(shader, clear_Kernel, ShaderIDs.instanceCountBuffer, instanceCountBuffer);
            cb.SetComputeBufferParam(shader, cull_Kernel, ShaderIDs.clusterBuffer, clusterBuffer);
            cb.SetComputeBufferParam(shader, cull_Kernel, ShaderIDs.resultBuffer, resultBuffer);
            cb.DispatchCompute(shader, clear_Kernel, 1, 1, 1);
            ComputeShaderUtility.Dispatch(shader, cb, cull_Kernel, clusterBuffer.count, 64);
            cb.SetGlobalBuffer(ShaderIDs.clusterBuffer, clusterBuffer);
            cb.SetGlobalBuffer(ShaderIDs.resultBuffer, resultBuffer);
            cb.SetGlobalBuffer(ShaderIDs.verticesBuffer, verticesBuffer);
            cb.DrawProceduralIndirect(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, instanceCountBuffer);
        }
    }
}