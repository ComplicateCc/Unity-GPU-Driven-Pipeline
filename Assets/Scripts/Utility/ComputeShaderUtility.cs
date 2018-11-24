using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class ComputeShaderUtility
{
    public static uint[] zero = new uint[] { 0 };
    public static void Dispatch(ComputeShader shader, CommandBuffer buffer, int kernal, int count, float threadGroupCount)
    {
        int threadPerGroup = Mathf.CeilToInt(count / threadGroupCount);
        buffer.SetComputeIntParam(shader, ShaderIDs._Count, count);
        buffer.DispatchCompute(shader, kernal, threadPerGroup, 1, 1);
    }
}
public unsafe static class NativeArrayUtility
{
    public static T* Ptr<T>(ref this NativeArray<T> arr) where T : unmanaged
    {
        return (T*)arr.GetUnsafePtr();
    }
    
    public static ref T Get<T>(ref this NativeArray<T> arr, int index) where T : unmanaged
    {
        return ref *((T*)arr.GetUnsafePtr() + index);
    }

    public static void CopyFrom<T>(this T[] array, T* source, int length) where T : unmanaged
    {
        fixed(T* dest = array)
        {
            UnsafeUtility.MemCpy(dest, source, length * sizeof(T));
        }
    }

    public static void CopyTo<T>(this T[] array, T* dest, int length) where T : unmanaged
    {
        fixed (T* source = array)
        {
            UnsafeUtility.MemCpy(dest, source, length * sizeof(T));
        }
    }
}