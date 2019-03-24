using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Transparent")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class TransEvent : PipelineEvent
    {

        protected override void Init(PipelineResources resources)
        {
            
        }
        public override bool CheckProperty()
        {
            return true;
        }
        protected override void Dispose()
        {
            
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            SortingSettings sortSettings = new SortingSettings(cam.cam);
            sortSettings.criteria = SortingCriteria.CommonTransparent;
            DrawingSettings drawSettings = new DrawingSettings(new ShaderTagId("Transparent"), sortSettings)
            {
                enableDynamicBatching = false,
                enableInstancing = false,
                perObjectData = UnityEngine.Rendering.PerObjectData.None
            };
            FilteringSettings filter = new FilteringSettings
            {
                excludeMotionVectorObjects = false,
                layerMask = cam.cam.cullingMask,
                renderQueueRange = RenderQueueRange.transparent,
                renderingLayerMask = (uint)cam.cam.cullingMask,
                sortingLayerRange = SortingLayerRange.all
            };
            data.buffer.SetRenderTarget(color: cam.targets.renderTargetIdentifier, depth: cam.targets.depthBuffer);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(data.cullResults, ref drawSettings, ref filter);
        }
    }
}