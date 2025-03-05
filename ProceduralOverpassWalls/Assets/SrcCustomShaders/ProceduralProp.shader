// Upgrade NOTE: replaced 'UNITY_INSTANCE_ID' with 'UNITY_VERTEX_INPUT_INSTANCE_ID'

Shader "Custom/ProceduralObject/Prop/TestShaderInd"
{
	Properties
	{
		// From https://docs.unity3d.com/Manual/built-in-shader-examples-receive-shadows.html
		_MainTex("Texture", 2D) = "white" {}
		_ACIMap("Alpha/ColorMask/Illumination (RGB)", 2D) = "black" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }

		Pass
		{
			Tags {"LightMode" = "ForwardBase"}
			CGPROGRAM
			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma enable_d3d11_debug_symbols
			#pragma multi_compile_instancing 
			#pragma instancing_options
			#include "UnityCG.cginc"
			#include "Lighting.cginc"

			// compile shader into multiple variants, with and without shadows
			// (we don't care about any lightmaps yet, so skip these variants)
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			// shadow helper functions and macros
			#include "AutoLight.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				SHADOW_COORDS(1) // put shadows data into TEXCOORD1
				fixed4 color : COLOR0;
				fixed3 diff : COLOR1;
				fixed3 ambient : COLOR2;
				float4 pos : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct MeshProperties {
				float4x4 pos;
				float4 color;
				float4 castShadow;
			};

			// float4 _Colors[1020];
				
			StructuredBuffer<MeshProperties> _Properties;

			v2f vert(appdata_full v, uint instanceID: SV_InstanceID)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				//o.pos = UnityObjectToClipPos(v.vertex);
				float4 pos = mul(_Properties[instanceID].pos, v.vertex);
				//float4 pos = v.vertex;
				o.pos = UnityObjectToClipPos(pos);
				o.uv = v.texcoord;
				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
				o.diff = nl * _LightColor0.rgb;
				o.ambient = ShadeSH9(half4(worldNormal,1));
				// compute shadows data
				TRANSFER_SHADOW(o)

				/*#ifdef UNITY_INSTANCING_ENABLED
					o.color = _Properties[instanceID].color;
				#else
					o.color = float4(1, 1, 1, 1);
				#endif*/
				o.color = _Properties[instanceID].color;
				return o;
			}

			sampler2D _MainTex;
			sampler2D _ACIMap;

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				col *= i.color;
				// compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
				fixed shadow = SHADOW_ATTENUATION(i);
				// darken light's illumination with shadow, keep ambient intact
				fixed3 lighting = i.diff * shadow + i.ambient;
				col.rgb *= lighting;
				fixed4 col2 = tex2D(_ACIMap, i.uv);
				fixed4 col_w_alpha = fixed4(col.r, col.g, col.b, col2.r);
				clip(col_w_alpha.w > 0.9f ? -1 : 1);
				return col_w_alpha;
			}
			ENDCG
		}

		//shadow casting support
		// UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"

		// shadow caster rendering pass, implemented manually
		// using macros from UnityCG.cginc
		//Pass
		//{
		//	Tags {"LightMode" = "ShadowCaster"}

		//	CGPROGRAM
		//	#pragma vertex vert
		//	#pragma fragment frag
		//	#pragma multi_compile_shadowcaster
		//	#pragma enable_d3d11_debug_symbols
		//	#include "UnityCG.cginc"
		//	#include "UnityInstancing.cginc"

		//	struct v2f {
		//		V2F_SHADOW_CASTER;
		//	};

		//	struct MeshProperties {
		//		float4x4 pos;
		//		float4 color;
		//		float4 castShadow;
		//	};

		//	StructuredBuffer<MeshProperties> _Properties;

		//	v2f vert(appdata_full v)
		//	{
		//		v2f o; 
		//		//UNITY_TRANSFER_INSTANCE_ID(v, o);
		//		//uint iID = v.instanceID;
		//		//float4 pos = mul(_Properties[iID].pos, v.vertex);
		//		//float4 pos = v.vertex;				
		//		//o.pos = UnityObjectToClipPos(pos);
		//		//o.pos = UnityObjectToClipPos(v.vertex);
		//		TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
		//		return o;
		//	}

		//	float4 frag(v2f i) : SV_Target
		//	{
		//		SHADOW_CASTER_FRAGMENT(i)
		//		//return float4(i.pos.x, i.pos.y, i.pos.z, 1.0);
		//	}
		//	ENDCG
		//}
	}
}