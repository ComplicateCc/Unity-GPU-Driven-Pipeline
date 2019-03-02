using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
using System.Reflection;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe sealed class RenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        #region STATIC_AREA

        public static RenderPipeline current;
        public static PipelineCommandData data;
        #endregion
        private struct Command
        {
            public object obj;
            public Action<object> func;
        }
        public PipelineResources resources;
        private static List<Command> afterRenderFrame = new List<Command>(10);
        private static List<Command> beforeRenderFrame = new List<Command>(10);
        private static List<CommandBuffer> bufferAfterFrame = new List<CommandBuffer>(10);
        public static T GetEvent<T>(PipelineResources.CameraRenderingPath path) where T : PipelineEvent
        {
            var allEvents = current.resources.allEvents;
            PipelineEvent[] events = allEvents[(int)path];
            for (int i = 0; i < events.Length; ++i)
            {
                PipelineEvent evt = events[i];
                if (evt.GetType() == typeof(T)) return (T)evt;
            }
            return null;
        }

        public static PipelineEvent GetEvent(PipelineResources.CameraRenderingPath path, Type targetType)
        {
            var allEvents = current.resources.allEvents;
            PipelineEvent[] events = allEvents[(int)path];
            for (int i = 0; i < events.Length; ++i)
            {
                PipelineEvent evt = events[i];
                if (evt.GetType() == targetType) return evt;
            }
            return null;
        }
        public static void AddCommandAfterFrame(object arg, Action<object> func)
        {
            afterRenderFrame.Add(new Command
            {
                func = func,
                obj = arg
            });
        }
        public static void ExecuteBufferAtFrameEnding(CommandBuffer buffer)
        {
            bufferAfterFrame.Add(buffer);
        }
        public static void AddCommandBeforeFrame(object arg, Action<object> func)
        {
            beforeRenderFrame.Add(new Command
            {
                func = func,
                obj = arg
            });
        }

        public RenderPipeline(PipelineResources resources)
        {
            resources.SetRenderingPath();
            var allEvents = resources.allEvents;
            GraphicsUtility.UpdatePlatform();
            MLight.ClearLightDict();
            this.resources = resources;
            current = this;
            data.buffer = new CommandBuffer();
            data.frustumPlanes = new Vector4[6];

            for (int i = 0; i < allEvents.Length; ++i)
            {
                PipelineEvent[] events = allEvents[i];
                if (events == null) continue;
                foreach (var j in events)
                {
                    j.Prepare((PipelineResources.CameraRenderingPath)i);
                }
                foreach (var j in events)
                {
                    j.InitEvent(resources);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (current != this) return;
            current = null;
            data.buffer.Dispose();
            var allEvents = resources.allEvents;
            foreach (var i in allEvents)
            {
                if (i != null)
                {
                    foreach (var j in i)
                    {
                        j.DisposeEvent();
                    }
                }
            }
        }
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            bool* propertyCheckedFlags = stackalloc bool[]
            {
                false,
                false,
                false
            };
            foreach (var i in beforeRenderFrame)
            {
                i.func(i.obj);
            }
            beforeRenderFrame.Clear();
            SceneController.SetState();
            foreach (var cam in cameras)
            {
                PipelineCamera pipelineCam = cam.GetComponent<PipelineCamera>();
                if (!pipelineCam)
                {
                    pipelineCam = Camera.main.GetComponent<PipelineCamera>();
                    if (!pipelineCam) continue;
                }
                Render(pipelineCam, BuiltinRenderTextureType.CameraTarget, ref renderContext, cam, propertyCheckedFlags);
                PipelineFunctions.ReleaseRenderTarget(data.buffer, ref pipelineCam.targets);
                data.ExecuteCommandBuffer();
                renderContext.Submit();
            }
            foreach (var i in bufferAfterFrame)
            {
                renderContext.ExecuteCommandBuffer(i);
                i.Clear();
            }
            bufferAfterFrame.Clear();
            foreach (var i in afterRenderFrame)
            {
                i.func(i.obj);
            }
            afterRenderFrame.Clear();
            renderContext.Submit();
        }

        private void Render(PipelineCamera pipelineCam, RenderTargetIdentifier dest, ref ScriptableRenderContext context, Camera cam, bool* pipelineChecked)
        {
            PipelineResources.CameraRenderingPath path = pipelineCam.renderingPath;
            pipelineCam.cam = cam;
            pipelineCam.EnableThis();
            if (!cam.TryGetCullingParameters(out data.cullParams)) return;
            context.SetupCameraProperties(cam);
            //Set Global Data
            data.context = context;
            data.cullParams.reflectionProbeSortingCriteria = ReflectionProbeSortingCriteria.ImportanceThenSize;
            data.cullResults = context.Cull(ref data.cullParams);

            PipelineFunctions.InitRenderTarget(ref pipelineCam.targets, cam, data.buffer);
            data.resources = resources;
            PipelineFunctions.GetViewProjectMatrix(cam, out data.vp, out data.inverseVP);
            for (int i = 0; i < data.frustumPlanes.Length; ++i)
            {
                Plane p = data.cullParams.GetCullingPlane(i);
                //GPU Driven RP's frustum plane is inverse from SRP's frustum plane
                data.frustumPlanes[i] = new Vector4(-p.normal.x, -p.normal.y, -p.normal.z, -p.distance);
            }
            var allEvents = resources.allEvents;
            var collect = allEvents[(int)pipelineCam.renderingPath];
#if UNITY_EDITOR
            //Need only check for Unity Editor's bug!
            if (!pipelineChecked[(int)pipelineCam.renderingPath])
            {
                pipelineChecked[(int)pipelineCam.renderingPath] = true;
                foreach (var e in collect)
                {
                    if (!e.CheckProperty())
                    {
                        e.CheckInit(resources);
                    }
                }
            }
#endif
            data.buffer.SetInvertCulling(pipelineCam.inverseRender);
            foreach (var e in collect)
            {
                if (e.Enabled)
                {
                    e.PreRenderFrame(pipelineCam, ref data);
                }
            }
            JobHandle.ScheduleBatchedJobs();
            foreach (var e in collect)
            {
                if (e.Enabled)
                {
                    e.FrameUpdate(pipelineCam, ref data);
                }
            }
            data.buffer.Blit(pipelineCam.targets.renderTargetIdentifier, dest);
        }
    }
}