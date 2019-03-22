#ifndef __RANDOM_INCLUDED__
#define __RANDOM_INCLUDED__

float4 _RandomSeed;
/*
inline uint2 ihash(uint2 n)
{
	n = (n << 13) ^ n;
	return (n*(n*n * 15731 + 789221) + 1376312589) & 2147483647;
}

inline uint3 ihash(uint3 n)
{
	n = (n << 13) ^ n;
	return (n*(n*n * 15731 + 789221) + 1376312589) & 2147483647;
}

inline float2 frand(uint2 n)
{
	return ihash(n) / 2147483647.0;
}

inline float3 frand(uint3 n)
{
	return ihash(n) / 2147483647.0;
}
*/
inline float2 cellNoise(float2 p)
{
	return (frac(sin(float2(dot(p, _RandomSeed.xy * float2(0.46224618, 0.98746513)), dot(p, _RandomSeed.zw * float2(0.75236874,0.83216759)))) * _RandomSeed.yw + float2(dot(p, _RandomSeed.wz * float2(0.82654136, 0.93452179)), dot(p, _RandomSeed.yx * float2(0.72654195, 0.89135764)))) * 2 - 1).yx;
	/*uint seed = dot(frac(p), float2(173741824.68716524, 163841384.765168716));
	return sin(float2(frand(uint2(seed, seed - 53))) * _RandomSeed.xy + _RandomSeed.zw);*/
}

inline float3 cellNoise(float3 p)
{
	float3 spot = float3(dot(p, _RandomSeed.xyz * float3(0.946216874, 0.89643168, 0.843268746)), dot(p, _RandomSeed.wzy * float3(0.92654135, 0.86314568, 0.96321489)), dot(p, _RandomSeed.ywx * float3(0.87965412, 0.31689541, 0.46235879)));
	float3 spot2 = float3(dot(p, _RandomSeed.zxy * float3(0.698745638, 0.79354621, 0.13568746)), dot(p, _RandomSeed.zyw * float3(0.43568795, 0.23654795, 0.69874652)), dot(p, _RandomSeed.xyw * float3(0.96541325, 0.79654136, 0.86541239)));
	return (frac(sin(spot) * _RandomSeed.wyz + spot2) * 2 - 1).yzx;
	/*
	uint seed = dot(frac(p), float3(107341824.46871357, 135846297.95167742, 121381384.63879163));
	return sin(float3(frand(uint3(seed, seed - 12, seed - 57))) * _RandomSeed.xyz + _RandomSeed.w);*/
}
#endif