// FIRED/HoloFresnel — the X-ray "holo-flesh" look from the GDD: a transparent
// additive rim glow. Applied to a body when the bullet passes through it, so the
// skeleton inside shows while the flesh becomes a neon ghost shell.
// Works on skinned meshes; URP-compatible.
Shader "FIRED/HoloFresnel"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0, 0.94, 1, 0.4)
        _RimPower ("Rim Power", Float) = 2.4
        _Fill ("Fill Brightness", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One      // additive: reads as hologram over anything
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewWS     : TEXCOORD1;
            };

            float4 _Color;
            float _RimPower;
            float _Fill;

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float ndv = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float rim = pow(1.0 - ndv, _RimPower);
                float3 col = _Color.rgb * (_Fill + rim * 1.8);
                return half4(col, _Color.a * saturate(_Fill + rim));
            }
            ENDHLSL
        }
    }
}
