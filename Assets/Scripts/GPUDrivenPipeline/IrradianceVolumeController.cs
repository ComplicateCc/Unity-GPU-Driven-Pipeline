using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using Unity.Mathematics;
using System;
using UnityEngine.Rendering;
namespace MPipeline
{
    public struct LoadedIrradiance
    {
        public uint3 resolution;
        public float3 size;
        public float3 position;
        public int renderTextureIndex;
    }
    public struct CoeffTexture
    {
        public RenderTexture coeff0;
        public RenderTexture coeff1;
        public RenderTexture coeff2;
        public RenderTexture coeff3;
        public RenderTexture coeff4;
        public RenderTexture coeff5;
        public RenderTexture coeff6;
        public CoeffTexture(uint3 size)
        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 0,
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                enableRandomWrite = true,
                width = (int)size.x,
                height = (int)size.y,
                volumeDepth = (int)size.z,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = UnityEngine.Rendering.ShadowSamplingMode.None,
                sRGB = false,
                useDynamicScale = false,
                useMipMap = false,
                vrUsage = VRTextureUsage.None
            };
            coeff0 = new RenderTexture(desc);
            coeff1 = new RenderTexture(desc);
            coeff2 = new RenderTexture(desc);
            coeff3 = new RenderTexture(desc);
            coeff4 = new RenderTexture(desc);
            coeff5 = new RenderTexture(desc);
            coeff6 = new RenderTexture(desc);
            coeff0.filterMode = FilterMode.Bilinear;
            coeff1.filterMode = FilterMode.Bilinear;
            coeff2.filterMode = FilterMode.Bilinear;
            coeff3.filterMode = FilterMode.Bilinear;
            coeff4.filterMode = FilterMode.Bilinear;
            coeff5.filterMode = FilterMode.Bilinear;
            coeff6.filterMode = FilterMode.Bilinear;
            coeff0.Create();
            coeff1.Create();
            coeff2.Create();
            coeff3.Create();
            coeff4.Create();
            coeff5.Create();
            coeff6.Create();
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(coeff0);
            UnityEngine.Object.Destroy(coeff1);
            UnityEngine.Object.Destroy(coeff2);
            UnityEngine.Object.Destroy(coeff3);
            UnityEngine.Object.Destroy(coeff4);
            UnityEngine.Object.Destroy(coeff5);
            UnityEngine.Object.Destroy(coeff6);
        }
    }
    [System.Serializable]
    public sealed unsafe class IrradianceVolumeController
    {
        public static IrradianceVolumeController current { get; private set; }
        public IrradianceResources resources;
        public NativeList<LoadedIrradiance> loadedIrradiance { get; private set; }
        public List<CoeffTexture> coeffTextures { get; private set; }
        private ComputeBuffer coeff;
        private PipelineResources pipelineRes;
        private Action<CommandBuffer> cubeToCoeff;
        public NativeList<int> _CoeffIDs { get; private set; }
        private const int coeffToTex3DKernel = 2;
        private bool isLoading;
        public void Init(PipelineResources res)
        {
            isLoading = false;
            _CoeffIDs = new NativeList<int>(7, Allocator.Persistent);
            string str;
            fixed (char* chr = "_CoeffTexture0")
            {
                str = new string(chr);
            }
            for (int i = 0; i < 7; ++i)
            {
                fixed (char* chr = str)
                {
                    chr[13] = (char)(i + 48);
                }
                _CoeffIDs.Add(Shader.PropertyToID(str));
            }
            pipelineRes = res;
            loadedIrradiance = new NativeList<LoadedIrradiance>(10, Allocator.Persistent);
            coeffTextures = new List<CoeffTexture>();
            current = this;
            cubeToCoeff = CoeffToRenderTexture;
        }

        private void LoadVolume(int index)
        {
            isLoading = true;
            IrradianceResources.Volume data = resources.allVolume[index];
            LoadedIrradiance irr = new LoadedIrradiance
            {
                resolution = data.resolution,
                position = data.position,
                size = data.size,
                renderTextureIndex = loadedIrradiance.Length
            };
            resolution = data.resolution;
            targetPath = data.path;
            loadedIrradiance.Add(irr);
            currentTexture = new CoeffTexture(data.resolution);
            coeffTextures.Add(currentTexture);
            len = (int)(data.resolution.x * data.resolution.y * data.resolution.z * 9);
            if (coeff != null && coeff.count < len)
            {
                coeff.Dispose();
                coeff = new ComputeBuffer(len, 12);
            }
            if (coeff == null)
            {
                coeff = new ComputeBuffer(len, 12);
            }
            LoadingThread.AddCommand((obj) =>
            {
                var controller = (IrradianceVolumeController)obj;
                controller.LoadVolumeAsync();
            }, this);
        }
        private int len;
        private uint3 resolution;
        private CoeffTexture currentTexture;
        private string targetPath;
        private byte[] bytes;

        public void LoadVolumeAsync()
        {
            if (bytes == null || bytes.Length < len) bytes = new byte[len];
            using (FileStream reader = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
            {
                int length = (int)reader.Length;
                reader.Read(bytes, 0, length);
            }
            lock (SceneController.commandQueue)
            {
                SceneController.commandQueue.Queue(FinishLoading());
            }
        }

        private IEnumerator FinishLoading()
        {
            coeff.SetData(bytes);
            yield return null;
            RenderPipeline.ExecuteBufferAtFrameEnding(cubeToCoeff);
            yield return null;
            yield return null;
            isLoading = false;
        }

        private void CoeffToRenderTexture(CommandBuffer buffer)
        {
            ComputeShader shader = pipelineRes.shaders.probeCoeffShader;
            buffer.SetComputeBufferParam(shader, coeffToTex3DKernel, ShaderIDs._Coeff, coeff);
            UIntPtr rtPtr = new UIntPtr(MUnsafeUtility.GetManagedPtr(currentTexture.coeff0));
            for(int i = 0; i < 7; ++i)
            {
                buffer.SetComputeTextureParam(shader, coeffToTex3DKernel, _CoeffIDs[i], MUnsafeUtility.GetObject<RenderTexture>((rtPtr + sizeof(UIntPtr) * i).ToPointer()));
            }
            buffer.SetComputeVectorParam(shader, ShaderIDs._Tex3DSize, new float4(resolution.x, resolution.y, resolution.z, 1));
            ComputeShaderUtility.Dispatch(shader, buffer, coeffToTex3DKernel, len / 9, 64);
        }

        public void Dispose()
        {
            current = null;
            _CoeffIDs.Dispose();
            foreach (var i in loadedIrradiance)
            {
                coeffTextures[i.renderTextureIndex].Dispose();
            }
            coeffTextures.Clear();
            loadedIrradiance.Dispose();
            if (coeff != null) coeff.Dispose();
        }
    }
}