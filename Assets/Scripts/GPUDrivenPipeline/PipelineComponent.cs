using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public struct Vector4Int
    {
        public int x;
        public int y;
        public int z;
        public int w;
        public Vector4Int(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }


    public struct PointLightStruct
    {
        public float3 lightColor;
        public float4 sphere;
        public int shadowIndex;
    }
    public struct Cone
    {
        public float3 vertex;
        public float height;
        public float3 direction;
        public float radius;
        public Cone(float3 position, float distance, float3 direction, float angle)
        {
            vertex = position;
            height = distance;
            this.direction = direction;
            radius = math.tan(angle) * height;
        }
    }
    public struct Capsule
    {
        public float3 direction;
        public float3 position;
        public float radius;
    }
    public struct SpotLight
    {
        public float3 lightColor;
        public Cone lightCone;
        public float angle;
        public Matrix4x4 vpMatrix;
        public float smallAngle;
        public float nearClip;
        public float3 lightRight;
        public int shadowIndex;
    };

    public unsafe struct CubemapViewProjMatrix
    {
        public int2 index;
        public void* mLightPtr;
        public Matrix4x4 forwardProjView;
        public Matrix4x4 backProjView;
        public Matrix4x4 upProjView;
        public Matrix4x4 downProjView;
        public Matrix4x4 rightProjView;
        public Matrix4x4 leftProjView;
        public float4* frustumPlanes;
    }

    public struct FogVolume
    {
        public float3x3 localToWorld;
        public float4x4 worldToLocal;
        public float3 position;
        public float3 extent;
        public float targetVolume;
    }

    public struct ReflectionData
    {
        public float3 position;
        public float3 extent;
        public float4 hdr;
        public float  blendDistance;
        public int    importance;
        public int    boxProjection;
    }

    public class PipelineBaseBuffer
    {
        public ComputeBuffer reCheckCount;
        public ComputeBuffer reCheckResult;
        public ComputeBuffer dispatchBuffer;
        public ComputeBuffer clusterBuffer;         //ObjectInfo
        public ComputeBuffer instanceCountBuffer;   //uint
        public ComputeBuffer resultBuffer;          //uint
        public ComputeBuffer verticesBuffer;        //Point
        public int clusterCount;
        public const int INDIRECTSIZE = 20;
        public const int UINTSIZE = 4;
        public const int CLUSTERCLIPCOUNT = 256;
        public const int CLUSTERVERTEXCOUNT = CLUSTERCLIPCOUNT * 6 / 4;

        /// <summary>
        /// Cluster cull with only frustum culling
        /// </summary>
        public const int ClusterCullKernel = 0;
        /// <summary>
        /// Clear Cluster data's kernel count
        /// </summary>
        public const int ClearClusterKernel = 1;
        /// <summary>
        /// Cluster cull with frustum & occlusion culling
        /// </summary>
        public const int ClusterCullOccKernel = 2;
        public const int VertexIndexKernel = 6;
        public const int MoveVertex = 7;
        public const int MoveCluster = 8;
        public const int SetVertexProperty = 9;
        public const int SetVertexLightmapIndex = 10;
    }

    public struct OcclusionBuffers
    {
        public const int FrustumFilter = 2;
        public const int OcclusionRecheck = 3;
        public const int ClearOcclusionData = 4;
    }

    [System.Serializable]
    public unsafe struct Matrix3x4
    {
        public float m00;
        public float m10;
        public float m20;
        public float m01;
        public float m11;
        public float m21;
        public float m02;
        public float m12;
        public float m22;
        public float m03;
        public float m13;
        public float m23;
        public const int SIZE = 48;
        public Matrix3x4(Matrix4x4 target)
        {
            m00 = target.m00;
            m01 = target.m01;
            m02 = target.m02;
            m03 = target.m03;
            m10 = target.m10;
            m11 = target.m11;
            m12 = target.m12;
            m13 = target.m13;
            m20 = target.m20;
            m21 = target.m21;
            m22 = target.m22;
            m23 = target.m23;
        }
        public Matrix3x4(Matrix4x4* target)
        {
            m00 = target->m00;
            m01 = target->m01;
            m02 = target->m02;
            m03 = target->m03;
            m10 = target->m10;
            m11 = target->m11;
            m12 = target->m12;
            m13 = target->m13;
            m20 = target->m20;
            m21 = target->m21;
            m22 = target->m22;
            m23 = target->m23;
        }
        public Matrix3x4(ref Matrix4x4 target)
        {
            m00 = target.m00;
            m01 = target.m01;
            m02 = target.m02;
            m03 = target.m03;
            m10 = target.m10;
            m11 = target.m11;
            m12 = target.m12;
            m13 = target.m13;
            m20 = target.m20;
            m21 = target.m21;
            m22 = target.m22;
            m23 = target.m23;
        }
    }

    public struct AspectInfo
    {
        public Vector3 inPlanePoint;
        public Vector3 planeNormal;
        public float size;
    }
    [System.Serializable]
    public struct Point
    {
        public Vector3 vertex;
        public Vector4 tangent;
        public Vector3 normal;
        public Vector2 texcoord;
        public uint objIndex;
        public Vector2 lightmapUV;
        public int lightmapIndex;
    }
    [System.Serializable]
    public struct CullBox
    {
        public Vector3 extent;
        public Vector3 position;
    }
    public struct PerObjectData
    {
        public Vector3 extent;
        public uint instanceOffset;
    }

    public struct PerspCam
    {
        public float3 right;
        public float3 up;
        public float3 forward;
        public float3 position;
        public float fov;
        public float nearClipPlane;
        public float farClipPlane;
        public float aspect;
        public float4x4 localToWorldMatrix;
        public float4x4 worldToCameraMatrix;
        public float4x4 projectionMatrix;
        public void UpdateTRSMatrix()
        {
            localToWorldMatrix.c0 = float4(right, 0);
            localToWorldMatrix.c1 = float4(up, 0);
            localToWorldMatrix.c2 = float4(forward, 0);
            localToWorldMatrix.c3 = float4(position, 1);
            worldToCameraMatrix = MatrixUtility.GetWorldToLocal(localToWorldMatrix);
            float4 row2 = -float4(worldToCameraMatrix.c0.z, worldToCameraMatrix.c1.z, worldToCameraMatrix.c2.z, worldToCameraMatrix.c3.z);
            worldToCameraMatrix.c0.z = row2.x;
            worldToCameraMatrix.c1.z = row2.y;
            worldToCameraMatrix.c2.z = row2.z;
            worldToCameraMatrix.c3.z = row2.w;
        }
        public void UpdateViewMatrix(float4x4 localToWorld)
        {
            worldToCameraMatrix = MatrixUtility.GetWorldToLocal(localToWorld);
            float4 row2 = -float4(worldToCameraMatrix.c0.z, worldToCameraMatrix.c1.z, worldToCameraMatrix.c2.z, worldToCameraMatrix.c3.z);
            worldToCameraMatrix.c0.z = row2.x;
            worldToCameraMatrix.c1.z = row2.y;
            worldToCameraMatrix.c2.z = row2.z;
            worldToCameraMatrix.c3.z = row2.w;
        }
        public void UpdateProjectionMatrix()
        {
            projectionMatrix = Matrix4x4.Perspective(fov, aspect, nearClipPlane, farClipPlane);
        }
    }

    public struct OrthoCam
    {
        public float4x4 worldToCameraMatrix;
        public float4x4 localToWorldMatrix;
        public float3 right;
        public float3 up;
        public float3 forward;
        public float3 position;
        public float size;
        public float nearClipPlane;
        public float farClipPlane;
        public float4x4 projectionMatrix;
        public void UpdateTRSMatrix()
        {
            localToWorldMatrix.c0 = new float4(right, 0);
            localToWorldMatrix.c1 = new float4(up, 0);
            localToWorldMatrix.c2 = new float4(forward, 0);
            localToWorldMatrix.c3 = new float4(position, 1);
            worldToCameraMatrix = MatrixUtility.GetWorldToLocal(localToWorldMatrix);
            worldToCameraMatrix.c0.z = -worldToCameraMatrix.c0.z;
            worldToCameraMatrix.c1.z = -worldToCameraMatrix.c1.z;
            worldToCameraMatrix.c2.z = -worldToCameraMatrix.c2.z;
            worldToCameraMatrix.c3.z = -worldToCameraMatrix.c3.z;
        }
        public void UpdateProjectionMatrix()
        {
            projectionMatrix = Matrix4x4.Ortho(-size, size, -size, size, nearClipPlane, farClipPlane);
        }
    }

    public struct StaticFit
    {
        public int resolution;
        public NativeArray<float3> frustumCorners;
        public Camera mainCamTrans;
    }
    public struct RenderTargets
    {
        public int renderTargetIdentifier;
        public int backupIdentifier;
        public int depthIdentifier;
        public int[] gbufferIndex;
        public RenderTargetIdentifier[] gbufferIdentifier;
        public bool initialized;
        public static RenderTargets Init()
        {
            RenderTargets rt;
            rt.gbufferIndex = new int[]
            {
                Shader.PropertyToID("_CameraGBufferTexture0"),
                Shader.PropertyToID("_CameraGBufferTexture1"),
                Shader.PropertyToID("_CameraGBufferTexture2"),
                Shader.PropertyToID("_CameraGBufferTexture3"),
                Shader.PropertyToID("_CameraMotionVectorsTexture"),
            };
            rt.gbufferIdentifier = new RenderTargetIdentifier[5];
            for (int i = 0; i < 5; ++i)
            {
                rt.gbufferIdentifier[i] = rt.gbufferIndex[i];
            }
            rt.backupIdentifier = default;
            rt.depthIdentifier = Shader.PropertyToID("_CameraDepthTexture");
            rt.renderTargetIdentifier = default;
            rt.initialized = true;
            return rt;
        }
        public int motionVectorTexture
        {
            get { return gbufferIndex[4]; }
        }
        public int normalIdentifier
        {
            get { return gbufferIndex[2]; }
        }
    }

    public struct PipelineCommandData
    {
        public Matrix4x4 vp;
        public Matrix4x4 inverseVP;
        public Vector4[] frustumPlanes;
        public CommandBuffer buffer;
        public ScriptableRenderContext context;
        public CullingResults cullResults;
        public ScriptableCullingParameters cullParams;
        public PipelineResources resources;
        public DrawingSettings defaultDrawSettings;
    }
}