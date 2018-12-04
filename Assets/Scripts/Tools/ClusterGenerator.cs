#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Unity.Jobs;
using System;
using Random = UnityEngine.Random;
namespace MPipeline
{
    public unsafe static class ClusterSortFunction
    {
        public struct Element
        {
            public float sign;
            public Fragment policy;
            public int leftValue;
            public int rightValue;
        }

        public static void ClusterSort(NativeArray<Fragment> fragments, NativeArray<float> distances)
        {
            if (fragments.Length == 0) return;
            NativeArray<Element> elements = new NativeArray<Element>(fragments.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Element* elementsPtr = (Element*)elements.GetUnsafePtr();
            Fragment* fragmentsPtr = (Fragment*)fragments.GetUnsafePtr();
            float* distancePtr = (float*)distances.GetUnsafePtr();
            Element newElement;
            newElement.sign = 0;
            newElement.policy = new Fragment();
            newElement.leftValue = -1;
            newElement.rightValue = -1;
            for (int i = 0; i < elements.Length; ++i)
            {
                newElement.sign = distancePtr[i];
                newElement.policy = fragmentsPtr[i];
                elementsPtr[i] = newElement;
            }
            for (int i = 1; i < fragments.Length; ++i)
            {
                int currentIndex = 0;
                STARTFIND:
                Element* currentIndexValue = elementsPtr + currentIndex;
                if ((elementsPtr + i)->sign < currentIndexValue->sign)
                {
                    if (currentIndexValue->leftValue < 0)
                    {
                        currentIndexValue->leftValue = i;
                    }
                    else
                    {
                        currentIndex = currentIndexValue->leftValue;
                        goto STARTFIND;
                    }
                }
                else
                {
                    if (currentIndexValue->rightValue < 0)
                    {
                        currentIndexValue->rightValue = i;
                    }
                    else
                    {
                        currentIndex = currentIndexValue->rightValue;
                        goto STARTFIND;
                    }
                }
            }
            int start = 0;
            Iterate(0, ref start, elements, fragments);
            elements.Dispose();
        }
        private static void Iterate(int i, ref int targetLength, NativeArray<Element> elements, NativeArray<Fragment> results)
        {
            int leftValue = elements[i].leftValue;
            if (leftValue >= 0)
            {
                Iterate(leftValue, ref targetLength, elements, results);
            }
            results[targetLength] = ((Element*)elements.GetUnsafePtr() + (ulong)i)->policy;
            targetLength++;
            int rightValue = elements[i].rightValue;
            if (rightValue >= 0)
            {
                Iterate(rightValue, ref targetLength, elements, results);
            }
        }
    }

