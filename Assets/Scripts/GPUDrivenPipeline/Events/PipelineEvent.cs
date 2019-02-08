using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;
namespace MPipeline
{
#if UNITY_EDITOR
    using UnityEditor;
    [CustomEditor(typeof(PipelineEvent), true)]
    public class EventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            PipelineEvent evt = serializedObject.targetObject as PipelineEvent;
            evt.Enabled = EditorGUILayout.Toggle("Enabled", evt.Enabled);
            EditorUtility.SetDirty(evt);
            base.OnInspectorGUI();
        }
    }
#endif
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RequireEventAttribute : Attribute
    {
        public Type[] events { get; private set; }
        public RequireEventAttribute(params Type[] allEvents)
        {
            events = allEvents;
        }
    }
    [System.Serializable]
    public abstract class PipelineEvent : ScriptableObject
    {
        [HideInInspector]
        [SerializeField]
        private bool enabled = false;
        private bool initialized = false;
        public bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                if (value == enabled) return;
                if (initialized)
                {
                    if (value)
                    {
                        if (dependingEvents != null)
                        {
                            foreach (var i in dependingEvents)
                            {
                                if (!i.enabled)
                                {
                                    return;
                                }
                            }
                            enabled = true;
                            OnEnableEvent();
                        }
                    }
                    else
                    {
                        if (dependedEvents != null)
                        {
                            foreach (var i in dependedEvents)
                            {
                                i.Enabled = false;
                            }
                        }
                        enabled = false;
                        OnDisableEvent();
                    }
                }
            }
        }

        private List<PipelineEvent> dependedEvents = null;
        private List<PipelineEvent> dependingEvents = null;
        public PipelineResources.CameraRenderingPath renderingPath { get; private set; }
        public void InitEvent(PipelineResources resources, PipelineResources.CameraRenderingPath renderingPath)
        {
            initialized = true;
            this.renderingPath = renderingPath;
            Init(resources);
            if (enabled) OnEnableEvent();
            else
            {
                if (dependedEvents != null)
                {
                    foreach (var i in dependedEvents)
                    {
                        i.Enabled = false;
                    }
                }
            }
        }
        public void Prepare()
        {
            RequireEventAttribute requireEvt = GetType().GetCustomAttribute<RequireEventAttribute>(true);
            if (requireEvt != null)
            {
                if (dependingEvents == null)
                    dependingEvents = new List<PipelineEvent>(requireEvt.events.Length);
                foreach (var t in requireEvt.events)
                {
                    PipelineEvent targetevt = RenderPipeline.GetEvent(renderingPath, t);
                    if (targetevt != null)
                    {
                        if (targetevt.dependedEvents == null)
                            targetevt.dependedEvents = new List<PipelineEvent>();
                        targetevt.dependedEvents.Add(this);
                        dependingEvents.Add(targetevt);
                    }
                }
            }
        }
        public void DisposeEvent()
        {
            initialized = false;
            if (enabled) OnDisableEvent();
            Dispose();
        }
        protected abstract void Init(PipelineResources resources);
        protected abstract void Dispose();
        public abstract bool CheckProperty();
        public virtual void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data) { }
        public virtual void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data) { }
        protected virtual void OnEnableEvent() { }
        protected virtual void OnDisableEvent() { }
    }
}