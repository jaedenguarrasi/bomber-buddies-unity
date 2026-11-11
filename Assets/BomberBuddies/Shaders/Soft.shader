// The original's smoke, dust and flashes: soft eight-sided puffs that fade from a solid middle to clear points, drawn last,
// see-through and without writing depth, so they veil the scene instead of walling it off. uv.x = 1 marks a puff that glows.
Shader "BomberBuddies/Soft"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _NightLight;
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float glow : TEXCOORD0; float fogFactor : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color; o.glow = v.uv.x;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                // the original's daylight on a face half turned to the sky (its gamma space, as the world shader)
                float3 nl = lerp(float3(1, 1, 1), _NightLight.rgb, _NightLight.a);
                float3 lit = i.color.rgb * (float3(0.48, 0.49, 0.51) + float3(0.56, 0.53, 0.47) * 0.5) * nl;
                float3 col = lerp(lit, i.color.rgb, saturate(i.glow));
                col = SRGBToLinear(saturate(col));
                col = MixFog(col, i.fogFactor * (1.0 - 0.7 * saturate(i.glow)));
                return half4(col, i.color.a);
            }
            ENDHLSL
        }
    }
}
