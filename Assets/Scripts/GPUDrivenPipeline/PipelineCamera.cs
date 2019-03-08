using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public unsafe sealed class PipelineCamera : MonoBehaviour
    {
        [System.NonSerialized]
        public Camera cam;
        [System.NonSerialized]
        public RenderTargets targets;
        public PipelineResources.CameraRenderingPath renderingPath = PipelineResources.CameraRenderingPath.GPUDeferred;
        public IPerCameraData[] allDatas;
        public bool inverseRender = false;
        public static NativeDictionary<int, UIntPtr> allCamera;
        public void EnableThis(PipelineResources res)
        {
            if (!targets.initialized)
                targets = RenderTargets.Init();
            if(allDatas == null || allDatas.Length < res.availiableEvents.Length)
            {
                allDatas = new IPerCameraData[res.availiableEvents.Length];
            }
        }

        private void OnEnable()
        {
            if (!allCamera.isCreated)
            {
                allCamera = new NativeDictionary<int, UIntPtr>(17, Unity.Collections.Allocator.Persistent, (i, j) => i == j);
            }
            allCamera.Add(gameObject.GetInstanceID(), new UIntPtr(MUnsafeUtility.GetManagedPtr(this)));
        }

        private void OnDisable()
        {
            allCamera.Remove(gameObject.GetInstanceID());
            if (allCamera.Length <= 0)
            {
                allCamera.Dispose();
            }
            
        }

        private void OnDestroy()
        {
            if (allDatas == null) return;
            for (int i = 0; i < allDatas.Length; ++i)
            {
                ref IPerCameraData cam = ref allDatas[i];
                if (cam  != null)
                {
                    cam.DisposeProperty();
                }
                cam = null;
            }
            allDatas = null;
        }
    }
}
