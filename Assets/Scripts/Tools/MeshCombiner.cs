
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Debug = UnityEngine.Debug;
using MStudio;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace MPipeline
{
    [Serializable]
    public struct Pair<T, V>
    {
        public T key;
        public V value;
        public Pair(T key, V value)
        {
            this.key = key;
            this.value = value;
        }
    }

    [Serializable]
    public struct Pair
    {
        public string key;
        public Texture2DArray value;
        public Pair(string key, Texture2DArray value)
        {
            this.key = key;
            this.value = value;
        }
    }
    public unsafe class MeshCombiner : MonoBehaviour
    {
#if UNITY_EDITOR
        string[] textureName = new string[]{"_MainTex",
    "_BumpMap",
    "_SpecularMap" };
        public void GetPoints(NativeList<Point> points, NativeList<int> triangles, Mesh targetMesh, int materialIndex, Transform transform)
        {
            int originLength = points.Length;
            Vector3[] vertices = targetMesh.vertices;
            Vector2[] uv = targetMesh.uv;
            Vector3[] normal = targetMesh.normals;
            Vector4[] tangents = targetMesh.tangents;
            Action<Vector2[], NativeList<Point>, int, int> SetUV;
            if (uv.Length == vertices.Length)
            {
                SetUV = (vec, pt, i, originI) => pt[i].texcoord = vec[originI];
            }
            else
            {
                SetUV = (vec, pt, i, originLen) => pt[i].texcoord = Vector3.zero;
            }
            Action<Vector3[], NativeList<Point>, int, int> SetNormal;
            if (normal.Length == vertices.Length)
            {
                SetNormal = (vec, pt, i, ori) => pt[i].normal = transform.localToWorldMatrix.MultiplyVector(vec[ori]);
            }
            else
            {
                SetNormal = (vec, pt, i, ori) => pt[i].normal = Vector3.zero;
            }
            Action<Vector4[], NativeList<Point>, int, int> SetTangent;
            if (tangents.Length == vertices.Length)
            {
                SetTangent = (vec, pt, i, ori) =>
                {
                    Vector3 worldTangent = vec[ori];
                    worldTangent = transform.localToWorldMatrix.MultiplyVector(worldTangent);
                    pt[i].tangent = worldTangent;
                    pt[i].tangent.w = vec[ori].w;
                };
            }
            else
            {
                SetTangent = (vec, pt, i, ori) => pt[i].tangent = Vector4.one;
            }
            points.AddRange(vertices.Length);
            for (int i = originLength; i < vertices.Length + originLength; ++i)
            {
                ref Point pt = ref points[i];
                int len = i - originLength;
                pt.vertex = transform.localToWorldMatrix.MultiplyPoint(vertices[len]);
                SetNormal(normal, points, i, len);
                SetTangent(tangents, points, i, len);
                SetUV(uv, points, i, len);
                pt.objIndex = (uint)materialIndex;
            }
            int[] triangleArray = targetMesh.triangles;
            for (int i = 0; i < triangleArray.Length; ++i)
            {
                triangleArray[i] += originLength;
            }
            triangles.AddRange(triangleArray);
        }
        public CombinedModel ProcessCluster(params MeshRenderer[] allRenderers)
        {
            MeshFilter[] allFilters = new MeshFilter[allRenderers.Length];
            int sumVertexLength = 0;
            int sumTriangleLength = 0;
            for (int i = 0; i < allFilters.Length; ++i)
            {
                allFilters[i] = allRenderers[i].GetComponent<MeshFilter>();
                sumVertexLength += allFilters[i].sharedMesh.vertexCount;
            }
            sumTriangleLength = (int)(sumVertexLength * 1.5);
            NativeList<Point> points = new NativeList<Point>(sumVertexLength, Allocator.Temp);
            NativeList<int> triangles = new NativeList<int>(sumTriangleLength, Allocator.Temp);
            List<Material> allMat = new List<Material>();
            for (int i = 0; i < allFilters.Length; ++i)
            {
                Mesh mesh = allFilters[i].sharedMesh;
                Material mat = allRenderers[i].sharedMaterial;
                int index;
                if ((index = allMat.IndexOf(mat)) < 0)
                {
                    index = allMat.Count;
                    allMat.Add(mat);
                }
                GetPoints(points, triangles, mesh, index, allFilters[i].transform);
            }
            Vector3 less = points[0].vertex;
            Vector3 more = points[0].vertex;

            for (int i = 1; i < points.Length; ++i)
            {
                Vector3 current = points[i].vertex;
                if (less.x > current.x) less.x = current.x;
                if (more.x < current.x) more.x = current.x;
                if (less.y > current.y) less.y = current.y;
                if (more.y < current.y) more.y = current.y;
                if (less.z > current.z) less.z = current.z;
                if (more.z < current.z) more.z = current.z;
            }

            Vector3 center = (less + more) / 2;
            Vector3 extent = more - center;
            Bounds b = new Bounds(center, extent * 2);
            CombinedModel md;
            md.bound = b;
            md.allPoints = points;
            md.triangles = triangles;
            md.containedMaterial = allMat;
            return md;
        }
        public List<Pair<string, float[]>> CombineProperty(List<Material> mats)
        {
            List<Pair<string, float[]>> props = new List<Pair<string, float[]>>();
            string[] propNames = new string[]
            {
                "_Glossiness",
                "_Occlusion",
                "_SpecularIntensity",
                "_MetallicIntensity",
                "_EmissionMultiplier"
            };
            foreach (var i in propNames)
            {
                float[] f = new float[mats.Count];
                for (int j = 0; j < f.Length; ++j)
                {
                    f[j] = mats[j].GetFloat(i);
                }
                props.Add(new Pair<string, float[]>(i, f));
            }
            return props;
        }
        public List<Pair<string, Color[]>> CombineColor(List<Material> mats)
        {
            List<Pair<string, Color[]>> props = new List<Pair<string, Color[]>>();
            string[] propNames = new string[]
            {
                "_Color",
                "_EmissionColor"
            };
            foreach (var i in propNames)
            {
                Color[] f = new Color[mats.Count];
                for (int j = 0; j < f.Length; ++j)
                {
                    f[j] = mats[j].GetColor(i);
                }
                props.Add(new Pair<string, Color[]>(i, f));
            }
            return props;
        }
        public PropertyValue[] GetProperty(List<Material> mats)
        {
            PropertyValue[] values = new PropertyValue[mats.Count];
            PropertyValue* pointer = (PropertyValue*)UnsafeUtility.AddressOf(ref values[0]);
            for(int i = 0; i < values.Length; ++i)
            {
                pointer[i].textureIndex = Vector3Int.one * -1;
            }
            var properties = CombineProperty(mats);
            foreach (var kv in properties)
            {
                ulong offset = (ulong)UnsafeUtility.GetFieldOffset(typeof(PropertyValue).GetField(kv.key));
                for (int i = 0; i < kv.value.Length; ++i)
                {
                    float* currentPointer = (float*)((ulong)(pointer + i) + offset);
                    *currentPointer = kv.value[i];
                }
            }
            var colors = CombineColor(mats);
            foreach (var kv in colors)
            {
                ulong offset = (ulong)UnsafeUtility.GetFieldOffset(typeof(PropertyValue).GetField(kv.key));
                for (int i = 0; i < kv.value.Length; ++i)
                {
                    Color* currentPointer = (Color*)((ulong)(pointer + i) + offset);
                    *currentPointer = kv.value[i];
                }
            }

            return values;
        }
        public TexturePaths[] GetTextures(List<Material> mats, out Texture[][] allTextures)
        {
            TexturePaths[] texs = new TexturePaths[textureName.Length];
            allTextures = new Texture[textureName.Length][];
            for (int a = 0; a < textureName.Length; ++a)
            {
                allTextures[a] = new Texture[mats.Count];
                TexturePaths curt = new TexturePaths();
                curt.texName = textureName[a];
                curt.instancingIDs = new string[mats.Count];
                for (int i = 0; i < mats.Count; ++i)
                {
                    Texture tex = mats[i].GetTexture(curt.texName);
                    allTextures[a][i] = tex;
                    curt.instancingIDs[i] = tex ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex)) : "";
                }
                texs[a] = curt;
            }
            return texs;
        }

        public void SaveTextures(TexturePaths[] pathes, Texture[][] textures)
        {
            Dictionary<string, bool> dict = new Dictionary<string, bool>();
            for (int i = 0; i < pathes.Length; ++i)
            {
                TexturePaths pt = pathes[i];
                for (int j = 0; j < pt.instancingIDs.Length; ++j)
                {
                    if (!string.IsNullOrEmpty(pt.instancingIDs[j]) && !dict.ContainsKey(pt.instancingIDs[j]))
                    {
                        dict.Add(pt.instancingIDs[j], true);
                        byte[] bytes = TextureStreaming.GetBytes((Texture2D)textures[i][j]);
                        File.WriteAllBytes("Assets/BinaryData/Textures/" + pt.instancingIDs[j] + ".txt", bytes);
                    }
                }
            }
        }

        public struct CombinedModel
        {
            public NativeList<Point> allPoints;
            public NativeList<int> triangles;
            public List<Material> containedMaterial;
            public Bounds bound;
        }
        public TextureFormat mainTexFormat = TextureFormat.ARGB32;
        public TextureFormat bumpMapFormat = TextureFormat.ARGB32;
        public TextureFormat specularFormat = TextureFormat.RGB24;
        public string modelName = "TestFile";

        [EasyButtons.Button]
        public void TryThis()
        {
            bool save = false;
            ClusterMatResources res = Resources.Load<ClusterMatResources>("MapMat/SceneManager");
            if (res == null)
            {
                save = true;
                res = ScriptableObject.CreateInstance<ClusterMatResources>();
                res.name = "SceneManager";
                res.clusterProperties = new List<ClusterProperty>();
            }
            Func<ClusterProperty, ClusterProperty, bool> equalCompare = (a, b) =>
            {
                return a.name == b.name;
            };
            ClusterProperty property = new ClusterProperty();
            property.name = modelName;
            foreach (var i in res.clusterProperties)
            {
                if (equalCompare(property, i))
                {
                    Debug.LogError("Already Contained Scene " + modelName);
                    return;
                }
            }
            CombinedModel model = ProcessCluster(GetComponentsInChildren<MeshRenderer>());
            property.clusterCount = ClusterGenerator.GenerateCluster(model.allPoints, model.triangles, model.bound, modelName);
            PropertyValue[] value = GetProperty(model.containedMaterial);
            Texture[][] textures;
            TexturePaths[] texs = GetTextures(model.containedMaterial, out textures);
            SaveTextures(texs, textures);
            property.properties = value;
            property.texPaths = texs;
            res.clusterProperties.Add(property);
            if (save)
                AssetDatabase.CreateAsset(res, "Assets/Resources/MapMat/SceneManager.asset");
        }
#endif
    }
    [Serializable]
    public struct PropertyValue
    {
        public float _SpecularIntensity;
        public float _MetallicIntensity;
        public Vector4 _EmissionColor;
        public float _EmissionMultiplier;
        public float _Occlusion;
        public float _Glossiness;
        public Vector4 _Color;
        public Vector3Int textureIndex;
    };
}
