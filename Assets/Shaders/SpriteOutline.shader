Shader "Game/SpriteOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Flash effect properties (same as CharacterFlash)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0

        // Outline effect properties
        _OutlineEnabled ("Outline Enabled", Float) = 0
        _OutlineColor ("Outline Color", Color) = (1,0,0,1)
        _OutlineThickness ("Outline Thickness", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // (1/width, 1/height, width, height)
            fixed4 _Color;
            fixed4 _FlashColor;
            float _FlashAmount;
            float _OutlineEnabled;
            fixed4 _OutlineColor;
            float _OutlineThickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv) * i.color;

                // --- Outline detection ---
                if (_OutlineEnabled > 0.5 && _OutlineThickness > 0)
                {
                    // If current pixel is transparent, check if any neighbor is opaque
                    if (texColor.a < 0.1)
                    {
                        float2 texelSize = _MainTex_TexelSize.xy * _OutlineThickness;

                        // Sample 8 directions around the current pixel
                        float neighborAlpha = 0;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2( texelSize.x, 0)).a;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2(-texelSize.x, 0)).a;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2(0,  texelSize.y)).a;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2(0, -texelSize.y)).a;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2( texelSize.x,  texelSize.y)).a;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2(-texelSize.x,  texelSize.y)).a;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2( texelSize.x, -texelSize.y)).a;
                        neighborAlpha += tex2D(_MainTex, i.uv + float2(-texelSize.x, -texelSize.y)).a;

                        if (neighborAlpha > 0)
                        {
                            // This is an outline pixel
                            fixed4 outlineCol = _OutlineColor;
                            outlineCol.rgb *= outlineCol.a; // Premultiply alpha
                            return outlineCol;
                        }
                    }
                }

                // --- Flash effect (same as CharacterFlash) ---
                texColor.rgb = lerp(texColor.rgb, _FlashColor.rgb * texColor.a, _FlashAmount);

                // Premultiply alpha
                texColor.rgb *= texColor.a;

                return texColor;
            }
            ENDCG
        }
    }
}
