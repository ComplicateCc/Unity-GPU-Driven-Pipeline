using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class OccluderDrawer : MonoBehaviour
{
    public static OccluderDrawer current { get; private set; }
    public List<Renderer> allRenderers = new List<Renderer>();
    void Awake()
    {
        current = this;
    }

    private void OnDestroy()
    {
        current = null;
    }

    public void Drawer(CommandBuffer cb, Material mat)
    {
        foreach(var i in allRenderers)
        {
            cb.DrawRenderer(i, mat, 0, 0);
        }
    }
}
