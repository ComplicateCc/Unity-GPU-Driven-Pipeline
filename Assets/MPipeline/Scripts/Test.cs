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
    /*    public UnityEngine.UI.Text txt;
        float deltaAcc = 0;
        float count = 0;
        private void Update()
        {
            deltaAcc += Time.deltaTime;
            count++;
            if (count >= 20)
            {
                deltaAcc /= count;
                txt.text = (deltaAcc * 1000).ToString();
                count = 0;
                deltaAcc = 0;
            }
        }*/
    private float initHeight;
    private void Start()
    {
        initHeight = transform.position.y;
    }
    private void Update()
    {
        transform.position = new Vector3(transform.position.x, initHeight + Mathf.Sin(Time.time * 50), transform.position.z);
    }
}