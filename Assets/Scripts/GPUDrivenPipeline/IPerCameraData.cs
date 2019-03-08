using System;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe abstract class IPerCameraData
    {
        public static T GetProperty<T>(PipelineCamera camera, Func<T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data = camera.allDatas[evt.EventPosition];
            if (data == null)
            {
                data = initFunc();
                camera.allDatas[evt.EventPosition] = data;
            }
            return (T)data;
        }

        public static void RemoveProperty<T>(PipelineCamera camera, PipelineEvent evt)
        {
            ref IPerCameraData data = ref camera.allDatas[evt.EventPosition];
            if (data != null)
            {
                data.DisposeProperty();
            }
            data = null;
        }

        public static T GetProperty<T>(PipelineCamera camera, Func<PipelineCamera, T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data = camera.allDatas[evt.EventPosition];
            if (data == null)
            {
                data = initFunc(camera);
                camera.allDatas[evt.EventPosition] = data;
            }
            return (T)data;
        }

        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineCamera, PipelineResources, T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data = camera.allDatas[evt.EventPosition];
            if (data == null)
            {
                data = initFunc(camera,resource);
                camera.allDatas[evt.EventPosition] = data;
            }
            return (T)data;
        }
        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineResources, T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data = camera.allDatas[evt.EventPosition];
            if (data == null)
            {
                data = initFunc(resource);
                camera.allDatas[evt.EventPosition] = data;
            }
            return (T)data;
        }
        public abstract void DisposeProperty();
    }
}
