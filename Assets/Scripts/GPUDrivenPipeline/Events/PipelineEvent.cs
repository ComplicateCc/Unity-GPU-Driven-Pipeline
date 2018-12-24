using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;
namespace MPipeline
{
    public abstract class PipelineEvent : MonoBehaviour
    {
        public static Func<PipelineEvent, PipelineEvent, int> compareFunc = (x, y) =>
        {
            if (x.layer > y.layer) return -1;
            if (x.layer < y.layer) return 1;
            return 0;
        };
        [HideInInspector]
        [SerializeField]
        private RenderPipeline.CameraRenderingPath m_renderPath = RenderPipeline.CameraRenderingPath.GPUDeferred;
        public RenderPipeline.CameraRenderingPath renderPath
        {
            get
            {
                return m_renderPath;
            }
            set
            {
                if (value == m_renderPath) return;
                bool alreadyEnabled = EnableEvent;
                EnableEvent = false;
                m_renderPath = value;
                EnableEvent = alreadyEnabled;
            }
        }
        [HideInInspector]
        public bool m_enabledInPipeline = false;
        [HideInInspector]
        public bool m_enableBeforePipeline = false;
        public float layer = 2000;
        private bool pre = false;
        private bool post = false;
        //Will Cause GC stress
        //So only run in editor or Awake
        public void SetAttribute()
        {
            object[] allAttribute = GetType().GetCustomAttributes(typeof(PipelineEventAttribute), false);
            foreach (var i in allAttribute)
            {
                if (i.GetType() == typeof(PipelineEventAttribute))
                {
                    PipelineEventAttribute att = i as PipelineEventAttribute;
                    pre = att.preRender;
                    post = att.postRender;
                    return;
                }
            }
            pre = false;
            post = false;

        }

        public bool EnableEvent
        {
            get
            {
                return m_enabledInPipeline || m_enableBeforePipeline;
            }
            set
            {
                enabledInPipeline = post && value;
                enableBeforePipeline = pre && value;
            }
        }

        private bool enabledInPipeline
        {
            get
            {
                return m_enabledInPipeline;
            }
            set
            {
                if (m_enabledInPipeline == value) return;
                m_enabledInPipeline = value;
                SetPipelineEnable(value, true, false, renderPath);
            }
        }

        private bool enableBeforePipeline
        {
            get
            {
                return m_enableBeforePipeline;
            }
            set
            {
                if (m_enableBeforePipeline == value) return;
                m_enableBeforePipeline = value;
                SetPipelineEnable(value, false, true, renderPath);
            }
        }

        private void SetPipelineEnable(bool value, bool after, bool before, RenderPipeline.CameraRenderingPath path)
        {
            if (value)
            {
                DrawEvent evt;
                if (!RenderPipeline.allDrawEvents.TryGetValue(path, out evt))
                {
                    evt = new DrawEvent(10);
                    RenderPipeline.allDrawEvents.Add(path, evt);
                }
                if (after) evt.drawEvents.InsertTo(this, compareFunc);
                if (before) evt.preRenderEvents.InsertTo(this, compareFunc);
            }
            else
            {
                DrawEvent evt;
                if (RenderPipeline.allDrawEvents.TryGetValue(path, out evt))
                {
                    if (after) evt.drawEvents.Remove(this);
                    if (before) evt.preRenderEvents.Remove(this);
                }
            }
        }

        public void InitEvent(PipelineResources resources)
        {
            SetAttribute();
            SetPipelineEnable(true, m_enabledInPipeline, m_enableBeforePipeline, m_renderPath);
            Init(resources);
        }
        public void DisposeEvent()
        {
            SetPipelineEnable(false, m_enabledInPipeline, m_enableBeforePipeline, m_renderPath);
            Dispose();
        }
        protected virtual void Init(PipelineResources resources) { }
        protected virtual void Dispose() { }
        public virtual void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data) { }
        public virtual void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data) { }
    }
}