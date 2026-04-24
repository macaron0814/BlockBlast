Shader "BlockBlast/HueShift"
{
    Properties
    {
        _MainTex    ("Texture",        2D)    = "white" {}
        _HueShift   ("Hue Shift",     Range(0, 1)) = 0
        _Saturation ("Saturation",    Range(0, 2)) = 1
        _Brightness ("Brightness",    Range(0, 2)) = 1
        _Alpha      ("Alpha",         Range(0, 1)) = 1
        _Color ("Tint Color", Color) = (1,1,1,1)
        [Toggle(UNITY_UI_CLIP_RECT)] _UseClipRect ("Use Clip Rect", Float) = 0
        _ClipRect   ("Clip Rect",     Vector) = (-32767,-32767,32767,32767)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float     _HueShift;
            float     _Saturation;
            float     _Brightness;
            float     _Alpha;
            fixed4    _Color;
            float4    _ClipRect;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float4 worldPos  : TEXCOORD1;
                float4 color     : COLOR;
            };

            // RGB → HSV
            float3 RGBtoHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float  d = q.x - min(q.w, q.y);
                float  e = 1e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0*d + e)),
                              d / (q.x + e),
                              q.x);
            }

            // HSV → RGB
            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.uv       = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = v.vertex;
                o.color    = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                float3 hsv = RGBtoHSV(col.rgb);
                hsv.x  = frac(hsv.x + _HueShift);
                hsv.y  = saturate(hsv.y * _Saturation);
                hsv.z  = saturate(hsv.z * _Brightness);
                col.rgb = HSVtoRGB(hsv);
                col.a  *= _Alpha;

#ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
#endif
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
