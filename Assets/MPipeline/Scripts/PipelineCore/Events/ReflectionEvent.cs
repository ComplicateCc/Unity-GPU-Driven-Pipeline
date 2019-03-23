using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
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
        public bool reflectionEnabled { get; private set; }
        private NativeArray<VisibleReflectionProbe> reflectProbes;
        private NativeArray<ReflectionData> reflectionData;
        private JobHandle storeDataHandler;
        private ComputeBuffer probeBuffer;
        private LightingEvent lightingEvents;
        private ComputeBuffer reflectionIndices;
        private Material reflectionMat;
        private NativeList<int> reflectionCubemapIDs;
        private Material irradianceVolumeMat;
       
        [SerializeField]
        public IrradianceCuller culler;
        [SerializeField]
        private StochasticScreenSpaceReflection ssrEvents = new StochasticScreenSpaceReflection();
        private static readonly int _ReflectionCubeMap = Shader.PropertyToID("_ReflectionCubeMap");

        public override bool CheckProperty()
        {
            return reflectionIndices.IsValid() && ssrEvents.MaterialEnabled()&& irradianceVolumeMat;
        }
        protected override void OnEnable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((buffer) =>
            {
                buffer.EnableShaderKeyword("ENABLE_REFLECTION");
            });
        }

        protected override void OnDisable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((buffer) =>
            {
                buffer.DisableShaderKeyword("ENABLE_REFLECTION");
            });
        }

        protected override void Init(PipelineResources resources)
        {
            probeBuffer = new ComputeBuffer(maximumProbe, sizeof(ReflectionData));
            lightingEvents = RenderPipeline.GetEvent<LightingEvent>();
            reflectionIndices = new ComputeBuffer(CBDRSharedData.XRES * CBDRSharedData.YRES * CBDRSharedData.ZRES * (maximumProbe + 1), sizeof(int));
            string old = "_ReflectionCubeMap";
            string newStr = new string(' ', old.Length + 1);
            reflectionCubemapIDs = new NativeList<int>(maximumProbe, maximumProbe, Allocator.Persistent);
            irradianceVolumeMat = new Material(resources.shaders.irradianceVolumeShader);
            fixed (char* ctr = old)
            {
                fixed (char* newCtr = newStr)
                {
                    for (int i = 0; i < old.Length; ++i)
                    {
                        newCtr[i] = ctr[i];
                    }
                    for (int i = 0; i < reflectionCubemapIDs.Length; ++i)
                    {
                        newCtr[old.Length] = (char)(i + 48);
                        reflectionCubemapIDs[i] = Shader.PropertyToID(newStr);
                    }
                }
            }
            reflectionMat = new Material(resources.shaders.reflectionShader);
            ssrEvents.Init(resources);
        }
        protected override void Dispose()
        {
            DestroyImmediate(irradianceVolumeMat);
            probeBuffer.Dispose();
            reflectionIndices.Dispose();
            reflectionCubemapIDs.Dispose();
            ssrEvents.Dispose();
            DestroyImmediate(reflectionMat);
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            culler.PreRenderFrame(ref data);
            reflectProbes = data.cullResults.visibleReflectionProbes;
            int count = Mathf.Min(maximumProbe, reflectProbes.Length);
            reflectionData = new NativeArray<ReflectionData>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            storeDataHandler = new StoreReflectionData
            {
                data = reflectionData.Ptr(),
                allProbes = reflectProbes.Ptr()
            }.Schedule(count, 32);
        }
        private void DrawGI(CommandBuffer buffer, ref CoeffTexture currentTexture, Matrix4x4 localToWorld)
        {
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[0], currentTexture.coeff0);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[1], currentTexture.coeff1);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[2], currentTexture.coeff2);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[3], currentTexture.coeff3);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[4], currentTexture.coeff4);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[5], currentTexture.coeff5);
            buffer.SetGlobalTexture(IrradianceVolumeController.current._CoeffIDs[6], currentTexture.coeff6);
            buffer.SetGlobalMatrix(ShaderIDs._WorldToLocalMatrix, localToWorld.inverse);
            buffer.DrawMesh(GraphicsUtility.cubeMesh, localToWorld, irradianceVolumeMat, 0, 0);
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            culler.FrameUpdate();
            if (culler.cullingResult.isCreated && culler.cullingResult.Length > 0)
            {
                data.buffer.SetRenderTarget(color: cam.targets.renderTargetIdentifier, depth: cam.targets.depthBuffer);
                foreach (var i in culler.cullingResult)
                {
                    ref LoadedIrradiance irr = ref IrradianceVolumeController.current.loadedIrradiance[i];
                    CoeffTexture coefTex = IrradianceVolumeController.current.coeffTextures[irr.renderTextureIndex];
                    DrawGI(data.buffer, ref coefTex, new Matrix4x4(float4(irr.localToWorld.c0, 0), float4(irr.localToWorld.c1, 0), float4(irr.localToWorld.c2, 0), float4(irr.position, 1)));
                }
            }
            storeDataHandler.Complete();
            int count = Mathf.Min(maximumProbe, reflectProbes.Length);
            reflectionEnabled = (count > 0);
            if (!reflectionEnabled) return;

            CommandBuffer buffer = data.buffer;
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
            for (int i = 0; i < count; ++i)
            {
                buffer.SetGlobalTexture(reflectionCubemapIDs[i], reflectProbes[i].texture);
            }
            if (ssrEvents.enabled)
            {
                ssrEvents.Render(ref data, cam, this);
                buffer.EnableShaderKeyword("ENABLE_SSR");

            }
            else
            {
                buffer.DisableShaderKeyword("ENABLE_SSR");
            }
            buffer.BlitSRTWithDepth(cam.targets.renderTargetIdentifier, cam.targets.depthBuffer, reflectionMat, 0);
            //TODO
        }
        [Unity.Burst.BurstCompile]
        public unsafe struct StoreReflectionData : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public VisibleReflectionProbe* allProbes;
            [NativeDisableUnsafePtrRestriction]
            public ReflectionData* data;
            public void Execute(int i)
            {
                ref ReflectionData dt = ref data[i];
                VisibleReflectionProbe vis = allProbes[i];
                dt.blendDistance = vis.blendDistance;
                float4x4 localToWorld = vis.localToWorldMatrix;
                dt.minExtent = (float3)vis.bounds.extents - dt.blendDistance * 0.5f;
                dt.maxExtent = (float3)vis.bounds.extents + dt.blendDistance * 0.5f;
                dt.boxProjection = vis.isBoxProjection ? 1 : 0;
                dt.position = localToWorld.c3.xyz;
                dt.hdr = vis.hdrData;
            }
        }

    }
}
