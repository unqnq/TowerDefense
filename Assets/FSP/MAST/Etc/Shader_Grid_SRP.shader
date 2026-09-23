// MAST grid overlay.
// Unlit so the grid stays equally visible regardless of scene lighting.
Shader "MAST/SRP/Shader_Grid_SRP"
{
	Properties
	{
		_GridTexture("GridTexture", 2D) = "white" {}
		_Opacity("Opacity", Range( 0 , 1)) = 0.5
		_Tint("Tint", Color) = (1,1,1,1)
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" "IgnoreProjector" = "True" }
		Cull Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _GridTexture;
			float4 _GridTexture_ST;
			fixed4 _Tint;
			fixed _Opacity;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert( appdata_base v )
			{
				v2f o;
				o.pos = UnityObjectToClipPos( v.vertex );
				o.uv = TRANSFORM_TEX( v.texcoord, _GridTexture );
				return o;
			}

			fixed4 frag( v2f i ) : SV_Target
			{
				fixed4 tex = tex2D( _GridTexture, i.uv );
				return fixed4( lerp( tex.rgb, _Tint.rgb, _Tint.a ), tex.a * _Opacity );
			}
			ENDCG
		}
	}
}
