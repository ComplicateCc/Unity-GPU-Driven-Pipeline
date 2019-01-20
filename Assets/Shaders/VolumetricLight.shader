Shader "Hidden/VolumetricLight"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

CGINCLUDE
#pragma target 5.0
#include "CGINC/VoxelLight.cginc"
#include "UnityCG.cginc"

Texture3D<float4> _VolumeTex; SamplerState sampler_VolumeTex;
Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;

float4 _Screen_TexelSize;

float4 _RandomSeed;
inline int ihash(int n)
{
	n = (n<<13)^n;
	return (n*(n*n*15731+789221)+1376312589) & 2147483647;
}

inline float frand(int n)
{
	return ihash(n) / 2147483647.0;
}

inline float2 cellNoise(int2 p)
{
	int i = p.y*256 + p.x;
	return sin(float2(frand(i), frand(i + 57)) * _RandomSeed.xy + _RandomSeed.zw);
}

float4 Fog(float linear01Depth, float2 screenuv)
{
	float z = linear01Depth * _NearFarClip.x;
	z = (z - _NearFarClip.y) / (1 - _NearFarClip.y);
	if (z < 0.0)
		return float4(0, 0, 0, 1);

	float3 uvw = float3(screenuv.x, screenuv.y, z);
	uvw.xy += cellNoise(uvw.xy * _Screen_TexelSize.zw) / ((float2)_ScreenSize.xy);
	return _VolumeTex.Sample(sampler_VolumeTex, uvw);
}
            struct v2fScreen
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            v2fScreen screenVert (appdata v)
            {
                v2fScreen o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

ENDCG
        pass
        {
            Cull off ZWrite off ZTest Always
            Blend oneMinusSrcAlpha srcAlpha
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float4 frag(v2fScreen i) : SV_TARGET
            {
                
                float linear01Depth = Linear01Depth(_CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv));
		        float4 fog = Fog(linear01Depth, i.uv);
		        return fog;
            }
            ENDCG
        }
    }
}