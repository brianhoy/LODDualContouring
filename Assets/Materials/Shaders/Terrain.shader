Shader "Custom/Terrain" {
    Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_MorphAmount ("Morph Amount", Range(0,1)) = 0
    }
	SubShader {
		Tags { "RenderType" = "Opaque" }
		CGPROGRAM
		#pragma surface surf Lambert vertex:vert
		struct Input {
			float2 uv_MainTex;
		};
		float _MorphAmount;
		void vert (inout appdata_full v) {
			v.vertex.xyz = (1 - _MorphAmount) * v.vertex.xyz + _MorphAmount * v.texcoord.xyz;
			v.normal.xyz += (1 - _MorphAmount) * v.normal.xyz + _MorphAmount * v.texcoord1.xyz;
		}
		sampler2D _MainTex;
		void surf (Input IN, inout SurfaceOutput o) {
			o.Albedo = tex2D (_MainTex, IN.uv_MainTex).rgb;
		}
		ENDCG
	} 
    Fallback "Diffuse"
  }
