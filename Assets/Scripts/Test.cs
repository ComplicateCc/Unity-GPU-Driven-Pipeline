using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using static Unity.Mathematics.math;

public unsafe class Test : MonoBehaviour
{
    [Button]
    void Run()
    {
        int3 voxel = PipelineFunctions.UpDimension(1000, int2(12, 5));
        Debug.Log(voxel);
        Debug.Log(PipelineFunctions.DownDimension(voxel, int2(12, 5)));
    }
}
