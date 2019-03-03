using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
public class TestEventAttribute : Attribute
{
    
}
public unsafe class Test : MonoBehaviour
{
    [EasyButtons.Button]
    void RunTest()
    {
    }
}
