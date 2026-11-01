// The original's sky: horizon to zenith gradient (pow 0.55), a darker band below the horizon, a sun disc, and stars that fade
// in as the sky goes to space. Used as the skybox material.
Shader "BomberBuddies/Sky"
{
    Properties
    {
        _Zen ("Zenith", Color) = (0.36, 0.62, 0.78, 1)
        _Hor ("Horizon", Color) = (0.87, 0.91, 0.87, 1)
        _Low ("Below horizon", Color) = (0.80, 0.87, 0.86, 1)
        _SunDir ("Sun direction", Vector) = (0.42, 0.78, 0.46, 0)
        _SunDisc ("Sun disc", Color) = (1, 0.96, 0.86, 1)
        _Stars ("Stars", Float) = 0
        _CloudCover ("Cloud cover", Float) = 1
        _CloudColor ("Cloud colour", Color) = (0.98, 0.98, 1, 1)
        _CloudLo ("Cloud lo", Float) = 0.5
        _CloudHi ("Cloud hi", Float) = 0.66
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Zen, _Hor, _Low, _SunDir, _SunDisc, _CloudColor;
            float _Stars, _CloudCover, _CloudLo, _CloudHi;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.dir = v.positionOS.xyz; return o; }
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vn2(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash2(i), hash2(i + float2(1, 0)), f.x), lerp(hash2(i + float2(0, 1)), hash2(i + float2(1, 1)), f.x), f.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 w = normalize(i.dir);
                float s = w.y;
                float3 col = s > 0 ? lerp(_Hor.rgb, _Zen.rgb, pow(s, 0.55)) : lerp(_Hor.rgb, _Low.rgb, saturate(-s * 7.0));
                float cov = 0;
                if (_CloudCover > 0 && s > 0.012)
                {
                    float2 cp = (w.xz / max(s, 0.07)) * 260.0 + _Time.y * float2(9, 3);
                    float n = 0.55 * vn2(cp * 0.0025) + 0.3 * vn2(cp * 0.006 + 3.7) + 0.15 * vn2(cp * 0.016 + 9.1);
                    cov = smoothstep(_CloudLo, _CloudHi, n) * smoothstep(0.012, 0.13, s) * _CloudCover;
                    float3 cc = _CloudColor.rgb * (0.78 + 0.3 * smoothstep(_CloudLo, _CloudHi + 0.22, n));
                    col = lerp(col, cc, cov);
                }
                float3 sun = normalize(_SunDir.xyz);
                float sd = saturate(dot(w, sun));
                col += _SunDisc.rgb * _SunDisc.a * (0.16 * pow(sd, 6.0) + 0.45 * pow(sd, 48.0) + 2.2 * smoothstep(0.9988, 0.9995, sd) * (1.0 - 0.9 * cov));
                if (_Stars > 0 && s > 0.05)
                {
                    float2 sp = floor(w.xz / max(s, 0.05) * 90.0) + floor(w.y * 300.0);
                    float h = hash2(sp);
                    float st = h > 0.9975 ? _Stars * (0.5 + 0.5 * hash2(sp + 1.3)) : 0.0;
                    col += st * (1.0 - cov);
                }
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
