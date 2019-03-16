using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Post Processing")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class FinalPostEvent : PipelineEvent
    {
        struct PostEffect
        {
            public PostProcessEffectRenderer renderer;
            public bool needBlit;
        }
        public PostProcessProfile profile;
        public PostProcessResources resources;
        public bool enableInEditor = true;
        private Dictionary<Type, PostEffect> allEvents;
        private PostProcessRenderContext postContext;
        private MotionBlurRenderer motionBlurRenderer;

        T AddEvents<S, T>(bool useBlit = false) where T : PostProcessEffectRenderer, new()
        {
            T renderer = new T();
            renderer.Init();
            allEvents.Add(typeof(S), new PostEffect { renderer = renderer, needBlit = useBlit });
            return renderer;
        }
        protected override void Init(PipelineResources res)
        {
            allEvents = new Dictionary<Type, PostEffect>(7);
            AddEvents<MotionBlur, MotionBlurRenderer>(true);
            AddEvents<LensDistortion, LensDistortionRenderer>();
            AddEvents<ChromaticAberration, ChromaticAberrationRenderer>();
            AddEvents<Bloom, BloomRenderer>();
            AddEvents<AutoExposure, AutoExposureRenderer>();
            AddEvents<ColorGrading, ColorGradingRenderer>();
            postContext = new PostProcessRenderContext();
            postContext.Reset();
            postContext.propertySheets = new PropertySheetFactory();
            postContext.resources = resources;
            postContext.logHistogram = new LogHistogram();
            postContext.uberSheet = new PropertySheet(new Material(resources.shaders.uber));
            Shader.SetGlobalFloat("_RenderViewportScaleFactor", 1);
            motionBlurRenderer = new MotionBlurRenderer();
        }

        public override bool CheckProperty()
        {
            return postContext != null && postContext.uberSheet.material != null;
        }

        protected override void Dispose()
        {
            var values = allEvents.Values;
            foreach (var i in values)
            {
                i.renderer.Release();
            }
            postContext.uberSheet.Release();
            postContext.logHistogram.Release();
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
#if UNITY_EDITOR
            if (!enableInEditor && RenderPipeline.renderingEditor)
            {
                data.buffer.Blit(cam.targets.renderTargetIdentifier, BuiltinRenderTextureType.CameraTarget);
                return;
            }
#endif
            postContext.camera = cam.cam;
            postContext.command = data.buffer;
            postContext.bloomBufferNameID = -1;
            postContext.sourceFormat = RenderTextureFormat.ARGBHalf;
            var settings = profile.settings;
            postContext.autoExposureTexture = RuntimeUtilities.whiteTexture;
            postContext.bloomBufferNameID = -1;
            RenderTargetIdentifier source, dest;
            postContext.source = cam.targets.renderTargetIdentifier;
            postContext.destination = cam.targets.backupIdentifier;
            postContext.logHistogram.Generate(postContext);
            foreach (var i in settings)
            {
                PostEffect ef;
                if (allEvents.TryGetValue(i.GetType(), out ef))
                {
                    if (ef.needBlit && i.active)
                    {
                        PipelineFunctions.RunPostProcess(ref cam.targets, out source, out dest);
                        postContext.source = source;
                        postContext.destination = dest;
                    }
                    ef.renderer.SetSettings(i);
                    ef.renderer.Render(postContext);
                }
            };
            // data.buffer.Blit(cam.targets.renderTargetIdentifier, BuiltinRenderTextureType.CameraTarget);
            data.buffer.BlitSRT(cam.targets.renderTargetIdentifier, BuiltinRenderTextureType.CameraTarget, postContext.uberSheet.material, 0, postContext.uberSheet.properties);
            if (postContext.bloomBufferNameID > -1) data.buffer.ReleaseTemporaryRT(postContext.bloomBufferNameID);
        }
    }
}