using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public unsafe class GSTestEvent : PipelineEvent
    {
        public Transform point;
        public Mesh cubeMesh;
        public ComputeBuffer _GrassPointsBuffer;
        public Texture2D posTex;
        public Texture2D normTex;
        public Material mat;
        private MaterialPropertyBlock block;
        protected override void Init(PipelineResources resources)
        {
            block = new MaterialPropertyBlock();
            GetDatasFromMesh(cubeMesh, out posTex, out normTex);
            _GrassPointsBuffer = new ComputeBuffer(1, sizeof(GrassPoint));
            NativeArray<GrassPoint> pt = new NativeArray<GrassPoint>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            GrassPoint* ptr = pt.Ptr();
            ptr->localToWorld = new Matrix3x4(point.localToWorldMatrix);
            ptr->replCoord = Vector2Int.zero;
            _GrassPointsBuffer.SetData(pt);
            pt.Dispose();
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            block.SetTexture("_GrassPosTexture", posTex);
            block.SetTexture("_GrassNormalTexture", normTex);
            block.SetBuffer("_GrassPointsBuffer", _GrassPointsBuffer);
            buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthIdentifier);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Points, 1, 1, block);
        }
        public static void GetDatasFromMesh(Mesh mesh, out Texture2D posTex, out Texture2D normTex)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            posTex = new Texture2D(1, triangles.Length, TextureFormat.RGBAFloat, false, true);
            normTex = new Texture2D(1, triangles.Length, TextureFormat.RGBAFloat, false, true);
            Color[] posColors = new Color[triangles.Length];
            Color[] normColors = new Color[triangles.Length];
            for(int i = 0; i < triangles.Length; ++i)
            {
                int index = triangles[i];
                ref Vector3 pos = ref vertices[index];
                ref Vector3 norm = ref normals[index];
                ref Vector2 uv = ref uvs[index];
                posColors[i] = new Color(pos.x, pos.y, pos.z, uv.x);
                normColors[i] = new Color(norm.x, norm.y, norm.z, uv.y);
            }
            posTex.SetPixels(posColors);
            normTex.SetPixels(normColors);
            posTex.Apply();
            normTex.Apply();
        }
    }

    public struct GrassPoint
    {
        public Matrix3x4 localToWorld;
        public Vector2Int replCoord;
    };
}