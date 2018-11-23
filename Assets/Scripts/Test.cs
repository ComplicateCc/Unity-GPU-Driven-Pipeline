using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading.Tasks;
using System;

public class Test : MonoBehaviour
{
    public string animationPath = "Assets/AnimationTest";
    public string bindedMeshPath = "Assets/BindPoses";
    public string animationName;
    public float time;
    public SkinnedMeshRenderer skinMeshRender;
    public Animation animation;
    private void Update()
    {
        AnimationState state = animation[animationName];
        state.time = time;
        animation.Sample();
    }
}