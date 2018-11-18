using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public unsafe class GeometryEvent : PipelineEvent
    {
        HizDepth hizDepth;
        Material linearMat;
        public enum OcclusionCullingMode
        {
            None, SingleCheck, DoubleCheck
        }
        public OcclusionCullingMode occCullingMod = OcclusionCullingMode.None;
        protected override void Init(PipelineResources resources)
        {
            hizDepth = new HizDepth();
            hizDepth.InitHiZ(resources);
            linearMat = new Material(resources.linearDepthShader);
            Application.targetFrameRate = int.MaxValue;
            proceduralBlock = new MaterialPropertyBlock();
        }

        protected override void Dispose()
        {
            hizDepth.DisposeHiZ();
        }
        public System.Func<IPerCameraData> getOcclusionData = () => new HizOcclusionData();
        private MaterialPropertyBlock proceduralBlock;
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            Material proceduralMaterial = data.baseBuffer.combinedMaterial;
            CommandBuffer buffer = data.buffer;
            HizOcclusionData hizData = IPerCameraData.GetProperty<HizOcclusionData>(cam, getOcclusionData);
            ref var baseBuffer = ref data.baseBuffer;
            var gpuFrustumShader = data.resources.gpuFrustumCulling;
            buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthIdentifier);
            buffer.ClearRenderTarget(true, true, Color.black);
            proceduralBlock.Clear();
            switch (occCullingMod)
            {
                case OcclusionCullingMode.None:
                    PipelineFunctions.SetBaseBuffer(ref baseBuffer, gpuFrustumShader, data.arrayCollection.frustumPlanes, buffer);
                    PipelineFunctions.SetShaderBuffer(ref baseBuffer, proceduralBlock);
                    PipelineFunctions.RunCullDispatching(ref baseBuffer, gpuFrustumShader, cam.cam.orthographic, buffer);
                    PipelineFunctions.RenderProceduralCommand(ref baseBuffer, proceduralMaterial, proceduralBlock, buffer);
                    buffer.DispatchCompute(gpuFrustumShader, 1, 1, 1, 1);
                    break;
                case OcclusionCullingMode.SingleCheck:
                    buffer.SetComputeVectorParam(gpuFrustumShader, ShaderIDs._CameraUpVector, hizData.lastFrameCameraUp);
                    buffer.SetComputeBufferParam(gpuFrustumShader, 5, ShaderIDs.clusterBuffer, baseBuffer.clusterBuffer);
                    buffer.SetComputeTextureParam(gpuFrustumShader, 5, ShaderIDs._HizDepthTex, hizData.historyDepth);
                    buffer.SetComputeVectorArrayParam(gpuFrustumShader, ShaderIDs.planes, data.arrayCollection.frustumPlanes);
                    buffer.SetComputeBufferParam(gpuFrustumShader, 5, ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                    buffer.SetComputeBufferParam(gpuFrustumShader, 5, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
                    buffer.SetComputeBufferParam(gpuFrustumShader, PipelineBaseBuffer.ComputeShaderKernels.ClearClusterKernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
                    ComputeShaderUtility.Dispatch(gpuFrustumShader, buffer, 5, baseBuffer.clusterCount, 64);
                    hizData.lastFrameCameraUp = cam.transform.up;
                    proceduralBlock.SetBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                    proceduralBlock.SetBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                    PipelineFunctions.RenderProceduralCommand(ref baseBuffer, proceduralMaterial, proceduralBlock, buffer);
                    buffer.DispatchCompute(gpuFrustumShader, PipelineBaseBuffer.ComputeShaderKernels.ClearClusterKernel, 1, 1, 1);
                    buffer.Blit(cam.targets.depthTexture, hizData.historyDepth, linearMat, 0);
                    hizDepth.GetMipMap(hizData.historyDepth, buffer);
                    break;
                case OcclusionCullingMode.DoubleCheck:
                    PipelineFunctions.UpdateOcclusionBuffer(
                    ref data.baseBuffer, gpuFrustumShader,
                    buffer,
                    hizData,
                    data.arrayCollection.frustumPlanes,
                    cam.cam.orthographic, baseBuffer.resultBuffer.count);
                    //绘制第一次剔除结果
                    PipelineFunctions.DrawLastFrameCullResult(ref data.baseBuffer, proceduralBlock, buffer, proceduralMaterial);
                    //更新Vector，Depth Mip Map
                    hizData.lastFrameCameraUp = cam.transform.up;
                    PipelineFunctions.ClearOcclusionData(ref data.baseBuffer, buffer, gpuFrustumShader);
                    buffer.Blit(cam.targets.depthTexture, hizData.historyDepth, linearMat, 0);
                    hizDepth.GetMipMap(hizData.historyDepth, buffer);
                    //使用新数据进行二次剔除
                    PipelineFunctions.OcclusionRecheck(ref data.baseBuffer, gpuFrustumShader, buffer, hizData);
                    //绘制二次剔除结果
                    buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthIdentifier);
                    PipelineFunctions.DrawRecheckCullResult(ref data.baseBuffer, proceduralBlock, proceduralMaterial, buffer);
                    buffer.Blit(cam.targets.depthTexture, hizData.historyDepth, linearMat, 0);
                    hizDepth.GetMipMap(hizData.historyDepth, buffer);
                    break;

            }
            data.ExecuteCommandBuffer();
        }
    }
    public class HizOcclusionData : IPerCameraData
    {
        public Vector3 lastFrameCameraUp = Vector3.up;
        public RenderTexture historyDepth;
        public HizOcclusionData()
        {
            historyDepth = new RenderTexture(512, 256, 0, RenderTextureFormat.RHalf);
            historyDepth.useMipMap = true;
            historyDepth.autoGenerateMips = false;
            historyDepth.filterMode = FilterMode.Point;
            historyDepth.wrapMode = TextureWrapMode.Clamp;
        }
        public override void DisposeProperty()
        {
            historyDepth.Release();
            Object.Destroy(historyDepth);
        }
    }
}