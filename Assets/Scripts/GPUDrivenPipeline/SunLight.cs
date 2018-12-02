using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using MPipeline;
[RequireComponent(typeof(Light))]
public class SunLight : MonoBehaviour
{
    public static SunLight current = null;
    public bool enableShadow = true;
    public ShadowmapSettings settings;
    public static ShadowMapComponent shadMap;
    private void Awake()
    {
        var light = GetComponent<Light>();
        if (current)
        {
            Debug.Log("Sun Light Should be Singleton!");
            Destroy(light);
            Destroy(this);
            return;
        }
        current = this;
        shadMap.light = light;
        light.enabled = false;
        shadMap.shadowmapTexture = new RenderTexture(settings.resolution, settings.resolution, 16, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        shadMap.shadowmapTexture.useMipMap = false;
        shadMap.shadowmapTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        shadMap.shadowmapTexture.volumeDepth = 4;
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

    private void OnDestroy()
    {
        if (current != this) return;
        current = null;
        shadMap.frustumCorners.Dispose();
        shadMap.shadowmapTexture.Release();
        Destroy(shadMap.shadowmapTexture);
        Destroy(shadMap.shadowDepthMaterial);
        shadMap.shadowFrustumPlanes.Dispose();
    }
}
