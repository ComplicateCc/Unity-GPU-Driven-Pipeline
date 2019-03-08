using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using MPipeline;
public class Test : MonoBehaviour
{
    [EasyButtons.Button]
    void Run()
    {
        GC.Collect(0, GCCollectionMode.Forced, true, true);
    }
}