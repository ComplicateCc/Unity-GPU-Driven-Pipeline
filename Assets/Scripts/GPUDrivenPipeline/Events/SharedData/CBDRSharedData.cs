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
    public struct Cone
    {
        public float3 vertex;
        public float height;
        public float3 direction;
        public float radius;
        public Cone(float3 position, float distance, float3 direction, float angle)
        {
            vertex = position;
            height = distance;
            this.direction = direction;
            radius = math.tan(angle) * height;
        }
    }
    public struct Capsule
    {
        public float3 direction;
        public float3 position;
        public float radius;
    }
    public struct SpotLight
    {
        public float3 lightColor;
        public float lightIntensity;
        public Cone lightCone;
        public float angle;
        public Matrix4x4 vpMatrix;
        public int shadowIndex;
    };
    public unsafe class CBDRSharedData : PipelineSharedData
    {
        public ComputeShader cbdrShader;
        public RenderTexture dirLightShadowmap;
        public RenderTexture cubemapShadowArray;
        public RenderTexture spotShadowArray;
        public RenderTexture xyPlaneTexture;
        public RenderTexture zPlaneTexture;
        public RenderTexture pointTileLightList;
        public RenderTexture spotTileLightList;
        public RenderTexture froxelpointTileLightList;
        public RenderTexture froxelSpotTileLightList;
        public ComputeBuffer allPointLightBuffer;
        public ComputeBuffer allSpotLightBuffer;
        public ComputeBuffer pointlightIndexBuffer;
        public ComputeBuffer spotlightIndexBuffer;
        public const int XRES = 32;
        public const int YRES = 16;
        public const int ZRES = 64;
        public const int MAXLIGHTPERCLUSTER = 16;
        public const int MAXPOINTLIGHTPERTILE = 64;
        public const int MAXSPOTLIGHTPERTILE = 64;
        public const int FROXELMAXPOINTLIGHTPERTILE = 32;
        public const int FROXELMAXSPOTLIGHTPERTILE = 32;
        public const int pointLightInitCapacity = 50;
        public const int spotLightInitCapacity = 50;
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
            spotlightIndexBuffer = new ComputeBuffer(XRES * YRES * ZRES * (MAXLIGHTPERCLUSTER + 1), sizeof(int));
            allPointLightBuffer = new ComputeBuffer(pointLightInitCapacity, sizeof(PointLightStruct));
            allSpotLightBuffer = new ComputeBuffer(pointLightInitCapacity, sizeof(SpotLight));
            desc.width = XRES;
            desc.height = YRES;
            desc.volumeDepth = MAXPOINTLIGHTPERTILE;
            desc.colorFormat = RenderTextureFormat.RInt;
            desc.dimension = TextureDimension.Tex3D;
            pointTileLightList = new RenderTexture(desc);
            pointTileLightList.Create();
            desc.volumeDepth = MAXSPOTLIGHTPERTILE;
            spotTileLightList = new RenderTexture(desc);
            spotTileLightList.Create();
            desc.volumeDepth = FROXELMAXPOINTLIGHTPERTILE;
            froxelpointTileLightList = new RenderTexture(desc);
            froxelpointTileLightList.Create();
            desc.volumeDepth = FROXELMAXSPOTLIGHTPERTILE;
            froxelSpotTileLightList = new RenderTexture(desc);
            froxelSpotTileLightList.Create();

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
        public int TBDRPointKernel
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

        public int TBDRSpotKernel
        {
            get
            {
                return useFroxel ? 8 : 7;
            }
        }

        public override void Dispose()
        {
            xyPlaneTexture.Release();
            zPlaneTexture.Release();
            pointlightIndexBuffer.Dispose();
            allPointLightBuffer.Dispose();
            allSpotLightBuffer.Dispose();
            spotlightIndexBuffer.Dispose();
            Object.Destroy(froxelpointTileLightList);
            Object.Destroy(froxelSpotTileLightList);
            Object.Destroy(pointTileLightList);
            Object.Destroy(spotTileLightList);
        }
    }
}
