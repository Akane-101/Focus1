Shader "Hidden/FocusGlareFX"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 0.92, 0.72, 1)
        _Intensity ("Intensity", Float) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragGlare
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;
            float _Intensity;

            half4 FragGlare(v2f_img i) : SV_Target
            {
                half4 color = tex2D(_MainTex, i.uv);
                float2 centered = i.uv * 2.0 - 1.0;
                float edge = saturate((length(centered * float2(1.05, 0.78)) - 0.22) / 0.95);
                edge = edge * edge;
                color.rgb += _Color.rgb * edge * _Intensity;
                color.rgb = saturate(color.rgb);
                return color;
            }
            ENDCG
        }
    }

    FallBack Off
}
