Shader "UI/SubtleGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _GlowColor ("Glow Color", Color) = (1,0.9,0.5,0.4)
        _GlowPower ("Glow Power", Range(1, 10)) = 2.5
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.6
        _GlowSpread ("Glow Spread", Range(0.001, 0.005)) = 0.0015
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            Name "Default"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _GlowColor;
            half _GlowPower;
            half _GlowIntensity;
            half _GlowSpread;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                
                OUT.color = v.color * _Color;
                return OUT;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                // Simple pulse effect
                float pulse = (sin(_Time.y * 0.5) * 0.15) + 0.85;
                
                half4 glow = color;
                glow.rgb = _GlowColor.rgb * _GlowIntensity * pulse;
                
                // Sample texture around the current pixel with restrained spread
                half4 blur = half4(0, 0, 0, 0);
                float step = _GlowSpread;
                
                for (float x = -3.0; x <= 3.0; x += 1.0)
                {
                    for (float y = -3.0; y <= 3.0; y += 1.0)
                    {
                        blur += tex2D(_MainTex, IN.texcoord + half2(x, y) * step);
                    }
                }
                
                blur /= 49.0; // 7x7 samples
                
                // Create a subtle glow effect
                half glowAlpha = pow(blur.a, _GlowPower) * _GlowColor.a * pulse;
                half4 glowResult = half4(glow.rgb, glowAlpha);
                
                // Blend with original image, preserving more of the original
                half4 result = lerp(glowResult, color, color.a * 0.9);
                result.a = max(color.a, glowAlpha * 0.6);
                
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                
                return result;
            }
            ENDCG
        }
    }
}