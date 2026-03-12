Shader "Custom/SimpleDistortionAddictive"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture (RGB)", 2D) = "white" {}
        _NormalMap ("Normal Map for Distortion", 2D) = "bump" {} // 타일링 조절 가능
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.1
        _SpeedX ("Scroll Speed X", Float) = 0.5
        _SpeedY ("Scroll Speed Y", Float) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
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
                float2 uv_dist : TEXCOORD1; // 노말맵용 UV 추가
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            float4 _MainTex_ST;
            float4 _NormalMap_ST; // 노말맵 타일링/오프셋 변수
            fixed4 _TintColor;
            float _DistortionStrength;
            float _SpeedX;
            float _SpeedY;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 메인 UV와 노말맵 UV를 각각 계산
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv_dist = TRANSFORM_TEX(v.uv, _NormalMap);
                
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 노말맵 UV에 시간 기반 애니메이션(Panning) 적용
                float2 pannedUV = i.uv_dist + float2(_Time.y * _SpeedX, _Time.y * _SpeedY);
                
                // 노말맵 샘플링 및 언팩 (0~1 범위를 -1~1 범위로)
                // UnpackNormal을 사용하면 모바일/PC 등 플랫폼 호환성이 좋아집니다.
                float3 normal = UnpackNormal(tex2D(_NormalMap, pannedUV));
                float2 distortion = normal.xy * _DistortionStrength;

                // 왜곡이 적용된 메인 텍스처 샘플링
                fixed4 col = tex2D(_MainTex, i.uv + distortion);
                
                return col * i.color;
            }
            ENDCG
        }
    }
}