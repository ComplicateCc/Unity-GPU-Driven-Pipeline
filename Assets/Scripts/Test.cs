using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using System.Threading;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using static Unity.Mathematics.math;
using UnityEngine.Rendering;
using MPipeline;
public unsafe class Test : MonoBehaviour
{
    public struct TestJob : IJobParallelFor
    {
        public int index;
        public void Execute(int index)
        {
            Interlocked.Add(ref this.index, index);
        }
    }
    [Button]
    public void Run()
    {
        TestJob job = new TestJob
        {
            index = 0
        };
        var handle = job.ScheduleRefBurst(100, 32);
        handle.Complete();
        Debug.Log(job.index);
    }
}
