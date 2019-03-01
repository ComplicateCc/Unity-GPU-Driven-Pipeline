#ifndef __LOCALLIGHTING_INCLUDE__
#define __LOCALLIGHTING_INCLUDE__
StructuredBuffer<PointLight> _AllPointLight;
StructuredBuffer<uint> _PointLightIndexBuffer;
StructuredBuffer<SpotLight> _AllSpotLight;
StructuredBuffer<uint> _SpotLightIndexBuffer;
float2 _CameraClipDistance; //X: Near Y: Far - Near
TextureCubeArray<float> _CubeShadowMapArray; SamplerState sampler_CubeShadowMapArray;
Texture2DArray<float> _SpotMapArray; SamplerState sampler_SpotMapArray;
float3 CalculateLocalLight(float2 uv, float4 WorldPos, float linearDepth, float3 AlbedoColor, float3 WorldNormal, float4 SpecularColor, float Roughness)
{
	float ShadowTrem;
	float3 ShadingColor = 0;
	float rate = saturate((linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y);
	uint3 voxelValue = uint3((uint2)(uv * float2(XRES, YRES)), (uint)(rate * ZRES));
	uint sb = GetIndex(voxelValue, VOXELSIZE, (MAXLIGHTPERCLUSTER + 1));
	uint2 LightIndex;// = uint2(sb + 1, _PointLightIndexBuffer[sb]);
	uint c;
	float3 ViewDir = normalize(_WorldSpaceCameraPos.rgb - WorldPos.rgb);
#if SPOTLIGHT
	float2 spotRandomSeed = uv;
	LightIndex = uint2(sb + 1, _SpotLightIndexBuffer[sb]);
	[loop]
	for (c = LightIndex.x; c < LightIndex.y; c++)
	{
		SpotLight Light = _AllSpotLight[_SpotLightIndexBuffer[c]];
		Cone SpotCone = Light.lightCone;

		float LightRange = SpotCone.radius;
		float3 LightPos = SpotCone.vertex;
		float3 LightColor = Light.lightColor;

		float LightAngle = Light.angle;
		float3 LightForward = SpotCone.direction;
		float3 Un_LightDir = LightPos - WorldPos.xyz;
		float lightDirLen = length(Un_LightDir);
		float3 LightDir = Un_LightDir / lightDirLen;
		float3 floatDir = normalize(ViewDir + LightDir);
		float ldh = -dot(LightDir, SpotCone.direction);
		//////BSDF Variable
		BSDFContext LightData;
		Init(LightData, WorldNormal, ViewDir, LightDir, floatDir);

		//////Shading
		ShadowTrem = dot(-Un_LightDir, SpotCone.direction) > Light.nearClip;
		float3 Energy = Spot_Energy(ldh, lightDirLen, LightColor, cos(Light.smallAngle), cos(LightAngle), 1.0 / LightRange, LightData.NoL);
		if (Light.shadowIndex >= 0)
		{
			//TODO
		//Soft Shadow
			//Get random number (-1, 1)
			//spotRandomSeed = cellNoise(spotRandomSeed);
			float4 clipPos = mul(Light.vpMatrix, WorldPos);
			clipPos /= clipPos.w;
			float2 lightUV = clipPos.xy * 0.5 + 0.5;
			float shadowDist = _SpotMapArray.Sample(sampler_SpotMapArray, float3(lightUV, Light.shadowIndex));
			ShadowTrem = (lightDirLen - 0.25) / SpotCone.height < shadowDist;
		}
		ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness) * ShadowTrem);


	}
#endif
#if POINTLIGHT
	float3 pointRandomSeed = ViewDir;
	LightIndex = uint2(sb + 1, _PointLightIndexBuffer[sb]);
	[loop]
	for (c = LightIndex.x; c < LightIndex.y; c++)
	{
		PointLight Light = _AllPointLight[_PointLightIndexBuffer[c]];
		float LightRange = Light.sphere.a;
		float3 LightPos = Light.sphere.rgb;
		float3 LightColor = Light.lightColor;

		float3 Un_LightDir = LightPos - WorldPos.xyz;
		float Length_LightDir = length(Un_LightDir);
		float3 LightDir = Un_LightDir / Length_LightDir;
		float3 floatDir = normalize(ViewDir + LightDir);
		ShadowTrem = 1;
		//////Shadow
		if (Light.shadowIndex >= 0) {
			//TODO
			//Soft Shadow
			//Get random number (-1, 1)
			//pointRandomSeed = cellNoise(pointRandomSeed);
			float DepthMap = (Length_LightDir - 0.25) / LightRange;
			float ShadowMap = _CubeShadowMapArray.Sample(sampler_CubeShadowMapArray, float4(Un_LightDir * float3(-1, -1, 1), Light.shadowIndex));
			// ShadingColor += ShadowMap;
		   //  continue;
			ShadowTrem = saturate(DepthMap < ShadowMap);
		}

		//////BSDF Variable
		BSDFContext LightData;
		Init(LightData, WorldNormal, ViewDir, LightDir, floatDir);

		//////Shading
		float3 Energy = Point_Energy(Un_LightDir, LightColor, 1 / LightRange, LightData.NoL) * ShadowTrem;
		ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness));
	}
#endif

	return ShadingColor;
}

#endif