    public unsafe class ClusterGenerator : MonoBehaviour
    {
        /// <returns></returns> Cluster Count
        public static int GenerateCluster(NativeList<Point> pointsFromMesh, NativeList<int> triangles, Bounds bd, string fileName)
        {
            List<Vector4Int> value = ClusterFunctions.AddTrianglesToDictionary(triangles);
            GetFragmentJob gt = new GetFragmentJob(bd.center, bd.extents, pointsFromMesh, value);
            (gt.Schedule(value.Count, 16)).Complete();
            var voxel = GetFragmentJob.voxelFragments;
            const int VoxelCount = ClusterFunctions.VoxelCount;
            Fragment first = new Fragment();
            Vector3Int inputPoint = new Vector3Int();
            for (int i = 0; i < VoxelCount; ++i)
            {
                for (int j = 0; j < VoxelCount; ++j)
                    for (int k = 0; k < VoxelCount; ++k)
                    {
                        var lst = voxel[i, j, k];
                        if (lst.Count > 0)
                        {
                            first = lst[0];
                            inputPoint = new Vector3Int(i, j, k);
                            goto BREAK;
                        }
                    }
            }
            BREAK:
            NativeArray<Fragment> resultCluster;
            List<NativeArray<Fragment>> allFragments = new List<NativeArray<Fragment>>();

            LOOP:
            bool stillLooping = ClusterFunctions.GetFragFromVoxel(voxel, first.position, inputPoint, out resultCluster);
            List<Vector3> vertices = new List<Vector3>(16 * 6);
            List<Vector3> normals = new List<Vector3>(16 * 6);
            if (stillLooping)
            {
                first = resultCluster[resultCluster.Length - 1];
                allFragments.Add(resultCluster);
                goto LOOP;
            }
            if (resultCluster.Length < (ClusterFunctions.ClusterCount + 1))
            {
                NativeArray<Fragment> lastResultCluster = new NativeArray<Fragment>(ClusterFunctions.ClusterCount + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < resultCluster.Length; ++i)
                {
                    lastResultCluster[i] = resultCluster[i];
                }
                for (int i = resultCluster.Length; i < lastResultCluster.Length; ++i)
                {
                    Fragment fg;
                    fg.indices = new Vector4Int(0, 0, 0, 0);
                    fg.position = Vector3.zero;
                    fg.voxel = Vector3Int.zero;
                    lastResultCluster[i] = fg;
                }
                resultCluster.Dispose();
                resultCluster = lastResultCluster;
                allFragments.Add(resultCluster);
            }
            gt.Dispose();
            NativeArray<ClusterMeshData> meshData = new NativeArray<ClusterMeshData>(allFragments.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<Point>[] allpoints = new NativeArray<Point>[allFragments.Count];
            int pointCount = 0;
            for (int i = 0; i < allpoints.Length; ++i)
            {
                allpoints[i] = new NativeArray<Point>((allFragments[i].Length - 1) * 4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                pointCount += allpoints[i].Length;
            }
            CollectJob.meshDatas = meshData;
            CollectJob.allFragments = allFragments;
            CollectJob.pointsArray = allpoints;
            CollectJob.pointsFromMesh = pointsFromMesh;
            CollectJob jb = new CollectJob();
            jb.Schedule(allFragments.Count, 1).Complete();
            CollectJob.allFragments = null;
            CollectJob.pointsArray = null;
            NativeArray<Point> pointsList = new NativeArray<Point>(pointCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0, pointsCount = 0; i < allpoints.Length; ++i)
            {
                var current = allpoints[i];
                for (int a = 0; a < current.Length; ++a)
                {
                    pointsList[pointsCount] = current[a];
                    pointsCount++;
                }
                current.Dispose();
            }
            byte[] meshDataArray;
            byte[] pointDataArray;
            ClusterFunctions.GetByteDataFromArray(meshData, pointsList, out meshDataArray, out pointDataArray);
            int len = meshData.Length;
            string filenameWithExtent = fileName + ".txt";
            File.WriteAllBytes("Assets/BinaryData/MapInfos/" + filenameWithExtent, meshDataArray);
            File.WriteAllBytes("Assets/BinaryData/MapPoints/" + filenameWithExtent, pointDataArray);
            //Dispose Native Array
            meshData.Dispose();
            pointsList.Dispose();
            pointsFromMesh.Dispose();
            triangles.Dispose();
            foreach (var i in allFragments)
            {
                i.Dispose();
            }
            return len;
        }
        public static NativeList<Point> GetPoints(Mesh testMesh, Matrix4x4 localToWorld, out NativeList<int> triangles)
        {
            Vector3[] verticesMesh = testMesh.vertices;
            Vector3[] normalsMesh = testMesh.normals;
            Vector2[] uv = testMesh.uv;
            Vector4[] tangents = testMesh.tangents;
            int[] tri = testMesh.triangles;
            triangles = new NativeList<int>(tri.Length, Allocator.Temp);
            triangles.AddRange(tri);
            NativeList<Point> pointsFromMesh = new NativeList<Point>(verticesMesh.Length, verticesMesh.Length, Allocator.Temp);
            for (int i = 0; i < pointsFromMesh.Length; ++i)
            {
                Vector3 vertex = localToWorld.MultiplyPoint3x4(verticesMesh[i]);
                Vector4 tangent = localToWorld.MultiplyVector(tangents[i]);
                Vector3 normal = localToWorld.MultiplyVector(normalsMesh[i]);
                tangent.w = tangents[i].w;
                Point p;
                p.tangent = tangent;
                p.normal = normal;
                p.vertex = vertex;
                p.texcoord = uv[i];
                p.objIndex = 0;
                pointsFromMesh[i] = p;
            }
            return pointsFromMesh;
        }

    }
    #region STRUCT
    public struct Vector4Int
    {
        public int x;
        public int y;
        public int z;
        public int w;
        public Vector4Int(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }
    public struct Fragment
    {
        public Vector4Int indices;
        public Vector3 position;
        public Vector3Int voxel;
    }

    #endregion
    public unsafe static class ClusterFunctions
    {
        public const int ClusterCount = PipelineBaseBuffer.CLUSTERCLIPCOUNT / 4;
        public const int VoxelCount = 10;
        public static void GetByteDataFromArray(NativeArray<ClusterMeshData> meshdata, NativeArray<Point> points, out byte[] meshBytes, out byte[] pointBytes)
        {
            meshBytes = new byte[meshdata.Length * sizeof(ClusterMeshData)];
            pointBytes = new byte[points.Length * sizeof(Point)];
            void* meshDataPtr = meshdata.GetUnsafePtr();
            void* pointsPtr = points.GetUnsafePtr();
            fixed (void* destination = &meshBytes[0])
            {
                UnsafeUtility.MemCpy(destination, meshDataPtr, meshBytes.Length);
            }
            fixed (void* destination = &pointBytes[0])
            {
                UnsafeUtility.MemCpy(destination, pointsPtr, pointBytes.Length);
            }
        }
        private static bool CheckCluster(Dictionary<Vector2Int, int> targetDict, Vector3Int triangle, out Vector4Int cluster)
        {
            cluster = new Vector4Int();
            NativeArray<Vector2Int> waitingTarget = new NativeArray<Vector2Int>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> currentTarget = new NativeArray<int>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            waitingTarget[0] = new Vector2Int(triangle.y, triangle.x);
            waitingTarget[1] = new Vector2Int(triangle.z, triangle.y);
            waitingTarget[2] = new Vector2Int(triangle.x, triangle.z);
            currentTarget[0] = triangle.z;
            currentTarget[1] = triangle.x;
            currentTarget[2] = triangle.y;
            for (int i = 0; i < 3; ++i)
            {
                int value;
                if (targetDict.TryGetValue(waitingTarget[i], out value))
                {
                    targetDict.Remove(waitingTarget[i]);
                    targetDict.Remove(new Vector2Int(waitingTarget[i].y, value));
                    targetDict.Remove(new Vector2Int(value, waitingTarget[i].x));
                    cluster = new Vector4Int(currentTarget[i], waitingTarget[i].y, waitingTarget[i].x, value);
                    return true;
                }
            }
            targetDict[new Vector2Int(triangle.x, triangle.y)] = triangle.z;
            targetDict[new Vector2Int(triangle.z, triangle.x)] = triangle.y;
            targetDict[new Vector2Int(triangle.y, triangle.z)] = triangle.x;
            waitingTarget.Dispose();
            currentTarget.Dispose();
            return false;
        }
        public static List<Vector4Int> AddTrianglesToDictionary(NativeList<int> triangleArray)
        {
            List<Vector4Int> allClusters = new List<Vector4Int>();
            Dictionary<Vector2Int, int> dict = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < triangleArray.Length; i += 3)
            {
                Vector4Int cluster;
                if (CheckCluster(dict, new Vector3Int(triangleArray[i], triangleArray[i + 1], triangleArray[i + 2]), out cluster))
                {
                    allClusters.Add(cluster);
                }
            }
            foreach (var i in dict.Keys)
            {
                int v = dict[i];
                allClusters.Add(new Vector4Int(i.x, i.y, v, v));
            }
            return allClusters;
        }

        public static Vector3Int GetVoxel(Vector3 left, Vector3 right)
        {
            Vector3 result;
            result.x = left.x / right.x;
            result.y = left.y / right.y;
            result.z = left.z / right.z;
            result *= VoxelCount;
            const float MinValue = VoxelCount - 0.1f;
            result.x = Mathf.Min(MinValue, result.x);
            result.y = Mathf.Min(MinValue, result.y);
            result.z = Mathf.Min(MinValue, result.z);
            return new Vector3Int((int)result.x, (int)result.y, (int)result.z);
        }

        public static void SetPointToVoxel(Vector4Int points, NativeList<Point> allPoints, List<Fragment>[,,] voxel, Vector3 leftPoint, Vector3 extent)
        {
            Vector3 position = allPoints[points.x].vertex
                               + allPoints[points.y].vertex
                               + allPoints[points.z].vertex
                               + allPoints[points.w].vertex;
            position /= 4;
            Vector3 distToLeft = position - leftPoint;
            Vector3Int voxelIndex = GetVoxel(distToLeft, extent);
            Fragment frag;
            frag.indices = points;
            frag.voxel = voxelIndex;
            frag.position = position;
            List<Fragment> lists = voxel[voxelIndex.x, voxelIndex.y, voxelIndex.z];
            lock (lists)
            {
                lists.Add(frag);
            }
        }

        private static void AddVoxelToList(List<Fragment>[,,] voxelArray, List<Fragment> targetArray, Vector3Int coord)
        {
            const int TARGETVOLUMECOUNT = VoxelCount * VoxelCount * VoxelCount;
            int volumeCount = 0;
            int spreadCount = 0;
            Vector2Int xAxisRange = new Vector2Int();
            Vector2Int yAxisRange = new Vector2Int();
            Vector2Int zAxisRange = new Vector2Int();
            while (volumeCount < TARGETVOLUMECOUNT)
            {
                xAxisRange = new Vector2Int(Mathf.Max(0, coord.x - spreadCount), Mathf.Min(VoxelCount, coord.x + spreadCount));
                yAxisRange = new Vector2Int(Mathf.Max(0, coord.y - spreadCount), Mathf.Min(VoxelCount, coord.y + spreadCount));
                zAxisRange = new Vector2Int(Mathf.Max(0, coord.z - spreadCount), Mathf.Min(VoxelCount, coord.z + spreadCount));
                volumeCount = (xAxisRange.y - xAxisRange.x) * (yAxisRange.y - yAxisRange.x) * (zAxisRange.y - zAxisRange.x);
                int listCount = 0;
                for (int x = xAxisRange.x; x < xAxisRange.y; ++x)
                {
                    for (int y = yAxisRange.x; y < yAxisRange.y; ++y)
                    {
                        for (int z = zAxisRange.x; z < zAxisRange.y; ++z)
                        {
                            listCount += voxelArray[x, y, z].Count;
                        }
                    }
                }
                if (listCount > ClusterCount)
                    break;
                spreadCount++;
            }
            for (int x = xAxisRange.x; x < xAxisRange.y; ++x)
            {
                for (int y = yAxisRange.x; y < yAxisRange.y; ++y)
                {
                    for (int z = zAxisRange.x; z < zAxisRange.y; ++z)
                    {
                        List<Fragment> lst = voxelArray[x, y, z];
                        targetArray.AddRange(lst);
                        lst.Clear();
                    }
                }
            }
        }

        public static bool GetFragFromVoxel(List<Fragment>[,,] voxelArray, Vector3 originPointPosition, Vector3Int inputPoint, out NativeArray<Fragment> resultCluster)
        {
            List<Fragment> fragments = new List<Fragment>(ClusterCount + 1);
            AddVoxelToList(voxelArray, fragments, inputPoint);

            if (ClusterCount < fragments.Count)
            {
                resultCluster = new NativeArray<Fragment>(ClusterCount + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i <= ClusterCount; ++i)
                {
                    resultCluster[i] = fragments[i];
                }
                NativeArray<float> distances = new NativeArray<float>(resultCluster.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < distances.Length; ++i)
                {
                    distances[i] = Vector3.SqrMagnitude(originPointPosition - resultCluster[i].position);
                }
                ClusterSortFunction.ClusterSort(resultCluster, distances);
                distances.Dispose();
                Fragment f = resultCluster[ClusterCount];
                voxelArray[f.voxel.x, f.voxel.y, f.voxel.z].Add(f);
                for (int i = ClusterCount + 1; i < fragments.Count; ++i)
                {
                    f = fragments[i];
                    voxelArray[f.voxel.x, f.voxel.y, f.voxel.z].Add(f);
                }
                return true;
            }
            else
            {
                resultCluster = new NativeArray<Fragment>(fragments.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < fragments.Count; ++i)
                {
                    resultCluster[i] = fragments[i];
                }
                return false;
            }
        }
    }
    public unsafe struct GetFragmentJob : IJobParallelFor
    {
        public static NativeList<Point> allPoint;
        public static List<Vector4Int> fragments;
        public static List<Fragment>[,,] voxelFragments;
        public static Vector3 leftPoint;
        public static Vector3 distance;
        public GetFragmentJob(Vector3 position, Vector3 extent, NativeList<Point> allpt, List<Vector4Int> frag)
        {
            allPoint = allpt;
            fragments = frag;
            leftPoint = position - extent;
            distance = extent * 2;
            const int VoxelCount = ClusterFunctions.VoxelCount;
            voxelFragments = new List<Fragment>[VoxelCount, VoxelCount, VoxelCount];
            for (int i = 0; i < VoxelCount; ++i)
                for (int j = 0; j < VoxelCount; ++j)
                    for (int k = 0; k < VoxelCount; ++k)
                    {
                        voxelFragments[i, j, k] = new List<Fragment>();
                    }
        }
        public void Dispose()
        {
            allPoint.Dispose();
            fragments = null;
            voxelFragments = null;
            GC.Collect();
        }

        public void Execute(int index)
        {
            ClusterFunctions.SetPointToVoxel(fragments[index], allPoint, voxelFragments, leftPoint, distance);
        }
    }
    public unsafe struct CollectJob : IJobParallelFor
    {
        public static List<NativeArray<Fragment>> allFragments;
        public static NativeArray<ClusterMeshData> meshDatas;
        public static NativeArray<Point>[] pointsArray;
        public static NativeList<Point> pointsFromMesh;
        public void Execute(int index)
        {
            NativeArray<Fragment> fragments = allFragments[index];
            Fragment* fragPtr = (Fragment*)fragments.GetUnsafePtr();
            ClusterMeshData data = new ClusterMeshData();
            NativeArray<Point> allPoints = pointsArray[index];
            for (int i = 0, pointCount = 0; i < fragments.Length - 1; ++i)
            {
                int* indices = (int*)&fragPtr[i].indices;
                for (int a = 0; a < 4; ++a)
                {
                    ref Point p = ref pointsFromMesh[indices[a]];
                    allPoints[pointCount] = p;
                    pointCount++;
                }
            }
            Point firstPoint = allPoints[0];
            float top = firstPoint.vertex.y;
            float down = firstPoint.vertex.y;
            float left = firstPoint.vertex.x;
            float right = firstPoint.vertex.x;
            float back = firstPoint.vertex.z;
            float front = firstPoint.vertex.z;
            for (int i = 1; i < allPoints.Length; ++i)
            {
                Point pt = allPoints[i];
                if (pt.vertex.y > top)
                    top = pt.vertex.y;
                if (pt.vertex.y < down)
                    down = pt.vertex.y;
                if (pt.vertex.x > right)
                    right = pt.vertex.x;
                if (pt.vertex.x < left)
                    left = pt.vertex.x;
                if (pt.vertex.z > front)
                    front = pt.vertex.z;
                if (pt.vertex.z < back)
                    back = pt.vertex.z;
            }
            Vector3 position = new Vector3((left + right) * 0.5f
                                         , (top + down) * 0.5f
                                         , (front + back) * 0.5f);
            Vector3 extents = new Vector3(right - position.x, top - position.y, front - position.z);
            data.position = position;
            data.extent = extents;
            meshDatas[index] = data;
        }
    }
}
#endif