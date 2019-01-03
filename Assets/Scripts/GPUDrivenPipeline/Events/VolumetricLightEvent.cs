using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Random = Unity.Mathematics.Random;
using System.Threading;
namespace MPipeline
{
    [PipelineEvent(true, true)]
    public unsafe class VolumetricLightEvent : PipelineEvent
    {
        private CBDRSharedData cbdr;
        private Material volumeMat;
        public float availableDistance = 64;
        const int marchStep = 64;
        const int scatterPass = 4;
        static readonly int3 downSampledSize = new int3(160, 90, 256);
        private ComputeBuffer randomBuffer;
        private Random rand;

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
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            ComputeShader scatter = data.resources.volumetricScattering;

            if (cbdr.lightFlag == 0)
            {
                cbdr.cubemapShadowArray = null;
                cbdr.dirLightShadowmap = null;
                return;
            }
            int pass = 0;
            if (cbdr.cubemapShadowArray != null)
                pass |= 0b001;
            if (cbdr.dirLightShadowmap != null)
                pass |= 0b010;
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
            buffer.SetGlobalVector(ShaderIDs._NearFarClip, new Vector4(cam.cam.farClipPlane / availableDistance, cam.cam.nearClipPlane / availableDistance, cam.cam.nearClipPlane));
            buffer.SetGlobalVector(ShaderIDs._Screen_TexelSize, new Vector4(1f / cam.cam.pixelWidth, 1f / cam.cam.pixelHeight, cam.cam.pixelWidth, cam.cam.pixelHeight));
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._FroxelTileLightList, cbdr.froxelTileLightList);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._RandomBuffer, randomBuffer);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, scatterPass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._LastVolume, historyVolume.lastVolume);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._DirShadowMap, cbdr.dirLightShadowmap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._CubeShadowMapArray, cbdr.cubemapShadowArray);
            buffer.SetGlobalVector(ShaderIDs._RandomSeed, (float4)(rand.NextDouble4() * 1000 + 100));

            cbdr.cubemapShadowArray = null;
            cbdr.dirLightShadowmap = null;
            buffer.SetComputeIntParam(scatter, ShaderIDs._LightFlag, (int)cbdr.lightFlag);
            buffer.DispatchCompute(scatter, pass, downSampledSize.x / 8, downSampledSize.y / 2, downSampledSize.z / marchStep);
            buffer.CopyTexture(ShaderIDs._VolumeTex, historyVolume.lastVolume);
            buffer.DispatchCompute(scatter, scatterPass, downSampledSize.x / 32, downSampledSize.y / 2, 1);
            buffer.BlitSRT(cam.targets.renderTargetIdentifier, volumeMat, 0);
            buffer.ReleaseTemporaryRT(ShaderIDs._VolumeTex);
            cbdr.lightFlag = 0;
            PipelineFunctions.ExecuteCommandBuffer(ref data);
        }

        protected override void Dispose()
        {
            Destroy(volumeMat);
            randomBuffer.Dispose();

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
