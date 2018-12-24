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
            allEvents = null;
        }

        private static List<Pair<RenderPipeline.CameraRenderingPath, PipelineSharedData>> allEvents = new List<Pair<RenderPipeline.CameraRenderingPath, PipelineSharedData>>();
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

