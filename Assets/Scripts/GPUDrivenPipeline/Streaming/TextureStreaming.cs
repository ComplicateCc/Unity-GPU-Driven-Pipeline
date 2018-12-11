using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe static class TextureStreaming
    {
        public static byte[] GetBytes(Texture2D texs)
        {
            if(!texs.isReadable)
            {
                Debug.LogError(texs.name + " is not readable!");
                return default;
            }
            Color32[] colors = texs.GetPixels32();
            byte[] values = new byte[colors.Length * sizeof(Color32)];
            fixed(Color32* source = colors)
            {
                UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref values[0]) , source, colors.Length * sizeof(Color32));
            }
            return values;
        }
    }
}