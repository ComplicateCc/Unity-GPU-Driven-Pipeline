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

    public UnityEngine.UI.Text txt;
    /*  float deltaAcc = 0;
      float count = 0;
      private void Update()
      {
          deltaAcc += Time.deltaTime;
          count++;
          if(count >= 20)
          {
              deltaAcc /= count;
              txt.text = (deltaAcc * 1000).ToString();
              count = 0;
              deltaAcc = 0;
          }
      }
      */
    public ComputeShader shader;
    [EasyButtons.Button]
    void Run()
    {
        ComputeBuffer bf = new ComputeBuffer(1024, 4);
        for (int a = 0; a < 100; ++a)
        {
           
            float[] sb = new float[1024];
            for (int i = 0; i < 1024; ++i)
            {
                sb[i] = 1;
            }
            bf.SetData(sb);
            shader.SetBuffer(0, "_Buffer", bf);
            shader.Dispatch(0, 1, 6, 1);
            bf.GetData(sb, 0, 0, 1);
            if (sb[0] != 1024) Debug.Log(sb[0]);
            
        }
        Debug.Log("Test Finished");
        bf.Dispose();
    }
}