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
        public ComputeShader shader;
        public ComputeBuffer buffer;
        [EasyButtons.Button]
        private void Try()
        {
            buffer = new ComputeBuffer(10, sizeof(uint));
            uint* stack = stackalloc uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            NativeArray<uint> ar = new NativeArray<uint>(10, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemCpy(ar.GetUnsafePtr(), stack, 10 * sizeof(uint));
            buffer.SetData(ar);
            int startPos = 1;
            int range = 4;
            int length = 10;
            Vector2Int value = GetOffset(startPos, range, length);
            int[] variables = new int[] {startPos, value.y};
            shader.SetInts("_Variables", variables);
            shader.SetBuffer(0, "_TestBuffer", buffer);
            shader.Dispatch(0, value.x, 1, 1);
            int[] values = new int[length - range];
            buffer.GetData(values);
            Debug.Log("Offset: " + value.x);
            foreach (var i in values)
            {
                Debug.Log(i);
            }
            buffer.Dispose();
        }

        public static Vector2Int GetOffset(int startPos, int range, int length)     //X: dispatch count Y: offset
        {
            int offset = length - (startPos + range);
            return new Vector2Int
            {
                x = Mathf.Min(offset, range),
                y = Mathf.Max(range, offset)
            };
        }
    }
}