using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MPipeline;
using EasyButtons;
public unsafe class Test : MonoBehaviour
{
    delegate void SBFunc(int i);
    [Button]
    public void RunTest()
    {
        SBFunc act = (i) => Debug.Log(i);
        void* ptr = MUnsafeUtility.GetManagedPtr(act);
        act = null;
        System.GC.Collect();
        act = MUnsafeUtility.GetObject<SBFunc>(ptr);
        act(5);
    }
}
