using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Random = Unity.Mathematics.Random;
using static Unity.Mathematics.math;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [PipelineEvent(true, true)]
    public unsafe class VolumetricLightEvent : PipelineEvent
    {
        private CBDRSharedData cbdr;
        private Material volumeMat;
        public float availableDistance = 64;
        const int marchStep = 64;
        const int scatterPass = 8;
        const int fogVolumePass = 9;
        static readonly int3 downSampledSize = new int3(160, 90, 256);
        private ComputeBuffer randomBuffer;
        private Random rand;
        private JobHandle jobHandle;
        private NativeArray<FogVolume> resultVolume;
        private int fogCount = 0;

        protected override void Init(PipelineResources resources)
        {
            randomBuffer = new ComputeBuffer(downSampledSize.x * downSampledSize.y * downSampledSize.z, sizeof(uint));
            NativeArray<uint> randomArray = new NativeArray<uint>(randomBuffer.count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            uint* randPtr = randomArray.Ptr();
            rand = new Random((uint)System.Guid.NewGuid().GetHashCode());
            for (int i = 0; i < randomArray.Length; ++i)
            {
                randPtr[i] = (uint)System.Guid.NewGuid().GetHashCode();
            }
            randomBuffer.SetData(randomArray);
            randomArray.Dispose();
            cbdr = PipelineSharedData.Get(renderPath, resources, (res) => new CBDRSharedData(res));
            volumeMat = new Material(resources.volumetricShader);
        }

        public override void OnEventDisable()
        {
            cbdr.useFroxel = false;
        }

        public override void OnEventEnable()
        {
            cbdr.useFroxel = true;
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
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
                    fogVolumeCount = fogCount.Ptr()
                }).Schedule(FogVolumeComponent.allVolumes.Length, 1);
            }
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            ComputeShader scatter = data.resources.volumetricScattering;

            if (cbdr.lightFlag == 0)
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
            
            buffer.SetGlobalFloat(ShaderIDs._MaxDistance, availableDistance);
            buffer.SetGlobalInt(ShaderIDs._FrameCount, Time.frameCount);
            HistoryVolumetric historyVolume = IPerCameraData.GetProperty(cam, () => new HistoryVolumetric());
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
                    buffer.SetGlobalFloat(ShaderIDs._TemporalWeight, 0.7f);
            }
            jobHandle.Complete();
            if (fogCount > 0)
            {
               
                CBDRSharedData.ResizeBuffer(ref cbdr.allFogVolumeBuffer, fogCount);
                cbdr.allFogVolumeBuffer.SetData(resultVolume, 0, 0, fogCount);
                ComputeShader cullShader = data.resources.cbdrShader;
                buffer.SetComputeTextureParam(cullShader, fogVolumePass, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
                buffer.SetComputeTextureParam(cullShader, fogVolumePass, ShaderIDs._FroxelFogVolumeList, cbdr.froxelFogVolumeList);
                buffer.SetComputeBufferParam(cullShader, fogVolumePass, ShaderIDs._AllFogVolume, cbdr.allFogVolumeBuffer);
                buffer.DispatchCompute(cullShader, fogVolumePass, 1, 1, fogCount);
            }
            buffer.SetGlobalVector(ShaderIDs._NearFarClip, new Vector4(cam.cam.farClipPlane / availableDistance, cam.cam.nearClipPlane / availableDistance, cam.cam.nearClipPlane));
            buffer.SetGlobalVector(ShaderIDs._Screen_TexelSize, new Vector4(1f / cam.cam.pixelWidth, 1f / cam.cam.pixelHeight, cam.cam.pixelWidth, cam.cam.pixelHeight));
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllFogVolume, cbdr.allFogVolumeBuffer);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._FroxelFogVolumeList, cbdr.froxelFogVolumeList);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._FroxelPointTileLightList, cbdr.froxelpointTileLightList);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._FroxelSpotTileLightList, cbdr.froxelSpotTileLightList);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._RandomBuffer, randomBuffer);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, scatterPass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._LastVolume, historyVolume.lastVolume);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._DirShadowMap, cbdr.dirLightShadowmap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._CubeShadowMapArray, cbdr.cubeArrayMap);
            buffer.SetGlobalVector(ShaderIDs._RandomSeed, (float4)(rand.NextDouble4() * 1000 + 100));

            cbdr.dirLightShadowmap = null;
            buffer.SetComputeIntParam(scatter, ShaderIDs._LightFlag, (int)cbdr.lightFlag);
            buffer.DispatchCompute(scatter, pass, downSampledSize.x / 2, downSampledSize.y / 2, downSampledSize.z / marchStep);
            buffer.CopyTexture(ShaderIDs._VolumeTex, historyVolume.lastVolume);
            buffer.DispatchCompute(scatter, scatterPass, downSampledSize.x / 32, downSampledSize.y / 2, 1);
            buffer.BlitSRT(cam.targets.renderTargetIdentifier, volumeMat, 0);
            buffer.ReleaseTemporaryRT(ShaderIDs._VolumeTex);
            cbdr.lightFlag = 0;
        }

        protected override void Dispose()
        {
            DestroyImmediate(volumeMat);
            randomBuffer.Dispose();
        }
        public unsafe struct FogVolumeCalculate : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public FogVolume* allVolume;
            [NativeDisableUnsafePtrRestriction]
            public int* fogVolumeCount;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool BoxUnderPlane(ref float4 plane, ref FogVolume fog, int i)
            {
                
                float3 absNormal = abs(normalize(mul(plane.xyz, fog.localToWorld)));
                return dot(fog.position, plane.xyz) - dot(absNormal, fog.extent) < -plane.w;
            }
            public void Execute(int index)
            {
                ref FogVolume vol = ref FogVolumeComponent.allVolumes[index].volume;
                for(int i = 0; i < 6; ++i)
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
