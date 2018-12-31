using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Random = UnityEngine.Random;
using System.Threading;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public unsafe class VolumetricLightEvent : PipelineEvent
    {
        private CBDRSharedData cbdr;
        private Material volumeMat;
        private static readonly int _TempMap = Shader.PropertyToID("_TempMap");
        private static readonly int _OriginMap = Shader.PropertyToID("_OriginMap");
        private static readonly int _DownSampledDepth = Shader.PropertyToID("_DownSampledDepth");
        private static readonly int _MaxDistance = Shader.PropertyToID("_MaxDistance");
        private static readonly int _VolumeTex = Shader.PropertyToID("_VolumeTex");
        public float availableDistance = 64;
        const int marchStep = 64;
        [Range(1, 4)]
        public int step = 1;

        protected override void Init(PipelineResources resources)
        {
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
                    VoxelPointLight(froxelPointLightArray, froxelLightCount, buffer, scatter, pass);
                }
                else
                {
                    cbdr.lightFlag &= 0b111111111110;//Kill point light if there is nothing in the culled list
                }
            }
            int2 downSampledSize = new int2(cam.cam.pixelWidth / 8, cam.cam.pixelHeight / 8);
            //Set Random
            buffer.SetGlobalVector(ShaderIDs._RandomNumber, new Vector4(Random.Range(10f, 50f), Random.Range(10f, 50f), Random.Range(10f, 50f), Random.Range(20000f, 40000f)));
            buffer.SetGlobalVector(ShaderIDs._RandomWeight, new Vector4(Random.value, Random.value, Random.value, Random.value));
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._RandomTex, cbdr.randomTex);
            buffer.SetGlobalFloat(_MaxDistance, availableDistance);
            buffer.SetGlobalInt(ShaderIDs._MarchStep, step * marchStep);
            //DownSample
            buffer.GetTemporaryRT(_DownSampledDepth, cam.cam.pixelWidth / 2, cam.cam.pixelHeight / 2, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(cam.targets.depthTexture, _DownSampledDepth, volumeMat, 0);
            buffer.GetTemporaryRT(_TempMap, cam.cam.pixelWidth / 4, cam.cam.pixelHeight / 4, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_DownSampledDepth, _TempMap, volumeMat, 0);
            buffer.ReleaseTemporaryRT(_DownSampledDepth);
            buffer.GetTemporaryRT(_DownSampledDepth, downSampledSize.x, downSampledSize.y, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_TempMap, _DownSampledDepth, volumeMat, 1);
            buffer.ReleaseTemporaryRT(_TempMap);
            //Volumetric Light
            buffer.GetTemporaryRT(_VolumeTex, new RenderTextureDescriptor
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
                volumeDepth = marchStep * step,
                vrUsage = VRTextureUsage.None
            });
            buffer.SetGlobalVector(ShaderIDs._ScreenSize, new Vector2(downSampledSize.x, downSampledSize.y));
            buffer.SetComputeTextureParam(scatter, pass, _DownSampledDepth, _DownSampledDepth);
            buffer.SetComputeTextureParam(scatter, pass, _VolumeTex, _VolumeTex);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._DirShadowMap, cbdr.dirLightShadowmap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._CubeShadowMapArray, cbdr.cubemapShadowArray);
            cbdr.cubemapShadowArray = null;
            cbdr.dirLightShadowmap = null;
            buffer.SetComputeIntParam(scatter, ShaderIDs._LightFlag, (int)cbdr.lightFlag);
            buffer.DispatchCompute(scatter, pass, (int)math.ceil(downSampledSize.x / 4f), (int)math.ceil(downSampledSize.y / 4f), step);
            buffer.BlitSRT(cam.targets.renderTargetIdentifier, volumeMat, 2);
            //Dispose
            buffer.ReleaseTemporaryRT(_DownSampledDepth);
            buffer.ReleaseTemporaryRT(_VolumeTex);
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
            buffer.SetGlobalVector(ShaderIDs._CameraClipDistance, new Vector4(cam.nearClipPlane, availableDistance - cam.nearClipPlane));
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.SetZPlaneKernel, 1, 1, 1);
        }

        public void VoxelPointLight(NativeArray<PointLightStruct> arr, int length, CommandBuffer buffer, ComputeShader targetShader, int pass)
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
            buffer.SetComputeBufferParam(targetShader, pass, ShaderIDs._AllPointLight, cbdr.froxelPointLightBuffer);
            buffer.SetComputeBufferParam(targetShader, pass, ShaderIDs._PointLightIndexBuffer, cbdr.froxelPointLightIndexBuffer);
            buffer.DispatchCompute(cbdrShader, PointLightKernel, 1, 1, CBDRSharedData.ZRES);
        }

        protected override void Dispose()
        {
            Destroy(volumeMat);
        }
    }
}
