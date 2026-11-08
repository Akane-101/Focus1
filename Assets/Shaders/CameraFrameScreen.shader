Shader "Hidden/CameraFrameScreen"
{
    Properties
    {
        _FrameGameTex("Game", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" "RenderType" = "Opaque" }

        Pass
        {
            Name "CameraFrameScreen"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrameGameTex);
            SAMPLER(sampler_FrameGameTex);
            float4 _LcdBounds;
            float4 _FrameAspects;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float2 size = max(_LcdBounds.zw - _LcdBounds.xy, float2(0.0001, 0.0001));
                output.uv = (input.positionOS.xy - _LcdBounds.xy) / size;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float lcdAspect = max(_FrameAspects.x, 0.001);
                float gameAspect = max(_FrameAspects.y, 0.001);
                float2 fitted = uv;
                if (lcdAspect > gameAspect)
                {
                    float span = gameAspect / lcdAspect;
                    fitted.x = (uv.x - 0.5) / span + 0.5;
                }
                else
                {
                    float span = lcdAspect / gameAspect;
                    fitted.y = (uv.y - 0.5) / span + 0.5;
                }

                if (fitted.x < 0 || fitted.x > 1 || fitted.y < 0 || fitted.y > 1)
                {
                    return half4(0.02, 0.02, 0.02, 1);
                }

                return SAMPLE_TEXTURE2D(_FrameGameTex, sampler_FrameGameTex, fitted);
            }
            ENDHLSL
        }
    }
}
