using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
public unsafe class MPointLight : MonoBehaviour
{
    [System.NonSerialized]
    public RenderTexture shadowMap;
    [System.NonSerialized]
    public int frameCount = -1;
    [System.NonSerialized]
    public Light light;
    private static Dictionary<Light, MPointLight> lightDict = new Dictionary<Light, MPointLight>(47);
    public static MPointLight GetPointLight(Light light)
    {
        MPointLight mp;
        if (lightDict.TryGetValue(light, out mp)) return mp;
        mp = light.gameObject.AddComponent<MPointLight>();
        return mp;
    }
    private void Awake()
    {
        light = GetComponent<Light>();
        lightDict.Add(light, this);
        shadowMap = new RenderTexture(new RenderTextureDescriptor
        {
            autoGenerateMips = false,
            bindMS = false,
            colorFormat = RenderTextureFormat.RHalf,
            depthBufferBits = 0,
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = 6,
            enableRandomWrite = false,
            height = 1024,
            width = 1024,
            memoryless = RenderTextureMemoryless.None,
            msaaSamples = 1,
            shadowSamplingMode = ShadowSamplingMode.None,
            sRGB = false,
            useMipMap = false,
            vrUsage = VRTextureUsage.None
        });
        shadowMap.Create();
    }

    private void OnDestroy()
    {
        shadowMap.Release();
        lightDict.Remove(light);
    }
}