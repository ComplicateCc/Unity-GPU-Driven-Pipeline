using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
namespace MPipeline
{
    public struct PipelineBaseBuffer
    {
        public ComputeBuffer reCheckCount;
        public ComputeBuffer reCheckResult;
        public ComputeBuffer dispatchBuffer;
        public ComputeBuffer clusterBuffer;         //ObjectInfo
        public ComputeBuffer instanceCountBuffer;   //uint
        public ComputeBuffer resultBuffer;          //uint
        public ComputeBuffer verticesBuffer;        //Point
        public ComputeBuffer propertyBuffer;
        public Material combinedMaterial;
        public int clusterCount;
        public const int INDIRECTSIZE = 20;
        public const int UINTSIZE = 4;
        public const int CLUSTERCLIPCOUNT = 256;
        public const int CLUSTERVERTEXCOUNT = CLUSTERCLIPCOUNT * 6 / 4;
        public static class ComputeShaderKernels
        {
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
        }
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
    public struct ShadowmapSettings
    {
        public int resolution;
        public float firstLevelDistance;
        public float secondLevelDistance;
        public float thirdLevelDistance;
        public float farestDistance;
        public Vector4 bias;
        public Vector4 normalBias;
        public Vector4 cascadeSoftValue;
    }

    public struct ShadowMapComponent
    {
        public OrthoCam shadCam;
        public Material shadowDepthMaterial;
        public MaterialPropertyBlock block;
        public RenderTexture shadowmapTexture;
        public NativeArray<Vector3> frustumCorners;
        public NativeArray<AspectInfo> shadowFrustumPlanes;
        public Light light;
    }
    [System.Serializable]
    public struct Point
    {
        public Vector3 vertex;
        public Vector4 tangent;
        public Vector3 normal;
        public Vector2 texcoord;
        public uint objIndex;
    }
    [System.Serializable]
    public struct ClusterMeshData
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
        public Vector3 right;
        public Vector3 up;
        public Vector3 forward;
        public Vector3 position;
        public float fov;
        public float nearClipPlane;
        public float farClipPlane;
        public float aspect;
        public Matrix4x4 localToWorldMatrix;
        public Matrix4x4 worldToCameraMatrix;
        public Matrix4x4 projectionMatrix;
        public void UpdateTRSMatrix()
        {
            localToWorldMatrix.SetColumn(0, right);
            localToWorldMatrix.SetColumn(1, up);
            localToWorldMatrix.SetColumn(2, forward);
            localToWorldMatrix.SetColumn(3, position);
            localToWorldMatrix.m33 = 1;
            worldToCameraMatrix = localToWorldMatrix.inverse;
            worldToCameraMatrix.SetRow(2, -worldToCameraMatrix.GetRow(2));
        }
        public void UpdateProjectionMatrix()
        {
            projectionMatrix = Matrix4x4.Perspective(fov, aspect, nearClipPlane, farClipPlane);
        }
    }

    public struct OrthoCam
    {
        public Matrix4x4 worldToCameraMatrix;
        public Matrix4x4 localToWorldMatrix;
        public Vector3 right;
        public Vector3 up;
        public Vector3 forward;
        public Vector3 position;
        public float size;
        public float nearClipPlane;
        public float farClipPlane;
        public Matrix4x4 projectionMatrix;
        public void UpdateTRSMatrix()
        {
            localToWorldMatrix.SetColumn(0, right);
            localToWorldMatrix.SetColumn(1, up);
            localToWorldMatrix.SetColumn(2, forward);
            localToWorldMatrix.SetColumn(3, position);
            localToWorldMatrix.m33 = 1;
            worldToCameraMatrix = localToWorldMatrix.inverse;
            worldToCameraMatrix.SetRow(2, -worldToCameraMatrix.GetRow(2));
        }
        public void UpdateProjectionMatrix()
        {
            projectionMatrix = Matrix4x4.Ortho(-size, size, -size, size, nearClipPlane, farClipPlane);
        }
    }

    public struct StaticFit
    {
        public int resolution;
        public Camera mainCamTrans;
        public NativeArray<Vector3> frustumCorners;
    }
    public struct RenderArray
    {
        public Vector4[] farFrustumCorner;
        public Vector4[] nearFrustumCorner;
        public Vector4[] frustumPlanes;
        public RenderArray(bool init)
        {
            if (init)
            {
                frustumPlanes = new Vector4[6];
                farFrustumCorner = new Vector4[6];
                nearFrustumCorner = new Vector4[6];
            }
            else
            {
                farFrustumCorner = null;
                nearFrustumCorner = null;
                frustumPlanes = null;
            }
        }
    }


    public struct RenderTargets
    {
        public RenderTexture renderTarget;
        public RenderTexture backupTarget;
        public RenderTexture[] gbufferTextures;
        public RenderTargetIdentifier[] gbufferIdentifier;
        public RenderTargetIdentifier renderTargetIdentifier;
        public RenderTargetIdentifier backupIdentifier;
        public RenderTargetIdentifier depthIdentifier;
        public int[] gbufferIndex;
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
                Shader.PropertyToID("_CameraDepthTexture")
            };
            rt.gbufferIdentifier = new RenderTargetIdentifier[6];
            rt.gbufferTextures = new RenderTexture[6];
            rt.renderTarget = null;
            rt.backupTarget = null;
            rt.backupIdentifier = default;
            rt.depthIdentifier = default;
            rt.renderTargetIdentifier = default;
            return rt;
        }
        public RenderTargetIdentifier depthTexture
        {
            get { return new RenderTargetIdentifier(gbufferTextures[5]); }
        }
        public RenderTargetIdentifier motionVectorTexture
        {
            get { return new RenderTargetIdentifier(gbufferTextures[4]); }
        }
        public RenderTargetIdentifier normalIdentifier
        {
            get { return new RenderTargetIdentifier(gbufferTextures[2]); }
        }
    }

    public struct PipelineCommandData
    {
        public Matrix4x4 vp;
        public Matrix4x4 inverseVP;
        public PipelineBaseBuffer baseBuffer;
        public RenderArray arrayCollection;
        public CommandBuffer buffer;
        public MPipeline.PipelineResources resources;
    }
}