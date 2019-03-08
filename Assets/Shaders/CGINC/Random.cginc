#ifndef __RANDOM_INCLUDED__
#define __RANDOM_INCLUDED__

float4 _RandomSeed;
inline int2 ihash(int2 n)
{
	n = (n << 13) ^ n;
	return (n*(n*n * 15731 + 789221) + 1376312589) & 2147483647;
}

inline int3 ihash(int3 n)
{
	n = (n << 13) ^ n;
	return (n*(n*n * 15731 + 789221) + 1376312589) & 2147483647;
}

inline float2 frand(int2 n)
{
	return ihash(n) / 2147483647.0;
}

inline float3 frand(int3 n)
{
	return ihash(n) / 2147483647.0;
}

inline float2 cellNoise(float2 p)
{
	int seed = dot(p, float2(214748364.68716524, 214748367.765168716));
	return sin(float2(frand(int2(seed, seed - 53))) * _RandomSeed.xy + _RandomSeed.zw);
}

inline float3 cellNoise(float3 p)
{
	int seed = dot(p, float3(214748347.46871357, 214743647.95167742, 247483647.63879163));
	return sin(float3(frand(int3(seed, seed - 12, seed - 57))) * _RandomSeed.xyz + _RandomSeed.w);
}
#endif