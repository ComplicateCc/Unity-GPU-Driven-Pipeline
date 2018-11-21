using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public class ClusterMatResources : ScriptableObject
    {
        [System.Serializable]
        public struct ClusterProperty
        {
            public string name;
            public int clusterCount;
        }
        public List<ClusterProperty> clusterProperties;
        public PropertyValue[] values;
        public List<Pair> textures;
    }
    
}
