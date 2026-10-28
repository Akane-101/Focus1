Shader "Hidden/FocusLayerFX"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "KawaseBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragKawase
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Offset;
            float4 _FocusTexelSize;
            float4 _BlitTexture_TexelSize;

            half4 FragKawase(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = _FocusTexelSize.xy;
                if (texel.x <= 0.0)
                {
                    texel = _BlitTexture_TexelSize.xy;
                }

                float2 offset = _Offset * texel;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, offset.y));
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, -offset.y));
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, offset.y));
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y));
                return color * 0.25h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "PremulBlend"
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlit
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragBlit(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            }
            ENDHLSL
        }

        Pass
        {
            Name "FillColor"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFill
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Color;

            half4 FragFill(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "StraightToPremul"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPremul
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragPremul(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "MixSharpBlur"
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMix
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_FocusSharpTex);
            SAMPLER(sampler_FocusSharpTex);
            float _FocusBlend;

            half4 FragMix(Varyings input) : SV_Target
            {
                half4 blur = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half4 sharp = SAMPLE_TEXTURE2D(_FocusSharpTex, sampler_FocusSharpTex, input.texcoord);
                return lerp(blur, sharp, saturate(_FocusBlend));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
