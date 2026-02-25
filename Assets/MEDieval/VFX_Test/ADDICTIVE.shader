Shader "Custom/FX/Additive_Dissolve_Distort_Builtin_Fixed"
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
        _GrabMix ("Grab Mix", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend One One
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;

            float4 _MainTex_ST;
            float4 _Tint;

            float _DissolveThreshold;
            float _DissolveEdgeWidth;
            float4 _EdgeColor;
            float _DistortStrength;
            float _DistortSpeed;
            float _NoiseScale;
            float _AlphaCut;
            float _GrabMix;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvNoise : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvNoise = v.uv * _NoiseScale;
                o.color = v.color * _Tint;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                // noise (animate)
                float2 noiseUV = i.uvNoise + float2(_DistortSpeed * _Time.y, 0);
                float noise = tex2D(_NoiseTex, noiseUV).r;

                float edge = smoothstep(_DissolveThreshold - _DissolveEdgeWidth,
                                        _DissolveThreshold + _DissolveEdgeWidth, noise);

                float keep = step(_DissolveThreshold, noise);
                float alpha = edge * baseCol.a;

                fixed4 edgeCol = _EdgeColor * (1.0 - edge);

                fixed4 outCol;
                outCol.rgb = baseCol.rgb * alpha
                           + edgeCol.rgb * (1.0 - keep);
                outCol.a = alpha;

                clip(outCol.a - _AlphaCut);
                return outCol;
            }
            ENDCG
        }
    }
    FallBack Off
}
