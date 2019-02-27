using System;
namespace MPipeline
{
    public abstract class IPerCameraData
    {
        public static T GetProperty<T>(PipelineCamera camera, Func<T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.postDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc();
                camera.postDatas.Add(typeof(T), data);
            }
            return (T)data;
        }

        public static bool GetProperty<T>(PipelineCamera camera, out T data) where T : IPerCameraData
        {
            IPerCameraData camData;
            bool b = camera.postDatas.TryGetValue(typeof(T), out camData);
            data = (T)camData;
            return b;
        }

        public static void RemoveProperty<T>(PipelineCamera camera)
        {
            camera.postDatas.Remove(typeof(T));
        }

        public static T GetProperty<T>(PipelineCamera camera, Func<PipelineCamera, T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.postDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc(camera);
                camera.postDatas.Add(typeof(T), data);
            }
            return (T)data;
        }

        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineCamera, PipelineResources, T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.postDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc(camera, resource);
                camera.postDatas.Add(typeof(T), data);
            }
            return (T)data;
        }
        public static T GetProperty<T>(PipelineCamera camera, PipelineResources resource, Func<PipelineResources, T> initFunc) where T : IPerCameraData
        {
            IPerCameraData data;
            if (!camera.postDatas.TryGetValue(typeof(T), out data))
            {
                data = initFunc(resource);
                camera.postDatas.Add(typeof(T), data);
            }
            return (T)data;
        }
        public abstract void DisposeProperty();
    }
}
