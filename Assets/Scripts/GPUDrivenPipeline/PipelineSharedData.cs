using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace MPipeline
{
    public class PipelineSharedData
    {
        private struct EventType
        {
            public RenderPipeline.CameraRenderingPath path;
            public Type eventType;
            public override int GetHashCode()
            {
                return eventType.GetHashCode() ^ path.GetHashCode();
            }
        }
        private static Dictionary<EventType, PipelineSharedData> allEvents = new Dictionary<EventType, PipelineSharedData>();

        public static void Add<T>(RenderPipeline.CameraRenderingPath path, T evt) where T : PipelineSharedData
        {
            allEvents.Add(new EventType { path = path, eventType = typeof(T) }, evt);
        }

        public static void Remove<T>(RenderPipeline.CameraRenderingPath path)
        {
            allEvents.Remove(new EventType { path = path, eventType = typeof(T) });
        }

        public static T Get<T>(RenderPipeline.CameraRenderingPath path, Func<T> getFunc) where T : PipelineSharedData
        {
            PipelineSharedData evt;
            if(allEvents.TryGetValue(new EventType { path = path, eventType = typeof(T) }, out evt))
            {
                return (T)evt;
            }
            else
            {
                evt = getFunc();
                allEvents.Add(new EventType { path = path, eventType = typeof(T) }, evt);
                return (T)evt;
            }
        }
        public static T Get<T, Arg>(RenderPipeline.CameraRenderingPath path, Arg arg, Func<Arg, T> getFunc) where T : PipelineSharedData
        {
            PipelineSharedData evt;
            if (allEvents.TryGetValue(new EventType { path = path, eventType = typeof(T) }, out evt))
            {
                return (T)evt;
            }
            else
            {
                evt = getFunc(arg);
                allEvents.Add(new EventType { path = path, eventType = typeof(T) }, evt);
                return (T)evt;
            }
        }
    }
}

