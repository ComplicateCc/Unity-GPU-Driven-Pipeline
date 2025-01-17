﻿using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using System.Runtime.CompilerServices;
using System;
public interface IObject
{
    void Init();
    void Dispose();
}
public interface IObject<A>
{
    void Init(A a);
    void Dispose();
}

public interface IObject<A, B>
{
    void Init(A a, B b);
    void Dispose();
}

public interface IObject<A, B, C>
{
    void Init(A a, B b, C c);
    void Dispose();
}

public interface IObject<A, B, C, D>
{
    void Init(A a, B b, C c, D d);
    void Dispose();
}
public unsafe static class MUnsafeUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Resize<T>(ref this NativeArray<T> arr, int targetLength, Allocator alloc) where T : unmanaged
    {
        if (targetLength <= arr.Length) return;
        NativeArray<T> newArr = new NativeArray<T>(targetLength, alloc);
        MemCpy(newArr.GetUnsafePtr(), arr.GetUnsafePtr(), sizeof(T) * arr.Length);
        arr.Dispose();
        arr = newArr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Ptr<T>(this NativeArray<T> arr) where T : unmanaged
    {
        return (T*)arr.GetUnsafePtr();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Get<T>(this NativeArray<T> arr, int index) where T : unmanaged
    {
        return ref *((T*)arr.GetUnsafePtr() + index);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyFrom<T>(this T[] array, T* source, int length) where T : unmanaged
    {
        fixed (T* dest = array)
        {
            MemCpy(dest, source, length * sizeof(T));
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Ptr<T>(this T[] array) where T : unmanaged
    {
        return (T*)AddressOf(ref array[0]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Ptr<T>(ref this T array) where T : unmanaged
    {
        return (T*)AddressOf(ref array);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyTo<T>(this T[] array, T* dest, int length) where T : unmanaged
    {
        fixed (T* source = array)
        {
            MemCpy(dest, source, length * sizeof(T));
        }
    }

    private struct PtrKeeper<T>
    {
        public T value;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* GetManagedPtr<T>(T obj) where T : class
    {
        PtrKeeper<T> keeper = new PtrKeeper<T> { value = obj };
        ulong* ptr = (ulong*)AddressOf(ref keeper);
        return (void*)*ptr;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetObject<T>(void* ptr) where T : class
    {
        PtrKeeper<T> keeper = new PtrKeeper<T>();
        ulong* keeperPtr = (ulong*)AddressOf(ref keeper);
        *keeperPtr = (ulong)ptr;
        return keeper.value;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Malloc<T>(long size, Allocator allocator) where T : unmanaged
    {
        long align = size % 16;
        return (T*)UnsafeUtility.Malloc(size, align == 0 ? 16 : (int)align, allocator);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* Malloc(long size, Allocator allocator)
    {
        long align = size % 16;
        return UnsafeUtility.Malloc(size, align == 0 ? 16 : (int)align, allocator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* New<T>(Allocator alloc) where T : unmanaged, IObject
    {
        Allocator* allocPtr = (Allocator*)Malloc(sizeof(T) + sizeof(Allocator), alloc) + sizeof(Allocator);
        *allocPtr = alloc;
        T* t = (T*)(allocPtr + 1);
        t->Init();
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delete<T>(T* value) where T : unmanaged , IObject
    {
        value->Dispose();
        Allocator* allocPtr = ((Allocator*)value) - 1;
        Free(allocPtr, *allocPtr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* New<T, A>(Allocator alloc, A a) where T : unmanaged, IObject<A>
    {
        Allocator* allocPtr = (Allocator*)Malloc(sizeof(T) + sizeof(Allocator), alloc) + sizeof(Allocator);
        *allocPtr = alloc;
        T* t = (T*)(allocPtr + 1);
        t->Init(a);
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delete<T, A>(T* value) where T : unmanaged, IObject<A>
    {
        value->Dispose();
        Allocator* allocPtr = ((Allocator*)value) - 1;
        Free(allocPtr, *allocPtr);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* New<T, A, B>(Allocator alloc, A a, B b) where T : unmanaged, IObject<A, B>
    {
        Allocator* allocPtr = (Allocator*)Malloc(sizeof(T) + sizeof(Allocator), alloc) + sizeof(Allocator);
        *allocPtr = alloc;
        T* t = (T*)(allocPtr + 1);
        t->Init(a, b);
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delete<T, A, B>(T* value) where T : unmanaged, IObject<A, B>
    {
        value->Dispose();
        Allocator* allocPtr = ((Allocator*)value) - 1;
        Free(allocPtr, *allocPtr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* New<T, A, B, C>(Allocator alloc, A a, B b, C c) where T : unmanaged, IObject<A, B, C>
    {
        Allocator* allocPtr = (Allocator*)Malloc(sizeof(T) + sizeof(Allocator), alloc) + sizeof(Allocator);
        *allocPtr = alloc;
        T* t = (T*)(allocPtr + 1);
        t->Init(a, b, c);
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delete<T, A, B, C>(T* value) where T : unmanaged, IObject<A, B, C>
    {
        value->Dispose();
        Allocator* allocPtr = ((Allocator*)value) - 1;
        Free(allocPtr, *allocPtr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* New<T, A, B, C, D>(Allocator alloc, A a, B b, C c, D d) where T : unmanaged, IObject<A, B, C, D>
    {
        Allocator* allocPtr = (Allocator*)Malloc(sizeof(T) + sizeof(Allocator), alloc) + sizeof(Allocator);
        *allocPtr = alloc;
        T* t = (T*)(allocPtr + 1);
        t->Init(a, b, c, d);
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delete<T, A, B, C, D>(T* value) where T : unmanaged, IObject<A, B, C, D>
    {
        value->Dispose();
        Allocator* allocPtr = ((Allocator*)value) - 1;
        Free(allocPtr, *allocPtr);
    }
}