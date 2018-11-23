using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
using UnityEngine;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
namespace MPipeline
{
    public struct SkinPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector2 uv;
        public Vector4 boneWeight;
        public Vector4Int boneIndex;
    };
    [PipelineEvent(true, true)]
    public unsafe class AnimationTestEvent : PipelineEvent
    {
        public SkinnedMeshRenderer skinMesh;
        public Material mat;
        private ComputeBuffer verticesBuffer;
        private ComputeBuffer resultBuffer;
        private ComputeBuffer boneBuffer;
        private JobHandle handle;
        private TransformAccessArray bonesArray;
        private NativeArray<Matrix3x4> boneMatrices;
        private Matrix4x4[] bindPoses;
        protected override void Init(PipelineResources resources)
        {
            Transform[] bones = skinMesh.bones;
            bonesArray = new TransformAccessArray(bones);
            Mesh mesh = skinMesh.sharedMesh;
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector4[] tangents = mesh.tangents;
            Vector3[] normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            bindPoses = mesh.bindposes;
            BoneWeight[] weights = mesh.boneWeights;
            boneBuffer = new ComputeBuffer(bones.Length, sizeof(Matrix3x4));
            NativeArray<SkinPoint> allSkinPoints = new NativeArray<SkinPoint>(triangles.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            SkinPoint* pointsPtr = allSkinPoints.Ptr();
            for (int i = 0; i < triangles.Length; ++i)
            {
                SkinPoint* currentPtr = pointsPtr + i;
                int index = triangles[i];
                currentPtr->position = vertices[index];
                currentPtr->tangent = tangents[index];
                currentPtr->normal = normals[index];
                ref BoneWeight bone = ref weights[index];
                currentPtr->boneWeight = new Vector4(bone.weight0, bone.weight1, bone.weight2, bone.weight3);
                currentPtr->boneIndex = new Vector4Int(bone.boneIndex0, bone.boneIndex1, bone.boneIndex2, bone.boneIndex3);
                currentPtr->uv = uv[index];
            }
            verticesBuffer = new ComputeBuffer(allSkinPoints.Length, sizeof(SkinPoint));
            resultBuffer = new ComputeBuffer(allSkinPoints.Length, sizeof(Point));
            verticesBuffer.SetData(allSkinPoints);
            allSkinPoints.Dispose();
        }

        protected override void Dispose()
        {
            bonesArray.Dispose();
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            boneMatrices = new NativeArray<Matrix3x4>(bonesArray.length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            GetBoneJob boneJob = new GetBoneJob
            {
                matrices = boneMatrices.Ptr(),
                bindPoses = (Matrix4x4*)UnsafeUtility.AddressOf(ref bindPoses[0])
            };
            handle = boneJob.Schedule(bonesArray);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            handle.Complete();
            boneBuffer.SetData(boneMatrices);
            boneMatrices.Dispose();
            CommandBuffer buffer = data.buffer;
            ComputeShader cs = data.resources.gpuSkin;
            buffer.SetComputeBufferParam(cs, 0, ShaderIDs.resultBuffer, resultBuffer);
            buffer.SetComputeBufferParam(cs, 0, ShaderIDs.verticesBuffer, verticesBuffer);
            buffer.SetComputeBufferParam(cs, 0, ShaderIDs.boneBuffer, boneBuffer);
            ComputeShaderUtility.Dispatch(cs, buffer, 0, verticesBuffer.count, 256);
            buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, resultBuffer);
            buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthIdentifier);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, resultBuffer.count);
            PipelineFunctions.ExecuteCommandBuffer(ref data);
        }
    }
    public unsafe struct GetBoneJob : IJobParallelForTransform
    {
        [NativeDisableUnsafePtrRestriction]
        public Matrix3x4* matrices;
        [NativeDisableUnsafePtrRestriction]
        public Matrix4x4* bindPoses;
        public void Execute(int index, TransformAccess transform)
        {
            matrices[index] = new Matrix3x4(Matrix4x4.TRS(transform.position, transform.rotation,transform.localScale) * bindPoses[index]);
        }
    }
}