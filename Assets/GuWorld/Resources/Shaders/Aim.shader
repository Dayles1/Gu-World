// A skill's aim (Render/SkillAim): solid shapes of see-through light, coloured by
// the mesh's vertex colours. Each face is shaded by which way it turns (lit from
// above and from the eye, its edges brighter where they turn away), so the shapes
// read as solid; faces marked by uv.z carry chevrons flowing along uv.x (metres
// along the path), pointing forward. What lies behind a wall still shows, faintly.
Shader "GuWorld/Aim"
{
    Properties
    {
        _Behind ("Behind walls", Range(0, 1)) = 0.3
        _Flow ("Flow speed", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Behind;
            float _Flow;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float4 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float4 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 viewWS : TEXCOORD2;
        };

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.viewWS = _WorldSpaceCameraPos - positionWS;
            output.color = input.color;
            output.uv = input.uv;
            return output;
        }

        half4 Shade(Varyings input)
        {
            float3 n = normalize(input.normalWS);
            float3 v = normalize(input.viewWS);
            float3 l = normalize(float3(0, 1, 0) + v * 0.8);
            half facing = saturate(dot(n, v));
            half diffuse = 0.42 + 0.58 * saturate(dot(n, l));
            half rim = pow(1 - facing, 3);

            // Chevrons, their points ahead (the middle of the band, |uv.y| 0, leads its edges), flowing forward.
            float wave = frac((input.uv.x + abs(input.uv.y) * 0.8) / 1.3 - _Time.y * _Flow);
            half chevron = smoothstep(0.0, 0.1, wave) * (1 - smoothstep(0.1, 0.42, wave)) * input.uv.z;

            half3 colour = input.color.rgb * (diffuse + rim * 0.9 + chevron * 1.4);
            half alpha = saturate(input.color.a * (1 + rim * 0.8) + chevron * 0.45 * saturate(input.color.a * 4));

            return half4(colour, alpha);
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
                half4 colour = Shade(input);
                return half4(colour.rgb, colour.a * _Behind);
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
                return Shade(input);
            }
            ENDHLSL
        }
    }
}
