Shader "Custom/FX_EnergyBall_Additive_HDR"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        [HDR]_Color("Tint Color (HDR)", Color) = (1,1,1,1)

        _MaskTex("Mask (Alpha) - optional", 2D) = "white" {}
        _DissolveTex("Dissolve Noise (Grayscale)", 2D) = "gray" {}
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0.0
        _DissolveEdge("Dissolve Edge Thickness", Range(0,1)) = 0.12
        [HDR]_EdgeColor("Edge Color (HDR)", Color) = (2,1,0.5,1)

        _DistortTex("Distortion Map (RG)", 2D) = "bump" {}
        _DistortStrength("Distortion Strength", Range(0,1)) = 0.06
        _DistortSpeed("Distort Pan Speed XY", Vector) = (0.35, -0.2, 0, 0)

        _NoiseScale("Noise UV Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        // Additive blend
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _DissolveTex;
            sampler2D _DistortTex;

            float4 _MainTex_ST;
            float4 _Color;
            float4 _EdgeColor;
            float _DissolveAmount;
            float _DissolveEdge;
            float _DistortStrength;
            float4 _DistortSpeed;
            float _NoiseScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // particle start color
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // helper to sample texture with correct UV scale/offset
            inline float4 SampleTex(sampler2D tex, float2 uv, float scale)
            {
                return tex2D(tex, uv * scale);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Panning for chaotic flow
                float2 pan = i.uv + _Time.y * _DistortSpeed.xy;

                // Sample distortion map (R,G) -> remap 0..1 to -1..+1
                float2 dRG = tex2D(_DistortTex, pan).rg * 2.0 - 1.0;
                // Optional extra fractal by sampling at scaled UV (noise scale)
                float2 dRG2 = tex2D(_DistortTex, pan * 1.7).rg * 2.0 - 1.0;
                // Combine small multi-octave for more chaotic motion
                float2 distort = lerp(dRG, dRG2, 0.5);

                // Final UV after distortion
                float2 uvDist = i.uv + distort * _DistortStrength;

                // Main texture sample (apply noise scale if wanted)
                float4 baseCol = tex2D(_MainTex, uvDist);

                // Mask (optional) - uses alpha of mask texture to keep spherical shape
                float mask = 1.0;
                #if defined(_MaskTex)
                mask = tex2D(_MaskTex, i.uv).a;
                #endif
                baseCol *= mask;

                // Multiply by particle vertex color and HDR tint
                baseCol.rgb *= (i.color.rgb * _Color.rgb);
                baseCol.a *= i.color.a * _Color.a;

                // Dissolve noise (use scaled noise to get different frequencies)
                float2 noiseUV = i.uv * _NoiseScale + _Time.y * 0.15;
                float n = tex2D(_DissolveTex, noiseUV).r;

                // discard area below threshold
                if (n < _DissolveAmount)
                    discard;

                // dissolve edge smoothing
                float edgeAlpha = smoothstep(_DissolveAmount, _DissolveAmount + _DissolveEdge, n);

                // edge emission mix (HDR)
                float3 finalRGB = lerp(_EdgeColor.rgb, baseCol.rgb, edgeAlpha);

                // output (additive) - keep alpha for sorting if needed
                fixed4 outCol = fixed4(finalRGB, baseCol.a * edgeAlpha);

                return outCol;
            }
            ENDCG
        }
    }
    FallBack Off
}
