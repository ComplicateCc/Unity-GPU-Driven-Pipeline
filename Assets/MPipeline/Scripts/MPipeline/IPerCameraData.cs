using System;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe abstract class IPerCameraData
    {
        public static T GetProperty<T>(PipelineCamera camera, Func<T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data;
            if(!camera.allDatas.TryGetValue(evt, out data))
            {
                data = initFunc();
                camera.allDatas.Add(evt, data);
            }
            return (T)data;
        }

        public static void RemoveProperty<T>(PipelineCamera camera, PipelineEvent evt)
        {
            IPerCameraData data = camera.allDatas[evt];
            if (data != null)
            {
                data.DisposeProperty();
            }
            data = null;
        }

        public static T GetProperty<T>(PipelineCamera camera, Func<PipelineCamera, T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.allDatas.TryGetValue(evt, out data))
            {
                data = initFunc(camera);
                camera.allDatas.Add(evt, data);
            }
            return (T)data;
        }

        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineCamera, PipelineResources, T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.allDatas.TryGetValue(evt, out data))
            {
                data = initFunc(camera,resource);
                camera.allDatas.Add(evt, data);
            }
            return (T)data;
        }
        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineResources, T> initFunc, PipelineEvent evt) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.allDatas.TryGetValue(evt, out data))
            {
                data = initFunc(resource);
                camera.allDatas.Add(evt, data);
            }
            return (T)data;
        }
        public abstract void DisposeProperty();
    }
}
