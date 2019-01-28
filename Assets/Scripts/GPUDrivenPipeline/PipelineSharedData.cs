using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace MPipeline
{
    public abstract class PipelineSharedData
    {
        public static void DisposeAll()
        {
            foreach(var i in allEvents)
            {
                i.value.Dispose();
            }
            allEvents.Clear();
        }
        
        private static List<Pair<RenderPipeline.CameraRenderingPath, PipelineSharedData>> allEvents = new List<Pair<RenderPipeline.CameraRenderingPath, PipelineSharedData>>(10);
        public static bool Get<T>(RenderPipeline.CameraRenderingPath path, out T value) where T: PipelineSharedData
        {
            foreach(var i in allEvents)
            {
                if(i.key == path && i.value.GetType() == typeof(T))
                {
                    value = i.value as T;
                    return true;
                }
            }
            value = null;
            return false;
        }
        public static void Remove<T>(RenderPipeline.CameraRenderingPath path) where T : PipelineSharedData
        {
            int index = -1;
            for(int i = 0; i < allEvents.Count; ++i)
            {
                var e = allEvents[i];
                if (e.key == path && e.value.GetType() == typeof(T))
                {
                    index = i;
                    break;
                }
            }
            if(index >= 0)
            {
                allEvents.RemoveAt(index);
            }
        }
        public static T Get<T>(RenderPipeline.CameraRenderingPath path, Func<T> getFunc) where T : PipelineSharedData
        {
            foreach (var i in allEvents)
            {
                if (i.key == path && i.value.GetType() == typeof(T))
                {
                    return i.value as T;
                }
            }
            T val = getFunc();
            allEvents.Add(new Pair<RenderPipeline.CameraRenderingPath, PipelineSharedData>
            {
                key = path,
                value = val
            });
            return val;
        }
        public static T Get<T, Arg>(RenderPipeline.CameraRenderingPath path, Arg arg, Func<Arg, T> getFunc) where T : PipelineSharedData
        {
            foreach (var i in allEvents)
            {
                if (i.key == path && i.value.GetType() == typeof(T))
                {
                    return i.value as T;
                }
            }
            T val = getFunc(arg);
            allEvents.Add(new Pair<RenderPipeline.CameraRenderingPath, PipelineSharedData>
            {
                key = path,
                value = val
            });
            return val;
        }
        public abstract void Dispose();
    }
}

