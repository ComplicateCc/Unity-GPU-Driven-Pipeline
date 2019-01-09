using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using MPipeline;
[ExecuteInEditMode]
[RequireComponent(typeof(Light))]
public class SunLight : MonoBehaviour
{
    public static SunLight current = null;
    public bool enableShadow = true;
    public ShadowmapSettings settings;
    public static ShadowMapComponent shadMap;
    public static Camera shadowCam;
    private void OnEnable()
    {
        var light = GetComponent<Light>();
        if (current)
        {
            if (current != this)
            {
                Debug.Log("Sun Light Should be Singleton!");
                Destroy(light);
                Destroy(this);
                return;
            }
            else
                OnDisable();
        }
        current = this;
        shadMap.light = light;
        light.enabled = false;
        if(!shadowCam)
        {
            shadowCam = GetComponent<Camera>();
            if(!shadowCam)
            {
                shadowCam = gameObject.AddComponent<Camera>();
            }
            shadowCam.enabled = false;
            shadowCam.aspect = 1;
            
        }
        shadMap.shadowmapTexture = new RenderTexture(new RenderTextureDescriptor
        {
            width = settings.resolution,
            height = settings.resolution,
            depthBufferBits = 16,
            colorFormat = RenderTextureFormat.RFloat,
            autoGenerateMips = false,
            bindMS = false,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
            enableRandomWrite = false,
            memoryless = RenderTextureMemoryless.None,
            shadowSamplingMode = UnityEngine.Rendering.ShadowSamplingMode.None,
            msaaSamples = 1,
            sRGB = false,
            useMipMap = false,
            volumeDepth = 4,
            vrUsage = VRTextureUsage.None
        });
        shadMap.cameraComponent = shadowCam;
        shadMap.shadowmapTexture.filterMode = FilterMode.Point;
        shadMap.frustumCorners = new NativeArray<Vector3>(8, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        shadMap.shadowDepthMaterial = new Material(Shader.Find("Hidden/ShadowDepth"));
        shadMap.shadowFrustumPlanes = new NativeArray<AspectInfo>(3, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    private void Update()
    {
        shadMap.shadCam.forward = transform.forward;
        shadMap.shadCam.up = transform.up;
        shadMap.shadCam.right = transform.right;
    }

    private void OnDisable()
    {
        if (current != this) return;
        current = null;
        shadMap.frustumCorners.Dispose();
        shadMap.shadowmapTexture.Release();
        DestroyImmediate(shadMap.shadowmapTexture);
        DestroyImmediate(shadMap.shadowDepthMaterial);
        shadMap.shadowFrustumPlanes.Dispose();
    }
}
