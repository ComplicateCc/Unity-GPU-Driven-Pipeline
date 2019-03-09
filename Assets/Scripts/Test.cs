using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using MPipeline;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Mathematics;
public unsafe class Test : MonoBehaviour
{
    public Transform box;
    public Transform plane;
    public UnityEngine.UI.Text txt;
    void Update()
    {
        float4 planeUp = VectorUtility.GetPlane(plane.up, plane.position);
        float4x4 mat = box.localToWorldMatrix;
        txt.text = VectorUtility.BoxIntersect(new float3x3(mat.c0.xyz, mat.c1.xyz, mat.c2.xyz), mat.c3.xyz, (float4*)AddressOf(ref planeUp), 1).ToString();
    }
}