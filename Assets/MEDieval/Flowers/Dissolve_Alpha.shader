Shader "Custom/Hyeonji_AlphaBlend_Dissolve_Distortion"
{
    Properties
    {
        [HDR] _MainColor ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture (Alpha)", 2D) = "white" {}
        _NoiseTex ("Noise Texture (Distortion & Dissolve)", 2D) = "gray" {}
        
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.1
        
        // Dissolve 설정
        _DissolveThreshold ("Dissolve Threshold", Range(0, 1)) = 0
        _DissolveEdge ("Dissolve Edge Width", Range(0, 0.5)) = 0.05
        [HDR] _EdgeColor ("Edge Color", Color) = (1,1,1,1)

        _MainSpeedX ("Main Speed X", Float) = 0
        _MainSpeedY ("Main Speed Y", Float) = 0
        
        _NoiseSpeedX ("Noise Speed X", Float) = 0.1
        _NoiseSpeedY ("Noise Speed Y", Float) = 0.1
    }

    SubShader
    {
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
                float4 color : COLOR;
                float4 customData : TEXCOORD1; // Dynamic Parameter를 받기 위한 통로
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv_noise : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float dissolveParam : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;

            fixed4 _MainColor;
            fixed4 _EdgeColor;
            float _DistortionStrength;
            float _DissolveThreshold;
            float _DissolveEdge;
            float _MainSpeedX, _MainSpeedY;
            float _NoiseSpeedX, _NoiseSpeedY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv += float2(_MainSpeedX, _MainSpeedY) * _Time.y;
                
                // 노이즈 텍스처 타일링 적용 (TRANSFORM_TEX 사용)
                o.uv_noise = TRANSFORM_TEX(v.uv, _NoiseTex);
                o.uv_noise += float2(_NoiseSpeedX, _NoiseSpeedY) * _Time.y;
                
                o.color = v.color;
                
                // 파티클의 Custom Data.x 값을 디졸브 수치로 사용
                // 만약 파티클 미사용 시 인스펙터의 _DissolveThreshold 사용
                o.dissolveParam = v.customData.x; 
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 노이즈 샘플링 (Distortion & Dissolve 공용)
                float4 noise = tex2D(_NoiseTex, i.uv_noise);
                
                // 2. Distortion 적용
                float2 distortion = (noise.rg * 2.0 - 1.0) * _DistortionStrength;
                fixed4 col = tex2D(_MainTex, i.uv + distortion);
                
                // 3. Dissolve 로직
                // Dynamic Parameter(i.dissolveParam)가 0이면 인스펙터 수치 사용
                float threshold = i.dissolveParam > 0 ? i.dissolveParam : _DissolveThreshold;
                
                // step 함수로 마스크 생성
                float dissolveMask = step(threshold, noise.r);
                
                // 테두리(Edge) 효과: 빛나는 외곽선 만들기
                float edgeMask = step(threshold - _DissolveEdge, noise.r) - dissolveMask;
                fixed4 edge = edgeMask * _EdgeColor;

                // 4. 최종 컬러 합성
                col *= _MainColor * i.color;
                col.rgb += edge.rgb; // 테두리 발광 더하기
                col.a *= dissolveMask; // 디졸브 마스크로 알파 커팅
                
                return col;
            }
            ENDCG
        }
    }
}