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
        public RenderTexture tileLightList;
        public RenderTexture froxelTileLightList;
        public ComputeBuffer allPointLightBuffer;
        public ComputeBuffer pointlightIndexBuffer;
        public const int XRES = 32;
        public const int YRES = 16;
        public const int ZRES = 16;
        public const int MAXLIGHTPERCLUSTER = 16;
        public const int MAXLIGHTPERTILE = 128;
        public const int FROXELMAXLIGHTPERTILE = 32;
        public const int pointLightInitCapacity = 50;
        public uint lightFlag = 0;
        public bool useFroxel = false;
        public float availiableDistance;

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
            desc.width = XRES;
            desc.height = YRES;
            desc.volumeDepth = MAXLIGHTPERTILE;
            desc.colorFormat = RenderTextureFormat.RInt;
            desc.dimension = TextureDimension.Tex3D;
            tileLightList = new RenderTexture(desc);
            tileLightList.Create();
            desc.volumeDepth = FROXELMAXLIGHTPERTILE;
            froxelTileLightList = new RenderTexture(desc);
            froxelTileLightList.Create();
            Debug.Log("Created");

        }
        public static void ResizeBuffer(ref ComputeBuffer buffer, int newCapacity)
        {
            if (newCapacity <= buffer.count) return;
            buffer.Dispose();
            buffer = new ComputeBuffer(newCapacity, buffer.stride);
        }
        public const int SetXYPlaneKernel = 0;
        public const int SetZPlaneKernel = 1;
        public const int DeferredCBDR = 2;
        public int TBDRKernel
        {
            get
            {
                return useFroxel ? 4 : 3;
            }
        }

        public int ClearKernel
        {
            get
            {
                return useFroxel ? 6 : 5;
            }
        }


        public override void Dispose()
        {
            xyPlaneTexture.Release();
            zPlaneTexture.Release();
            pointlightIndexBuffer.Dispose();
            allPointLightBuffer.Dispose();
            Object.Destroy(froxelTileLightList); 
            Object.Destroy(tileLightList); 
        }
    }
}
