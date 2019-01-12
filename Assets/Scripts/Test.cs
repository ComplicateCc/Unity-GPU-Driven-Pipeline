using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using MPipeline;
public unsafe class Test : MonoBehaviour
{
    public float4* GetCullingPlanes(float4x4 projectMatrix)
    {
        float4* ptr = (float4*)UnsafeUtility.Malloc(6 * sizeof(float4), 16, Unity.Collections.Allocator.Temp);
        ptr[0] = projectMatrix.c3 + projectMatrix.c0;
        ptr[1] = projectMatrix.c3 - projectMatrix.c0;
        ptr[2] = projectMatrix.c3 + projectMatrix.c1;
        ptr[3] = projectMatrix.c3 - projectMatrix.c1;
        ptr[4] = projectMatrix.c3 + projectMatrix.c2;
        ptr[5] = projectMatrix.c3 - projectMatrix.c2;
        return ptr;
    }

    [EasyButtons.Button]
    public void Try()
    {
        Camera cam = GetComponent<Camera>();
        float4* ptr = GetCullingPlanes(cam.projectionMatrix);
        Debug.Log(cam.projectionMatrix);
        Debug.Log("Near: " + ptr[4]);
        Debug.Log("Far: " + ptr[5]);
    }
}
