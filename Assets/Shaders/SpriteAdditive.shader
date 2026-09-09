// Sprite aditivo para capas de luz: halos de farol, resplandor de TV y,
// más adelante, los seis sprites de luz de las cinemáticas.
//
// Por qué un shader propio: el Sprite-Unlit-Default de URP no expone el
// estado de blending, y las luces necesitan Blend One One. Escrito en HLSL
// contra Core.hlsl para que sea URP nativo; un shader CG del pipeline viejo
// funcionaría de casualidad y se rompería en cualquier upgrade.
//
// No participa del sistema de luces 2D a propósito: estas capas SON luz, no
// superficies iluminadas. Por eso tampoco las toca el ciclo de día.
Shader "BuenosDias/Sprite Additive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensidad", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            Name "SpriteAdditive"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Sin _MainTex_ST a propósito: el SRP Batcher de 2D no soporta las
            // propiedades _TexelSize / _ST y desactiva el batching para todos los
            // renderers que usen este material. Los sprites vienen con las UV ya
            // resueltas, así que el tiling no hace falta.
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 tinted = texel * input.color;

                // En aditivo el alpha no recorta: modula cuánto suma. Por eso se
                // premultiplica, y así animar el alpha sirve para el parpadeo.
                return half4(tinted.rgb * tinted.a * _Intensity, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
