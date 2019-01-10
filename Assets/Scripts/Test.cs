using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using MPipeline;
public unsafe class Test : MonoBehaviour
{
    [EasyButtons.Button]
    public void Try()
    {
        Camera cam = GetComponent<Camera>();
        float3* frustumCorner = stackalloc float3[8];
           }
}
