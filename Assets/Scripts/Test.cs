using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using System.Threading;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using System.Reflection;
using UnityEngine.Jobs;
using MPipeline;
using Random = UnityEngine.Random;
public unsafe class Test : MonoBehaviour
{
    [Button]
    public void Run()
    {
        NativeDictionary<int, int> dict = new NativeDictionary<int, int>(150, Unity.Collections.Allocator.Persistent, (i,j) => i == j);
        for(int i = 0; i < 200; ++i)
        {
            dict.Add(i, Random.Range(0, 10000));
        }
        foreach(var i in dict)
        {
            Debug.Log(i);
        }
        dict.Dispose();
    }
}
