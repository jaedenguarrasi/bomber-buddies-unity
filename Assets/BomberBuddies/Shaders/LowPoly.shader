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
        _Mat ("Surface (-1: from the mesh)", Float) = -1
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
            float _Spec, _Shine, _Contrast, _Desat, _Mat;
            CBUFFER_END
            // the night: a global dimmer, and the player's torch (a cone from _TorchPos along _TorchDir, w = range)
            float4 _NightLight; float4 _TorchPos; float4 _TorchDir;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD3; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float fogFactor : TEXCOORD2;
                float mat : TEXCOORD3;
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
                o.mat = _Mat >= 0.0 ? _Mat : v.uv.x;
                return o;
            }

            // ---- the original's surfaces: every material is a little procedural pattern laid over the face colour, in the
            // original's own units (1 unit = 0.12 m), straight from its fragment shader ----
            float gmod(float x, float y) { return x - y * floor(x / y); }
            float h21(float2 p) { p = float2(gmod(p.x, 512.0), gmod(p.y, 512.0)); float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973)); q += dot(q, q.yzx + 33.33); return frac((q.x + q.y) * q.z); }
            float vn2(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f); return lerp(lerp(h21(i), h21(i + float2(1.0, 0.0)), f.x), lerp(h21(i + float2(0.0, 1.0)), h21(i + float2(1.0, 1.0)), f.x), f.y); }
            float3 Surface(float m, float3 base, float3 W, inout float3 N, float3 V, out float spK, out float shin)
            {
                float2 uv = abs(N.y) > 0.6 ? W.xz : (abs(N.x) > abs(N.z) ? W.zy : W.xy);
                float3 alb = base; spK = 0.05; shin = 12.0;
                if (m < 0.5) { }
                else if (m < 1.5) { alb *= 0.96 + 0.08 * vn2(uv * 2.0); }
                else if (m < 2.5) { float2 f = frac(uv * 0.25); float sm = 1.0 - smoothstep(0.0, 0.05, min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y))); float2 rp = float2(frac(uv.x + 0.5) - 0.5, min(f.y, 1.0 - f.y)); float riv = smoothstep(0.11, 0.05, length(rp * float2(1.0, 1.5))); alb *= (0.94 + 0.12 * vn2(uv * 1.3) + 0.05 * vn2(uv * 9.0)) * (1.0 - 0.22 * sm) * (1.0 + 0.22 * riv); spK = 0.4 + 0.3 * riv; shin = 28.0; }
                else if (m < 3.5) { float2 b = float2(uv.x / 2.4, uv.y); b.x += step(0.5, frac(b.y * 0.5)) * 0.5; float2 f = frac(b); float mort = max(1.0 - smoothstep(0.0, 0.09, f.x), 1.0 - smoothstep(0.0, 0.17, f.y)); float mc = dot(base, float3(0.333, 0.333, 0.333)) * 1.05 + 0.1; alb = lerp(base * (0.82 + 0.34 * h21(floor(b))) * (0.93 + 0.12 * vn2(uv * 5.0)), float3(mc, mc, mc), mort); }
                else if (m < 4.5) { alb *= 0.84 + 0.22 * vn2(uv * 0.7) + 0.1 * vn2(uv * 5.0) - 0.07 * vn2(float2(uv.x * 4.0, uv.y * 0.12)); }
                else if (m < 5.5) { float al = abs(N.x) > abs(N.z) ? W.x : W.z; float ac = abs(N.x) > abs(N.z) ? W.z : W.x; float row = floor(al / 1.3); float ff = frac(al / 1.3); float t = frac(ac / 2.6 + gmod(row, 2.0) * 0.5); alb *= (0.84 + 0.3 * h21(float2(floor(ac / 2.6 + gmod(row, 2.0) * 0.5), row))) * (1.0 - 0.38 * (1.0 - smoothstep(0.0, 0.14, ff))) * (1.0 - 0.18 * (1.0 - smoothstep(0.0, 0.05, t))) * (0.95 + 0.1 * vn2(uv * 4.0)); }
                else if (m < 6.5) { float pl = floor(uv.y / 1.4); alb *= (0.86 + 0.24 * h21(float2(pl, 0.0))) * (0.88 + 0.2 * vn2(float2(uv.x * 0.5, uv.y * 8.0))) * (1.0 - 0.3 * (1.0 - smoothstep(0.0, 0.07, frac(uv.y / 1.4)))); }
                else if (m < 7.5) { float g = saturate((base.g - max(base.r, base.b)) * 10.0); float n1 = vn2(uv * 0.45), n2 = vn2(uv * 2.6 + 7.0), n3 = vn2(uv * 10.0 + 3.0); alb *= 0.82 + 0.22 * n1 + (0.14 * n2 + 0.1 * n3) * (0.45 + 0.55 * g); }
                else if (m < 8.5) { float2 wuv = W.xz; float t = _Time.y; float2 o1 = float2(t * 0.05, t * 0.02), o2 = float2(t * 0.07, t * 0.04); float w0 = vn2(wuv * 0.045 + o1) + 0.5 * vn2(wuv * 0.16 - o2); float wx = vn2((wuv + float2(1.2, 0.0)) * 0.045 + o1) + 0.5 * vn2((wuv + float2(1.2, 0.0)) * 0.16 - o2); float wz = vn2((wuv + float2(0.0, 1.2)) * 0.045 + o1) + 0.5 * vn2((wuv + float2(0.0, 1.2)) * 0.16 - o2); N = normalize(float3((w0 - wx) * 1.6, 0.55, (w0 - wz) * 1.6)); float fr = pow(1.0 - max(0.0, dot(N, V)), 3.0); alb = lerp(base * (0.9 + 0.2 * w0), _AmbU.rgb * 1.25, fr * 0.55); spK = 1.1; shin = 90.0; }
                else if (m < 9.5) { alb *= 0.72 + 0.44 * vn2(uv * 1.6) + 0.14 * vn2(uv * 6.5); }
                else if (m < 10.5) { float fr = pow(1.0 - max(0.0, dot(N, V)), 2.0); alb = lerp(base * (0.92 + 0.1 * vn2(uv * 0.4)), _AmbU.rgb, fr * 0.5); spK = 0.7; shin = 48.0; }
                else if (m < 11.5) { alb *= 0.7 + 0.36 * vn2(uv * 0.33) + 0.16 * vn2(uv * 1.6) + 0.1 * vn2(uv * 7.0); }
                else if (m < 12.5) { alb *= (0.95 + 0.06 * vn2(uv * 6.0) + 0.04 * vn2(uv * 24.0)) * float3(1.03, 0.98, 0.95); spK = 0.05; shin = 12.0; }
                else if (m < 13.5) { float c1 = vn2(uv * 0.9), c2 = vn2(uv * 2.1 + 5.0); float b1 = smoothstep(0.46, 0.54, c1), b2 = smoothstep(0.52, 0.6, c2) * (1.0 - b1); alb *= lerp(1.0, 0.74, b1) * lerp(1.0, 1.16, b2) * (0.95 + 0.1 * vn2(uv * 36.0)); spK = 0.02; }
                else if (m < 14.5) { float2 g = float2(uv.x * 2.0, uv.y * 2.6); g.x += step(0.5, frac(g.y * 0.5)) * 0.5; float2 f = frac(g) - 0.5; float r = length(f * float2(1.0, 1.25)); float sc = smoothstep(0.52, 0.28, r); alb *= (0.78 + 0.3 * sc) * (0.88 + 0.24 * h21(floor(g))); spK = 0.2; shin = 22.0; }
                else if (m < 15.5) { alb *= 0.86 + 0.18 * vn2(uv * 3.0) + 0.07 * vn2(uv * 14.0); spK = 0.09; shin = 18.0; }
                else { alb *= 0.82 + 0.26 * vn2(float2(uv.x * 2.5, uv.y * 22.0)) + 0.08 * vn2(uv * 5.0); spK = 0.03; }
                return alb;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float spK, shin;
                float3 alb = Surface(floor(i.mat + 0.5), i.color.rgb, i.positionWS / 0.12, n, vdir, spK, shin);
                float emissive = i.color.a < 0.999 ? 1.0 : 0.0;
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                float nd = saturate(dot(n, sun.direction));
                float sh = sun.shadowAttenuation;
                // all of this is in the original's gamma space; converted to linear at the end
                float3 nl = lerp(float3(1, 1, 1), _NightLight.rgb, _NightLight.a);
                float3 amb = lerp(_AmbD.rgb, _AmbU.rgb, n.y * 0.5 + 0.5) * _Light.rgb * nl;
                float3 sunC = _SunColor.rgb * _Light.rgb * nl;
                float3 v = vdir;
                float3 h = normalize(sun.direction + v);
                float spec = pow(saturate(dot(n, h)), shin) * spK;
                float3 col = alb * (amb + sunC * nd * sh) + sunC * spec * sh * (0.3 + 0.7 * nd);
                col = lerp(col, i.color.rgb * _Light.rgb, emissive);
                if (_TorchDir.w > 0.0)
                {
                    float3 tv = i.positionWS - _TorchPos.xyz; float td = length(tv); tv /= max(td, 0.001);
                    float cone = smoothstep(0.80, 0.96, dot(tv, _TorchDir.xyz));
                    float fall = saturate(1.0 - td / _TorchDir.w); fall *= fall;
                    float tn = saturate(dot(n, -tv)) * 0.6 + 0.4;
                    col += alb * float3(1.0, 0.95, 0.8) * cone * fall * tn * 1.6;
                }
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
