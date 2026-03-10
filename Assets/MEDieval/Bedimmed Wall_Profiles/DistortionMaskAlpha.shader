Shader "Custom/SimpleDistortionMaskAlpha"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture (RGB)", 2D) = "white" {}
        _MaskTex ("Mask Texture (Alpha/Grayscale)", 2D) = "white" {}
        _NormalMap ("Normal Map for Distortion", 2D) = "bump" {}
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.1
        _SpeedX ("Scroll Speed X", Float) = 0.5
        _SpeedY ("Scroll Speed Y", Float) = 0.5
    }

    SubShader
    {
        // 알파 블렌드를 위해 Queue를 Transparent로 설정
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        // Alpha Blend 설정 (전통적인 투명도 방식)
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off 
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float2 uv_mask : TEXCOORD1;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _NormalMap;
            float4 _MainTex_ST;
            float4 _MaskTex_ST;
            fixed4 _TintColor;
            float _DistortionStrength;
            float _SpeedX;
            float _SpeedY;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv_mask = TRANSFORM_TEX(v.uv, _MaskTex);
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. 왜곡 계산
                float2 pannedUV = i.uv + float2(_Time.y * _SpeedX, _Time.y * _SpeedY);
                float4 normalSample = tex2D(_NormalMap, pannedUV);
                float2 distortion = (normalSample.rg * 2.0 - 1.0) * _DistortionStrength;

                // 2. 텍스처 샘플링
                fixed4 col = tex2D(_MainTex, i.uv + distortion);
                fixed4 mask = tex2D(_MaskTex, i.uv_mask);
                
                // 3. 최종 컬러와 알파 계산
                fixed4 finalColor;
                finalColor.rgb = col.rgb * i.color.rgb;
                // 메인 텍스처 알파 * 마스크 R채널 * 파티클 시스템 알파
                finalColor.a = col.a * mask.r * i.color.a; 
                
                return finalColor;
            }
            ENDCG
        }
    }
}