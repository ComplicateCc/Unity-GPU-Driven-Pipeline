#ifndef __PLANE_INCLUDE__
#define __PLANE_INCLUDE__
inline float4 GetPlane(float3 normal, float3 inPoint)
{
    return float4(normal, -dot(normal, inPoint));
}
inline float4 GetPlane(float3 a, float3 b, float3 c)
{
    float3 normal = normalize(cross(b - a, c - a));
    return float4(normal, -dot(normal, a));
}

inline float4 GetPlane(float4 a, float4 b, float4 c)
{
    a /= a.w;
    b /= b.w;
    c /= c.w;
    float3 normal = normalize(cross(b.xyz - a.xyz, c.xyz - a.xyz));
    return float4(normal, -dot(normal, a.xyz));
}

inline float GetDistanceToPlane(float4 plane, float3 inPoint)
{
    return dot(plane.xyz, inPoint) + plane.w;
}

float BoxIntersect(float3 extent, float3 position, float4 planes[6]){
    float result = 1;
    for(uint i = 0; i < 6; ++i)
    {
        float4 plane = planes[i];
        float3 absNormal = abs(plane.xyz);
        result *= ((dot(position, plane.xyz) - dot(absNormal, extent)) < -plane.w) ? 1.0 : 0.0;
    }
    return result;
}

float SphereIntersect(float4 sphere, float4 planes[2])
{
    float result = 1;
    for(uint i = 0; i < 2; ++i)
    {
        result *= (GetDistanceToPlane(planes[i], sphere.xyz) < sphere.w) ? 1.0 : 0.0;
    }
    return result;
}

float SphereIntersect(float4 sphere, float4 planes[4])
{
    float result = 1;
    for(uint i = 0; i < 4; ++i)
    {
        result *= (GetDistanceToPlane(planes[i], sphere.xyz) < sphere.w) ? 1.0 : 0.0;
    }
    return result;
}
#endif