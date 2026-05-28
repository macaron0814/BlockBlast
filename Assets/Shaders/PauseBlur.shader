Shader "BlockBlast/UI/PauseBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 8)) = 0
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;
            fixed4 _TintColor;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _BlurSize;

                fixed4 col = tex2D(_MainTex, i.uv) * 0.20;
                col += tex2D(_MainTex, i.uv + float2( offset.x, 0)) * 0.10;
                col += tex2D(_MainTex, i.uv + float2(-offset.x, 0)) * 0.10;
                col += tex2D(_MainTex, i.uv + float2(0,  offset.y)) * 0.10;
                col += tex2D(_MainTex, i.uv + float2(0, -offset.y)) * 0.10;
                col += tex2D(_MainTex, i.uv + float2( offset.x,  offset.y)) * 0.10;
                col += tex2D(_MainTex, i.uv + float2(-offset.x,  offset.y)) * 0.10;
                col += tex2D(_MainTex, i.uv + float2( offset.x, -offset.y)) * 0.10;
                col += tex2D(_MainTex, i.uv + float2(-offset.x, -offset.y)) * 0.10;

                col *= i.color;
                return col;
            }
            ENDCG
        }
    }
}
