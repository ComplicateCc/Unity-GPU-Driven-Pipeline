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
        Camera cam = GetComponent<Camera>();
        Debug.Log(cam.ViewportToWorldPoint(new Vector3(0, 0, cam.farClipPlane)));
        Debug.Log(cam.ViewportToWorldPoint(new Vector3(1, 0, cam.farClipPlane)));
        Debug.Log(cam.ViewportToWorldPoint(new Vector3(0, 1, cam.farClipPlane)));
        Debug.Log(cam.ViewportToWorldPoint(new Vector3(1, 1, cam.farClipPlane)));
        float3* corners = stackalloc float3[4];
    }
}
