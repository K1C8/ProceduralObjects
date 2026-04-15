// Upgrade NOTE: replaced 'UNITY_INSTANCE_ID' with 'UNITY_VERTEX_INPUT_INSTANCE_ID'

// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

Shader "Custom/ProceduralObject/Prop/TestShaderInd" 
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _ColorV0 ("Color Variation 0", Color) = (1,1,1,1)
        _ColorV1 ("Color Variation 1", Color) = (1,1,1,1)
        _ColorV2 ("Color Variation 2", Color) = (1,1,1,1)
        _ColorV3 ("Color Variation 3", Color) = (1,1,1,1)
        _SpecColor ("Specular Color", Color) = (0.5,0.5,0.5,0)
        _MainTex ("Diffuse (RGB)", 2D) = "gray" { }
        _XYSMap ("NormalX/NormalY/Specular (RGB)", 2D) = "bump" { }
        _ACIMap ("Alpha/ColorMask/Illumination (RGB)", 2D) = "black" { }
        _RollLocation0 ("Roll Location 0", Vector) = (0,0,0,1)
        _RollLocation1 ("Roll Location 1", Vector) = (0,0,0,1)
        _RollLocation2 ("Roll Location 2", Vector) = (0,0,0,1)
        _RollLocation3 ("Roll Location 3", Vector) = (0,0,0,1)
        _RollParams0 ("Roll Params 0", Vector) = (0,1,0,10)
        _RollParams1 ("Roll Params 1", Vector) = (0,1,0,10)
        _RollParams2 ("Roll Params 2", Vector) = (0,1,0,10)
        _RollParams3 ("Roll Params 3", Vector) = (0,1,0,10)
        _AtlasRect ("Atlas Rect", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        //shadow casting support
        
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }

            // ZClip Off
            ZTest Less
            Cull Off

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma multi_compile_instancing 
            #pragma instancing_options
            #include "UnityCG.cginc"

            sampler2D _ACIMap;

            struct MeshProperties
            {
                float4x4 pos;        // assumed: instance ObjectToWorld
                float4   color;      // unused in this pass
                float4   castShadow; // castShadow.x assumed 0/1
            };
            StructuredBuffer<MeshProperties> _Properties;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                uint   iid    : SV_InstanceID; // works for DrawMeshInstanced(Indirect)
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;

                // Pass instance id to fragment (so we can read castShadow there too if desired)
                nointerpolation uint iid : TEXCOORD1;
            };

            static float4 ApplyUnityShadowBias(float4 clipPos)
            {
                // Reconstructs the exact pattern in GLSL:
                // bias = unity_LightShadowBias.x / clip.w; saturate
                // zbiased = clip.z + bias
                // zmax = max(-clip.w, zbiased)
                // clip.z = unity_LightShadowBias.y * (zmax - zbiased) + zbiased
                float bias = unity_LightShadowBias.x / clipPos.w;
                bias = saturate(bias);

                float zBiased = clipPos.z + bias;
                float zMax    = max(-clipPos.w, zBiased);
                clipPos.z     = unity_LightShadowBias.y * (zMax - zBiased) + zBiased;

                return clipPos;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.iid = v.iid;
                // o.iid = v.iid;
                MeshProperties mp = _Properties[v.iid];
                // MeshProperties mp = _Properties[instanceID];
                float4 tempShadow = mp.castShadow;

                // Instance object->world
                float4 worldPos = mul(mp.pos, v.vertex);

                // In ShadowCaster pass, UNITY_MATRIX_VP is the shadow camera VP for the current light.
                float4 clipPos = mul(UNITY_MATRIX_VP, worldPos);

                clipPos = ApplyUnityShadowBias(clipPos); 

                o.pos = clipPos;
                o.uv  = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                MeshProperties mp = _Properties[i.iid];
                clip(mp.castShadow.x - 0.5);

                // Same cutout sense as the compiled shader:
                // discard if _ACIMap.r > 0.5  ==> keep only r <= 0.5
                float a = tex2D(_ACIMap, i.uv).r;
                clip(0.5 - a);

                // Color irrelevant for shadow map; depth write is what matters.
                return 1.0.xxxx;
            }
            ENDCG
        }

        Pass
        {
            Name "PREPASS"
            Tags { "LightMode"="PrePassBase" }

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex   vert
            #pragma fragment frag

            // keyword parity
            #pragma multi_compile __ MULTI_INSTANCE
            #pragma multi_compile __ INFOMODE_OFF
            #pragma multi_compile __ SHADING

            #include "UnityCG.cginc"

            sampler2D _XYSMap;
            sampler2D _ACIMap;
            sampler2D _RoadUpwardWetXYS;

            float4 _MainTex_ST;
            float4 _AtlasRect;        // xy = min, zw = size (as used in compiled code)
            float4 _SimulationTime;   // .z used
            float4 _WeatherParams;    // .y and .w used

            struct MeshProperties
            {
                float4x4 pos;        // instance ObjectToWorld from Matrix4x4.TRS(..., Vector3.one)
                float4   color;      // unused in this pass
                float4   castShadow; // unused in this pass
            };
            StructuredBuffer<MeshProperties> _Properties;

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
                float2 uv      : TEXCOORD0;
                uint   iid     : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;

                // World-space TBN basis (3 vectors)
                float3 tbn0 : TEXCOORD1;
                float3 tbn1 : TEXCOORD2;
                float3 tbn2 : TEXCOORD3;

                // xyz: object-space position (as in compiled shader), w: upward mask
                float4 objPos_upMask : TEXCOORD4;

                nointerpolation uint iid : TEXCOORD5; // if later want per-instance params
            };

            // Matches the cubic smoothstep the GLSL builds:
            // t = clamp((normal.y - 0.1) * 1.66666675, 0, 1)
            // upMask = t*t*(3 - 2*t)
            float ComputeUpwardMask(float normalY)
            {
                float t = saturate((normalY - 0.1) * 1.66666675);
                return t * t * (3.0 - 2.0 * t);
            }

            // Derivative-safe clamp of UVs within atlas rect (prevents bleeding across tiles)
            float2 ClampUVToAtlas(float2 uv, float4 atlasRect)
            {
                // dd = max(abs(ddx), abs(ddy)) * 2
                float2 du = ddx(uv);
                float2 dv = ddy(uv);
                float2 dd = max(abs(du), abs(dv)) * 2.0;

                // pad = min(atlasRect.zw * 0.25, dd)
                float2 pad = min(atlasRect.zw * 0.25, dd);

                // uvMin = atlasRect.xy + pad
                // uvMax = atlasRect.xy + atlasRect.zw - pad
                float2 uvMin = atlasRect.xy + pad;
                float2 uvMax = atlasRect.xy + atlasRect.zw - pad;

                return clamp(uv, uvMin, uvMax);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.iid = v.iid;

                MeshProperties mp = _Properties[v.iid];

                // Instance transform
                float4 worldPos = mul(mp.pos, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);

                // UV transform (compiled shader does this)
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                // Build world-space TBN from instance matrix.
                // Since TRS uses scale=1, we can treat (float3x3)mp.pos as pure rotation.
                float3x3 M = (float3x3)mp.pos;

                float3 worldN = normalize(mul(M, v.normal));
                float3 worldT = normalize(mul(M, v.tangent.xyz));

                // unity_WorldTransformParams.w is handedness flip for negative scaling in Unity;
                // instance scale is 1, but keep it for parity.
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                float3 worldB = normalize(cross(worldN, worldT) * tangentSign);

                // In the compiled fragment, they dot three incoming vectors with the TS normal.
                // So we pass basis vectors that reconstruct world normal:
                // world = T * n.x + B * n.y + N * n.z
                o.tbn0 = worldT;
                o.tbn1 = worldB;
                o.tbn2 = worldN;

                // They pass the *object-space* position in TEXCOORD4.xyz for wet sampling.
                // Keep exactly that: object-space vertex position, not world position.
                o.objPos_upMask.xyz = v.vertex.xyz;
                o.objPos_upMask.w   = ComputeUpwardMask(v.normal.y);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // ---- Atlas-safe UV ----
                // float2 uv = ClampUVToAtlas(i.uv, _AtlasRect);
                float2 uv = i.uv;

                // ---- Cutout ----
                float4 aci = tex2D(_ACIMap, uv);
                // discard if aci.r > 0.5  (keep only <= 0.5)
                clip(0.5 - aci.r);

                // ---- Base normal + ¡°spec¡± channel from XYS ----
                float4 xys = tex2D(_XYSMap, uv);

                float2 baseXY = xys.xy * 2.0 - 1.0;

                // Alpha output: (1 - z)^2 * 32, clamped to >= 1
                float a = (1.0 - xys.z);
                a = a * a * 32.0;
                a = max(a, 1.0);

                // ---- Wet / upward perturbation ----
                // Compiled code:
                // u = objPos.xzyy * (1/16,1/16,0.13,0.17); sample at u.zw + u.xy
                float4 p = i.objPos_upMask.xzyy * float4(0.0625, 0.0625, 0.13, 0.17);
                float2 wetUV = p.zw + p.xy;

                float4 wet = tex2D(_RoadUpwardWetXYS, wetUV);
                float2 wetXY = wet.xy * 2.0 - 1.0;

                // seed = dot(wetXY, (314.15921, 439.982208))
                float seed = dot(wetXY, float2(314.15921, 439.982208));

                // x = clamp(wet.z + sin(_SimulationTime.z*2 + seed) * seed*_WeatherParams.y*0.02, 0, 1)
                float wave = sin(_SimulationTime.z * 2.0 + seed);
                float amp  = seed * _WeatherParams.y * 0.02;
                float wetFactor = saturate(wet.z + amp * wave);

                // center and apply upward mask:
                // wetFactor = upMask * (wetFactor - 0.5) + 0.5
                wetFactor = i.objPos_upMask.w * (wetFactor - 0.5) + 0.5;

                // offset = wetXY * wetFactor
                float2 offset = wetXY * wetFactor;

                // finalXY = baseXY + offset * _WeatherParams.w
                float2 finalXY = baseXY + offset * _WeatherParams.w;

                // reconstruct Z
                float zz = 1.0 - min(dot(finalXY, finalXY), 1.0);
                float3 nTS = float3(finalXY, sqrt(max(zz, 0.0)));

                // ---- TS -> world using TBN ----
                float3 nWorld =
                    i.tbn0 * nTS.x +
                    i.tbn1 * nTS.y +
                    i.tbn2 * nTS.z;

                nWorld = normalize(nWorld);

                // Encode world normal to 0..1
                float3 enc = nWorld * 0.5 + 0.5;

                return float4(enc, a);
            }
            ENDCG
        }

        /*
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
                // uint   iid    : SV_InstanceID;
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

                //#ifdef UNITY_INSTANCING_ENABLED
                //    o.color = _Properties[instanceID].color;
                //#else
                //    o.color = float4(1, 1, 1, 1);
                //#endif
                o.color = _Properties[instanceID].color;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _ACIMap;

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 col2 = tex2D(_ACIMap, i.uv);
                if (col2.g < 0.5)
                {
                    col *= i.color * (1 - col2.g);
                }
                // compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
                fixed shadow = SHADOW_ATTENUATION(i);
                // darken light's illumination with shadow, keep ambient intact
                fixed3 lighting = i.diff * shadow + i.ambient;
                col.rgb *= lighting;
                fixed4 col_w_alpha = fixed4(col.r, col.g, col.b, col2.r);
                clip(col_w_alpha.w > 0.5f ? -1 : 1);
                return col_w_alpha;
            }
            ENDCG
        } */

        Pass
        {
            Name "PREPASS"
            Tags { "LightMode"="PrePassFinal" "Queue"="AlphaTest" "RenderType"="Prop" }
            ZWrite Off
            ZTest Equal

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag

            // Keep these as decompiled
            #pragma multi_compile __ MULTI_INSTANCE
            #pragma multi_compile __ INFOMODE_OFF
            #pragma multi_compile __ SHADING

            #include "UnityCG.cginc"

            // ---------- Textures ----------
            sampler2D   _MainTex;
            sampler2D   _XYSMap;
            sampler2D   _ACIMap;
            sampler2D   _RoadUpwardWetXYS;
            samplerCUBE _EnvironmentCubemap;
            sampler2D   _LightBuffer;

            // ---------- Uniforms ----------
            float4 _MainTex_ST;
            float4 _AtlasRect;         // xy=min, zw=size (as used by the decompiled variant)
            float4 _SimulationTime;    // .z used
            float4 _WeatherParams;     // .y and .w used
            // float3 _WorldSpaceCameraPos;

            // SH coefficients (Unity built-in)
            // float4 unity_SHAr, unity_SHAg, unity_SHAb;
            // float4 unity_SHBr, unity_SHBg, unity_SHBb;
            // float4 unity_SHC;

            // ---------- Instancing buffer ----------
            struct MeshProperties
            {
                float4x4 pos;        // Object->World from Matrix4x4.TRS(position, rotation, Vector3.one)
                float4   color;      // player tint (rgb). alpha optionally used as illum scale
                float4   castShadow; // unused here
            };
            StructuredBuffer<MeshProperties> _Properties;

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
                float2 uv      : TEXCOORD0;
                uint   iid     : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0; // already _MainTex_ST transformed (then atlas-clamped in frag)

                // TBN basis in world space (packed like decompiled: 3 vec3 used in dot())
                float3 tbn0     : TEXCOORD1; // worldT
                float3 tbn1     : TEXCOORD2; // worldB
                float3 tbn2     : TEXCOORD3; // worldN

                float3 worldPos : TEXCOORD4;

                // object pos (for wet sampling) + upward mask in .w
                float4 objPos_upMask : TEXCOORD5;

                // screenPos for light buffer sampling
                float4 screenPos : TEXCOORD6;

                // SH ambient
                float3 shAmbient : TEXCOORD7;

                // player tint
                float4 tint : COLOR0;
            };

            static float ComputeUpwardMask(float ny_obj)
            {
                // matches the compiled: smoothstep-ish around normal.y
                float t = saturate((ny_obj - 0.1) * 1.666666);
                return t * t * (3.0 - 2.0 * t);
            }

            static float2 ClampUVToAtlas(float2 uv, float4 atlasRect)
            {
                // Same logic as the decompiled fragment:
                float2 du = ddx(uv);
                float2 dv = ddy(uv);
                float2 dd = max(abs(du), abs(dv)) * 2.0;

                float2 pad = min(atlasRect.zw * 0.25, dd);
                float2 uvMin = atlasRect.xy + pad;
                float2 uvMax = atlasRect.xy + atlasRect.zw - pad;
                return clamp(uv, uvMin, uvMax);
            }

            v2f vert(appdata v)
            {
                v2f o;

                MeshProperties mp = _Properties[v.iid];

                // Object->World (instance)
                float4 wpos4 = mul(mp.pos, v.vertex);
                o.worldPos = wpos4.xyz;
                o.pos = mul(UNITY_MATRIX_VP, wpos4);

                // UV transform
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                // Build world TBN from instance rotation
                float3x3 M = (float3x3)mp.pos; // scale=1 -> pure rotation

                float3 wN = normalize(mul(M, v.normal));
                float3 wT = normalize(mul(M, v.tangent.xyz));
                float  sign = v.tangent.w * unity_WorldTransformParams.w;
                float3 wB = normalize(cross(wN, wT) * sign);

                o.tbn0 = wT;
                o.tbn1 = wB;
                o.tbn2 = wN;

                // Wet sampling uses OBJECT space vertex pos in the decompile
                o.objPos_upMask.xyz = v.vertex.xyz;
                o.objPos_upMask.w   = ComputeUpwardMask(v.normal.y);

                // screen pos for _LightBuffer
                o.screenPos = ComputeScreenPos(o.pos);

                // SH ambient (same structure as the decompiled vertex)
                float4 n4 = float4(wN, 1.0);
                float3 shA = float3(dot(unity_SHAr, n4),
                                    dot(unity_SHAg, n4),
                                    dot(unity_SHAb, n4));

                float4 vB = wN.xyzz * wN.yzzx;
                float3 shB = float3(dot(unity_SHBr, vB),
                                    dot(unity_SHBg, vB),
                                    dot(unity_SHBb, vB));

                float shc = wN.x * wN.x - wN.y * wN.y;
                o.shAmbient = shA + shB + unity_SHC.xyz * shc;

                o.tint = mp.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Atlas-safe UV clamp first (matches the multi_instance fragment)
                // float2 uv = ClampUVToAtlas(i.uv, _AtlasRect);
                float2 uv = i.uv;

                // Cutout (same inverted sense: discard if ACI.r > 0.5)
                float4 aci = tex2D(_ACIMap, uv);
                clip(0.5 - aci.r);

                // View direction (camera -> point)
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Sample base textures at atlas-clamped UV
                float4 mainTex = tex2D(_MainTex, uv);
                float3 xys     = tex2D(_XYSMap,  uv).xyz;

                // Decode TS normal XY
                float2 baseXY = xys.xy * 2.0 - 1.0;
                float invZ    = 1.0 - xys.z;               // matches: (-xys.z)+1

                // Wet field sampling (same as decompile: p = objPos.xzyy * constants; uv = p.zw+p.xy)
                float4 p = i.objPos_upMask.xzyy * float4(0.0625, 0.0625, 0.13, 0.17);
                float2 wetUV = p.zw + p.xy;

                float4 wet = tex2D(_RoadUpwardWetXYS, wetUV);
                float2 wetXY = wet.xy * 2.0 - 1.0;

                float seed = dot(wetXY, float2(314.15921, 439.982208));
                float wave = sin(_SimulationTime.z * 2.0 + seed);

                float wetAnim = saturate(wet.z + (seed * _WeatherParams.y * 0.02) * wave);
                wetAnim = i.objPos_upMask.w * (wetAnim - 0.5) + 0.5;

                // Apply wet normal offset scaled by _WeatherParams.w
                float2 finalXY = baseXY + (wetXY * wetAnim) * _WeatherParams.w;

                // Reconstruct Z
                float zz = 1.0 - min(dot(finalXY, finalXY), 1.0);
                float3 nTS = float3(finalXY, sqrt(max(zz, 0.0)));

                // TS -> World
                float3 N = normalize(i.tbn0 * nTS.x + i.tbn1 * nTS.y + i.tbn2 * nTS.z);

                // Reflect vector
                float3 R = reflect(-V, N);

                // ----- Cubemap LOD + reflection weight (mirrors decompiled math) -----
                // t = wetAnim * 0.2 + 0.4
                float t = wetAnim * 0.2 + 0.4;

                // a = (1 - _WeatherParams.w * t)^2
                float a = 1.0 - _WeatherParams.w * t;
                float a2 = a * a;

                // lod = min(1 - invZ? , a2) * 8 in the decompile.
                // decompile uses: min( (1-invZ) = xys.z? actually it uses min(a2, xys.z) * 8
                float lod = min(a2, xys.z) * 8.0;

                float3 env = texCUBElod(_EnvironmentCubemap, float4(R, lod)).rgb;

                // reflection gain: max(invZ, t*_WeatherParams.w) * 0.2
                float reflBase = t * _WeatherParams.w;
                float reflGain = max(invZ, reflBase) * 0.2;
                float3 refl = env * reflGain;

                // lightbuffer alpha factor: max(0.5*xys.z? no: max(0.5*invZ, 0.5*(reflBase*wetAnim))
                float lightAlphaFactor = max(0.5 * invZ, 0.5 * (reflBase * wetAnim));

                // ----- Albedo with player tint + ACI.g mask -----
                // Decompiled:
                // u_xlat8.xyz = mainTex.xyz * userColor.xyz;
		        // u_xlat0.xyz = (-mainTex.xyz) * userColor.xyz + mainTex.xyz;
		        // u_xlat0.xyz = _ACIMap.yyy * u_xlat0.xyz + u_xlat8.xyz;
                float3 baseCol = mainTex.rgb;
                float3 tinted  = baseCol * i.tint.rgb * i.tint.rgb;

                float3 albedo  = lerp(tinted, baseCol, aci.g);
                // float3 albedo = baseCol * (colorMask * (1 - i.tint.rgb) + i.tint.rgb);

                // Reflection is multiplied by (albedo + 0.25) in decompile
                float3 reflColored = refl * (albedo + 0.25);

                // basePlusRefl = albedo * a2 + reflColored   (a2 corresponds to u_xlat3.xxx in decompile)
                float3 basePlusRefl = albedo * a2 + reflColored;

                // ----- Light buffer -----
                float2 lightUV = i.screenPos.xy / i.screenPos.w;
                float4 lb = tex2D(_LightBuffer, lightUV);

                // Safety: avoid log2(0) -> NaNs on some drivers
                lb = max(lb, 1e-6);

                // float4 L = log2(lb);
                float4 L = lb;

                // In decompile:
                // lit = basePlusRefl + (-L.a) * lightAlphaFactor
                float3 lit = basePlusRefl + (L.a) * lightAlphaFactor;

                // ambientTerm = (-L.rgb) + SH
                float3 ambientTerm = ((L.rgb) + i.shAmbient);

                // ----- Emissive (ACI.b) -----
                // Decompile: emissive = (aci.b * vs_COLOR0.w * ObjectColorMap.w) * 10
                // Removed ObjectColorMap. Use player alpha as intensity scale (or set it = 1).
                float illumScale = i.tint.a;            // if unused, just set mp.color.a = 1
                float emiss = aci.b * illumScale * 10.0;

                // Decompile¡¯s final structure:
                // out = ambientTerm * lit + (albedo * emiss + reflColored)
                float3 outCol = ambientTerm * lit + (albedo * emiss + reflColored);

                return float4(outCol, 1.0);
            }
            ENDCG
        }
    }
}