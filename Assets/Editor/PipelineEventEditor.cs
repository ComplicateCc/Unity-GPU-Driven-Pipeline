using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace MPipeline
{
    [CustomEditor(typeof(PipelineEvent), true)]
    public class PipelineEventEditor : Editor
    {
        PipelineEvent targetObj;
        private void OnEnable()
        {
            targetObj = serializedObject.targetObject as PipelineEvent;
        }

        public override void OnInspectorGUI()
        {
            
            targetObj.SetAttribute();
            Undo.RecordObject(targetObj, "Basic Pipeline Setting");
            EditorGUILayout.LabelField("Pipeline Settings:");
            bool value = EditorGUILayout.Toggle("Enable In Pipeline", targetObj.EnableEvent);
            targetObj.renderPath = (RenderPipeline.CameraRenderingPath)EditorGUILayout.EnumPopup("Target Path:", targetObj.renderPath);
            targetObj.EnableEvent = value;
            if(value && !targetObj.EnableEvent)
            {
                Debug.LogError("The PipelineEvent Attribute has not be writen into this component!");
            }
            base.OnInspectorGUI();
        }
    }
}