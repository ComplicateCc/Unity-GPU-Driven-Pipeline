using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CountComponent : MonoBehaviour
{
    [System.NonSerialized]
    public int indexInList;
}

public class ComponentList<T> where T : CountComponent
{
    public T[] componentArray
    {
        get; private set;
    }
    public int count
    {
        get; private set;
    }
    public ComponentList(int capacity)
    {
        capacity = Mathf.Max(1, capacity);
        componentArray = new T[capacity];
        count = 0;
    }
    public void Add(T value)
    {
        if(count == componentArray.Length)
        {
            T[] newArray = new T[count * 2];
            componentArray = newArray;
        }
        componentArray[count] = value;
        value.indexInList = count;
        count++;
    }

    public void Remove(T value)
    {
        if(count <= 1)
        {
            count = 0;
            return;
        }
        int last = count - 1;
        componentArray[value.indexInList] = componentArray[last];
        componentArray[last].indexInList = value.indexInList;
        count--;
    }
}