using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.IO;
using MPipeline;
public unsafe class Test : MonoBehaviour
{
    [Button]
    public void Try()
    {
        NativeDictionary<int, int> nativeDictionary = new NativeDictionary<int, int>(50, Allocator.Temp, (i, j) => i == j);
        nativeDictionary.Add(1, 3);
        nativeDictionary.Add(2, 4);
        int result;
        nativeDictionary.Get(1, out result);
        Debug.Log(result);
        nativeDictionary.Get(2, out result);
        Debug.Log(result);
        nativeDictionary.Get(3, out result);
        Debug.Log(result);
        nativeDictionary.Dispose();
    }
}
