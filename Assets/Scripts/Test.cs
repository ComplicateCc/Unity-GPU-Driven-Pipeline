using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Diagnostics;
public unsafe class Test : MonoBehaviour
{
    public static int value = 0;
    public class Base
    {
        public virtual void run()
        {
            value++;
        }
    }
    public class NewClass : Base
    {
        public override void run()
        {
            value++;
        }
    }
    public System.Action act = () => { value++; };
    public static Base bs = new NewClass();
    private void Update()
    {
        Stopwatch watch = new Stopwatch();
        watch.Start();
        for (int i = 0; i < 10000000; ++i)
        {
            //bs.run();
           act();
        }
        watch.Stop();
        UnityEngine.Debug.Log(watch.ElapsedTicks);
    }
}
