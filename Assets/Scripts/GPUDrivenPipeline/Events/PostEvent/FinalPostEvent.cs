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
        public PostProcessProfile profile;
        public PostProcessResources resources;
        public Dictionary<Type, PostProcessEffectRenderer> allEvents;
        private PostProcessRenderContext postContext;

        T AddEvents<S, T>() where T : PostProcessEffectRenderer, new()
        {
            T renderer = new T();
            renderer.Init();
            allEvents.Add(typeof(S), renderer);
            return renderer;
        }
        protected override void Init(PipelineResources res)
        {
            allEvents = new Dictionary<Type, PostProcessEffectRenderer>(7);
            AddEvents<ColorGrading, ColorGradingRenderer>();
            AddEvents<AutoExposure, AutoExposureRenderer>();
            postContext = new PostProcessRenderContext();
            postContext.Reset();
            postContext.propertySheets = new PropertySheetFactory();
            postContext.resources = resources;
            postContext.logHistogram = new LogHistogram();
            postContext.uberSheet = new PropertySheet(new Material(resources.shaders.uber));
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
                i.Release();
            }
            postContext.uberSheet.Release();
            postContext.logHistogram.Release();
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            postContext.camera = cam.cam;
            postContext.command = data.buffer;
            postContext.sourceFormat = RenderTextureFormat.ARGBHalf;
            var settings = profile.settings;
            postContext.autoExposureTexture = RuntimeUtilities.whiteTexture;
            postContext.bloomBufferNameID = -1;
            int source, dest;
            PipelineFunctions.RunPostProcess(ref cam.targets, out source, out dest);
            postContext.source = source;
            postContext.destination = dest;
            postContext.logHistogram.Generate(postContext);
            foreach (var i in settings)
            {
                PostProcessEffectRenderer renderer;
                if (allEvents.TryGetValue(i.GetType(), out renderer))
                {
                    renderer.SetSettings(i);
                    renderer.Render(postContext);
                }
            };
            data.buffer.SetGlobalTexture(UnityEngine.Rendering.PostProcessing.ShaderIDs.AutoExposureTex, postContext.autoExposureTexture);
            data.buffer.BlitSRT(source, dest, postContext.uberSheet.material, 0, postContext.uberSheet.properties);
        }
    }
}