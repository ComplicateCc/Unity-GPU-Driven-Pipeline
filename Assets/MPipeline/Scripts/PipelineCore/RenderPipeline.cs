﻿using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
using System.Reflection;
using Unity.Collections;
using Unity.Mathematics;
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
        public static bool renderingEditor { get; private set; }
        private static List<Command> afterRenderFrame = new List<Command>(10);
        private static List<Command> beforeRenderFrame = new List<Command>(10);
        public static PipelineResources.CameraRenderingPath currentPath { get; private set; }
        private static List<Action<CommandBuffer>> bufferAfterFrame = new List<Action<CommandBuffer>>(10);
#if UNITY_EDITOR
        private struct EditorBakeCommand
        {
            public NativeList<float4x4> worldToCamera;
            public NativeList<float4x4> projection;
            public PipelineCamera pipelineCamera;
            public RenderTexture texArray;
            public RenderTexture tempTex;
            public CommandBuffer buffer;
        }
        private static List<EditorBakeCommand> bakeList = new List<EditorBakeCommand>();
        public static void AddRenderingMissionInEditor(NativeList<float4x4> worldToCameras, NativeList<float4x4> projections, PipelineCamera targetCameras, RenderTexture texArray, RenderTexture tempTexture, CommandBuffer buffer)
        {
            bakeList.Add(new EditorBakeCommand
            {
                worldToCamera = worldToCameras,
                projection = projections,
                texArray = texArray,
                pipelineCamera = targetCameras,
                tempTex = tempTexture,
                buffer = buffer
            });
        }
#else
        public static void AddRenderingMissionInEditor(NativeList<float4x4> worldToCameras, NativeList<float4x4> projections, PipelineCamera targetCameras, RenderTexture texArray, RenderTexture tempTexture, CommandBuffer buffer)
        {
        //Shouldn't do anything in runtime
        }
#endif
        public static T GetEvent<T>() where T : PipelineEvent
        {
            var events = current.resources.availiableEvents;
            for (int i = 0; i < events.Length; ++i)
            {
                PipelineEvent evt = events[i];
                if (evt.GetType() == typeof(T)) return (T)evt;
            }
            return null;
        }

        public static PipelineEvent GetEvent(Type type)
        {
            var events = current.resources.availiableEvents;
            for (int i = 0; i < events.Length; ++i)
            {
                PipelineEvent evt = events[i];
                if (evt.GetType() == type) return evt;
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

        public static void ExecuteBufferAtFrameEnding(Action<CommandBuffer> buffer)
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
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].Prepare();
            }
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].InitEvent(resources);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
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
            foreach (var i in PipelineCamera.allCamera)
            {
                PipelineCamera cam = MUnsafeUtility.GetObject<PipelineCamera>(i.ToPointer());
                var values = cam.allDatas.Values;
                foreach (var j in values)
                {
                    j.DisposeProperty();
                }
                cam.allDatas.Clear();
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
            GraphicsSettings.useScriptableRenderPipelineBatching = resources.useSRPBatcher;
            foreach (var i in beforeRenderFrame)
            {
                i.func(i.obj);
            }
            beforeRenderFrame.Clear();
            SceneController.SetState();
#if UNITY_EDITOR
            foreach (var pair in bakeList)
            {
                PipelineCamera pipelineCam = pair.pipelineCamera;
                for (int i = 0; i < pair.worldToCamera.Length; ++i)
                {
                    pipelineCam.cam.worldToCameraMatrix = pair.worldToCamera[i];
                    pipelineCam.cam.projectionMatrix = pair.projection[i];
                    Render(pipelineCam, ref renderContext, pipelineCam.cam, propertyCheckedFlags);
                    data.buffer.Blit(pipelineCam.targets.renderTargetIdentifier, pair.tempTex);
                    PipelineFunctions.ReleaseRenderTarget(data.buffer, ref pipelineCam.targets);
                    data.buffer.CopyTexture(pair.tempTex, 0, 0, pair.texArray, i, 0);
                    data.ExecuteCommandBuffer();
                    renderContext.Submit();
                }
                pair.worldToCamera.Dispose();
                pair.projection.Dispose();
                renderContext.ExecuteCommandBuffer(pair.buffer);
                pair.buffer.Clear();
                renderContext.Submit();
            }
            bakeList.Clear();
#endif

            if (!PipelineCamera.allCamera.isCreated) return;

            foreach (var cam in cameras)
            {
                PipelineCamera pipelineCam;
                UIntPtr pipelineCamPtr;
                if (!PipelineCamera.allCamera.Get(cam.gameObject.GetInstanceID(), out pipelineCamPtr))
                {
#if UNITY_EDITOR
                    renderingEditor = true;
                    if (!PipelineCamera.allCamera.Get(Camera.main.gameObject.GetInstanceID(), out pipelineCamPtr))
                        continue;
#else
                    continue;
#endif
                }
                else
                {
                    renderingEditor = false;
                }
                pipelineCam = MUnsafeUtility.GetObject<PipelineCamera>(pipelineCamPtr.ToPointer());
                Render(pipelineCam, ref renderContext, cam, propertyCheckedFlags);
                PipelineFunctions.ReleaseRenderTarget(data.buffer, ref pipelineCam.targets);
                data.ExecuteCommandBuffer();
                renderContext.Submit();
            }
            foreach (var i in afterRenderFrame)
            {
                i.func(i.obj);
            }
            afterRenderFrame.Clear();
            if (bufferAfterFrame.Count > 0)
            {
                foreach (var i in bufferAfterFrame)
                {
                    i(data.buffer);
                }
                data.ExecuteCommandBuffer();
                bufferAfterFrame.Clear();
                renderContext.Submit();
            }
        }

        private void Render(PipelineCamera pipelineCam, ref ScriptableRenderContext context, Camera cam, bool* pipelineChecked)
        {
            PipelineResources.CameraRenderingPath path = pipelineCam.renderingPath;
            currentPath = path;
            pipelineCam.cam = cam;
            pipelineCam.EnableThis(resources);
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
            var collect = allEvents[(int)path];
#if UNITY_EDITOR
            //Need only check for Unity Editor's bug!
            if (!pipelineChecked[(int)path])
            {
                pipelineChecked[(int)path] = true;
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
            data.buffer.SetInvertCulling(false);
        }
    }
}