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

    public unsafe class TerrainDrawStreaming
    {
        public const int removeKernel = 0;
        public ComputeBuffer verticesBuffer { get; private set; }
        public ComputeBuffer clusterBuffer { get; private set; }
        public ComputeBuffer resultBuffer { get; private set; }
        public ComputeBuffer instanceCountBuffer { get; private set; }
        public ComputeBuffer removebuffer { get; private set; }
        public NativeList<ulong> referenceBuffer;
        private ComputeShader transformShader;
        public TerrainDrawStreaming(int2 meshSize, int maximumLength, ComputeShader transformShader)
        {
            //Initialize Mesh and triangles
            int2 vertexCount = meshSize + 1;
            NativeArray<TerrainVertex> terrainVertexArray = new NativeArray<TerrainVertex>(vertexCount.x * vertexCount.y, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            TerrainVertex* arrPtr = terrainVertexArray.Ptr();
            for (int x = 0; x < vertexCount.x; ++x)
            {
                for (int y = 0; y < vertexCount.y; ++y)
                {
                    arrPtr[y * vertexCount.x + x] = new TerrainVertex
                    {
                        uv = new float2(x, y) / vertexCount,     //TODO: UV should be controlled by tools
                        localPos = new float2(x, y) / vertexCount - new float2(0.5f, 0.5f),
                        vertexIndex = new int2(x, y)
                    };
                }
            }

            NativeArray<TerrainVertex> triangles = new NativeArray<TerrainVertex>(6 * meshSize.x * meshSize.y, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            verticesBuffer = new ComputeBuffer(triangles.Length, sizeof(TerrainVertex));
            TerrainVertex* trianglePtr = triangles.Ptr();
            for (int x = 0, count = 0; x < meshSize.x; ++x)
            {
                for (int y = 0; y < meshSize.y; ++y)
                {
                    int4 indices = new int4(vertexCount.x * y + x, vertexCount.x * (y + 1) + x, vertexCount.x * y + (x + 1), vertexCount.x * (y + 1) + (x + 1));
                    trianglePtr[count] = arrPtr[indices.x];
                    trianglePtr[count + 1] = arrPtr[indices.y];
                    trianglePtr[count + 2] = arrPtr[indices.z];
                    trianglePtr[count + 3] = arrPtr[indices.y];
                    trianglePtr[count + 4] = arrPtr[indices.w];
                    trianglePtr[count + 5] = arrPtr[indices.z];
                    count += 6;
                }
            }
            verticesBuffer.SetData(triangles);
            triangles.Dispose();
            terrainVertexArray.Dispose();
            removebuffer = new ComputeBuffer(100, sizeof(int2));
            //Initialize indirect
            clusterBuffer = new ComputeBuffer(maximumLength, sizeof(TerrainVertex));
            referenceBuffer = new NativeList<ulong>(maximumLength, Allocator.Persistent);
            this.transformShader = transformShader;
            resultBuffer = new ComputeBuffer(maximumLength, sizeof(int));
            instanceCountBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            NativeArray<int> indirect = new NativeArray<int>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            indirect[0] = verticesBuffer.count;
            instanceCountBuffer.SetData(indirect);
            indirect.Dispose();
        }
        #region LOAD_AREA
        public void AddQuadTrees(NativeList<ulong> addList)
        {
            TerrainQuadTree.QuadTreeNode** tree = (TerrainQuadTree.QuadTreeNode**)addList.unsafePtr;
            int length = addList.Length;
            NativeArray<TerrainPanel> panel = new NativeArray<TerrainPanel>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            TerrainPanel* panelPtr = panel.Ptr();
            for (int i = 0; i < length; ++i)
            {
                tree[i]->listPosition = referenceBuffer.Length + i;
                panelPtr[i] = tree[i]->panel;
            }
            clusterBuffer.SetData(panel, 0, referenceBuffer.Length, length);
            panel.Dispose();
            referenceBuffer.AddRange(addList);
            addList.Clear();
        }

        public void RemoveQuadTrees(NativeList<ulong> removeList)
        {
            int length = removeList.Length;
            TerrainQuadTree.QuadTreeNode** tree = (TerrainQuadTree.QuadTreeNode**)removeList.unsafePtr;
            int targetLength = referenceBuffer.Length - length;
            int len = 0;
            if (targetLength <= 0)
            {
                referenceBuffer.Clear();
                return;
            }
            for (int i = 0; i < length; ++i)
            {
                TerrainQuadTree.QuadTreeNode* currentNode = tree[length];
                if (currentNode->listPosition >= targetLength)
                {
                    referenceBuffer[currentNode->listPosition] = 0;
                    currentNode->listPosition = -1;
                }
            }
            NativeArray<int2> transformList = new NativeArray<int2>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int2* transformPtr = transformList.Ptr();
            len = 0;
            int currentIndex = referenceBuffer.Length - 1;
            for (int i = 0; i < length; ++i)
            {
                TerrainQuadTree.QuadTreeNode* treeNode = tree[length];
                if (treeNode->listPosition < 0) continue;
                while (referenceBuffer[currentIndex] == 0)
                {
                    currentIndex--;
                    if (currentIndex < 0)
                        goto FINALIZE;
                }
                TerrainQuadTree.QuadTreeNode* lastNode = (TerrainQuadTree.QuadTreeNode*)referenceBuffer[currentIndex];
                currentIndex--;
                transformPtr[len] = new int2(treeNode->listPosition, lastNode->listPosition);
                len++;
                lastNode->listPosition = treeNode->listPosition;
                referenceBuffer[lastNode->listPosition] = (ulong)lastNode;
                treeNode->listPosition = -1;
            }
            removeList.Clear();
            referenceBuffer.RemoveLast(length);
            FINALIZE:
            if (len <= 0) return;
            if (len > removebuffer.count)
            {
                removebuffer.Dispose();
                removebuffer = new ComputeBuffer(len, sizeof(int2));
            }
            referenceBuffer.RemoveLast(length);
            removebuffer.SetData(transformList, 0, 0, len);
            transformShader.SetBuffer(0, ShaderIDs._IndexBuffer, removebuffer);
            transformShader.SetBuffer(0, ShaderIDs.clusterBuffer, clusterBuffer);
            ComputeShaderUtility.Dispatch(transformShader, 0, len, 64);
            transformList.Dispose();
        }

        #endregion
        public void Dispose()
        {
            verticesBuffer.Dispose();
            instanceCountBuffer.Dispose();
            resultBuffer.Dispose();
            clusterBuffer.Dispose();
            removebuffer.Dispose();
            referenceBuffer.Dispose();
        }
    }

    public struct TerrainVertex
    {
        public float2 uv;
        public int2 vertexIndex;
        public float2 localPos;
    }

    public struct TerrainPanel
    {
        public float3 extent;
        public float3 position;
        public int4 textureIndex;
        public int heightMapIndex;
    }

    public unsafe struct TerrainQuadTree
    {
        public struct QuadTreeNode
        {
            /*  public QuadTreeNode* leftUp;
              public QuadTreeNode* leftDown;
              public QuadTreeNode* rightUp;
              public QuadTreeNode* rightDown;*/
            public TerrainPanel panel;
            public int listPosition;
        }
        public NativeList<QuadTreeNode> originTrees;
    }
}
