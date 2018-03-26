Shader "Custom/Terrain" {
    Properties {
		_MainTex ("Texture", 2D) = "white" {}
		//_MorphAmount ("Morph Amount", Range(0,1)) = 0
		_ChunkPosition ("Chunk Position", Vector) = (0, 0, 0, 0)
		_LOD ("LOD", Int) = 0

		_Side("Side", 2D) = "white" {}
		_Top("Top", 2D) = "white" {}
		_Bottom("Bottom", 2D) = "white" {}
		_SideScale("Side Scale", Float) = 2
		_TopScale("Top Scale", Float) = 2
		_BottomScale ("Bottom Scale", Float) = 2

    } 
	SubShader {
		CGPROGRAM
		#pragma surface surf Lambert vertex:vert
		struct Input {
			float3 worldPos;
			float3 worldNormal;
		};
		int _LOD;
		int _ChunkRadius;
		int _ChunkMinimumSize;
		int _ChunkResolution;
		float4 _ViewerPosition;
		float4 _ChunkPosition;

		sampler2D _Side, _Top, _Bottom;
		float _SideScale, _TopScale, _BottomScale;

		void vert (inout appdata_full v, out Input o) {
			int scale = pow(2, _LOD);
			int scale2 = scale * 2;
			int chunkSize = _ChunkMinimumSize * scale;

			float4 worldPos = mul(unity_ObjectToWorld, v.vertex); //_ChunkPosition + ( (v.vertex / (float)_ChunkResolution) * (float)chunkSize);
			int3 voxelPosition = (worldPos / scale2) * scale2;
			int3 localVoxelPosition = (v.vertex / scale2) * scale2;

			float morphAmount = 0;

			morphAmount += clamp(0 + (-_ViewerPosition.x + voxelPosition.x - chunkSize * (_ChunkRadius - 3)) / (chunkSize), 0, 1);
			morphAmount += clamp(0 + (_ViewerPosition.x - voxelPosition.x - chunkSize * (_ChunkRadius - 1)) / (chunkSize), 0, 1);

			morphAmount += clamp(0 + (-_ViewerPosition.y + voxelPosition.y - chunkSize * (_ChunkRadius - 3)) / (chunkSize), 0, 1);
			morphAmount += clamp(0 + (_ViewerPosition.y - voxelPosition.y - chunkSize * (_ChunkRadius - 1)) / (chunkSize), 0, 1);

			morphAmount += clamp(0 + (-_ViewerPosition.z + voxelPosition.z - chunkSize * (_ChunkRadius - 3)) / (chunkSize), 0, 1);
			morphAmount += clamp(0 + (_ViewerPosition.z - voxelPosition.z - chunkSize * (_ChunkRadius - 1)) / (chunkSize), 0, 1);

			//o.customColor = float4((voxelPosition.x + 256) / 512.0, 0, 0, 1);


			morphAmount = clamp(morphAmount, 0.0, 1.0);
			v.vertex.xyz = (1 - morphAmount) * v.vertex.xyz + morphAmount * v.texcoord.xyz;
			v.normal.xyz = (1 - morphAmount) * v.normal.xyz + morphAmount * v.texcoord1.xyz;

			int3 middle = (localVoxelPosition + int3(1, 1, 1));
			
			o.worldNormal = v.normal;
			o.worldPos = mul(unity_ObjectToWorld, v.vertex);

			if(distance(v.vertex, middle) < 0.05) {
				//o.customColor = float4(0, 1, 0, 0);
			}
		}
		sampler2D _MainTex;
		void surf (Input IN, inout SurfaceOutput o) {
			float3 projNormal = saturate(pow(IN.worldNormal * 1.4, 4));
			
			// SIDE X
			float3 x = tex2D(_Side, frac(IN.worldPos.zy * _SideScale)) * abs(IN.worldNormal.x);
			
			// TOP / BOTTOM
			float3 y = 0;
			if (IN.worldNormal.y > 0) {
				y = tex2D(_Top, frac(IN.worldPos.zx * _TopScale)) * abs(IN.worldNormal.y);
			} else {
				y = tex2D(_Bottom, frac(IN.worldPos.zx * _BottomScale)) * abs(IN.worldNormal.y);
			}
			
			// SIDE Z	
			float3 z = tex2D(_Side, frac(IN.worldPos.xy * _SideScale)) * abs(IN.worldNormal.z);
			
			o.Albedo = z;
			o.Albedo = lerp(o.Albedo, x, projNormal.x);
			o.Albedo = lerp(o.Albedo, y, projNormal.y);
		}
		ENDCG
	} 
    Fallback "Diffuse"
  }
