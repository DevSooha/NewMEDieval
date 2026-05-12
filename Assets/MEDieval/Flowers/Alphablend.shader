Shader "Custom/Hyeonji_AlphaBlend_Distortion"
{
    Properties
    {
        [HDR] _MainColor ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture (Alpha)", 2D) = "white" {}
        _NoiseTex ("Noise Texture for Distortion", 2D) = "gray" {}
        
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.1
        
        _MainSpeedX ("Main Speed X", Float) = 0
        _MainSpeedY ("Main Speed Y", Float) = 0
        
        _NoiseSpeedX ("Noise Speed X", Float) = 0.1
        _NoiseSpeedY ("Noise Speed Y", Float) = 0.1
    }

    SubShader
    {
        // 핵심: AlphaBlend 설정
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // 파티클 시스템의 컬러값을 받기 위함
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv_noise : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;

            fixed4 _MainColor;
            float _DistortionStrength;
            float _MainSpeedX, _MainSpeedY;
            float _NoiseSpeedX, _NoiseSpeedY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 메인 텍스처 UV 및 Panner
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv += float2(_MainSpeedX, _MainSpeedY) * _Time.y;
                
                // 노이즈 텍스처 UV 및 Panner
                o.uv_noise = TRANSFORM_TEX(v.uv, _NoiseTex);
                o.uv_noise += float2(_NoiseSpeedX, _NoiseSpeedY) * _Time.y;
                
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 노이즈 텍스처를 읽어서 왜곡값 계산
                float4 noise = tex2D(_NoiseTex, i.uv_noise);
                float2 distortion = (noise.rg * 2.0 - 1.0) * _DistortionStrength;

                // 2. 왜곡이 적용된 UV로 메인 텍스처 샘플링
                fixed4 col = tex2D(_MainTex, i.uv + distortion);
                
                // 3. HDR 컬러와 파티클 시스템 컬러(Color over Lifetime 등) 곱하기
                col *= _MainColor * i.color;
                
                return col;
            }
            ENDCG
        }
    }
}