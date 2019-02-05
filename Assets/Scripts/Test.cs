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
using UnityEngine.Rendering;
using MPipeline;
public unsafe class Test : MonoBehaviour
{
    public int i;
    [Button]
    public void Run()
    {
        //   for (int a = 0; a < 10000; ++a)
        //   {
        NativeDictionary<int, int> nativeDictionary = new NativeDictionary<int, int>(50, Unity.Collections.Allocator.Persistent, (i, j) => i == j);
        for (int i = 0; i < 5000; ++i)
        {
            nativeDictionary.Add(i, i + 10);
        }

        for (int i = 0; i < 5000; ++i)
        {
            if (nativeDictionary[i] == 0)
                Debug.Log(i);

        }
        nativeDictionary.Dispose();
        //  }
    }
}
