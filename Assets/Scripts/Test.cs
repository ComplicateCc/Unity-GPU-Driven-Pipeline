using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
public unsafe class Test : MonoBehaviour
{
    public static bool contactWithPlane(float4x4 localToWorldMat, float3 extent, float4 plane)
    {
        float3 position = localToWorldMat.c3.xyz;
        float3x3 mat = new float3x3();
        mat.c0 = localToWorldMat.c0.xyz;
        mat.c1 = localToWorldMat.c1.xyz;
        mat.c2 = localToWorldMat.c2.xyz;
        float3 absNormal = abs(mul(plane.xyz, mat));
        return (dot(position, plane.xyz) - dot(absNormal, extent)) < -plane.w;
    }
    [SerializeField]
    UnityEngine.UI.Text txt;
    [SerializeField]
    Transform plane;
    [SerializeField]
    Transform box;
    private void Update()
    {
        Plane p = new Plane(plane.up, plane.position);
        txt.text = contactWithPlane(box.localToWorldMatrix, box.localScale * 0.5f, float4(p.normal, p.distance)).ToString();
    }
}
