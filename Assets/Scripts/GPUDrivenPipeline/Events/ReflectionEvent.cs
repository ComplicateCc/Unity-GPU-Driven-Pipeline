using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Reflection")]
    [RequireEvent(typeof(LightingEvent))]
    public unsafe sealed class ReflectionEvent : PipelineEvent
    {
        [Range(1, 32)]
        public int maximumProbe = 8;
        public enum Resolution
        {
            x64 = 64, x128 = 128, x256 = 256, x512 = 512, x1024 = 1024
        }
        public Resolution unitedResolution = Resolution.x128;
        private List<VisibleReflectionProbe> reflectProbes;
        private NativeArray<ReflectionData> reflectionData;
        private JobHandle storeDataHandler;
        private ComputeBuffer probeBuffer;
        private RenderTexture reflectionAtlas;
        public override bool CheckProperty()
        {
            return reflectionAtlas != null;
        }
        protected override void Init(PipelineResources resources)
        {
            reflectionAtlas = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.CubeArray,
                enableRandomWrite = false,
                height = (int)unitedResolution,
                width = (int)unitedResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = maximumProbe * 6,
                vrUsage = VRTextureUsage.None
            });
            reflectionAtlas.filterMode = FilterMode.Bilinear;
            reflectionAtlas.Create();
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
            int count = Mathf.Min(maximumProbe, reflectProbes.Count);
            reflectionData = new NativeArray<ReflectionData>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            StoreReflectionData.allProbes = reflectProbes;
            storeDataHandler = new StoreReflectionData
            {
                data = reflectionData.Ptr()
            }.Schedule(count, 32);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            storeDataHandler.Complete();

            StoreReflectionData.allProbes = null;
        }
        public unsafe struct StoreReflectionData : IJobParallelFor
        {
            public static List<VisibleReflectionProbe> allProbes;
            [NativeDisableUnsafePtrRestriction]
            public ReflectionData* data;
            public void Execute(int i)
            {
                ref ReflectionData dt = ref data[i];
                VisibleReflectionProbe vis = allProbes[i];
                dt.blendDistance = vis.blendDistance;
                dt.localToWorld = vis.localToWorld;
                dt.extent = vis.bounds.extents;
                dt.boxProjection = vis.boxProjection;
                dt.hdr = vis.hdr;
                dt.importance = vis.importance;
            }
        }

    }
}
