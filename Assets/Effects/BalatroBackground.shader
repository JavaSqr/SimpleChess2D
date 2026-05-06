Shader "Custom/BalatroBackground"
{
    Properties
    {
        _SpinRotation ("Spin Rotation", Float) = -2.0
        _SpinSpeed ("Spin Speed", Float) = 7.0
        _Offset ("Offset", Vector) = (0,0,0,0)
        _Color1 ("Color 1", Color) = (0.871, 0.267, 0.231, 1)
        _Color2 ("Color 2", Color) = (0.0, 0.42, 0.706, 1)
        _Color3 ("Color 3", Color) = (0.086, 0.137, 0.145, 1)
        _Contrast ("Contrast", Float) = 3.5
        _Lighting ("Lighting", Float) = 0.4
        _SpinAmount ("Spin Amount", Float) = 0.25
        _PixelFilter ("Pixel Filter", Float) = 745.0
        _SpinEase ("Spin Ease", Float) = 1.0
        _IsRotate ("Is Rotate", Float) = 0
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

            float _SpinRotation;
            float _SpinSpeed;
            float2 _Offset;
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float _Contrast;
            float _Lighting;
            float _SpinAmount;
            float _PixelFilter;
            float _SpinEase;
            float _IsRotate;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = v.uv * _ScreenParams.xy;
                return o;
            }

            float4 effect(float2 screenSize, float2 screen_coords)
            {
                float pixel_size = length(screenSize.xy) / _PixelFilter;
                float2 uv = (floor(screen_coords*(1.0/pixel_size))*pixel_size - 0.5*screenSize.xy)/length(screenSize.xy) - _Offset;
                float uv_len = length(uv);
                
                float speed = (_SpinRotation*_SpinEase*0.2);
                if(_IsRotate > 0.5){
                    speed = _Time.y * speed;
                }
                speed += 302.2;

                float new_pixel_angle = atan2(uv.y, uv.x) + speed - _SpinEase*20.*(_SpinAmount*uv_len + (1. - _SpinAmount));

                float2 mid = (screenSize/length(screenSize))/2.0;
                uv = float2(
                    uv_len * cos(new_pixel_angle) + mid.x,
                    uv_len * sin(new_pixel_angle) + mid.y
                ) - mid;

                uv *= 30.0;
                speed = _Time.y * _SpinSpeed;

                float2 uv2 = float2(uv.x + uv.y, uv.x + uv.y);

                for(int i = 0; i < 5; i++)
                {
                    uv2 += sin(max(uv.x, uv.y)) + uv;
                    uv += 0.5 * float2(
                        cos(5.1123314 + 0.353*uv2.y + speed*0.131121),
                        sin(uv2.x - 0.113*speed)
                    );
                    uv -= cos(uv.x + uv.y) - sin(uv.x*0.711 - uv.y);
                }

                float contrast_mod = (0.25*_Contrast + 0.5*_SpinAmount + 1.2);
                float paint_res = min(2.0, max(0.0,length(uv)*(0.035)*contrast_mod));

                float c1p = max(0.0,1.0 - contrast_mod*abs(1.0-paint_res));
                float c2p = max(0.0,1.0 - contrast_mod*abs(paint_res));
                float c3p = 1.0 - min(1.0, c1p + c2p);

                float light = (_Lighting - 0.2)*max(c1p*5.0 - 4.0, 0.0) + _Lighting*max(c2p*5.0 - 4.0, 0.0);

                float4 col = (0.3/_Contrast)*_Color1 + (1.0 - 0.3/_Contrast)*
                    (_Color1*c1p + _Color2*c2p + float4(c3p*_Color3.rgb, c3p*_Color1.a)) + light;

                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return effect(_ScreenParams.xy, i.screenPos);
            }
            ENDCG
        }
    }
}