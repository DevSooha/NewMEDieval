Shader "Custom/DistortionDissolveFullVertex_Fixed"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture (RGB)", 2D) = "white" {}
        
        [Header(Mask and Dissolve)]
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskYOffset ("Manual Mask Y Offset", Float) = 0.0
        _NoiseTex ("Noise for Dissolve", 2D) = "white" {}
        _DissolveAmount ("Manual Dissolve Amount", Range(0, 1.1)) = 0.0
        _DissolveEdge ("Edge Width", Range(0, 0.2)) = 0.05
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 2, 3, 1)

        [Header(Distortion)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _DistortionStrength ("Manual Strength", Range(0, 1)) = 0.1
        _SpeedX ("Scroll Speed X", Float) = 0.5
        _SpeedY ("Scroll Speed Y", Float) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
                float4 uv2 : TEXCOORD1; // Particle Custom1.xyz
                fixed4 color : COLOR;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv_mask : TEXCOORD1;
                float2 dissolve_distort : TEXCOORD3; // x: dissolve, y: distortion
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _NoiseTex;
            sampler2D _NormalMap;
            float4 _MainTex_ST;
            float4 _MaskTex_ST;
            
            fixed4 _TintColor;
            fixed4 _EdgeColor;
            float _MaskYOffset;
            float _DissolveAmount;
            float _DissolveEdge;
            float _DistortionStrength;
            float _SpeedX;
            float _SpeedY;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // 1. Mask Y (Custom1.x 사용)
                o.uv_mask = TRANSFORM_TEX(v.uv, _MaskTex);
                o.uv_mask.y += _MaskYOffset + v.uv2.x; 
                
                // 2. Dissolve & Distortion (Custom1.y, Custom1.z 사용)
                // v.uv2.y가 Dissolve, v.uv2.z가 Distortion 강도입니다.
                o.dissolve_distort.x = v.uv2.y; 
                o.dissolve_distort.y = v.uv2.z; 
                
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 파티클 시스템의 Custom1.z 값과 인스펙터의 기본 강도를 합산
                float finalDistortStrength = _DistortionStrength + i.dissolve_distort.y;

                // 왜곡 계산
                float2 pannedUV = i.uv + float2(_Time.y * _SpeedX, _Time.y * _SpeedY);
                float4 normalSample = tex2D(_NormalMap, pannedUV);
                float2 distortion = (normalSample.rg * 2.0 - 1.0) * finalDistortStrength;

                // 텍스처 샘플링
                fixed4 col = tex2D(_MainTex, i.uv + distortion);
                fixed4 mask = tex2D(_MaskTex, i.uv_mask);
                float noise = tex2D(_NoiseTex, i.uv).r;

                // 디졸브 수치 결정 (Custom1.y 사용)
                float finalDissolve = saturate(_DissolveAmount + i.dissolve_distort.x);
                float dissolveMask = step(finalDissolve, noise);
                
                // 에지 효과
                float edgeMask = step(finalDissolve - _DissolveEdge, noise) - dissolveMask;
                fixed3 finalRGB = (col.rgb * i.color.rgb) + (edgeMask * _EdgeColor.rgb);

                fixed4 finalColor;
                finalColor.rgb = finalRGB;
                finalColor.a = col.a * mask.r * i.color.a * dissolveMask;
                
                return finalColor;
            }
            ENDCG
        }
    }
}