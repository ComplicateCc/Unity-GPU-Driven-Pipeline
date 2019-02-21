using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Unity.Mathematics;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Reflection")]
    [RequireEvent(typeof(LightingEvent))]
    public unsafe sealed class ReflectionEvent : PipelineEvent
    {
        const int maximumProbe = 8;
        public enum Resolution
        {
            x64 = 64, x128 = 128, x256 = 256, x512 = 512, x1024 = 1024
        }
        public Resolution unitedResolution = Resolution.x128;
        private NativeArray<VisibleReflectionProbe> reflectProbes;
        private NativeArray<ReflectionData> reflectionData;
        private JobHandle storeDataHandler;
        private ComputeBuffer probeBuffer;
        private CubemapArray reflectionAtlas;
       

        public override bool CheckProperty()
        {
            return reflectionAtlas != null;
        }
        protected override void Init(PipelineResources resources)
        {
            reflectionAtlas = new CubemapArray((int)unitedResolution, maximumProbe, TextureFormat.BC6H, true, true);
            reflectionAtlas.filterMode = FilterMode.Trilinear;
            probeBuffer = new ComputeBuffer(maximumProbe, sizeof(ReflectionData));
        }
        protected override void Dispose()
        {
            DestroyImmediate(reflectionAtlas);
            probeBuffer.Dispose();
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            reflectProbes = data.cullResults.visibleReflectionProbes;
            int count = Mathf.Min(maximumProbe, reflectProbes.Length);
            reflectionData = new NativeArray<ReflectionData>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            storeDataHandler = new StoreReflectionData
            {
                data = reflectionData.Ptr(),
                allProbes = reflectProbes
            }.Schedule(count, 32);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            storeDataHandler.Complete();
            int count = Mathf.Min(maximumProbe, reflectProbes.Length);
            CommandBuffer buffer = data.buffer;
            for (int i = 0; i < count; ++i)
            {
                Texture tex = reflectProbes[i].texture;
                if (!tex) continue;
#if UNITY_EDITOR
                if (tex.width != (int)unitedResolution)
                {
                    Debug.LogError("Probe: " + reflectProbes[i].reflectionProbe.name + "'s resolution is wrong!");
                    return;
                }
#endif
                for (int j = 0; j < 6; ++j)
                {
                    buffer.CopyTexture(tex, j, 0, reflectionAtlas, j + i * 6, 0);
                }
            }
        }
        public unsafe struct StoreReflectionData : IJobParallelFor
        {
            public NativeArray<VisibleReflectionProbe> allProbes;
            [NativeDisableUnsafePtrRestriction]
            public ReflectionData* data;
            public void Execute(int i)
            {
                ref ReflectionData dt = ref data[i];
                VisibleReflectionProbe vis = allProbes[i];
                dt.blendDistance = vis.blendDistance;

                float4x4 localToWorld = vis.localToWorldMatrix;
                dt.localToWorld = float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);
                dt.extent = vis.bounds.extents;
                dt.boxProjection = vis.isBoxProjection ? 1 : 0;
                dt.position = localToWorld.c3.xyz;
                dt.hdr = vis.hdrData;
                dt.importance = vis.importance;
            }
        }

    }
}
