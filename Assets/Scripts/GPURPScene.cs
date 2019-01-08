using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public class GPURPScene : MonoBehaviour
    {
        public SceneControllerWithGPURPEnabled gpurp;
        private LoadingThread loadingThread;
        private GPURPScene current;
        private void Awake()
        {
            if(current != null)
            {
                Debug.LogError("GPU RP Scene should be singleton!");
                Destroy(this);
                return;
            }
            current = this;
            gpurp.Awake();
            loadingThread = new LoadingThread();
        }

        private void Update()
        {
            gpurp.Update(this);
            loadingThread.Update(gpurp.commandQueue);
        }

        private void OnDestroy()
        {
            gpurp.Dispose();
            loadingThread.Dispose();
            current = null;
        }
    }
}
