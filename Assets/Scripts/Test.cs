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
public unsafe class Test : MonoBehaviour
{
    private static void UpdateFunc()
    {
        Debug.Log("SB");
    }
    [Button]
    public void Run()
    {
    }
}
