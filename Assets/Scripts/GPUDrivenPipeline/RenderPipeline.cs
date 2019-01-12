using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
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
        public static Dictionary<CameraRenderingPath, DrawEvent> allDrawEvents = new Dictionary<CameraRenderingPath, DrawEvent>();
        #endregion
        private struct Command
        {
            public object obj;
            public Action<object> func;
        }
        private GameObject pipelinePrefab;
        private PipelineEvent[] allEvents;
        public PipelineResources resources;
        private static List<Command> afterRenderFrame = new List<Command>(10);
        private static List<Command> beforeRenderFrame = new List<Command>(10);
        public static void AddCommandAfterFrame(object arg, Action<object> func)
        {
            afterRenderFrame.Add(new Command
            {
                func = func,
                obj = arg
            });
        }
        public static void AddCommandBeforeFrame(object arg, Action<object> func)
        {
            beforeRenderFrame.Add(new Command
            {
                func = func,
                obj = arg
            });
        }
        public RenderPipeline(GameObject pipelinePrefab, PipelineResources resources)
        {
            allDrawEvents.Clear();
            MLight.ClearLightDict();
            this.pipelinePrefab = pipelinePrefab;
            this.resources = resources;
            current = this;
            data.buffer = new CommandBuffer();
            data.frustumPlanes = new Vector4[6];
            allEvents = pipelinePrefab.GetComponentsInChildren<PipelineEvent>();
            foreach (var i in allEvents)
                i.InitEvent(resources);
        }

        public override void Dispose()
        {
            if (current != this) return;
            current = null;

            foreach (var i in allEvents)
                i.DisposeEvent();
            allEvents = null;
            data.buffer.Dispose();
            PipelineSharedData.DisposeAll();
        }

        public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            foreach(var i in beforeRenderFrame)
            {
                i.func(i.obj);
            }
            beforeRenderFrame.Clear();
            SceneController.SetSingleton();
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
                renderContext.Submit();
            }
            foreach (var i in afterRenderFrame)
            {
                i.func(i.obj);
            }
            afterRenderFrame.Clear();
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
            DrawEvent evt;
            if (allDrawEvents.TryGetValue(path, out evt))
            {
                //Pre Calculate Events
                foreach (var i in evt.preRenderEvents)
                {
                    i.PreRenderFrame(pipelineCam, ref data);
                }
                //Run job system together
                JobHandle.ScheduleBatchedJobs();
                //Start Prepare Render Targets
                //Frame Update Events
                foreach (var i in evt.drawEvents)
                {
                    i.FrameUpdate(pipelineCam, ref data);
                }
            }
            data.buffer.Blit(pipelineCam.targets.renderTargetIdentifier, dest);
            data.ExecuteCommandBuffer();
        }
    }
    public struct DrawEvent
    {
        public List<PipelineEvent> drawEvents;
        public List<PipelineEvent> preRenderEvents;
        public DrawEvent(int capacity)
        {
            drawEvents = new List<PipelineEvent>(capacity);
            preRenderEvents = new List<PipelineEvent>(capacity);
        }
    }
}