using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public class ClusterMatResources : ScriptableObject
    {
        public int clusterCount;
        public PropertyValue[] values;
        public List<Pair> textures;
    }
}
