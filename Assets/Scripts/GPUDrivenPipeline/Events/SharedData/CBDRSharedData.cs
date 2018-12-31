using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using Unity.Jobs;
using Random = UnityEngine.Random;
namespace MPipeline
{
    public struct PointLightStruct
    {
        public float3 lightColor;
        public float lightIntensity;
        public float4 sphere;
        public int shadowIndex;
    }
    public unsafe class CBDRSharedData : PipelineSharedData
    {
        public ComputeShader cbdrShader;
        public RenderTexture dirLightShadowmap;
        public RenderTexture cubemapShadowArray;
        public RenderTexture xyPlaneTexture;
        public RenderTexture zPlaneTexture;
        public RenderTexture froxelZPlaneTexture;
        public ComputeBuffer allPointLightBuffer;
        public ComputeBuffer pointlightIndexBuffer;
        public ComputeBuffer froxelPointLightBuffer;
        public ComputeBuffer froxelPointLightIndexBuffer;
        public Texture2D randomTex { get; private set; }
        public const int XRES = 32;
        public const int YRES = 16;
        public const int ZRES = 64;
        public const int MAXLIGHTPERCLUSTER = 8;
        public const int pointLightInitCapacity = 50;
        public uint lightFlag = 0;
        public NativeArray<PointLightStruct> pointLightArray;
        public int* pointLightCount = null;
       

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
            froxelZPlaneTexture = new RenderTexture(zPlaneTexture.descriptor);
            froxelPointLightIndexBuffer = new ComputeBuffer(pointlightIndexBuffer.count, pointlightIndexBuffer.stride);
            froxelPointLightBuffer = new ComputeBuffer(allPointLightBuffer.count, allPointLightBuffer.stride);
            randomTex = new Texture2D(1024, 2, TextureFormat.RGBAFloat, false, true);
            Color[] colors = new Color[2048];
            for(int i = 0; i < 2048; ++i)
            {
                colors[i] = new Color(Random.value, Random.value, Random.value, Random.value);
            }
            randomTex.SetPixels(colors);
            randomTex.Apply();
        }
        public static void ResizeBuffer(ref ComputeBuffer buffer, int newCapacity)
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
            froxelPointLightBuffer.Dispose();
            froxelPointLightIndexBuffer.Dispose();
            Object.Destroy(froxelZPlaneTexture); 
            Object.Destroy(randomTex);
        }
    }
}
