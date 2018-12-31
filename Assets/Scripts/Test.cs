using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class Test : MonoBehaviour
{
    public int i = 5;
    public void Update()
    {
        for (int a = 0; a < 100000; ++a)
        {
            i = 5;
            Action<int> act = (i) =>
            {
                
                i++;
            };
            act(i);
        }
    }
}
