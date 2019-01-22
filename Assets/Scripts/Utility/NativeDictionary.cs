using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using System;
namespace MPipeline
{
    unsafe struct DictData
    {
        public int capacity;
        public int length;
        public void* start;
        public Allocator alloc;
    }
    public unsafe struct NativeDictionary<K, V> where K : unmanaged where V : unmanaged
    {
        static readonly int stride = sizeof(K) + sizeof(V) + 8;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static V* GetV(K* ptr)
        {
            return (V*)(ptr + 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static K** GetNextPtr(K* ptr)
        {
            ulong num = (ulong)ptr;
            num += (ulong)(sizeof(K) + sizeof(V));
            return (K**)num;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private K** GetK(int index)
        {
            return (K**)data->start + index;
        }
        private DictData* data;
        public bool isCreated { get; private set; }
        public Func<K, K, bool> equalsFunc;
        private NativeList<ulong> container;
        private T* Malloc<T>(int size, Allocator alloc) where T : unmanaged
        {
            T* result =(T*)UnsafeUtility.Malloc(size, 16, alloc);
            container.Add((ulong)result);
            return result;
        }
        public NativeDictionary(int capacity, Allocator alloc, Func<K, K, bool> equals)
        {
            equalsFunc = equals;
            isCreated = true;
            data = (DictData*)UnsafeUtility.Malloc(sizeof(DictData), 16, alloc);
            data->capacity = capacity;
            data->length = 0;
            data->alloc = alloc;
            data->start = UnsafeUtility.Malloc(8 * capacity, 16, alloc);
            UnsafeUtility.MemClear(data->start, 8 * capacity);
            container = new NativeList<ulong>(capacity, alloc);
        }

        public void Add(K key, V value)
        {
            //TODO
            //Resize
            int index = key.GetHashCode() % data->capacity;
            K** currentPos = GetK(index);
            while((*currentPos) != null)
            {
                currentPos = GetNextPtr(*currentPos);
            }
            (*currentPos) = Malloc<K>(stride, data->alloc);
            (**currentPos) = key;
            (*GetV(*currentPos)) = value;
            (*GetNextPtr(*currentPos)) = null;
        }

        public void Dispose()
        {
            Allocator alloc = data->alloc;
            foreach(var i in container)
            {
                UnsafeUtility.Free((void*)i, alloc);
            }
            UnsafeUtility.Free(data->start, alloc);
            UnsafeUtility.Free(data, alloc);
            container.Dispose();
            isCreated = false;
        }

        public bool Get(K key, out V value)
        {
            int index = key.GetHashCode() % data->capacity;
            K** currentPos = GetK(index);
            while((*currentPos) != null)
            {
                if(equalsFunc(**currentPos, key))
                {
                    value = *GetV(*currentPos);
                    return true;
                }
                currentPos = GetNextPtr(*currentPos);
            }
            value = default;
            return false;
        }
    }
}
