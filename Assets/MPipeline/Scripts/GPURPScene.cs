using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public class GPURPScene : MonoBehaviour
    {
        public PipelineResources resources;
        [Header("Material Settings:")]
        public int resolution = 1024;
        public int texArrayCapacity = 50;
        public int propertyCapacity = 500;
        public int lightmapCapacity = 5;
        public int lightmapAtlasSize = 4096;
        public string mapResources = "SceneManager";
        private GPURPScene current;

     
        private static void SetBuffer(Transform trans, TransformAccessArray array)
        {
            if (trans.childCount > 0)
            {
                for (int i = 0; i < trans.childCount; ++i)
                {
                    SetBuffer(trans.GetChild(i), array);
                }
            }
            else
            {
                array.Add(trans);
            }
        }
        private JobHandle jobHandle;
        public float3 offset;
        private void Awake()
        {
            if (current != null)
            {
                Debug.LogError("GPU RP Scene should be singleton!");
                Destroy(this);
                return;
            }
            current = this;
            SceneController.Awake(resources, resolution, texArrayCapacity, lightmapAtlasSize, lightmapCapacity, propertyCapacity, mapResources);
        }

        public int targetVolume = 0;
        private void Update()
        {
            SceneController.Update(this);
        }

        private void OnDestroy()
        {
            SceneController.Dispose();
            current = null;
        }
    }
}
