using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using MPipeline;

public unsafe class Test : MonoBehaviour
{
    public int fuck = 3;
    [EasyButtons.Button]
    void RunTest()
    {
        void* ptr = MUnsafeUtility.GetManagedPtr(this);
        Test t = MUnsafeUtility.GetObject<Test>(ptr);
        Debug.Log(fuck);
    }
}
