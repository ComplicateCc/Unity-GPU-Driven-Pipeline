using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
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
        public Dictionary<Type, IPerCameraData> postDatas = new Dictionary<Type, IPerCameraData>(47);
        public bool inverseRender = false;
        public static NativeDictionary<int, UIntPtr> allCamera;
        public void EnableThis()
        {
            if (!targets.initialized)
                targets = RenderTargets.Init();
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
            DisableThis();
        }

        public void DisableThis()
        {
            foreach (var i in postDatas.Values)
            {
                i.DisposeProperty();
            }
            postDatas.Clear();
        }
    }
}
