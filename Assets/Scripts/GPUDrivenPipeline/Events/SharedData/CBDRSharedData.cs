using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
namespace MPipeline
{
    public struct PointLightStruct
    {
        public Vector3 lightColor;
        public float lightIntensity;
        public Vector4 sphere;
    }
    public unsafe class CBDRSharedData : PipelineSharedData
    {
        private ComputeShader cbdrShader;
        private RenderTexture xyPlaneTexture;
        private RenderTexture zPlaneTexture;
        private RenderTexture pointLightTexture;
        private ComputeBuffer allPointLightBuffer;
        private ComputeBuffer pointlightIndexBuffer;

        const int RES = 16;
        const int ZRES = 128;
        const int MAXLIGHTPERCLUSTER = 16;

        public CBDRSharedData(PipelineResources res, int pointLightInitCapacity)
        {
            cbdrShader = res.cbdrShader;
            RenderTextureDescriptor desc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBFloat,
                depthBufferBits = 0,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                width = RES,
                height = RES,
                volumeDepth = 4,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            };
            xyPlaneTexture = new RenderTexture(desc);
            xyPlaneTexture.filterMode = FilterMode.Point;
            xyPlaneTexture.Create();
            desc.dimension = TextureDimension.Tex2D;
            desc.volumeDepth = 1;
            desc.width = ZRES;
            desc.height = 2;
            zPlaneTexture = new RenderTexture(desc);
            zPlaneTexture.Create();
            desc.dimension = TextureDimension.Tex3D;
            desc.colorFormat = RenderTextureFormat.RGInt;
            desc.width = RES;
            desc.height = RES;
            desc.volumeDepth = ZRES;
            pointLightTexture = new RenderTexture(desc);
            pointLightTexture.Create();
            pointlightIndexBuffer = new ComputeBuffer(RES * RES * ZRES * MAXLIGHTPERCLUSTER, sizeof(int));
            allPointLightBuffer = new ComputeBuffer(pointLightInitCapacity, sizeof(PointLightStruct));
        }

        private void ResizeLightBuffer(int newCapacity)
        {
            if (newCapacity <= allPointLightBuffer.count) return;
            allPointLightBuffer.Dispose();
            allPointLightBuffer = new ComputeBuffer(newCapacity, sizeof(PointLightStruct));
        }
        const int SetXYPlaneKernel = 0;
        const int SetZPlaneKernel = 1;
        const int CBDRKernel = 2;

        public void SetDatas(Camera cam, NativeArray<PointLightStruct> arr, int length, CommandBuffer buffer)
        {
            if (length <= 0) return;
            ResizeLightBuffer(length);
            allPointLightBuffer.SetData(arr, 0, 0, length);
            buffer.SetComputeTextureParam(cbdrShader, SetXYPlaneKernel, ShaderIDs._XYPlaneTexture, xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, SetZPlaneKernel, ShaderIDs._ZPlaneTexture, zPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRKernel, ShaderIDs._XYPlaneTexture, xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRKernel, ShaderIDs._ZPlaneTexture, zPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRKernel, ShaderIDs._PointLightTexture, pointLightTexture);
            buffer.SetComputeBufferParam(cbdrShader, CBDRKernel, ShaderIDs._AllPointLight, allPointLightBuffer);
            buffer.SetComputeBufferParam(cbdrShader, CBDRKernel, ShaderIDs._PointLightIndexBuffer, pointlightIndexBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._AllPointLight, allPointLightBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._PointLightIndexBuffer, pointlightIndexBuffer);
            Transform camTrans = cam.transform;
            buffer.SetGlobalVector(ShaderIDs._CameraFarPos, camTrans.position + cam.farClipPlane * camTrans.forward);
            buffer.SetGlobalVector(ShaderIDs._CameraNearPos, camTrans.position + cam.nearClipPlane * camTrans.forward);
            buffer.SetGlobalVector(ShaderIDs._CameraForward, camTrans.forward);
            buffer.SetGlobalInt(ShaderIDs._PointLightCount, length);
            buffer.DispatchCompute(cbdrShader, SetXYPlaneKernel, 1, 1, 1);
            buffer.DispatchCompute(cbdrShader, SetZPlaneKernel, 1, 1, 1);
            buffer.DispatchCompute(cbdrShader, CBDRKernel, 1, 1, ZRES);
            buffer.SetGlobalTexture(ShaderIDs._PointLightTexture, pointLightTexture);
        }
        public void Dispose()
        {
            xyPlaneTexture.Release();
            zPlaneTexture.Release();
            pointlightIndexBuffer.Dispose();
            allPointLightBuffer.Dispose();
            pointLightTexture.Release();
        }
    }
}
