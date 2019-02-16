using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Reflection")]
    [RequireEvent(typeof(LightingEvent))]
    public class ReflectionEvent : PipelineEvent
    {
        public override bool CheckProperty()
        {
            return true;
        }
        protected override void Init(PipelineResources resources)
        {
            
        }
        protected override void Dispose()
        {

        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            
        }
    }
}
