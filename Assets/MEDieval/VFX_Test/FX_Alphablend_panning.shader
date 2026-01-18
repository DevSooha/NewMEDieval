Shader "Custom/EnergyBall_Dissolve_Distortion"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Tint Color (HDR)", Color) = (1,1,1,1)

        _MaskTex("Mask Texture", 2D) = "white" {}

        _DissolveTex("Dissolve Noise", 2D) = "gray" {}
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0.0
        _DissolveEdge("Dissolve Edge Thickness", Range(0,1)) = 0.1
        _DissolveEdgeColor("Dissolve Edge Color (HDR)", Color) = (1,0.5,0,1)

        _DistortTex("Distortion Map (RG)", 2D) = "gray" {}
        _DistortStrength("Distortion Strength", Range(0,1)) = 0.1
        _DistortSpeed("Distortion Flow Speed", Vector) = (0.2, 0.3, 0, 0)
    }

    SubShader
    {
        Tags{
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _DissolveTex;
            sampler2D _DistortTex;
            sampler2D _MaskTex;

            float4 _MainTex_ST;
            float4 _MaskTex_ST;

            float4 _Color;
            float4 _DissolveEdgeColor;

            float _DissolveAmount;
            float _DissolveEdge;
            float _DistortStrength;
            float4 _DistortSpeed;

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
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Mask
                float mask = tex2D(_MaskTex, TRANSFORM_TEX(i.uv, _MaskTex)).r;

                // Distortion Chaotic Flow (RG only)
                float2 flowUV = i.uv + frac(_Time.y * _DistortSpeed.xy);
                float2 distort = tex2D(_DistortTex, flowUV).rg * 2.0 - 1.0;
                float2 distortedUV = i.uv + distort * _DistortStrength * mask;

                // Main Texture
                float4 col = tex2D(_MainTex, distortedUV);
                col *= i.color * _Color;

                // Dissolve Calculation
                float noise = tex2D(_DissolveTex, i.uv).r;
                float edge = smoothstep(_DissolveAmount, _DissolveAmount + _DissolveEdge, noise);

                // Clip dissolved area
                if (noise < _DissolveAmount) discard;

                // Edge Glow
                col.rgb = lerp(_DissolveEdgeColor.rgb, col.rgb, edge);

                // Apply Mask Transparency
                col.a *= mask;

                return col;
            }
            ENDCG
        }
    }
}
