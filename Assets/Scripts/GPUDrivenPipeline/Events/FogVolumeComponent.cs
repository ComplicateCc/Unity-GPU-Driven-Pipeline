using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    public unsafe class FogVolumeComponent : MonoBehaviour
    {
        public struct FogVolumeContainer
        {
            public FogVolume volume;
            public void* light;
        }
        
        public static NativeList<FogVolumeContainer> allVolumes;
        public FogVolumeContainer* container = null;
        public float volume = 1;
        private void OnEnable()
        {
            if (!allVolumes.isCreated)
                allVolumes = new NativeList<FogVolumeContainer>(30, Allocator.Persistent);

            FogVolumeContainer currentcon;
            currentcon.light = MUnsafeUtility.GetManagedPtr(this);
            float3x3 localToWorld = new float3x3
            {
                c0 = transform.right,
                c1 = transform.up,
                c2 = transform.forward
            };
            float4x4 worldToLocal = new float4x4
            {
                c0 = float4(localToWorld.c0, 0),
                c1 = float4(localToWorld.c1, 0),
                c2 = float4(localToWorld.c2, 0),
                c3 = float4(transform.position, 1)
            };
            FogVolume volume = new FogVolume
            {
                extent = transform.localScale * 0.5f,
                localToWorld = localToWorld,
                position = transform.position,
                worldToLocal = inverse(worldToLocal),
                targetVolume = this.volume
            };
            currentcon.volume = volume;
            allVolumes.Add(currentcon);
            container = allVolumes.unsafePtr + allVolumes.Length - 1;
        }

        private void OnDisable()
        {
            *container = allVolumes[allVolumes.Length - 1];
            FogVolumeComponent lastComp = MUnsafeUtility.GetObject<FogVolumeComponent>(container->light);
            lastComp.container = container;
            allVolumes.RemoveLast();
        }
    }
}