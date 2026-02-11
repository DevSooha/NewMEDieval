Shader "Custom/SimpleDistortionAddictive"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture (RGB)", 2D) = "white" {}
        _NormalMap ("Normal Map for Distortion", 2D) = "bump" {}
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.1
        _SpeedX ("Scroll Speed X", Float) = 0.5
        _SpeedY ("Scroll Speed Y", Float) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        // Addictive(가산) 블렌딩 설정
        Blend One One
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
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _DistortionStrength;
            float _SpeedX;
            float _SpeedY;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // UV Panning 계산
                float2 pannedUV = i.uv + float2(_Time.y * _SpeedX, _Time.y * _SpeedY);
                
                // Normal Map에서 왜곡 값 추출 (UnpackNormal 사용하지 않고 단순화)
                float4 normalSample = tex2D(_NormalMap, pannedUV);
                float2 distortion = (normalSample.rg * 2.0 - 1.0) * _DistortionStrength;

                // 왜곡이 적용된 메인 텍스처 샘플링
                fixed4 col = tex2D(_MainTex, i.uv + distortion);
                
                // 파티클 컬러(Alpha 포함)와 HDR 컬러 곱하기
                return col * i.color;
            }
            ENDCG
        }
    }
}