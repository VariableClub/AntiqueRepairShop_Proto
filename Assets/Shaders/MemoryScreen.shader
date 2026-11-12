// 기억 영상 화면 (MEM-05·06·07). 기획서 14장 '기억 영상'.
//  - _UseVideoAlpha = 1: 투명 배경 영상(알파)을 수선소 위에 겹친다
//  - _UseVideoAlpha = 0: 검은 배경 영상 + 가장자리 페이드(_EdgeFade)로 대신한다
//  - 손상 마스크: 영역 3칸까지. 영역 안을 흐리게 하고(9칸 평균) 찢긴 결 모양으로 비운다. 칸마다 세기(_MaskAmtN)는 C#이 구간에 맞춰 켠다
//  - _Alpha: 등장·소멸 페이드
Shader "Dokkaebi/MemoryScreen"
{
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        _Alpha ("Alpha", Range(0,1)) = 1
        _UseVideoAlpha ("Use Video Alpha", Range(0,1)) = 1
        _EdgeFade ("Edge Fade", Range(0,0.5)) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _MaskRect0 ("Mask 0 (xMin,yMin,xMax,yMax)", Vector) = (0,0,0,0)
        _MaskRect1 ("Mask 1", Vector) = (0,0,0,0)
        _MaskRect2 ("Mask 2", Vector) = (0,0,0,0)
        _MaskAmt0 ("Mask 0 Amount", Range(0,1)) = 0
        _MaskAmt1 ("Mask 1 Amount", Range(0,1)) = 0
        _MaskAmt2 ("Mask 2 Amount", Range(0,1)) = 0
        _BlurRadius ("Blur Radius (uv)", Range(0,0.05)) = 0.014
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Alpha, _UseVideoAlpha, _EdgeFade, _BlurRadius;
            fixed4 _Tint;
            float4 _MaskRect0, _MaskRect1, _MaskRect2;
            float _MaskAmt0, _MaskAmt1, _MaskAmt2;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = hash21(i), b = hash21(i + float2(1,0)), c = hash21(i + float2(0,1)), d = hash21(i + float2(1,1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 영역 안 1, 바깥 0. 가장자리는 들쭉날쭉하게 (찢긴 가장자리)
            float region(float2 uv, float4 r)
            {
                if (r.z <= r.x || r.w <= r.y) return 0;
                float2 c = (r.xy + r.zw) * 0.5;
                float2 h = (r.zw - r.xy) * 0.5;
                float2 d = abs(uv - c) - h;
                float dist = max(d.x, d.y);
                float jag = (noise2(uv * 38.0) - 0.5) * 0.04;
                return 1.0 - smoothstep(-0.03, 0.01, dist + jag);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 col = tex2D(_MainTex, uv);

                float m = max(max(region(uv, _MaskRect0) * _MaskAmt0, region(uv, _MaskRect1) * _MaskAmt1), region(uv, _MaskRect2) * _MaskAmt2);
                float a = lerp(1.0, col.a, _UseVideoAlpha);

                if (m > 0.001)
                {
                    float r = _BlurRadius;
                    fixed4 b = col;
                    b += tex2D(_MainTex, uv + float2( r, 0));
                    b += tex2D(_MainTex, uv + float2(-r, 0));
                    b += tex2D(_MainTex, uv + float2(0,  r));
                    b += tex2D(_MainTex, uv + float2(0, -r));
                    b += tex2D(_MainTex, uv + float2( r,  r) * 0.7);
                    b += tex2D(_MainTex, uv + float2(-r,  r) * 0.7);
                    b += tex2D(_MainTex, uv + float2( r, -r) * 0.7);
                    b += tex2D(_MainTex, uv + float2(-r, -r) * 0.7);
                    b /= 9.0;
                    float grey = dot(b.rgb, float3(0.3, 0.59, 0.11));
                    b.rgb = lerp(b.rgb, grey.xxx, 0.5) * 0.75;
                    // 찢긴 결: 비스듬한 긴 줄무늬 노이즈
                    float2 q = float2(uv.x * 6.0 + uv.y * 2.0, uv.y * 48.0);
                    float tear = smoothstep(0.62, 0.7, noise2(q));
                    col.rgb = lerp(col.rgb, b.rgb, m);
                    a *= 1.0 - m * tear * 0.9;
                }

                float e = 1.0;
                if (_EdgeFade > 0.0001)
                {
                    float edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                    e = smoothstep(0.0, _EdgeFade, edge);
                }

                return fixed4(col.rgb * _Tint.rgb, saturate(a * e * _Alpha * _Tint.a));
            }
            ENDCG
        }
    }
}
