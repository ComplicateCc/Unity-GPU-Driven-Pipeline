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
        public int value1;
        public int value2;
        [EasyButtons.Button]
        void Try()
        {
            Debug.Log(value1 ^ value2);
            
        }
    }

}