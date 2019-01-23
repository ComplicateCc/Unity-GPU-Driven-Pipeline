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
            Unlit, Forward, GPUDeferred
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
        public static CameraRenderingPath currentRenderingPath { get; private set; }
        private PipelineEvent[] gpurpEvents;
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
            MLight.ClearLightDict();
            this.resources = resources;
            current = this;
            data.buffer = new CommandBuffer();
            data.frustumPlanes = new Vector4[6];
            gpurpEvents = resources.gpurpEvents.GetAllEvents();
            foreach(var i in gpurpEvents)
            {
                i.InitEvent(resources);
            }
        }

        public override void Dispose()
        {
            if (current != this) return;
            current = null;
            data.buffer.Dispose();
            foreach (var i in gpurpEvents)
            {
                i.DisposeEvent();
            }
            PipelineSharedData.DisposeAll();
        }

        public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            foreach(var i in beforeRenderFrame)
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
                Render(pipelineCam, BuiltinRenderTextureType.CameraTarget, ref renderContext, cam);
                PipelineFunctions.ReleaseRenderTarget(data.buffer, ref pipelineCam.targets);
                data.ExecuteCommandBuffer();
            }
            foreach(var i in bufferAfterFrame)
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

        private void Render(PipelineCamera pipelineCam, RenderTargetIdentifier dest, ref ScriptableRenderContext context, Camera cam)
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
            PipelineEvent[] events = null;
            switch (pipelineCam.renderingPath)
            {
                case CameraRenderingPath.GPUDeferred:
                    events = gpurpEvents;
                    break;
            }
            currentRenderingPath = pipelineCam.renderingPath;
            foreach (var e in events) {
                if(e.enabled && e.preEnable)
                {
                    e.PreRenderFrame(pipelineCam, ref data);
                }
            }
            JobHandle.ScheduleBatchedJobs();
            foreach (var e in events)
            {
                if (e.enabled && e.postEnable)
                {
                    e.FrameUpdate(pipelineCam, ref data);
                }
            }
            data.buffer.Blit(pipelineCam.targets.renderTargetIdentifier, dest);
        }
    }
}