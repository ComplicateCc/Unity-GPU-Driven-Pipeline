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
        private static readonly int _VolumeTex = Shader.PropertyToID("_VolumeTex");
        public float availableDistance = 64;
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
            //Set Voxel Based Lighting
            VoxelLightCommonData(buffer, cam.cam);
            if (!cbdr.directLightEnabled && !cbdr.pointLightEnabled) return;
            if (cbdr.pointLightEnabled)
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
                    cbdr.pointLightEnabled = false;
                }
            }
            //Set Flags
            buffer.SetKeyword("DIRLIGHT", cbdr.directLightEnabled);
            buffer.SetKeyword("DIRLIGHTSHADOW", cbdr.directLightShadowEnable);
            buffer.SetKeyword("POINTLIGHT", cbdr.pointLightEnabled);
            //Set Random
            buffer.SetGlobalVector(ShaderIDs._RandomNumber, new Vector4(Random.Range(10f, 50f), Random.Range(10f, 50f), Random.Range(10f, 50f), Random.Range(20000f, 40000f)));
            buffer.SetGlobalVector(ShaderIDs._RandomWeight, new Vector4(Random.value, Random.value, Random.value, Random.value));
            buffer.SetGlobalTexture(ShaderIDs._RandomTex, cbdr.randomTex);
            //DownSample
            buffer.GetTemporaryRT(_TempMap, cam.cam.pixelWidth / 2, cam.cam.pixelHeight / 2, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            buffer.SetGlobalTexture(_OriginMap, cam.targets.depthTexture);
            buffer.BlitSRT(_TempMap, volumeMat, 5);
            buffer.SetGlobalTexture(_OriginMap, _TempMap);
            buffer.GetTemporaryRT(_DownSampledDepth, cam.cam.pixelWidth / 4, cam.cam.pixelHeight / 4, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_DownSampledDepth, volumeMat, 1);
            buffer.ReleaseTemporaryRT(_TempMap);
            //Volumetric Light
            buffer.GetTemporaryRT(_VolumeTex, cam.cam.pixelWidth / 4, cam.cam.pixelHeight / 4, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_VolumeTex, volumeMat, 0);
            buffer.GetTemporaryRT(_TempMap, cam.cam.pixelWidth / 4, cam.cam.pixelHeight / 4, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_VolumeTex, _TempMap, volumeMat, 3);
            buffer.BlitSRT(_TempMap, _VolumeTex, volumeMat, 4);
            buffer.ReleaseTemporaryRT(_TempMap);

            buffer.BlitSRT(cam.targets.renderTargetIdentifier, volumeMat, 2);
            //Dispose
            buffer.ReleaseTemporaryRT(_DownSampledDepth);
            buffer.ReleaseTemporaryRT(_VolumeTex);
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
            buffer.SetGlobalBuffer(ShaderIDs._AllPointLight, cbdr.froxelPointLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._PointLightIndexBuffer, cbdr.froxelPointLightIndexBuffer);
            buffer.DispatchCompute(cbdrShader, PointLightKernel, 1, 1, CBDRSharedData.ZRES);
        }

        protected override void Dispose()
        {
            Destroy(volumeMat);
        }
    }
}
