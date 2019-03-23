using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Decal")]
    [RequireEvent(typeof(LightingEvent))]
    public unsafe sealed class DecalEvent : PipelineEvent
    {
        private const int maxDecalPerCluster = 16;
        private const int decalInitCount = 35;
        private ComputeBuffer decalCountBuffer;
        private ComputeBuffer decalBuffer;
        private LightingEvent lightingEvt;
        public override bool CheckProperty()
        {
            return decalCountBuffer.IsValid() && decalBuffer.IsValid();
        }

        protected override void Init(PipelineResources resources)
        {
            lightingEvt = RenderPipeline.GetEvent<LightingEvent>();
            decalCountBuffer = new ComputeBuffer(CBDRSharedData.XRES * CBDRSharedData.YRES * CBDRSharedData.ZRES * (maxDecalPerCluster + 1), sizeof(uint));
            decalBuffer = new ComputeBuffer(decalInitCount, sizeof(DecalData));
        }

        protected override void Dispose()
        {
            decalBuffer.Dispose();
            decalBuffer = null;
            decalCountBuffer.Dispose();
            decalCountBuffer = null;
            lightingEvt = null;
        }

    }
}
