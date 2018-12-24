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
        public int shadowIndex;
    }
    public unsafe class CBDRSharedData : PipelineSharedData
    {
        public ComputeShader cbdrShader;
        public RenderTexture xyPlaneTexture;
        public RenderTexture zPlaneTexture;
        public ComputeBuffer allPointLightBuffer;
        public ComputeBuffer pointlightIndexBuffer;
        public Texture2D randomTex { get; private set; }
        public const int XRES = 32;
        public const int YRES = 16;
        public const int ZRES = 512;
        public const int MAXLIGHTPERCLUSTER = 8;
        public bool directLightEnabled = false;
        public bool directLightShadowEnable = false;
        public bool pointLightEnabled = false;
        public const int pointLightInitCapacity = 50;

        public CBDRSharedData(PipelineResources res)
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
                width = XRES,
                height = YRES,
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
            pointlightIndexBuffer = new ComputeBuffer(XRES * YRES * ZRES * (MAXLIGHTPERCLUSTER + 1), sizeof(int));
            allPointLightBuffer = new ComputeBuffer(pointLightInitCapacity, sizeof(PointLightStruct));
            randomTex = new Texture2D(1024, 2, TextureFormat.RGBAFloat, false, true);
            Color[] colors = new Color[2048];
            for(int i = 0; i < 2048; ++i)
            {
                colors[i] = new Color(Random.value, Random.value, Random.value, Random.value);
            }
            randomTex.SetPixels(colors);
            randomTex.Apply();
        }
        public static void ResizeBuffer(ComputeBuffer buffer, int newCapacity)
        {
            if (newCapacity <= buffer.count) return;
            buffer.Dispose();
            buffer = new ComputeBuffer(newCapacity, buffer.stride);
        }
        public const int SetXYPlaneKernel = 0;
        public const int SetZPlaneKernel = 1;
        public const int PointLightKernel = 2;

        public override void Dispose()
        {
            xyPlaneTexture.Release();
            zPlaneTexture.Release();
            pointlightIndexBuffer.Dispose();
            allPointLightBuffer.Dispose();
            Object.Destroy(randomTex);
        }
    }
}
