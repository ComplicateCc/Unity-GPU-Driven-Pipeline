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
	int seed = dot(p, float2(641338.4168541, 963955.16871685));
	return sin(float2(frand(int2(seed, seed - 53))) * _RandomSeed.xy + _RandomSeed.zw);
}

inline float3 cellNoise(float3 p)
{
	int seed = dot(p, float3(641738.4168541, 9646285.16871685, 3186964.168734));
	return sin(float3(frand(int3(seed, seed - 12, seed - 57))) * _RandomSeed.xyz + _RandomSeed.w);
}
#endif