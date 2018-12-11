using UnityEngine;
using System;
using System.Reflection;
using UnityEditor;
using Unity.Collections;
using System.Threading.Tasks;
namespace MPipeline
{
    public unsafe class Test : MonoBehaviour
    {
        private int value = 2;
        public Material tex;
        private NativeArray<int> inf;
        [EasyButtons.Button]
        void Try()
        {
            Task t = Task.Run(() =>
            {
                Debug.Log(inf.Length);
                inf = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Debug.Log(inf.Length);
                inf.Dispose();
            });
            
        }
    }

}