using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
public unsafe class Test : MonoBehaviour
{
    public struct SB : ITest
    {
        public Matrix4x4 mat;
        public Matrix4x4 mat0;
        public Matrix4x4 mat1;
        public Matrix4x4 mat2;
        public Matrix4x4 mat3;
        public void Execute()
        {
            
        }
    }

    private void RunTest(ITest tst)
    {
        tst.Execute();
    }

    private void Update()
    {
        SB sb = new SB();
        for (int i = 0; i < 10000; ++i)
        {
            RunTest(sb);
        }
    }

}
public interface ITest
{
    void Execute();
}
