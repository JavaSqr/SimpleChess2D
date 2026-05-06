Shader "Custom/NeonWaterfall"
{
    Properties
    {
        _FlowSpeed ("Flow Speed", Float) = 1.5
        _SpinInfluence ("Spin Influence", Float) = 2.0
        _Scale ("Scale", Float) = 3.0

        _Color1 ("Color 1", Color) = (0.871, 0.267, 0.231, 1)
        _Color2 ("Color 2", Color) = (0.0, 0.42, 0.706, 1)
        _Color3 ("Color 3", Color) = (0.086, 0.137, 0.145, 1)

        _Contrast ("Contrast", Float) = 3.5
        _Emission ("Emission", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _FlowSpeed;
            float _SpinInfluence;
            float _Scale;

            float4 _Color1;
            float4 _Color2;
            float4 _Color3;

            float _Contrast;
            float _Emission;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 warp(float2 p, float t)
            {
                for(int i = 0; i < 4; i++)
                {
                    p += float2(
                        cos(p.y * 3.0 + t * 0.7),
                        sin(p.x * 3.0 - t * 0.5)
                    ) * 0.3;

                    p += float2(
                        sin(p.y * 5.0 - t),
                        cos(p.x * 5.0 + t)
                    ) * 0.15;
                }
                return p;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5;

                float t = _Time.y * _FlowSpeed;

                // поток вниз
                uv.y += t;

                // хаотичный варп (как в Balatro)
                uv = warp(uv * _Scale, t);

                float len = length(uv);

                // радиальная деформация (чтобы был "энергетический" вайб)
                float angle = atan2(uv.y, uv.x);
                angle += len * _SpinInfluence;

                uv = float2(cos(angle), sin(angle)) * len;

                // генерация паттерна
                float v = sin(uv.x * 2.0) + sin(uv.y * 2.0);
                v += sin((uv.x + uv.y) * 1.5);

                v = abs(v);

                float contrast = _Contrast;

                float c1 = saturate(1.0 - contrast * abs(1.0 - v));
                float c2 = saturate(1.0 - contrast * abs(v));
                float c3 = 1.0 - saturate(c1 + c2);

                float4 col =
                    _Color1 * c1 +
                    _Color2 * c2 +
                    float4(_Color3.rgb * c3, 1.0);

                // неоновый эффект
                col.rgb *= _Emission;
                col.rgb = pow(col.rgb, 1.2);

                return col;
            }
            ENDCG
        }
    }
}