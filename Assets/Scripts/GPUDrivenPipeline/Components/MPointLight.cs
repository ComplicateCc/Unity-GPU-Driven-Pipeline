using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

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
    public RenderTexture shadowmapTexture
    {
        get
        {
            if(m_shadowmapTexture == null)
            {
                m_shadowmapTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
                m_shadowmapTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
                m_shadowmapTexture.filterMode = FilterMode.Point;
            }
            return m_shadowmapTexture;
        }
    }
    private RenderTexture m_shadowmapTexture;
    /*
    private void Update()
    {
        position = transform.position;
    }*/

    private void OnDestroy()
    {
        if(m_shadowmapTexture != null)
        {
            m_shadowmapTexture.Release();
            Destroy(m_shadowmapTexture);
        }
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
