using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Random = Unity.Mathematics.Random;
using System.Threading;
namespace MPipeline
{
    [PipelineEvent(false, true)]
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
            for(int i = 0; i < randomArray.Length; ++i)
            {
                randPtr[i] = (uint)rand.NextInt();
            }
            randomBuffer.SetData(randomArray);
            randomArray.Dispose();
            cbdr = PipelineSharedData.Get(renderPath, resources, (res) => new CBDRSharedData(res));
            volumeMat = new Material(resources.volumetricShader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PlaneContact(ref float4 plane, ref float4 sphere)
        {
            return math.dot(plane.xyz, sphere.xyz) + plane.w < sphere.w;
        }
        public static NativeArray<PointLightStruct> GetCulledPointLight(ref float4 plane, PointLightStruct* allPointLight, ref int froxelLightCount, int lightCount)
        {
            NativeArray<PointLightStruct> sct = new NativeArray<PointLightStruct>(lightCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            PointLightStruct* froxelPointLight = sct.Ptr();
            for (int index = 0; index < lightCount; ++index)
            {
                if (PlaneContact(ref plane, ref allPointLight[index].sphere))
                {
                    int lastIndex = Interlocked.Increment(ref froxelLightCount) - 1;
                    froxelPointLight[lastIndex] = allPointLight[index];
                }
            }
            return sct;
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            ComputeShader scatter = data.resources.volumetricScattering;

            //Set Voxel Based Lighting
            VoxelLightCommonData(buffer, cam.cam);
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
            if ((cbdr.lightFlag & 1) != 0)
            {
                int froxelLightCount = 0;
                Transform camTrans = cam.cam.transform;
                float3 inPoint = camTrans.position + camTrans.forward * availableDistance;
                float3 normal = camTrans.forward;
                float4 plane = new float4(normal, -math.dot(normal, inPoint));
                var froxelPointLightArray = GetCulledPointLight(ref plane, cbdr.pointLightArray.Ptr(), ref froxelLightCount, *cbdr.pointLightCount);
                if (froxelLightCount > 0)
                {
                    cbdr.froxelPointLightBuffer.SetData(froxelPointLightArray, 0, 0, froxelLightCount);
                    VoxelPointLight(froxelPointLightArray, froxelLightCount, buffer);
                }
                else
                {
                    cbdr.lightFlag &= 0b111111111110;//Kill point light if there is nothing in the culled list
                }
            }
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
                if(historyVolume.lastVolume.volumeDepth != desc.volumeDepth)
                {
                    historyVolume.lastVolume.Release();
                    historyVolume.lastVolume.volumeDepth = desc.volumeDepth;
                    historyVolume.lastVolume.Create();
                    buffer.SetGlobalFloat(ShaderIDs._TemporalWeight, 0);
                }
                else
                    buffer.SetGlobalFloat(ShaderIDs._TemporalWeight, 0.7f);
            }
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllPointLight, cbdr.froxelPointLightBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._PointLightIndexBuffer, cbdr.froxelPointLightIndexBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._RandomBuffer, randomBuffer);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, scatterPass, ShaderIDs._VolumeTex, ShaderIDs._VolumeTex);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._LastVolume, historyVolume.lastVolume);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._DirShadowMap, cbdr.dirLightShadowmap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._CubeShadowMapArray, cbdr.cubemapShadowArray);
            buffer.SetGlobalVector(ShaderIDs._Screen_TexelSize, new Vector4(1f / cam.cam.pixelWidth, 1f / cam.cam.pixelHeight, cam.cam.pixelWidth, cam.cam.pixelHeight));
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

        private void VoxelLightCommonData(CommandBuffer buffer, Camera cam)
        {
            ComputeShader cbdrShader = cbdr.cbdrShader;
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.SetZPlaneKernel, ShaderIDs._ZPlaneTexture, cbdr.froxelZPlaneTexture);
            Transform camTrans = cam.transform;
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraFarPos, camTrans.position + availableDistance * camTrans.forward);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraNearPos, camTrans.position + cam.nearClipPlane * camTrans.forward);
            buffer.SetComputeVectorParam(cbdrShader, ShaderIDs._CameraForward, camTrans.forward);
            buffer.SetGlobalVector(ShaderIDs._NearFarClip, new Vector4(cam.farClipPlane / availableDistance, cam.nearClipPlane / availableDistance, cam.nearClipPlane));
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetZPlaneKernel, 1, 1, 1);
        }

        public void VoxelPointLight(NativeArray<PointLightStruct> arr, int length, CommandBuffer buffer)
        {
            CBDRSharedData.ResizeBuffer(ref cbdr.froxelPointLightBuffer, length);
            ComputeShader cbdrShader = cbdr.cbdrShader;
            const int PointLightKernel = CBDRSharedData.PointLightKernel;
            cbdr.froxelPointLightBuffer.SetData(arr, 0, 0, length);
            buffer.SetGlobalInt(ShaderIDs._PointLightCount, length);
            buffer.SetComputeTextureParam(cbdrShader, PointLightKernel, ShaderIDs._XYPlaneTexture, cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, PointLightKernel, ShaderIDs._ZPlaneTexture, cbdr.froxelZPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, PointLightKernel, ShaderIDs._AllPointLight, cbdr.froxelPointLightBuffer);
            buffer.SetComputeBufferParam(cbdrShader, PointLightKernel, ShaderIDs._PointLightIndexBuffer, cbdr.froxelPointLightIndexBuffer);
            buffer.DispatchCompute(cbdrShader, PointLightKernel, 1, 1, CBDRSharedData.ZRES);
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
