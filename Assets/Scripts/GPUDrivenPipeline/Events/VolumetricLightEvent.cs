using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using static Unity.Mathematics.math;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [RequireEvent(typeof(LightingEvent))]
    [CreateAssetMenu(menuName = "GPURP Events/Volumetric Scattering")]
    public unsafe class VolumetricLightEvent : PipelineEvent
    {
        public float availableDistance = 64;
        [Range(0.01f, 100f)]
        public float indirectIntensity = 1;
        const int marchStep = 64;
        const int scatterPass = 8;
        const int clearPass = 9;
        const int calculateGI = 10;
        static readonly int3 downSampledSize = new int3(160, 90, 256);
        private ComputeBuffer randomBuffer;
        private JobHandle jobHandle;
        private NativeArray<FogVolume> resultVolume;
        private int fogCount = 0;
        private LightingEvent lightingData;
        public override bool CheckProperty()
        {
            return randomBuffer.IsValid();
        }
        protected override void Init(PipelineResources resources)
        {
            lightingData = RenderPipeline.GetEvent<LightingEvent>();
            randomBuffer = new ComputeBuffer(downSampledSize.x * downSampledSize.y * downSampledSize.z, sizeof(uint));
            NativeArray<uint> randomArray = new NativeArray<uint>(randomBuffer.count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            uint* randPtr = randomArray.Ptr();
            for (int i = 0; i < randomArray.Length; ++i)
            {
                randPtr[i] = (uint)-System.Guid.NewGuid().GetHashCode();
            }
            randomBuffer.SetData(randomArray);
            randomArray.Dispose();
        }
        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            ref CBDRSharedData cbdr = ref lightingData.cbdr;
            cbdr.availiableDistance = availableDistance;
            fogCount = 0;

            if (FogVolumeComponent.allVolumes.isCreated && FogVolumeComponent.allVolumes.Length > 0)
            {
                resultVolume = new NativeArray<FogVolume>(FogVolumeComponent.allVolumes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                float4* frustumPlanes = (float4*)UnsafeUtility.Malloc(6 * sizeof(float4), 16, Allocator.Temp);
                UnsafeUtility.MemCpy(frustumPlanes, data.frustumPlanes.Ptr(), 6 * sizeof(float4));
                Transform camTrans = cam.transform;
                float3 inPoint = camTrans.position + camTrans.forward * cbdr.availiableDistance;
                float3 normal = camTrans.forward;
                float4 plane = float4(normal, -dot(normal, inPoint));
                frustumPlanes[5] = plane;
                jobHandle = (new FogVolumeCalculate
                {
                    allVolume = resultVolume.Ptr(),
                    frustumPlanes = frustumPlanes,
                    fogVolumeCount = fogCount.Ptr(),
                    fogVolume = FogVolumeComponent.allVolumes.unsafePtr
                }).Schedule(FogVolumeComponent.allVolumes.Length, 1);
            }
        }
        protected override void OnEnable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((buffer) =>
            {
                buffer.EnableShaderKeyword("ENABLE_VOLUMETRIC");
            });
        }

        protected override void OnDisable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((buffer) =>
            {
                buffer.DisableShaderKeyword("ENABLE_VOLUMETRIC");
            });
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            ComputeShader scatter = data.resources.shaders.volumetricScattering;
            ref CBDRSharedData cbdr = ref lightingData.cbdr;
            if (cbdr.lightFlag == 0 && lightingData.culler.cullingResult.Length == 0)
            {
                cbdr.dirLightShadowmap = null;
                return;
            }
            int pass = 0;
            if (cbdr.dirLightShadowmap != null)
                pass |= 0b010;
            if (cbdr.pointshadowCount > 0)
                pass |= 0b001;
            if (cbdr.spotShadowCount > 0)
                pass |= 0b100;
            //TODO
            //Enable fourth bit as Global Illumination

            buffer.SetGlobalFloat(ShaderIDs._MaxDistance, availableDistance);
            buffer.SetGlobalInt(ShaderIDs._FrameCount, Time.frameCount);
            HistoryVolumetric historyVolume = IPerCameraData.GetProperty(cam, () => new HistoryVolumetric(), this);
            //Volumetric Light
            RenderTextureDescriptor desc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true,
                height = downSampledSize.y,
                width = downSampledSize.x,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = downSampledSize.z,
                vrUsage = VRTextureUsage.None
            };
            buffer.GetTemporaryRT(ShaderIDs._VolumeTex, desc, FilterMode.Bilinear);
            if (!historyVolume.lastVolume)
            {
                historyVolume.lastVolume = new RenderTexture(desc);
                historyVolume.lastVolume.filterMode = FilterMode.Bilinear;
                historyVolume.lastVolume.wrapMode = TextureWrapMode.Clamp;
                historyVolume.lastVolume.Create();
                buffer.SetGlobalFloat(ShaderIDs._TemporalWeight, 0);
            }
            else
            {
                if (historyVolume.lastVolume.volumeDepth != desc.volumeDepth)
                {
                    historyVolume.lastVolume.Release();
                    historyVolume.lastVolume.volumeDepth = desc.volumeDepth;
                    historyVolume.lastVolume.Create();
                    buffer.SetGlobalFloat(ShaderIDs._TemporalWeight, 0);
                }
                else
                    buffer.SetGlobalFloat(ShaderIDs._TemporalWeight, 0.85f);
            }
            jobHandle.Complete();
            if (fogCount > 0)
            {
                cbdr.allFogVolumeBuffer.SetData(resultVolume, 0, 0, fogCount);
            }
            buffer.SetGlobalVector(ShaderIDs._NearFarClip, new Vector4(cam.cam.farClipPlane / availableDistance, cam.cam.nearClipPlane / availableDistance, cam.cam.nearClipPlane));
            buffer.SetGlobalVector(ShaderIDs._Screen_TexelSize, new Vector4(1f / cam.cam.pixelWidth, 1f / cam.cam.pixelHeight, cam.cam.pixelWidth, cam.cam.pixelHeight));
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllFogVolume, cbdr.allFogVolumeBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeIntParam(scatter, ShaderIDs._FogVolumeCount, fogCount);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._RandomBuffer, randomBuffer);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, scatterPass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, clearPass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._LastVolume, historyVolume.lastVolume);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._DirShadowMap, cbdr.dirLightShadowmap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._CubeShadowMapArray, cbdr.cubeArrayMap);
            /*
            if (ProbeBaker.allBakers.Count > 0)
            {
                buffer.SetComputeBufferParam(scatter, calculateGI, ShaderIDs._RandomBuffer, randomBuffer);
                buffer.SetComputeTextureParam(scatter, calculateGI, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
                buffer.SetComputeFloatParam(scatter, ShaderIDs._IndirectIntensity, indirectIntensity);
                for (int i = 0; i < ProbeBaker.allBakers.Count; ++i)
                {
                    ProbeBaker baker = ProbeBaker.allBakers[i];
                    if (baker.isRendered)
                    {
                        baker.SetCoeffTextures(buffer, scatter, calculateGI);
                        buffer.DispatchCompute(scatter, calculateGI, downSampledSize.x / 2, downSampledSize.y / 2, downSampledSize.z / marchStep);
                    }
                }
            }*/
            cbdr.dirLightShadowmap = null;
            buffer.SetComputeIntParam(scatter, ShaderIDs._LightFlag, (int)cbdr.lightFlag);
            buffer.DispatchCompute(scatter, clearPass, downSampledSize.x / 2, downSampledSize.y / 2, downSampledSize.z / marchStep);
            buffer.DispatchCompute(scatter, pass, downSampledSize.x / 2, downSampledSize.y / 2, downSampledSize.z / marchStep);
            buffer.CopyTexture(ShaderIDs._VolumeTex, historyVolume.lastVolume);
            buffer.DispatchCompute(scatter, scatterPass, downSampledSize.x / 32, downSampledSize.y / 2, 1);
            cbdr.lightFlag = 0;
        }

        protected override void Dispose()
        {
            randomBuffer.Dispose();
        }
        [Unity.Burst.BurstCompile]
        public unsafe struct FogVolumeCalculate : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public FogVolume* allVolume;
            [NativeDisableUnsafePtrRestriction]
            public int* fogVolumeCount;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            [NativeDisableUnsafePtrRestriction]
            public FogVolumeComponent.FogVolumeContainer* fogVolume;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool BoxUnderPlane(ref float4 plane, ref FogVolume fog, int i)
            {
                float3 absNormal = abs(normalize(mul(plane.xyz, fog.localToWorld)));
                return dot(fog.position, plane.xyz) - dot(absNormal, fog.extent) < -plane.w;
            }
            public void Execute(int index)
            {
                ref FogVolume vol = ref fogVolume[index].volume;
                for (int i = 0; i < 6; ++i)
                {
                    if (!BoxUnderPlane(ref frustumPlanes[i], ref vol, i))
                        return;
                }
                int last = Interlocked.Increment(ref *fogVolumeCount) - 1;
                allVolume[last] = vol;
            }
        }

    }
    public class HistoryVolumetric : IPerCameraData
    {
        public RenderTexture lastVolume = null;
        public override void DisposeProperty()
        {
            if (lastVolume != null)
            {
                lastVolume.Release();
                lastVolume = null;
            }
        }
    }
}
