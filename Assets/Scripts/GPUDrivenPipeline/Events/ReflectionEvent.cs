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
        private LightingEvent lightingEvents;
        private CubemapArray reflectionAtlas;
        private ComputeBuffer reflectionIndices;
        private Material deferredReflectMat;
        public override bool CheckProperty()
        {
            return reflectionAtlas != null;
        }
        protected override void Init(PipelineResources resources)
        {
            deferredReflectMat = new Material(resources.shaders.reflectionShader);
            reflectionAtlas = new CubemapArray((int)unitedResolution, maximumProbe, TextureFormat.BC6H, true, true);
            reflectionAtlas.filterMode = FilterMode.Trilinear;
            probeBuffer = new ComputeBuffer(maximumProbe, sizeof(ReflectionData));
            lightingEvents = RenderPipeline.GetEvent<LightingEvent>(renderingPath);
            reflectionIndices = new ComputeBuffer(CBDRSharedData.XRES * CBDRSharedData.YRES * CBDRSharedData.ZRES * (maximumProbe + 1), sizeof(int));
        }
        protected override void Dispose()
        {
            DestroyImmediate(reflectionAtlas);
            probeBuffer.Dispose();
            reflectionIndices.Dispose();
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
            if (count == 0) return;

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
            ComputeShader cullingShader = data.resources.shaders.reflectionCullingShader;
            ref CBDRSharedData cbdr = ref lightingEvents.cbdr;
            probeBuffer.SetData(reflectionData, 0, 0, count);
            buffer.SetComputeTextureParam(cullingShader, 0, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cullingShader, 0, ShaderIDs._ZPlaneTexture, cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cullingShader, 0, ShaderIDs._ReflectionIndices, reflectionIndices);
            buffer.SetComputeBufferParam(cullingShader, 0, ShaderIDs._ReflectionData, probeBuffer);
            buffer.SetComputeIntParam(cullingShader, ShaderIDs._Count, count);
            buffer.DispatchCompute(cullingShader, 0, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalBuffer(ShaderIDs._ReflectionIndices, reflectionIndices);
            buffer.SetGlobalBuffer(ShaderIDs._ReflectionData, probeBuffer);
            buffer.SetGlobalTexture(ShaderIDs._ReflectionCubeMap, reflectionAtlas);
            //   buffer.BlitSRT(cam.targets.renderTargetIdentifier, deferredReflectMat, 0);
            //TODO
        }
        [Unity.Burst.BurstCompile]
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
                dt.extent = vis.bounds.extents;
                dt.boxProjection = vis.isBoxProjection ? 1 : 0;
                dt.position = localToWorld.c3.xyz;
                dt.hdr = vis.hdrData;
                dt.importance = vis.importance;
            }
        }

    }
}
