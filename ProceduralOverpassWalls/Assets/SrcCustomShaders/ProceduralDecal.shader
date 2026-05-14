Shader "Custom/ProceduralObject/Prop/TestDecalIndShader"
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

        _DecalSize   ("Decal Size", Vector) = (8,4,8,0)
        _DecalTiling ("Decal Tiling", Vector) = (1,0,1,0)
        _FadeDistanceFactor ("Fade Distance Factor", Float) = 0.000001
    }

    SubShader
    {
        Tags { "FORCENOSHADOWCASTING"="true" "QUEUE"="AlphaTest+10" "RenderType"="Decal" }

        Pass
        {
            Name "PREPASS"
            Tags
            {
                "LIGHTMODE" = "PREPASSFINAL"
                "FORCENOSHADOWCASTING" = "true"
                "QUEUE" = "AlphaTest+10"
                "RenderType" = "Decal"
            }

            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            Stencil
            {
                Ref 1
                ReadMask 1
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile __ MULTI_INSTANCE
            #pragma multi_compile __ INFOMODE_OFF
            #pragma multi_compile __ SHADING

            #include "UnityCG.cginc"

            struct MeshProperties
            {
                float4x4 pos;       // Object -> World
                float4   color;     // RGBA tint
                float4   castShadow;
            };
            StructuredBuffer<MeshProperties> _Properties;

            sampler2D _CameraDepthTexture;
            sampler2D _MainTex;
            sampler2D _XYSMap;
            sampler2D _ACIMap;
            sampler2D _RoadUpwardWetXYS;
            samplerCUBE _EnvironmentCubemap;
            sampler2D _LightBuffer;

            // The following variables are provided by UnityCG
            // float3 _WorldSpaceCameraPos;
            // float4 _ProjectionParams;
            // float4 _ZBufferParams;
            // float4 unity_SHAr;
            // float4 unity_SHAg;
            // float4 unity_SHAb;
            // float4 unity_SHBr;
            // float4 unity_SHBg;
            // float4 unity_SHBb;
            // float4 unity_SHC;

            float4 _SimulationTime;
            float4 _WeatherParams;
            float4 _DecalSize;
            float4 _DecalTiling;
            float  _FadeDistanceFactor;

            float3 _ViewportRight;
            float3 _ViewportUp;
            float3 _ViewportForward;

          

            struct appdata
            {
                float4 vertex : POSITION;
                uint   iid    : SV_InstanceID;
            };

            struct v2f
            {
                float4 posCS     : SV_POSITION;
                float4 screenPos : TEXCOORD0;

                float4 tint      : COLOR0;

                float3 decalWS   : TEXCOORD1;

                float3 w2l0      : TEXCOORD2;
                float3 w2l1      : TEXCOORD3;
                float3 w2l2      : TEXCOORD4;

                float3 reflBase  : TEXCOORD5;
                float3 shAmbient : TEXCOORD6;
                uint   iid    : SV_InstanceID; // works for DrawMeshInstanced(Indirect)
            };

            float3 EvalSH(float3 n)
            {
                float4 n4 = float4(n, 1.0);

                float3 shA;
                shA.x = dot(unity_SHAr, n4);
                shA.y = dot(unity_SHAg, n4);
                shA.z = dot(unity_SHAb, n4);

                float4 nB = n.yzzx * n.xyzz;

                float3 shB;
                shB.x = dot(unity_SHBr, nB);
                shB.y = dot(unity_SHBg, nB);
                shB.z = dot(unity_SHBb, nB);

                float shC = n.x * n.x - n.y * n.y;

                return shA + shB + unity_SHC.xyz * shC;
            }

            v2f vert(appdata v)
            {
                v2f o;

                MeshProperties mp = _Properties[v.iid];

                float4 worldPos = mul(mp.pos, v.vertex);
                o.posCS = mul(UNITY_MATRIX_VP, worldPos);
                o.screenPos = ComputeScreenPos(o.posCS);

                o.tint = mp.color;
                o.iid = v.iid;
                float3x3 localToWorld = (float3x3)mp.pos;
                float3x3 worldToLocal = transpose(localToWorld); // valid because scale is 1

                o.decalWS = float3(mp.pos._m03, mp.pos._m13, mp.pos._m23);

                o.w2l0 = worldToLocal[0];
                o.w2l1 = worldToLocal[1];
                o.w2l2 = worldToLocal[2];

                // Original shader used normalized world-space decal up axis,
                // derived indirectly from unity_WorldToObject[*].y.
                // For TRS with unit scale, this is just local Y transformed to world.
                float3 decalUpWS = normalize(float3(mp.pos._m01, mp.pos._m11, mp.pos._m21));

                // Match original vs_TEXCOORD0:
                // cameraToVertex reflected around decalUpWS
                float3 toCamera = _WorldSpaceCameraPos - worldPos.xyz;
                o.reflBase = reflect(-toCamera, decalUpWS);

                o.shAmbient = EvalSH(decalUpWS);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Screen UV
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Early return for testing
                // return float4(1, 0, 0, 1);

                // Reconstruct view ray
                float2 ndc = screenUV * 2.0 - 1.0;
                float3 rayDir = _ViewportForward + _ViewportRight * ndc.x + _ViewportUp * ndc.y;

                // Sample scene depth and reconstruct world position
                float rawDepth = tex2D(_CameraDepthTexture, screenUV).r;

                float linear01 = 1.0 / (_ZBufferParams.x * rawDepth + _ZBufferParams.y);
                float depthWS = linear01 * _ProjectionParams.z;

                float3 worldPos = _WorldSpaceCameraPos + rayDir * depthWS;

                // Distance fade
                float fade = saturate(1.0 - depthWS * depthWS * _FadeDistanceFactor);

                // World -> decal local
                float3 d = worldPos - i.decalWS;

                float3 local;
                local.x = dot(i.w2l0, d);
                local.y = dot(i.w2l1, d);
                local.z = dot(i.w2l2, d);

                // Wet lookup uses world xz
                float2 wetUV = worldPos.xz * 0.0625;
                float4 wetTex = tex2D(_RoadUpwardWetXYS, wetUV);

                // Box bounds
                float3 halfSize = _DecalSize.xyz * 0.5;
                float3 remain = halfSize - abs(local);

                // Decal UV
                float2 uv = local.xz / _DecalSize.xz + 0.5;
                uv *= _DecalTiling.xz;

                // Height falloff
                float h = saturate(remain.y / (_DecalSize.y * 0.25));
                float hSmooth = h * h * (3.0 - 2.0 * h);

                float4 aci = tex2D(_ACIMap, uv);

                float alphaMask = (1.0 - aci.r) * i.tint.a;
                float alpha = hSmooth * alphaMask * fade;

                float clipTest = min(alpha - 0.01, min(remain.x, remain.z));
                clip(clipTest);

                // Weather / wet perturbation
                float2 wetXY = wetTex.xy * 2.0 - 1.0;
                float wetSeed = dot(wetXY, float2(314.15921, 439.982208));

                float wetWave = sin(_SimulationTime.z * 2.0 + wetSeed);
                float wetAnim = saturate(wetTex.z + wetSeed * _WeatherParams.y * 0.02 * wetWave);

                float2 wetOffset = wetXY * wetAnim * _WeatherParams.w;

                float4 xys = tex2D(_XYSMap, uv);
                float4 mainTex = tex2D(_MainTex, uv);

                float2 baseXY = xys.xy * 2.0 - 1.0;
                float invSpec = 1.0 - xys.z;

                float2 finalXY = baseXY * alpha + wetOffset;

                float nz = sqrt(saturate(1.0 - dot(finalXY, finalXY)));
                float3 reflDir = normalize(float3(i.reflBase.x + finalXY.x, nz, i.reflBase.z + finalXY.y));

                // Reflection terms
                float t = wetAnim * 0.2 + 0.4;
                float envWeight = t * _WeatherParams.w;
                float fadeTerm = 1.0 - _WeatherParams.w * t;
                float fadeSq = fadeTerm * fadeTerm;

                float envMask = 1.0 - invSpec * alpha;
                float lightMask = invSpec * alpha;

                float outAlpha = alpha;
                float reflGain = max(envWeight, lightMask) * 0.2;
                float reflMip  = min(fadeSq, envMask) * 8.0;

                float3 env = texCUBElod(_EnvironmentCubemap, float4(reflDir, reflMip)).rgb;
                float3 refl = env * reflGain;

                // Tinting
                float3 baseCol = mainTex.rgb;
                float3 tinted  = baseCol * (i.tint.rgb * i.tint.rgb);
                float3 albedo  = lerp(tinted, baseCol, aci.g);

                float3 reflColored = refl * (albedo + 0.25);

                // Two branches from original logic
                float3 emissiveBranch = albedo * aci.b + reflColored;
                float3 litBranch = albedo * fadeSq + reflColored;

                // Light buffer + SH, screenUV is the same as lightUV in prop shader.
                float3 lb = tex2D(_LightBuffer, screenUV).rgb;
                lb = max(lb, 1e-6);

                // Candidates:
                // float3 lightTerm =  -log2(lb);
                // float3 lightTerm =   log2(lb);
                float3 lightTerm = lb;
                float3 ambientTerm = lightTerm + i.shAmbient;

                float3 finalRGB = ambientTerm * litBranch + emissiveBranch;

                return float4(finalRGB, outAlpha);
            }
            ENDCG
        }
    }
}
