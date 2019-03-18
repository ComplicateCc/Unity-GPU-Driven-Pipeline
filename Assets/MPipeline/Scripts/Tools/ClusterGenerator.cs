#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using System;
namespace MPipeline
{
    public unsafe static class ClusterGenerator
    {
        struct Triangle
        {
            public float3 a;
            public float3 b;
            public float3 c;
            public Triangle* last;
            public Triangle* next;
        }
        struct Voxel
        {
            public Triangle* start;
            public int count;
            public void Add(Triangle* ptr)
            {
                if (start != null)
                {
                    start->last = ptr;
                    ptr->next = start;
                }
                start = ptr;
                count++;
            }
            public Triangle* Pop()
            {
                if (start->next != null)
                {
                    start->next->last = null;
                }
                Triangle* last = start;
                start = start->next;
                count--;
                return last;
            }
        }
        /// <returns></returns> Cluster Count
        public static int GenerateCluster(NativeList<float3> pointsFromMesh, NativeList<int> triangles, Bounds bd, string fileName)
        {
            NativeList<CullBox> boxes; NativeList<float3> points;
            GetCluster(pointsFromMesh, triangles, bd, out boxes, out points);

            string filenameWithExtent = fileName + ".mpipe";
            byte[] bytes = new byte[boxes.Length * sizeof(CullBox)];
            UnsafeUtility.MemCpy(bytes.Ptr(), boxes.unsafePtr, bytes.Length);
            File.WriteAllBytes("Assets/BinaryData/MapInfos/" + filenameWithExtent, bytes);
            bytes = new byte[points.Length * sizeof(float3)];
            UnsafeUtility.MemCpy(bytes.Ptr(), points.unsafePtr, bytes.Length);
            File.WriteAllBytes("Assets/BinaryData/MapPoints/" + filenameWithExtent, bytes);
            //Dispose Native Array
            return boxes.Length;
        }

        public static void GetCluster(NativeList<float3> pointsFromMesh, NativeList<int> triangles, Bounds bd, out NativeList<CullBox> boxes, out NativeList<float3> points)
        {
            NativeList<Triangle> trs = GenerateTriangle(triangles, pointsFromMesh);
            Voxel[,,] voxels = GetVoxelData(trs, 100, bd);
            GetClusterFromVoxel(voxels, out boxes, out points, triangles.Length, 100);
        }

        private static NativeList<Triangle> GenerateTriangle(NativeList<int> triangles, NativeList<float3> points)
        {
            NativeList<Triangle> retValue = new NativeList<Triangle>(triangles.Length / 3, Allocator.TempJob);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Triangle tri = new Triangle
                {
                    a = points[triangles[i]],
                    b = points[triangles[i + 1]],
                    c = points[triangles[i + 2]],
                    last = null,
                    next = null
                };
                retValue.Add(tri);
            }
            return retValue;
        }

        private static Voxel[,,] GetVoxelData(NativeList<Triangle> trianglesFromMesh, int voxelCount, Bounds bound)
        {
            Voxel[,,] voxels = new Voxel[voxelCount, voxelCount, voxelCount];
            for (int x = 0; x < voxelCount; ++x)
                for (int y = 0; y < voxelCount; ++y)
                    for (int z = 0; z < voxelCount; ++z)
                    {
                        voxels[x, y, z] = new Voxel();
                    }
            float3 downPoint = bound.center - bound.extents;
            for (int i = 0; i < trianglesFromMesh.Length; ++i)
            {
                ref Triangle tr = ref trianglesFromMesh[i];
                float3 position = (tr.a + tr.b + tr.c) / 3;
                float3 localPos = saturate((position - downPoint) / bound.size);
                int3 coord = (int3)(localPos * voxelCount);
                coord = min(coord, voxelCount - 1);
                voxels[coord.x, coord.y, coord.z].Add((Triangle*)UnsafeUtility.AddressOf(ref tr));
            }
            return voxels;
        }

