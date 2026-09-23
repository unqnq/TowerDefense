// MAST paint-area indicator.
Shader "MAST/SRP/Shader_PaintArea_SRP"
{
	Properties
	{
		_Color1("Color", Color) = (0,0,0,0)
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			fixed4 _Color1;

			float4 vert( float4 vertex : POSITION ) : SV_POSITION
			{
				return UnityObjectToClipPos( vertex );
			}

			fixed4 frag() : SV_Target
			{
				return _Color1;
			}
			ENDCG
		}
	}
}
