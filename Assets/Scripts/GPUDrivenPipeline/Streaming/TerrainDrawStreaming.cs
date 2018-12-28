using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace MPipeline
{

    public unsafe class TerrainDrawingPolicy
    {
        private ComputeBuffer meshBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer indirectBuffer;
        private void InitMeshTriangleBuffer(int2 meshSize)
        {
            int2 vertexCount = meshSize + 1;
            meshBuffer = new ComputeBuffer(vertexCount.x * vertexCount.y, sizeof(TerrainVertex));
            NativeArray<TerrainVertex> terrainVertexArray = new NativeArray<TerrainVertex>(meshBuffer.count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            TerrainVertex* arrPtr = terrainVertexArray.Ptr();
            for (int x = 0; x < vertexCount.x; ++x)
            {
                for (int y = 0; y < vertexCount.y; ++y)
                {
                    arrPtr[y * vertexCount.x + x] = new TerrainVertex
                    {
                        uv = new float2(x, y) / vertexCount,     //TODO: UV should be controlled by tools
                        localPos = new float2(x, y) / vertexCount - new float2(0.5f, 0.5f)
                    };
                }
            }
            meshBuffer.SetData(terrainVertexArray);
            terrainVertexArray.Dispose();
            triangleBuffer = new ComputeBuffer(6 * meshSize.x * meshSize.y, sizeof(int));
            NativeArray<int> triangles = new NativeArray<int>(triangleBuffer.count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int* trianglePtr = triangles.Ptr();
            for (int x = 0, count = 0; x < meshSize.x; ++x)
            {
                for (int y = 0; y < meshSize.y; ++y)
                {
                    int4 indices = new int4(vertexCount.x * y + x, vertexCount.x * (y + 1) + x, vertexCount.x * y + (x + 1), vertexCount.x * (y + 1) + (x + 1));
                    trianglePtr[count] = indices.x;
                    trianglePtr[count + 1] = indices.y;
                    trianglePtr[count + 2] = indices.z;
                    trianglePtr[count + 3] = indices.y;
                    trianglePtr[count + 4] = indices.w;
                    trianglePtr[count + 5] = indices.z;
                    count += 6;
                }
            }
            triangleBuffer.SetData(triangles);
            triangles.Dispose();
        }
    }

    public struct TerrainVertex
    {
        public float2 uv;
        public float2 localPos;
    }

    public struct TerrainPanel
    {
        public float2 position;
        public float size;
    }

    public unsafe struct TerrainQuadTree
    {
        public struct QuadTreeNode
        {
            public QuadTreeNode* leftUp;
            public QuadTreeNode* leftDown;
            public QuadTreeNode* rightUp;
            public QuadTreeNode* rightDown;
            public int listPosition;
        }
        public NativeList<QuadTreeNode> originTrees;
    }
}
