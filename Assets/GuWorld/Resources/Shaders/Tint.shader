// A see-through tint for showing shapes over a body (the body page's parts):
// unlit, one colour, brighter towards its rim so its outline reads. Its depth
// test is a material setting — an inner organ (the heart) is drawn over what
// hides it.
Shader "GuWorld/Tint"
{
    Properties
    {
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 0.6)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Back

        Pass
        {
            Name "Tint"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ZTest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float facing = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half rim = pow(1 - facing, 2);
                half3 colour = _BaseColor.rgb * (0.85 + 0.6 * rim);
                return half4(colour, saturate(_BaseColor.a * (0.7 + 0.6 * rim)));
            }
            ENDHLSL
        }
    }
}
