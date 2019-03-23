using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class ShouShouEditor : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);
        Material targetMat = materialEditor.target as Material;
        if(targetMat.GetTexture("_DetailAlbedo") == null && targetMat.GetTexture("_DetailNormal") == null)
        {
            targetMat.DisableKeyword("DETAIL_ON");
        }else
        {
            targetMat.EnableKeyword("DETAIL_ON");
        }
        if(targetMat.GetFloat("_Cutoff") < 0.00001f)
        {
            targetMat.DisableKeyword("CUT_OFF");
        }else
        {
            targetMat.EnableKeyword("CUT_OFF");
        }
    }
}
