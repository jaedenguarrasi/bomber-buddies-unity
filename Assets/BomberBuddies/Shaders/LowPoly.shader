// The original game's look: flat-shaded geometry with one colour per face (vertex colour), lit by the sun, sky from above and
// ground bounce from below, linear fog, a slight S-curve and desaturation. Alpha < 1 on a vertex marks it emissive (unlit).
Shader "BomberBuddies/LowPoly"
{
    Properties
    {
        _AmbU ("Ambient from above (gamma)", Vector) = (0.56, 0.60, 0.68, 1)
        _AmbD ("Ambient from below (gamma)", Vector) = (0.40, 0.38, 0.34, 1)
        _SunColor ("Sun colour (gamma)", Vector) = (0.56, 0.53, 0.47, 1)
        _Light ("Sky light", Vector) = (1, 1, 1, 1)
        _Spec ("Specular", Float) = 0.05
        _Shine ("Shininess", Float) = 12
        _Contrast ("S-curve", Float) = 0.18
        _Desat ("Desaturation", Float) = 0.10
        _Tint ("Tint", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _AmbU, _AmbD, _Tint, _SunColor, _Light;
            float _Spec, _Shine, _Contrast, _Desat;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color * _Tint;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 alb = i.color.rgb;
                float emissive = i.color.a < 0.999 ? 1.0 : 0.0;
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                float nd = saturate(dot(n, sun.direction));
                float sh = sun.shadowAttenuation;
                // all of this is in the original's gamma space; converted to linear at the end
                float3 amb = lerp(_AmbD.rgb, _AmbU.rgb, n.y * 0.5 + 0.5) * _Light.rgb;
                float3 sunC = _SunColor.rgb * _Light.rgb;
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 h = normalize(sun.direction + v);
                float spec = pow(saturate(dot(n, h)), _Shine) * _Spec;
                float3 col = alb * (amb + sunC * nd * sh) + sunC * spec * sh * (0.3 + 0.7 * nd);
                col = lerp(col, alb * _Light.rgb, emissive);
                // the original's slight S-curve and 10 % desaturation
                float3 sc = col * col * (3.0 - 2.0 * col);
                col = lerp(col, sc, _Contrast);
                float luma = dot(col, float3(0.3, 0.59, 0.11));
                col = lerp(col, luma.xxx, _Desat);
                col = SRGBToLinear(saturate(col));
                col = MixFog(col, i.fogFactor * (1.0 - 0.7 * emissive));
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
