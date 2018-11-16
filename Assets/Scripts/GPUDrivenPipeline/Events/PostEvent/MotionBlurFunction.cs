using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using PPSShaderIDs = UnityEngine.Rendering.PostProcessing.ShaderIDs;
namespace MPipeline
{
    public struct MotionBlurData
    {
        public Material motionBlurMat;
        public bool resetHistory;
        public MotionBlur settings;
    }
    public static class MotionBlurFunction
    {
        enum Pass
        {
            VelocitySetup,
            TileMax1,
            TileMax2,
            TileMaxV,
            NeighborMax,
            Reconstruction
        }
        /*
        public static void Render(ref PostSharedData context, ref MotionBlurData data, RenderTexture source, RenderTexture dest)
        {
            if (data.resetHistory)
            {
                data.resetHistory = false;
                Graphics.Blit(source, dest);
                return;
            }
            const float kMaxBlurRadius = 5f;
            const RenderTextureFormat vectorRTFormat = RenderTextureFormat.RGHalf;
            const RenderTextureFormat packedRTFormat = RenderTextureFormat.ARGB2101010;
            int maxBlurPixels = (int)(kMaxBlurRadius * context.screenSize.y / 100);
            // Calculate the TileMax size.
            // It should be a multiple of 8 and larger than maxBlur.
            int tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;

            // Pass 1 - Velocity/depth packing
            var settings = data.settings;
            var motionblurMat = data.motionBlurMat;
            var velocityScale = settings.shutterAngle / 360f;
            motionblurMat.SetFloat(PPSShaderIDs.VelocityScale, velocityScale);
            motionblurMat.SetFloat(PPSShaderIDs.MaxBlurRadius, maxBlurPixels);
            motionblurMat.SetFloat(PPSShaderIDs.RcpMaxBlurRadius, 1f / maxBlurPixels);

            RenderTexture vbuffer = RenderTexture.GetTemporary(context.screenSize.x, context.screenSize.y, 0, packedRTFormat, RenderTextureReadWrite.Linear);
            motionblurMat.SetTexture(PPSShaderIDs.VelocityTex, vbuffer);
            vbuffer.filterMode = FilterMode.Point;
            PostFunctions.BlitFullScreen(vbuffer, motionblurMat, (int)Pass.VelocitySetup);

            RenderTexture tile2 = RenderTexture.GetTemporary(context.screenSize.x / 2, context.screenSize.y / 2, 0, vectorRTFormat, RenderTextureReadWrite.Linear);
            tile2.filterMode = FilterMode.Point;
            PostFunctions.BlitFullScreen(vbuffer, tile2, motionblurMat, (int)Pass.TileMax1);

            // Pass 3 - Second TileMax filter (1/2 downsize)
            RenderTexture tile4 = RenderTexture.GetTemporary(context.screenSize.x / 4, context.screenSize.y / 4, 0, vectorRTFormat, RenderTextureReadWrite.Linear);
            tile4.filterMode = FilterMode.Point;
            PostFunctions.BlitFullScreen(tile2, tile4, motionblurMat, (int)Pass.TileMax2);
            RenderTexture.ReleaseTemporary(tile2);

            // Pass 4 - Third TileMax filter (1/2 downsize)
            RenderTexture tile8 = RenderTexture.GetTemporary(context.screenSize.x / 8, context.screenSize.y / 8, 0, vectorRTFormat, RenderTextureReadWrite.Linear);
            tile4.filterMode = FilterMode.Point;
            PostFunctions.BlitFullScreen(tile4, tile8, motionblurMat, (int)Pass.TileMax2);
            RenderTexture.ReleaseTemporary(tile4);

            // Pass 5 - Fourth TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8f - 1f) * -0.5f;
            motionblurMat.SetVector(PPSShaderIDs.TileMaxOffs, tileMaxOffs);
            motionblurMat.SetFloat(PPSShaderIDs.TileMaxLoop, (int)(tileSize / 8f));

            int neighborMaxWidth = context.screenSize.x / tileSize;
            int neighborMaxHeight = context.screenSize.y / tileSize;

            RenderTexture tile = RenderTexture.GetTemporary(neighborMaxWidth, neighborMaxHeight, 0, vectorRTFormat, RenderTextureReadWrite.Linear);
            tile.filterMode = FilterMode.Point;
            PostFunctions.BlitFullScreen(tile8, tile, motionblurMat, (int)Pass.TileMaxV);
            RenderTexture.ReleaseTemporary(tile8);

            // Pass 6 - NeighborMax filter
            RenderTexture neighborMax = RenderTexture.GetTemporary(neighborMaxWidth, neighborMaxHeight, 0, vectorRTFormat, RenderTextureReadWrite.Linear);// = PPSShaderIDs.NeighborMaxTex;
            motionblurMat.SetTexture(PPSShaderIDs.NeighborMaxTex, neighborMax);
            PostFunctions.BlitFullScreen(tile, neighborMax, motionblurMat, (int)Pass.NeighborMax);
            RenderTexture.ReleaseTemporary(tile);

            // Pass 7 - Reconstruction pass
            motionblurMat.SetFloat(PPSShaderIDs.LoopCount, Mathf.Clamp(settings.sampleCount / 2, 1, 64));
            PostFunctions.BlitFullScreen(source, dest, motionblurMat, (int)Pass.Reconstruction);
            RenderTexture.ReleaseTemporary(vbuffer);
            RenderTexture.ReleaseTemporary(neighborMax);
        }
        */
    }
}
