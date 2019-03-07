using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static MUnsafeUtility;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Irradiance")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class IrradianceVolumeEvent : PipelineEvent
    {
        public IrradianceResources targetResoures;
        protected override void Init(PipelineResources resources)
        {
            
        }

        protected override void Dispose()
        {
            
        }

        public override bool CheckProperty()
        {
            return true;
        }
    }
}
