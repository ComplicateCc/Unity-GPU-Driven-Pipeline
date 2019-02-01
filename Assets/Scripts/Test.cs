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
    [EasyButtons.Button]
    public void Run()
    {
        Camera cam = GetComponent<Camera>();
        GraphicsUtility.UpdatePlatform();
        Debug.Log((Matrix4x4)GraphicsUtility.GetGPUProjectionMatrix(cam.projectionMatrix, false));
        Debug.Log(GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
        Debug.Log((Matrix4x4)GraphicsUtility.GetGPUProjectionMatrix(cam.projectionMatrix, true));
        Debug.Log(GL.GetGPUProjectionMatrix(cam.projectionMatrix, true));
    }
}
