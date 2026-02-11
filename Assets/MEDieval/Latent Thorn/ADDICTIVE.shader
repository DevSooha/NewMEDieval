Shader "Custom/SimpleAddictiveHDR"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        // Addictive 블렌딩 (검은색은 투명하게, 밝은색은 겹칠수록 더 밝게)
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
                fixed4 color : COLOR; // 파티클 시스템의 컬러 데이터를 받는 부분
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // 파티클의 컬러와 인스펙터의 HDR 컬러를 미리 곱함
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 텍스처 샘플링
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                // 최종 컬러 계산: 텍스처 RGB * 컬러 RGB * 알파
                // Addictive 모드에서는 알파가 곧 밝기(강도)가 됩니다.
                fixed4 col;
                col.rgb = tex.rgb * i.color.rgb * (tex.a * i.color.a);
                col.a = 1.0; // Addictive이므로 최종 알파는 의미 없음
                
                return col;
            }
            ENDCG
        }
    }
}