using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
namespace MPipeline
{
    public unsafe struct CBDRSharedData
    {
        public ComputeShader cbdrShader;
        public RenderTexture spotArrayMap;
        public RenderTexture cubeArrayMap;
        public RenderTexture dirLightShadowmap;
        public RenderTexture xyPlaneTexture;
        public RenderTexture zPlaneTexture;
        public RenderTexture pointTileLightList;
        public RenderTexture spotTileLightList;
        public ComputeBuffer allFogVolumeBuffer;
        public ComputeBuffer allPointLightBuffer;
        public ComputeBuffer allSpotLightBuffer;
        public ComputeBuffer pointlightIndexBuffer;
        public ComputeBuffer spotlightIndexBuffer;
        public const int XRES = 32;
        public const int YRES = 16;
        public const int ZRES = 128;
        public const int MAXLIGHTPERCLUSTER = 16;
        public const int MAXPOINTLIGHTPERTILE = 64;
        public const int MAXSPOTLIGHTPERTILE = 64;
        public const int pointLightInitCapacity = 50;
        public const int spotLightInitCapacity = 50;
        public const int SetXYPlaneKernel = 0;
        public const int SetZPlaneKernel = 1;
        public const int DeferredCBDR = 2;
        public const int MAXIMUMPOINTLIGHTCOUNT = 4;
        public const int MAXIMUMSPOTLIGHTCOUNT = 8;
        public uint lightFlag;
        public float availiableDistance;
        public int spotShadowCount;
        public int pointshadowCount;
        public bool CheckAvailiable()
        {
            return spotArrayMap != null && cubeArrayMap != null;
        }
        public CBDRSharedData(PipelineResources res)
        {
            dirLightShadowmap = null;
            availiableDistance = 0;
            spotShadowCount = 0;
            pointshadowCount = 0;
            lightFlag = 0;
            cbdrShader = res.shaders.cbdrShader;
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
            cubeArrayMap = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RHalf,
                depthBufferBits = 16,
                dimension = TextureDimension.CubeArray,
                volumeDepth = 6 * MAXIMUMPOINTLIGHTCOUNT,
                enableRandomWrite = false,
                height = MLight.cubemapShadowResolution,
                width = MLight.cubemapShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            });
            cubeArrayMap.filterMode = FilterMode.Bilinear;
            cubeArrayMap.Create();
            spotArrayMap = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RHalf,
                depthBufferBits = 16,
                dimension = TextureDimension.Tex2DArray,
                enableRandomWrite = false,
                height = MLight.perspShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = MAXIMUMSPOTLIGHTCOUNT,
                vrUsage = VRTextureUsage.None,
                width = MLight.perspShadowResolution
            });
            spotArrayMap.filterMode = FilterMode.Bilinear;
            spotArrayMap.Create();
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
            allFogVolumeBuffer = new ComputeBuffer(30, sizeof(FogVolume));
        }
        public static void ResizeBuffer(ref ComputeBuffer buffer, int newCapacity)
        {
            if (newCapacity <= buffer.count) return;
            buffer.Dispose();
            buffer = new ComputeBuffer(newCapacity, buffer.stride);
        }
        public int TBDRPointKernel
        {
            get
            {
                return 3;
            }
        }

        public int ClearKernel
        {
            get
            {
                return 4;
            }
        }

        public int TBDRSpotKernel
        {
            get
            {
                return 5;
            }
        }

        public void Dispose()
        {
            xyPlaneTexture.Release();
            zPlaneTexture.Release();
            pointlightIndexBuffer.Dispose();
            allPointLightBuffer.Dispose();
            allSpotLightBuffer.Dispose();
            spotlightIndexBuffer.Dispose();
            allFogVolumeBuffer.Dispose();
            Object.DestroyImmediate(pointTileLightList);
            Object.DestroyImmediate(spotTileLightList);
            Object.DestroyImmediate(cubeArrayMap);
            Object.DestroyImmediate(spotArrayMap);
        }
    }
}
