Shader "UI/Opening Slides/Film Wear"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Overlay Color", Color) = (0, 0, 0, 1)
        _BaseOpacity ("Base Opacity", Range(0, 1)) = 0.28
        _GrainStrength ("Grain Strength", Range(0, 1)) = 0.12
        _GrainScale ("Grain Scale", Range(16, 1024)) = 360
        _ScratchStrength ("Scratch Strength", Range(0, 1)) = 0.24
        _ScratchCount ("Scratch Count", Range(1, 128)) = 34
        _DustScale ("Dust Scale", Range(8, 512)) = 150
        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.045
        _AnimationSpeed ("Animation Speed", Range(0, 5)) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _TextureSampleAdd ("Texture Sample Add", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "FilmWear"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _BaseOpacity;
            float _GrainStrength;
            float _GrainScale;
            float _ScratchStrength;
            float _ScratchCount;
            float _DustScale;
            float _FlickerStrength;
            float _AnimationSpeed;

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 cellUv = frac(value);
                cellUv = cellUv * cellUv * (3.0 - 2.0 * cellUv);

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), cellUv.x);
                float top = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), cellUv.x);
                return lerp(bottom, top, cellUv.y);
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                output.worldPosition = input.vertex;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.texcoord) + _TextureSampleAdd;
                float timeStep = floor(_Time.y * _AnimationSpeed * 1.7);

                float grain = Hash21(floor(input.texcoord * _GrainScale) + timeStep);
                float flicker = ValueNoise(float2(timeStep * 0.37, 2.9)) - 0.5;

                float scratchCell = floor(input.texcoord.x * _ScratchCount);
                float scratchSeed = Hash11(scratchCell + timeStep * 19.3);
                float scratchPosition = abs(frac(input.texcoord.x * _ScratchCount) - scratchSeed);
                float scratchLine = step(0.79, scratchSeed) * (1.0 - smoothstep(0.0015, 0.014, scratchPosition));
                float scratchBreakup = smoothstep(0.30, 0.70, ValueNoise(float2(scratchCell * 4.1, input.texcoord.y * 68.0 + timeStep * 0.45)));
                float scratch = scratchLine * scratchBreakup;

                float dust = step(0.993, Hash21(floor(input.texcoord * _DustScale) + timeStep * 7.1));
                float dirt = smoothstep(0.88, 0.98, ValueNoise(input.texcoord * float2(6.0, 10.0) + timeStep * 0.11));
                float wear = max(scratch, dust);

                float alpha = _BaseOpacity;
                alpha += (grain - 0.5) * _GrainStrength;
                alpha += flicker * _FlickerStrength;
                alpha += dirt * 0.08;
                alpha -= wear * _ScratchStrength;
                alpha = saturate(alpha) * source.a * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
