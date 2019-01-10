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
        public SceneControllerWithGPURPEnabled gpurp;
        private LoadingThread loadingThread;
        private GPURPScene current;
        public Camera mainCamera;
        public Transform transformParents;
        private TransformAccessArray transformArray;
        private static void Count(Transform trans, ref int length)
        {
            if (trans.childCount > 0)
            {
                for (int i = 0; i < trans.childCount; ++i)
                {
                    Count(trans.GetChild(i), ref length);
                }
            }
            else
            {
                length++;
            }
        }
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
            gpurp.Awake(resources);
            loadingThread = new LoadingThread();
            int length = 0;
            Count(transformParents, ref length);
            transformArray = new TransformAccessArray(length, 32);
            SetBuffer(transformParents, transformArray);
        }

        public void QueueJob()
        {
            jobHandle = (new MoveTransform
            {
                offset = offset
            }).Schedule(transformArray);
            jobHandle.Complete();
            Shader.SetGlobalVector(ShaderIDs._SceneOffset, new float4(offset.xyz, lengthsq(offset.xyz) > 0.01f ? 1 : 0));
            mainCamera.transform.position += (Vector3)offset;
            gpurp.TransformMapPosition(0);
            RenderPipeline.AddCommandAfterFrame(this, (o) => Shader.SetGlobalVector(ShaderIDs._SceneOffset, Vector4.zero));
        }
        [EasyButtons.Button]
        public void MoveScene()
        {
            RenderPipeline.AddCommandBeforeFrame(this, (o) => ((GPURPScene)o).QueueJob());
        }

        private void Update()
        {
            gpurp.Update(this);
            loadingThread.Update(gpurp.commandQueue);
        }

        private void OnDestroy()
        {
            transformArray.Dispose();
            gpurp.Dispose();
            loadingThread.Dispose();
            current = null;
        }
        private struct MoveTransform : IJobParallelForTransform
        {
            public Vector3 offset;
            public void Execute(int i, TransformAccess access)
            {
                access.position += offset;
            }
        }
    }
}
