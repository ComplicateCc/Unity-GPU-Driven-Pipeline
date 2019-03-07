using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System;
[CreateAssetMenu(menuName = "Irradiance Resources")]
public class IrradianceResources : ScriptableObject
{
    [Serializable]
    public struct Volume
    {
        public uint3 resolution;
        public float3 size;
        public float3 position;
        public string volumeName;
        public string path;
    }
    public List<Volume> allVolume;
}
