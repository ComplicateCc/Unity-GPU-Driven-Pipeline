using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{

    public unsafe class Decal : MonoBehaviour
    {
        private unsafe struct DecalComponent
        {
            public DecalData data;
            public void* comp;
        }
        public float2 startUV = 0;
        public float2 endUV = 1;
        private static NativeList<DecalComponent> decalDatas;
        public static int allDecalCount
        {
            get
            {
                if (decalDatas.isCreated)
                    return decalDatas.Length;
                return 0;
            }
        }
        public static ref DecalData GetData(int index)
        {
            return ref decalDatas[index].data;
        }
        private int index;
        private void Awake()
        {
            if (!decalDatas.isCreated) decalDatas = new NativeList<DecalComponent>(10, Unity.Collections.Allocator.Persistent);
            float4x4 localToWorld = transform.localToWorldMatrix;
            decalDatas.Add(new DecalComponent
            {
                comp = MUnsafeUtility.GetManagedPtr(this),
                data = new DecalData
                {
                    endUV = endUV,
                    position = transform.position,
                    rotation = float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz),
                    startUV = startUV
                }
            });
        }

        private void OnDestroy()
        {
            Decal lastDec = MUnsafeUtility.GetObject<Decal>(decalDatas[decalDatas.Length - 1].comp);
            lastDec.index = index;
            decalDatas[index] = decalDatas[decalDatas.Length - 1];
            decalDatas.RemoveLast();
        }
    }
}
