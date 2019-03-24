using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Rendering;
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
        private DecalCullJob cullJob;
        private NativeArray<DecalData> decalCullResults;
        private JobHandle handle;
        public Texture decalAtlas;
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

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            decalCullResults = new NativeArray<DecalData>(Decal.allDecalCount, Allocator.Temp);
            cullJob = new DecalCullJob
            {
                count = 0,
                decalDatas = (DecalData*)decalCullResults.GetUnsafePtr(),
                frustumPlanes = (float4*)data.frustumPlanes.Ptr()
            };
            handle = cullJob.ScheduleRef(Decal.allDecalCount, 32);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            handle.Complete();
            if(decalBuffer.count < cullJob.count)
            {
                decalBuffer.Dispose();
                decalBuffer = new ComputeBuffer(cullJob.count, sizeof(DecalData));
            }
            decalBuffer.SetData(decalCullResults, 0, 0, cullJob.count);
            CommandBuffer buffer = data.buffer;
            ComputeShader shader = data.resources.shaders.decalCullingShader;
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._DecalCountBuffer, decalCountBuffer);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._DecalBuffer, decalBuffer);
            buffer.SetComputeTextureParam(shader, 0, ShaderIDs._XYPlaneTexture, lightingEvt.cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(shader, 0, ShaderIDs._ZPlaneTexture, lightingEvt.cbdr.zPlaneTexture);
            buffer.SetComputeIntParam(shader, ShaderIDs._Count, cullJob.count);
            buffer.DispatchCompute(shader, 0, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._DecalBuffer, decalBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._DecalCountBuffer, decalCountBuffer);
            buffer.SetGlobalTexture(ShaderIDs._DecalAtlas, decalAtlas);
        }

        protected override void Dispose()
        {
            decalBuffer.Dispose();
            decalBuffer = null;
            decalCountBuffer.Dispose();
            decalCountBuffer = null;
            lightingEvt = null;
        }
        private struct DecalCullJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            public DecalData* decalDatas;
            public int count;
            public void Execute(int index)
            {
                ref DecalData data = ref Decal.GetData(index);
                if(VectorUtility.BoxIntersect(data.rotation, data.position, frustumPlanes, 6))
                {
                    int currentInd = System.Threading.Interlocked.Increment(ref count) - 1;
                    decalDatas[currentInd] = data;
                }
            }
        }
    }
}
