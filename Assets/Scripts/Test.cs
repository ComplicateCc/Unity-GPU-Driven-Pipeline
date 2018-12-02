using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe class Test : MonoBehaviour
    {
        NativeArray<int> values;
        private void Start()
        {
            values = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
        private void OnDestroy()
        {
            values.Dispose();
        }
        public void Update()
        {
            if(Input.GetKeyDown(KeyCode.Space))
            {
               
                LoadingThread.AddCommand(() => { Debug.Log(values[0]); });
                
            }
        }
    }
}