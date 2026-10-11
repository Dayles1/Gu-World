// Wire lines for the hitbox view (Render/HitboxView): unlit, coloured by the
// mesh's vertex colours. Lines in front of things are drawn bright; lines behind
// them faint, so a shape hidden in a wall still shows where it is.
Shader "GuWorld/Lines"
{
    Properties
    {
        _Behind ("Behind walls", Range(0, 1)) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Behind;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
        };

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.color = input.color;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Behind"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Greater

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                return half4(input.color.rgb, input.color.a * _Behind);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Front"
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            half4 Fragment(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
