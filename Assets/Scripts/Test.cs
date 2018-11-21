using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading.Tasks;
using System;
public unsafe class Test : MonoBehaviour
{
    [EasyButtons.Button]
    void TestThis()
    {
        ComputeBuffer bf = new ComputeBuffer(6, 4);
        int[] a = new int[] { 1, 2, 3 };
        bf.SetData(a, 0, 0, 3);
        bf.SetData(a, 0, 3, 3);
        int[] b = new int[6];
        bf.GetData(b);
        foreach(var i in b)
        {
            Debug.Log(i);
        }
    }
}
