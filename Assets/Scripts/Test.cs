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
public unsafe class Test : MonoBehaviour
{
    CommandBuffer buffer;
    ComputeBuffer cb;
    public Material mat;
    private void Awake()
    {
        cb = new ComputeBuffer(12, 4);
        buffer = new CommandBuffer();
    }
    void Update()
    {
        buffer.DrawProceduralIndirect(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, cb);
    }
}
