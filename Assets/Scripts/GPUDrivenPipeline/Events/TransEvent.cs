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
        private ReflectionEvent reflectionEvent;
        private VolumetricLightEvent volumeEvent;
        protected override void Init(PipelineResources resources)
        {
            reflectionEvent = RenderPipeline.GetEvent<ReflectionEvent>(renderingPath);
            volumeEvent = RenderPipeline.GetEvent<VolumetricLightEvent>(renderingPath);
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
            if(SunLight.current && SunLight.current.enabled && SunLight.current.gameObject.activeSelf)
            {
                data.buffer.EnableShaderKeyword("ENABLE_SUN");
                data.buffer.SetKeyword("ENABLE_SUNSHADOW", SunLight.current.enableShadow);
            }
            else
            {
                data.buffer.DisableShaderKeyword("ENABLE_SUN");
            }
            data.buffer.SetKeyword("ENABLE_REFLECTION", reflectionEvent != null && reflectionEvent.Enabled);
            data.buffer.SetKeyword("ENABLE_VOLUMETRIC", volumeEvent != null && volumeEvent.Enabled);
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(data.cullResults, ref drawSettings, ref filter);
        }
    }
}