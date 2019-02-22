Shader "ShouShouPBR"
{
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.3
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		_EmissionMultiplier("Emission Level", Range(1, 20)) = 1
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_SpecularMap("R(specular), B(Smoothness), A(AO)", 2D) = "white"{}
	}
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf StandardSpecular fullforwardshadows

        #pragma target 5.0
            struct Input
        {
            float2 uv_texcoord;
        };
    float _SpecularIntensity;
	float _MetallicIntensity;
    float4 _EmissionColor;
		float _Occlusion;
		float _VertexScale;
		float _VertexOffset;
		sampler2D _BumpMap;
		sampler2D _SpecularMap;
		sampler2D _MainTex; float4 _MainTex_ST;
		sampler2D _DetailAlbedo; float4 _DetailAlbedo_ST;
		sampler2D _DetailNormal;

		float _Glossiness;
		float4 _Color;


        void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			// Albedo comes from a texture tinted by color
			float2 uv = IN.uv_texcoord;// - parallax_mapping(IN.uv_MainTex,IN.viewDir);
			float2 detailUV = TRANSFORM_TEX(uv, _DetailAlbedo);
			float4 spec = tex2D(_SpecularMap,uv);
			uv = TRANSFORM_TEX(uv, _MainTex);
			float4 c = tex2D (_MainTex, uv);
			o.Albedo = c.rgb;
			float4 detailColor = tex2D(_DetailAlbedo, detailUV);
			o.Albedo = lerp(detailColor.rgb, o.Albedo, spec.b) * _Color.rgb;
			o.Alpha = 1;
			o.Occlusion = lerp(min(detailColor.a, c.a), c.a, spec.b);
			o.Occlusion = lerp(1, o.Occlusion, _Occlusion);
			o.Specular = lerp(_SpecularIntensity * spec.r, o.Albedo * _SpecularIntensity * spec.r, _MetallicIntensity); 
			o.Smoothness = _Glossiness * spec.g;
			o.Normal = UnpackNormal(tex2D(_BumpMap,uv));
			float3 detailNormal = UnpackNormal(tex2D(_DetailNormal, detailUV));
			o.Normal = lerp(detailNormal, o.Normal, spec.b);
			
			o.Emission = _EmissionColor;
		}
        ENDCG
    }
    FallBack "Diffuse"
}