        private static void GetClusterFromVoxel(Voxel[,,] voxels, out NativeList<CullBox> cullBoxes, out NativeList<float3> points, int vertexCount, int voxelSize)
        {
            int3 voxelCoord = 0;
            float3 lessPoint = float.MaxValue;
            float3 morePoint = float.MinValue;
            int clusterCount = Mathf.CeilToInt((float)vertexCount / PipelineBaseBuffer.CLUSTERCLIPCOUNT);
            points = new NativeList<float3>(clusterCount * PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.TempJob);
            cullBoxes = new NativeList<CullBox>(clusterCount, Allocator.Temp);
            //Collect all full
            for (int i = 0; i < clusterCount - 1; ++i)
            {
                NativeList<float3> currentPoints = new NativeList<float3>(PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.TempJob);
                int lastedVertex = PipelineBaseBuffer.CLUSTERCLIPCOUNT / 3;
                ref Voxel currentVoxel = ref voxels[voxelCoord.x, voxelCoord.y, voxelCoord.z];
                int loopStart = min(currentVoxel.count, max(lastedVertex - currentVoxel.count, 0));
                for (int j = 0; j < loopStart; j++)
                {
                    Triangle* tri = currentVoxel.Pop();
                    currentPoints.Add(tri->a);
                    currentPoints.Add(tri->b);
                    currentPoints.Add(tri->c);
                }
                lastedVertex -= loopStart;

                for (int size = 1; lastedVertex > 0; size++)
                {
                    int3 leftDown = max(voxelCoord - size, 0);
                    int3 rightUp = min(voxelSize, voxelCoord + size);
                    for (int x = leftDown.x; x < rightUp.x; ++x)
                        for (int y = leftDown.y; y < rightUp.y; ++y)
                            for (int z = leftDown.z; z < rightUp.z; ++z)
                            {
                                ref Voxel vxl = ref voxels[x, y, z];
                                int vxlCount = vxl.count;
                                for (int j = 0; j < vxlCount; ++j)
                                {
                                    voxelCoord = int3(x, y, z);
                                    Triangle* tri = vxl.Pop();
                                    //   try
                                    // {
                                    currentPoints.Add(tri->a);
                                    currentPoints.Add(tri->b);
                                    currentPoints.Add(tri->c);
                                    /* }
                                     catch
                                     {
                                         Debug.Log(vxlCount);
                                         Debug.Log(tri->a);
                                         Debug.Log(tri->b);
                                         Debug.Log(tri->c);
                                         Debug.Log(currentPoints.Length);
                                         return;
                                     }*/
                                    lastedVertex--;
                                    if (lastedVertex <= 0) goto CONTINUE;
                                }
                            }

                }
                CONTINUE:
                points.AddRange(currentPoints);
                lessPoint = float.MaxValue;
                morePoint = float.MinValue;
                foreach (var j in currentPoints)
                {
                    if (j.x < lessPoint.x) lessPoint.x = j.x;
                    else if (j.x > morePoint.x) morePoint.x = j.x;
                    if (j.y < lessPoint.y) lessPoint.y = j.y;
                    else if (j.y > morePoint.y) morePoint.y = j.y;
                    if (j.z < lessPoint.z) lessPoint.z = j.z;
                    else if (j.z > morePoint.z) morePoint.z = j.z;
                }
                CullBox cb = new CullBox
                {
                    extent = (morePoint - lessPoint) / 2,
                    position = (morePoint + lessPoint) / 2
                };
                cullBoxes.Add(cb);
                currentPoints.Dispose();
            }
            //Collect and degenerate
            NativeList<float3> leftedPoints = new NativeList<float3>(PipelineBaseBuffer.CLUSTERCLIPCOUNT, Allocator.Temp);
            for (int x = 0; x < voxelSize; ++x)
                for (int y = 0; y < voxelSize; ++y)
                    for (int z = 0; z < voxelSize; ++z)
                    {
                        ref Voxel vxl = ref voxels[x, y, z];
                        int vxlCount = vxl.count;
                        for (int j = 0; j < vxlCount; ++j)
                        {
                            Triangle* tri = vxl.Pop();
                            leftedPoints.Add(tri->a);
                            leftedPoints.Add(tri->b);
                            leftedPoints.Add(tri->c);

                        }
                    }
            if (leftedPoints.Length <= 0) return;
            lessPoint = float.MaxValue;
            morePoint = float.MinValue;
            foreach (var j in leftedPoints)
            {
                if (j.x < lessPoint.x) lessPoint.x = j.x;
                else if (j.x > morePoint.x) morePoint.x = j.x;
                if (j.y < lessPoint.y) lessPoint.y = j.y;
                else if (j.y > morePoint.y) morePoint.y = j.y;
                if (j.z < lessPoint.z) lessPoint.z = j.z;
                else if (j.z > morePoint.z) morePoint.z = j.z;
            }
            CullBox lastBox = new CullBox
            {
                extent = (morePoint - lessPoint) / 2,
                position = (morePoint + lessPoint) / 2
            };
            cullBoxes.Add(lastBox);
            for (int i = leftedPoints.Length; i < PipelineBaseBuffer.CLUSTERCLIPCOUNT; i++)
            {
                leftedPoints.Add(new float3());
            }
            points.AddRange(leftedPoints);
        }
    }
}
#endif