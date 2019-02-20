using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Unity.Mathematics;
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
    "_SpecularMap",
        "_DetailAlbedo",
        "_DetailNormal"};
        public ComputeShader lightmapShader;
        const int texToBufferKernel = 0;
        const int bufferToTexKernel = 1;
        const int texToBufferARGBKernel = 2;
        const int bufferTotexARGBKernel = 3;
        public void GetPoints(NativeList<Point> points, NativeList<int> triangles, Mesh targetMesh, int* allMaterialsIndex, Transform transform, Material[] allMats, float4 lightmapScaleOffset, int lightmapIndex)
        {
            int originLength = points.Length;
            Vector3[] vertices = targetMesh.vertices;
            Vector2[] uv = targetMesh.uv;
            Vector2[] uv2 = targetMesh.uv2;
            Vector3[] normal = targetMesh.normals;
            Vector4[] tangents = targetMesh.tangents;
            points.AddRange(vertices.Length);
            for (int i = originLength; i < vertices.Length + originLength; ++i)
            {
                ref Point pt = ref points[i];
                int len = i - originLength;
                pt.vertex = transform.localToWorldMatrix.MultiplyPoint(vertices[len]);
                if (normal.Length == vertices.Length)
                {
                    points[i].normal = transform.localToWorldMatrix.MultiplyVector(normal[len]);
                }
                else
                {
                    points[i].normal = Vector3.zero;
                }
                if (tangents.Length == vertices.Length)
                {
                    Vector3 worldTangent = tangents[len];
                    worldTangent = transform.localToWorldMatrix.MultiplyVector(worldTangent);
                    points[i].tangent = worldTangent;
                    points[i].tangent.w = tangents[len].w;
                }
                else
                {
                    points[i].tangent = Vector4.one;
                }
                if (uv.Length == vertices.Length)
                    points[i].texcoord = uv[len];
                else
                    points[i].texcoord = Vector2.zero;
                if (uv2.Length == vertices.Length)
                    points[i].lightmapUV = (float2)uv2[len] * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
                else
                    points[i].lightmapUV = Vector2.zero;
                points[i].lightmapIndex = lightmapIndex;
                //TODO
            }
            for (int subCount = 0; subCount < targetMesh.subMeshCount; ++subCount)
            {
                int[] triangleArray = targetMesh.GetTriangles(subCount);
                for (int i = 0; i < triangleArray.Length; ++i)
                {
                    triangleArray[i] += originLength;
                    ref Point pt = ref points[triangleArray[i]];
                    pt.objIndex = (uint)allMaterialsIndex[subCount];
                }
                triangles.AddRange(triangleArray);
            }

        }
        public CombinedModel ProcessCluster(params MeshRenderer[] allRenderers)
        {
            MeshFilter[] allFilters = new MeshFilter[allRenderers.Length];
            int sumVertexLength = 0;
            int sumTriangleLength = 0;
            NativeDictionary<int, int> dicts = new NativeDictionary<int, int>(allRenderers.Length, Allocator.Temp, (i, j) => j == i);
            List<Texture2D> allLightmaps = new List<Texture2D>();
            dicts[-1] = -1;
            foreach (var i in allRenderers)
            {
                int ind = i.lightmapIndex;
                LightmapData[] allData = LightmapSettings.lightmaps;
                if (ind >= 0)
                {
                    if (!dicts.Contains(ind))
                    {
                        dicts.Add(ind, allLightmaps.Count);
                        allLightmaps.Add(allData[ind].lightmapColor);
                    }
                }
            }
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
                Material[] mats = allRenderers[i].sharedMaterials;
                int* index = stackalloc int[mats.Length];
                for (int a = 0; a < mats.Length; ++a)
                {
                    if ((index[a] = allMat.IndexOf(mats[a])) < 0)
                    {
                        index[a] = allMat.Count;
                        allMat.Add(mats[a]);
                    }
                }
                GetPoints(points, triangles, mesh, index, allFilters[i].transform, mats, allRenderers[i].lightmapScaleOffset, dicts[allRenderers[i].lightmapIndex]);
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
            md.lightmaps = allLightmaps;
            dicts.Dispose();
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

        public List<Pair<string, Vector4[]>> CombineScaleOffset(List<Material> mats)
        {
            List<Pair<string, Vector4[]>> props = new List<Pair<string, Vector4[]>>();
            string[] matNames = new string[]
            {
                "_MainTex", "_DetailAlbedo"
            };
            string[] propNames = new string[]
            {
                "mainScaleOffset", "detailScaleOffset"
            };
            for(int i = 0; i < matNames.Length; ++i)
            {
                Vector4[] vecs = new Vector4[mats.Count];
                for(int j = 0; j < vecs.Length; ++j)
                {
                    Vector2 scale = mats[j].GetTextureScale(matNames[i]);
                    Vector2 offset = mats[j].GetTextureOffset(matNames[i]);
                    vecs[j] = new Vector4(scale.x, scale.y, offset.x, offset.y);
                }
                props.Add(new Pair<string, Vector4[]>(propNames[i], vecs));
            }
            return props;
        }
        public PropertyValue[] GetProperty(List<Material> mats)
        {
            PropertyValue[] values = new PropertyValue[mats.Count];
            PropertyValue* pointer = (PropertyValue*)UnsafeUtility.AddressOf(ref values[0]);
            for (int i = 0; i < values.Length; ++i)
            {
                pointer[i].textureIndex = Vector3Int.one * -1;
                pointer[i].detailTextureIndex = Vector2Int.one * -1;
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
            var scaleOffset = CombineScaleOffset(mats);
            foreach (var kv in scaleOffset)
            {
                ulong offset = (ulong)UnsafeUtility.GetFieldOffset(typeof(PropertyValue).GetField(kv.key));
                for (int i = 0; i < kv.value.Length; ++i)
                {
                    Vector4* currentPointer = (Vector4*)((ulong)(pointer + i) + offset);
                    *currentPointer = kv.value[i];
                }
            }
            return values;
        }
        public TexturePaths[] GetTextures(List<Material> mats, out Texture[,] allTextures)
        {
            TexturePaths[] texs = new TexturePaths[textureName.Length];
            allTextures = new Texture[textureName.Length, mats.Count];
            for (int a = 0; a < textureName.Length; ++a)
            {
                TexturePaths curt = new TexturePaths();
                curt.texName = textureName[a];
                curt.instancingIDs = new string[mats.Count];
                for (int i = 0; i < mats.Count; ++i)
                {
                    Texture tex = mats[i].GetTexture(curt.texName);
                    allTextures[a, i] = tex;
                    curt.instancingIDs[i] = tex ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex)) : "";
                }
                texs[a] = curt;
            }
            return texs;
        }

        public void SaveTextures(TexturePaths[] pathes, Texture[,] textures)
        {
            Dictionary<string, bool> dict = new Dictionary<string, bool>();
            byte[] bytes = null;
            for (int i = 0; i < pathes.Length; ++i)
            {
                TexturePaths pt = pathes[i];
                for (int j = 0; j < pt.instancingIDs.Length; ++j)
                {
                    if (!string.IsNullOrEmpty(pt.instancingIDs[j]) && !dict.ContainsKey(pt.instancingIDs[j]))
                    {
                        dict.Add(pt.instancingIDs[j], true);
                        TextureToBytes((Texture2D)textures[i, j], ref bytes, false);
                        string path = "Assets/BinaryData/Textures/" + pt.instancingIDs[j] + ".txt";
                        File.WriteAllBytes(path, bytes);
                    }
                }
            }
        }

        public void TextureToBytes(Texture2D lightmap, ref byte[] results, bool isRGBM)
        {
            int pass = isRGBM ? texToBufferKernel : texToBufferARGBKernel;
            ComputeBuffer tempBuffer = new ComputeBuffer(lightmap.width * lightmap.height, sizeof(int));
            lightmapShader.SetTexture(pass, "_InputTex", lightmap);
            lightmapShader.SetBuffer(pass, "_Buffer", tempBuffer);
            lightmapShader.SetInt("_Width", lightmap.width);
            lightmapShader.Dispatch(pass, lightmap.width / 8, lightmap.height / 8, 1);
            if (results == null || results.Length != (tempBuffer.count * tempBuffer.stride))
            {
                results = new byte[(tempBuffer.count * tempBuffer.stride)];
            }
            tempBuffer.GetData(results);
        }

        public struct CombinedModel
        {
            public NativeList<Point> allPoints;
            public NativeList<int> triangles;
            public List<Material> containedMaterial;
            public List<Texture2D> lightmaps;
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
            Texture[,] textures;
            TexturePaths[] texs = GetTextures(model.containedMaterial, out textures);
            SaveTextures(texs, textures);
            property.properties = value;
            property.texPaths = texs;
            LightmapPaths[] lightmapGUIDs = new LightmapPaths[model.lightmaps.Count];
            byte[] bytes = null;
            for (int i = 0; i < lightmapGUIDs.Length; ++i)
            {
                TextureToBytes(model.lightmaps[i], ref bytes, true);
                lightmapGUIDs[i] = new LightmapPaths
                {
                    name = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(model.lightmaps[i])),
                    size = model.lightmaps[i].width
                };
                File.WriteAllBytes("Assets/BinaryData/Lightmaps/" + lightmapGUIDs[i].name + ".txt", bytes);
            }
            property.lightmapGUIDs = lightmapGUIDs;
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
        public float _Occlusion;
        public float _Glossiness;
        public Vector4 _Color;
        public Vector3Int textureIndex;
        public Vector2Int detailTextureIndex;
        public Vector4 mainScaleOffset;
        public Vector4 detailScaleOffset;
    }
}
