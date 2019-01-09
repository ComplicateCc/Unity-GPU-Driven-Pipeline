using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using UnityEngine.Jobs;
[ExecuteInEditMode]
public unsafe class MLight : MonoBehaviour
{
    public const int cubemapShadowResolution = 1024;
    public const int perspShadowResolution = 2048;
    [System.NonSerialized]
    public RenderTexture shadowMap;
    [System.NonSerialized]
    public int frameCount = -1;
    [System.NonSerialized]
    public Light light;
    private static Dictionary<Light, MLight> lightDict = new Dictionary<Light, MLight>(47);
    [System.NonSerialized]
    public bool useCubemap;
    public Camera shadowCam { get; private set; }
    public static void ClearLightDict()
    {
        lightDict.Clear();
    }
    public static MLight GetPointLight(Light light)
    {
        MLight mp;
        if (lightDict.TryGetValue(light, out mp)) return mp;
        mp = light.GetComponent<MLight>();
        if (mp)
        {
            lightDict.Add(light, mp);
            return mp;
        }
        mp = light.gameObject.AddComponent<MLight>();
        return mp;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateShadowCacheType(bool useCubemap)
    {
        if (useCubemap == this.useCubemap)
            return;
        useCubemap = this.useCubemap;
        if (shadowMap)
        shadowMap.Release();
        if (useCubemap)
        {
            shadowMap = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = 6,
                enableRandomWrite = false,
                height = cubemapShadowResolution,
                width = cubemapShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            });
        }
        else
        {
            shadowMap = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                enableRandomWrite = false,
                height = perspShadowResolution,
                width = perspShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            });
        }
        shadowMap.Create();
    }
    private void OnEnable()
    {
        light = GetComponent<Light>();
        lightDict.Add(light, this);
        if(!shadowCam)
        {
            shadowCam = GetComponent<Camera>();
            if(!shadowCam)
            {
                shadowCam = gameObject.AddComponent<Camera>();
            }
            shadowCam.enabled = false;
        }
        useCubemap = light.type == LightType.Point;
        shadowCam.orthographic = false;
        if (useCubemap)
        {
            shadowMap = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = 6,
                enableRandomWrite = false,
                height = cubemapShadowResolution,
                width = cubemapShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            });
        }
        else
        {
            shadowMap = new RenderTexture(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.RHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                enableRandomWrite = false,
                height = perspShadowResolution,
                width = perspShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            });
        }
        shadowMap.Create();
    }


    private void OnDisable()
    {
        if(shadowMap)
        shadowMap.Release();
        if(light)
        lightDict.Remove(light);
    }
}