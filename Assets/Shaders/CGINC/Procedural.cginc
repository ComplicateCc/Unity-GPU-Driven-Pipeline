#ifndef PROCEDURAL
#define PROCEDURAL

#define CLUSTERCLIPCOUNT 255
#define CLUSTERVERTEXCOUNT 255
#define PLANECOUNT 6
    struct PropertyValue
    {
        float _SpecularIntensity;
        float _MetallicIntensity;
        float4 _EmissionColor;
        float _Occlusion;
        float _Glossiness;
        float4 _Color;
        int3 textureIndex;
        int2 detailTextureIndex;
        float4 mainScaleOffset;
        float4 detailScaleOffset;
    };
struct Point{
    float3 vertex;
    float4 tangent;
    float3 normal;
    float2 texcoord;
	uint objIndex;
    float2 lightmapUV;
    int lightmapIndex;
};
#ifndef COMPUTESHADER		//Below is Not for compute shader
StructuredBuffer<Point> verticesBuffer;
StructuredBuffer<uint> resultBuffer;
static const uint IndexArray[6] = 
{
	0,	1,	2,
	1,	3,	2
};
inline Point getVertex(uint vertexID, uint instanceID)
{
    instanceID = resultBuffer[instanceID];
	uint vertID = instanceID * CLUSTERCLIPCOUNT;
	return verticesBuffer[vertID + vertexID];
}
#endif
#endif