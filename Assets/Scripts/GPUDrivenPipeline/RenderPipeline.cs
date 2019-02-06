using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
using System.Reflection;
using UnityEngine.Experimental.Rendering;
using RenderPipeline = MPipeline.RenderPipeline;
namespace MPipeline
{
    public unsafe class RenderPipeline : UnityEngine.Experimental.Rendering.RenderPipeline
    {
        #region STATIC_AREA
        public enum CameraRenderingPath
        {
            Unlit = 0, Forward = 1, GPUDeferred = 2
        }
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
        private static Dictionary<CameraRenderingPath, PipelineEventsCollection> allEvents;
        public static T GetEvent<T>(CameraRenderingPath path) where T : PipelineEvent
        {
            List<PipelineEvent> events = allEvents[path].allEvents;
            for (int i = 0; i < events.Count; ++i)
            {
                PipelineEvent evt = events[i];
                if (evt.GetType() == typeof(T)) return (T)evt;
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
            GraphicsUtility.UpdatePlatform();
            MLight.ClearLightDict();
            this.resources = resources;
            current = this;
            data.buffer = new CommandBuffer();
            data.frustumPlanes = new Vector4[6];
            allEvents = resources.GetAllEvents();
            var keys = allEvents.Keys;
            foreach (var i in keys)
            {
                List<PipelineEvent> events = allEvents[i].allEvents;
                foreach (var j in events)
                {
                    j.InitEvent(resources, i);
                }
            }
        }

        public override void Dispose()
        {
            if (current != this) return;
            current = null;
            data.buffer.Dispose();
            var values = allEvents.Values;
            foreach (var i in values)
            {
                List<PipelineEvent> allEvents = i.allEvents;
                foreach (var j in allEvents)
                {
                    j.DisposeEvent();
                }
            }
        }
        public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
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
            CameraRenderingPath path = pipelineCam.renderingPath;
            pipelineCam.cam = cam;
            pipelineCam.EnableThis();
            if (!CullResults.GetCullingParameters(cam, out data.cullParams)) return;
            context.SetupCameraProperties(cam);
            //Set Global Data
            data.defaultDrawSettings = new DrawRendererSettings(cam, new ShaderPassName(""));
            data.context = context;
            data.cullResults = CullResults.Cull(ref data.cullParams, context);

            PipelineFunctions.InitRenderTarget(ref pipelineCam.targets, cam, data.buffer);
            data.resources = resources;
            PipelineFunctions.GetViewProjectMatrix(cam, out data.vp, out data.inverseVP);
            for (int i = 0; i < data.frustumPlanes.Length; ++i)
            {
                Plane p = data.cullParams.GetCullingPlane(i);
                //GPU Driven RP's frustum plane is inverse from SRP's frustum plane
                data.frustumPlanes[i] = new Vector4(-p.normal.x, -p.normal.y, -p.normal.z, -p.distance);
            }
            PipelineEventsCollection collect = allEvents[pipelineCam.renderingPath];
#if UNITY_EDITOR
            //Need only check for Unity Editor's bug!
            if (!pipelineChecked[(int)pipelineCam.renderingPath])
            {
                pipelineChecked[(int)pipelineCam.renderingPath] = true;
                foreach (var e in collect.allEvents)
                {
                    if (!e.CheckProperty())
                    {
                        e.Init(resources);
                    }
                }
            }
#endif
            foreach (var e in collect.preEvents)
            {
                if (e.enabled)
                {
                    e.PreRenderFrame(pipelineCam, ref data);
                }
            }
            JobHandle.ScheduleBatchedJobs();
            foreach (var e in collect.postEvents)
            {
                if (e.enabled)
                {
                    e.FrameUpdate(pipelineCam, ref data);
                }
            }
            data.buffer.Blit(pipelineCam.targets.renderTargetIdentifier, dest);
        }
    }
}