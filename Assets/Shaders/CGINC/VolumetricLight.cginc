#ifndef __VOLUMETRIC_INCLUDE__
#define __VOLUMETRIC_INCLUDE__
#include "CGINC/VoxelLight.cginc"

Texture3D<float4> _VolumeTex; SamplerState sampler_VolumeTex;
float4 _Screen_TexelSize;
#ifndef __RANDOM_INCLUDED__
float4 _RandomSeed;
#endif
inline int2 ihash_volume(int2 n)
{
	n = (n<<13)^n;
	return (n*(n*n*15731+789221)+1376312589) & 2147483647;
}

inline float2 frand_volume(int2 n)
{
	return ihash_volume(n) / 2147483647.0;
}

inline float2 cellNoise_volume(int2 p)
{
	int i = p.y*256 + p.x;
	return sin(float2(frand_volume(int2(i, i + 57))) * _RandomSeed.xy + _RandomSeed.zw) * 0.8;
}

float4 Fog(float linear01Depth, float2 screenuv)
{
	float z = linear01Depth * _NearFarClip.x;
	z = (z - _NearFarClip.y) / (1 - _NearFarClip.y);
	if (z < 0.0)
		return float4(0, 0, 0, 1);
    z = pow(z, 1 / 1.5);
	float3 uvw = float3(screenuv.x, screenuv.y, z);
	uvw.xy += cellNoise_volume(uvw.xy * _Screen_TexelSize.zw) / ((float2)_ScreenSize.xy);
	return _VolumeTex.Sample(sampler_VolumeTex, uvw);
}
#endif