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
using MPipeline;
public unsafe class Test : MonoBehaviour
{
    public int i;
    const int MAX_BRIGHTNESS = 4;
    uint EncodeColor(float3 rgb)
    {
        float y = max(max(rgb.x, rgb.y), rgb.z);
        y = clamp(ceil(y * 255 / MAX_BRIGHTNESS), 1, 255);
        rgb *= 255 * 255 / (y * MAX_BRIGHTNESS);
        uint4 i = (uint4)float4(rgb, y);
        return i.x | (i.y << 8) | (i.z << 16) | (i.w << 24);
    }

    float3 DecodeColor(uint data)
    {
        float r = (data) & 0xff;
        float g = (data >> 8) & 0xff;
        float b = (data >> 16) & 0xff;
        float a = (data >> 24) & 0xff;
        return float3(r, g, b) * a * MAX_BRIGHTNESS / (255 * 255);
    }
    [Button]
    public void Run()
    {
        float3 test = float3(2.324f, 3.1348f, 6.1384f);
        Debug.Log(test);
        uint encoded = EncodeColor(test);
        Debug.Log(encoded);
        Debug.Log(DecodeColor(encoded));
    }
}
