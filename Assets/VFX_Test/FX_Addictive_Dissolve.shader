Shader "Custom/FX/Additive_Dissolve_Distort_Builtin"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Tint ("Tint Color", Color) = (1,1,1,1)
        _NoiseTex ("Noise Texture (for dissolve & distortion)", 2D) = "white" {}
        _DissolveThreshold ("Dissolve Threshold (0..1)", Range(0,1)) = 0.0
        _DissolveEdgeWidth ("Edge Width", Range(0.0,0.5)) = 0.08
        _EdgeColor ("Edge Color", Color) = (1,0.6,0,1)
        _DistortStrength ("Distortion Strength", Range(0,1)) = 0.08
        _DistortSpeed ("Distortion Speed", Range(0,5)) = 0.8
        _NoiseScale ("Noise Scale", Float) = 4.0
        _AlphaCut ("Alpha Cutoff (optional)", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend One One    // Additive blend
        Lighting Off

        // GrabPass for screen-space distortion. It's expensive; only needed if _DistortStrength > 0
        GrabPass { "_GrabTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            sampler2D _GrabTexture;
            float4 _MainTex_ST;
            float4 _NoiseTex_ST;

            float4 _Tint;
            float _DissolveThreshold;
            float _DissolveEdgeWidth;
            float4 _EdgeColor;
            float _DistortStrength;
            float _DistortSpeed;
            float _NoiseScale;
            float _AlphaCut;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // particle color can be used to modulate dissolve or tint
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvNoise : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvNoise = v.uv * _NoiseScale;
                o.screenPos = ComputeScreenPos(o.pos);
                o.color = v.color * _Tint;
                return o;
            }

            fixed4 SampleGrab(sampler2D grabTex, float4 screenPos, float2 offset)
            {
                // UNITY macro to get proper UV for grab texture
                float2 grabUV = screenPos.xy / screenPos.w;
                #if UNITY_UV_STARTS_AT_TOP
                    grabUV.y = 1 - grabUV.y;
                #endif
                grabUV += offset;
                return tex2D(grabTex, grabUV);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // base color
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                // noise sample (animate)
                float2 noiseUV = i.uvNoise + float2(_DistortSpeed * _Time.y, 0);
                float noise = tex2D(_NoiseTex, noiseUV).r;

                // Dissolve mask: compare noise to threshold
                float edge = smoothstep(_DissolveThreshold - _DissolveEdgeWidth, _DissolveThreshold + _DissolveEdgeWidth, noise);
                // edge is 0..1 where 0 = before threshold (transparent), 1 = kept
                // We'll use 'edge' to composite an emissive edge color
                float keep = step(_DissolveThreshold, noise);
                // Soft alpha for smooth transition
                float alpha = edge * baseCol.a;

                // Distortion offset applied to grab texture (screen-space refraction)
                float2 distortOffset = float2(0,0);
                if (_DistortStrength > 0.0001)
                {
                    // Use noise to create small offset; can also use normal map for better result
                    float2 n = (tex2D(_NoiseTex, i.uvNoise + _Time.y * _DistortSpeed).rg - 0.5) * 2.0;
                    distortOffset = n * _DistortStrength / 100.0; // small offset in UV space
                }

                // Sample background (grab) with offset and composite (additive)
                fixed4 bg = SampleGrab(_GrabTexture, i.screenPos, distortOffset);

                // Edge glow color (only around dissolve border)
                fixed4 edgeCol = _EdgeColor * (1.0 - edge);

                // Final color: additive of base * alpha + edge glow + subtle background refraction
                fixed4 outCol = fixed4(0,0,0,0);
                outCol.rgb = baseCol.rgb * alpha + edgeCol.rgb * (1.0 - keep) + bg.rgb * (_DistortStrength * 0.5);
                outCol.a = alpha; // alpha for ordering (though additive cares less)

                // optional alpha cutoff
                clip(outCol.a - _AlphaCut);

                return outCol;
            }
            ENDCG
        }
    }
    FallBack Off
}
