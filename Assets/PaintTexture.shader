Shader "NewChromantics/PaintTexture"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		PaintUv("PaintUv", VECTOR) = (0,0,0,0)
		PaintBrushSize("PaintBrushSize", Range(0,0.1) ) = 0.1
		PaintBrushColour("PaintBrushColour", Color ) = (1,0,0,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
		
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float PaintBrushSize;
			float2 PaintUv;
			float4 PaintBrushColour;
	
			

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			

			fixed4 frag (v2f i) : SV_Target
			{
				if ( distance( i.uv, PaintUv ) < PaintBrushSize )
					return float4( PaintBrushColour );
				return tex2D( _MainTex, i.uv );
			}
			ENDCG
		}
	}
}
