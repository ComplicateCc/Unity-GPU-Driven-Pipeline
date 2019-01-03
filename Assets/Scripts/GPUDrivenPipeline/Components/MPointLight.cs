using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
public unsafe class MPointLight : MonoBehaviour
{
    public static List<MPointLight> allPointLights = new List<MPointLight>();
    public bool useShadow = true;
    public float range = 5;
    public Color color = Color.white;
    public float intensity = 1;
    public float radius = 1;
    public float length = 1;
    private int index;
    [System.NonSerialized]
    public Vector3 position;
    [System.NonSerialized]
    public RenderTexture shadowMap;
    [System.NonSerialized]
    public int frameCount = -1;

    private void Awake()
    {
        shadowMap = RenderTexture.GetTemporary(new RenderTextureDescriptor
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
    }

    private void OnEnable()
    {
        position = transform.position;
        index = allPointLights.Count;
        allPointLights.Add(this);
    }

    private void OnDisable()
    {
        if (allPointLights.Count <= 1)
        {
            allPointLights.Clear();
            return;
        }
        
        int last = allPointLights.Count - 1;
        allPointLights[index] = allPointLights[last];
        allPointLights[index].index = index;
        allPointLights.RemoveAt(last);
    }
}