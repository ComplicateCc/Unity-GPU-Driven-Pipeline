using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using UnityEngine.Jobs;
#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(MLight))]
public class MLightEditor : Editor
{
    private MLight target;
    private void OnEnable()
    {
        target = serializedObject.targetObject as MLight;
    }
    public override void OnInspectorGUI()
    {
        if (!target.enabled || !target.gameObject.activeSelf) return;
        target.useShadow = EditorGUILayout.Toggle("Use Shadow", target.useShadow);
        target.useShadowCache = EditorGUILayout.Toggle("Use Shadow Cache", target.useShadowCache);
        target.spotNearClip = EditorGUILayout.Slider("Spot Nearclip", target.spotNearClip, 0, target.light.range);
        target.smallSpotAngle = EditorGUILayout.Slider("Small Spotangle", target.smallSpotAngle, 0, target.light.spotAngle);
    }
}
#endif
[ExecuteInEditMode]
public unsafe class MLight : MonoBehaviour
{
    public const int cubemapShadowResolution = 1024;
    public const int perspShadowResolution = 2048;
    [SerializeField]
    private bool m_useShadow = false;
    public bool useShadow
    {
        get
        {
            return m_useShadow;
        }
        set
        {
            if (m_useShadow != value)
            {
                m_useShadow = value;
                updateShadowCache = true;
                if (value)
                {
                    if (m_useShadowCache && !shadowMap)
                    {
                        GenerateShadowCache();
                    }

                }
                else
                {
                    if (m_useShadowCache && shadowMap)
                    {
                        ReleaseShadowCache();
                    }
                }
            }
        }
    }
    public bool updateShadowCache;
    [SerializeField]
    private bool m_useShadowCache;
    public bool useShadowCache
    {
        get
        {
            return m_useShadowCache;
        }
        set
        {
            if (m_useShadowCache != value)
            {
                m_useShadowCache = value;
                if (value)
                {
                    updateShadowCache = true;
                    if (useShadow)
                    {
                        GenerateShadowCache();
                    }
                }
                else
                {
                    if (shadowMap)
                    {
                        ReleaseShadowCache();
                    }
                }
            }
        }
    }
    public float smallSpotAngle = 30;
    public float spotNearClip = 0.3f;
    private static Dictionary<Light, MLight> lightDict = new Dictionary<Light, MLight>(47);
    public RenderTexture shadowMap { get; private set; }
    public Light light { get; private set; }
    private bool useCubemap;
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
    public static bool GetPointLight(Light light, out MLight mLight)
    {
        return lightDict.TryGetValue(light, out mLight);
    }
    public static void AddMLight(Light light)
    {
        MLight mp = light.GetComponent<MLight>();
        if (mp)
            lightDict.Add(light, mp);
        else
            mp = light.gameObject.AddComponent<MLight>();
    }
    private void ReleaseShadowCache()
    {
        RenderTexture.ReleaseTemporary(shadowMap);
        shadowMap = null;
    }

    private void GenerateShadowCache()
    {
        if (useCubemap)
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
                height = cubemapShadowResolution,
                width = cubemapShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            });
            shadowMap.filterMode = FilterMode.Bilinear;
        }
        else
        {
            shadowMap = RenderTexture.GetTemporary(new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.Shadowmap,
                depthBufferBits = 16,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                enableRandomWrite = false,
                height = perspShadowResolution,
                width = perspShadowResolution,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.RawDepth,
                sRGB = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            });
            shadowMap.filterMode = FilterMode.Bilinear;
        }
    }
    public void UpdateShadowCacheType(bool useCubemap)
    {
        if (useCubemap == this.useCubemap)
            return;
        this.useCubemap = useCubemap;
        updateShadowCache = true;
        if (shadowMap)
        {
            ReleaseShadowCache();
        }
        GenerateShadowCache();
    }
    private void OnEnable()
    {
        light = GetComponent<Light>();
        if (light.shadows != LightShadows.None)
        {
            useShadow = true;
            light.shadows = LightShadows.None;
        }
        lightDict.Add(light, this);
        updateShadowCache = true;
        if (!shadowCam)
        {
            shadowCam = GetComponent<Camera>();
            if (!shadowCam)
            {
                shadowCam = gameObject.AddComponent<Camera>();
            }
            shadowCam.hideFlags = HideFlags.HideInInspector;
            shadowCam.projectionMatrix = Matrix4x4.identity;
            shadowCam.enabled = false;
            shadowCam.aspect = 1;
        }
        useCubemap = light.type == LightType.Point;
        if (useShadowCache)
        {
            GenerateShadowCache();
        }
    }

    private void OnDisable()
    {
        if (shadowMap)
        {
            ReleaseShadowCache();
        }
        if (light)
            lightDict.Remove(light);
    }
}