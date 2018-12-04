#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class TextureSaver : ScriptableWizard
{
    public string path = "Assets/SceneData/";
    public string fileName = "TestScene";
    public GameObject sceneParent;
    [MenuItem("MPipeline/Save Scene's Texture")]
    private static void CreateWizard()
    {
        DisplayWizard<TextureSaver>("Animation Generator", "Generate");
    }

    private void OnWizardCreate()
    {
        MeshRenderer[] renderers = sceneParent.GetComponentsInParent<MeshRenderer>();
        Dictionary<Material, int> allMats = new Dictionary<Material, int>();
        int count = 0;
        foreach(var i in renderers)
        {
            if (allMats.ContainsKey(i.sharedMaterial)) continue;
            allMats.Add(i.sharedMaterial, count);
            count++;
        }
        SceneTexResources res = ScriptableObject.CreateInstance<SceneTexResources>();
        res.name = sceneParent.name;
        List<Material> mats = new List<Material>(allMats.Keys);
        res.allMaterials = mats.ToArray();
        AssetDatabase.CreateAsset(res, path + fileName + ".asset");
    }
}

#endif