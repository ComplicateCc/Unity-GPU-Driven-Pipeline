using UnityEngine;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using MPipeline;
using UnityEngine.Rendering;
public unsafe sealed class Test : MonoBehaviour
{
    public double number;
    [EasyButtons.Button]
    void Run()
    {
        float* pt = (float*)AddressOf(ref number);
        Debug.Log(*pt);
    }
}

