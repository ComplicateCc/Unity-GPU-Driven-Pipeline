#ifndef __RANDOM_INCLUDED__
#define __RANDOM_INCLUDED__

float4 _RandomSeed;
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

inline float2 cellNoise(float2 p)
{
	uint seed = dot(frac(p), float2(173741824.68716524, 163841384.765168716));
	return sin(float2(frand(uint2(seed, seed - 53))) * _RandomSeed.xy + _RandomSeed.zw);
}

inline float3 cellNoise(float3 p)
{
	uint seed = dot(frac(p), float3(107341824.46871357, 135846297.95167742, 121381384.63879163));
	return sin(float3(frand(uint3(seed, seed - 12, seed - 57))) * _RandomSeed.xyz + _RandomSeed.w);
}
#endif