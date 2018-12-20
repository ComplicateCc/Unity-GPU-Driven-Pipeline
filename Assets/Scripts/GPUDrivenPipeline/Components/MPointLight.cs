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
    
    private void Update()
    {
        position = transform.position;
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
