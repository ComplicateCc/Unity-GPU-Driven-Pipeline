using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using static Unity.Mathematics.math;
using MPipeline;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Mathematics;
public unsafe class Test : MonoBehaviour
{
    public Transform box;
    public Transform plane;
    public UnityEngine.UI.Text txt;
    private static void GetMatrix(float4x4* allmat, ref PerspCam persp, float3 position)
    {
        persp.position = position;
        //X
        persp.up = float3(0, 1, 0);
        persp.right = float3(0, 0, -1);
        persp.forward = float3(1, 0, 0);
        persp.UpdateTRSMatrix();
        allmat[1] = persp.worldToCameraMatrix;
        //-X
        persp.up = float3(0, 1, 0);
        persp.right = float3(0, 0, 1);
        persp.forward = float3(-1, 0, 0);
        persp.UpdateTRSMatrix();
        allmat[0] = persp.worldToCameraMatrix;
        //Y
        persp.right = float3(-1, 0, 0);
        persp.up = float3(0, 0, 1);
        persp.forward = float3(0, 1, 0);
        persp.UpdateTRSMatrix();
        allmat[2] = persp.worldToCameraMatrix;
        //-Y
        persp.right = float3(-1, 0, 0);
        persp.up = float3(0, 0, -1);
        persp.forward = float3(0, -1, 0);
        persp.UpdateTRSMatrix();
        allmat[3] = persp.worldToCameraMatrix;
        //Z
        persp.right = float3(1, 0, 0);
        persp.up = float3(0, 1, 0);
        persp.forward = float3(0, 0, 1);
        persp.UpdateTRSMatrix();
        allmat[5] = persp.worldToCameraMatrix;
        //-Z
        persp.right = float3(-1, 0, 0);
        persp.up = float3(0, 1, 0);
        persp.forward = float3(0, 0, -1);
        persp.UpdateTRSMatrix();
        allmat[4] = persp.worldToCameraMatrix;
    }
    [EasyButtons.Button]
    void Run()
    {
        float4x4* allMat = stackalloc float4x4[6];
        PerspCam persp = new PerspCam();
        persp.aspect = 1;
        persp.farClipPlane = 1;
        persp.nearClipPlane = 0.1f;
        persp.fov = 90f;
        GetMatrix(allMat, ref persp, float3(0, 0, 0));
        persp.UpdateProjectionMatrix();
        for(int i = 0; i < 6; ++i)
        {
            float4x4 invvp = inverse(GL.GetGPUProjectionMatrix(mul(persp.projectionMatrix, allMat[i]), false));

            string sb = "";
            float4 value = mul(invvp, float4(-1, -1, 0, 1));
            value /= value.w;
            sb += "UV00: " +  value.xyz;
            value = mul(invvp, float4(-1, 1, 0, 1));
            value /= value.w;
            sb += "  UV01: " + value.xyz;
            value = mul(invvp, float4(1, 1, 0, 1));
            value /= value.w;
            sb += "  UV11: " + value.xyz;
            value = mul(invvp, float4(1, -1, 0, 1));
            value /= value.w;
            sb += "  UV10: " + value.xyz;
            Debug.Log(sb);
        }
    }
}