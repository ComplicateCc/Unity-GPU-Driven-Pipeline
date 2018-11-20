
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Debug = UnityEngine.Debug;
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
        public CombinedModel ProcessCluster(MeshRenderer[] allRenderers)
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
        public List<Pair> CombineTexture(List<Material> mats, Vector2Int size)
        {
            List<Pair> tex = new List<Pair>();
            string[] textureNames = new string[]
            {
                "_MainTex",
                "_BumpMap",
                "_SpecularMap",
                "_OcclusionMap"
            };

            bool* isLinear = stackalloc bool[]
            {
                false,
                true,
                true,
                true
            };

            TextureFormat* formats = stackalloc TextureFormat[]
            {
                mainTexFormat,
                bumpMapFormat,
                specularFormat,
                occlusionFormat
            };

            Color* defaultColors = stackalloc Color[]
            {
                Color.white,
                new Color(0.5f, 0.5f, 1),
                Color.white,
                Color.white
            };
            void SetTexture(Texture2DArray texArray, Color defaultColor, Texture2D currentTex, int index)
            {
                if (currentTex && !currentTex.isReadable)
                {
                    Debug.LogError("Texture" + currentTex.name + " is Not Readable!");
                    currentTex = null;
                }
                if (currentTex == null)
                {
                    Color[] colors = new Color[size.x * size.y];
                    for (int i = 0; i < colors.Length; ++i)
                    {
                        colors[i] = defaultColor;
                    }
                    texArray.SetPixels(colors, index);
                }
                else
                {

                    Color[] colors = new Color[size.x * size.y];
                    for (int x = 0; x < size.x; ++x)
                    {
                        for (int y = 0; y < size.y; ++y)
                        {
                            colors[y * size.y + x] = currentTex.GetPixelBilinear((float)x / size.x, (float)y / size.y);
                        }
                    }
                    texArray.SetPixels(colors, index);
                }
            }
            for (int i = 0; i < textureNames.Length; ++i)
            {
                Texture2DArray texArray = new Texture2DArray(size.x, size.y, mats.Count, formats[i], false, isLinear[i]);
                for (int j = 0; j < mats.Count; ++j)
                {
                    SetTexture(texArray, defaultColors[i], (Texture2D)mats[j].GetTexture(textureNames[i]), j);
                }
                texArray.Apply();
                tex.Add(new Pair(textureNames[i], texArray));
            }
            return tex;
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
        public struct CombinedModel
        {
            public NativeList<Point> allPoints;
            public NativeList<int> triangles;
            public List<Material> containedMaterial;
            public Bounds bound;
        }
        public List<Material> mats;
        public TextureFormat mainTexFormat = TextureFormat.ARGB32;
        public TextureFormat bumpMapFormat = TextureFormat.ARGB32;
        public TextureFormat specularFormat = TextureFormat.RGB24;
        public TextureFormat occlusionFormat = TextureFormat.R8;
        public string modelName = "TestFile";
#if UNITY_EDITOR
        [EasyButtons.Button]
        public void TryThis()
        {
            CombinedModel model = ProcessCluster(GetComponentsInChildren<MeshRenderer>());
            ClusterMatResources res = ScriptableObject.CreateInstance<ClusterMatResources>();
            ClusterGenerator.GenerateCluster(model.allPoints, model.triangles, model.bound, modelName, res);
            PropertyValue[] value = GetProperty(model.containedMaterial);
            var texs = CombineTexture(model.containedMaterial, Vector2Int.one * 1024);

            res.values = value;
            // List<Pair> finalPair = new List<Pair>(texs.Count);
            foreach (var i in texs)
            {
                AssetDatabase.CreateAsset(i.value, "Assets/Resources/MapMat/" + modelName + i.key + ".asset");
                // finalPair.Add(new Pair(i.key, Resources.Load<Texture2DArray>("MapMat/" + modelName + i.key)));
            }
            res.textures = texs;
            AssetDatabase.CreateAsset(res, "Assets/Resources/MapMat/" + modelName + ".asset");
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
    };
}
