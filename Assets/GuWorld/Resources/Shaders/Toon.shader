// GU World's people, drawn as the art direction asks ("Hero 2.0" in Reference/Design):
// cel shading in two steps of light without a gradient, cold bluish shadows, a cold
// rim along the silhouette, and a dark outline of one thickness all round (thinner on
// the face, where a mesh's UV3.x says so). Found by name (GuWorld/Toon), so it lives in
// Resources and the build keeps it.
Shader "GuWorld/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha cut-off", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha clip", Float) = 0

        _ShadeColor("Shadow tint", Color) = (0.56, 0.6, 0.76, 1)
        _ShadeStep("Shadow edge (half-Lambert)", Range(0, 1)) = 0.52
        _ShadeSoft("Shadow edge softness", Range(0.001, 0.2)) = 0.015
        _Ambient("Ambient share", Range(0, 2)) = 0.55

        _RimColor("Rim colour", Color) = (0.62, 0.71, 0.84, 1)
        _RimStep("Rim width (fresnel)", Range(0, 1)) = 0.72
        _RimStrength("Rim strength", Range(0, 1)) = 0.32

        _OutlineColor("Outline colour", Color) = (0.03, 0.024, 0.04, 1)
        _OutlineWidth("Outline width (per metre away)", Range(0, 0.01)) = 0.0018
        _OutlineFar("Outline stops widening beyond (m)", Float) = 14

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half4 _ShadeColor;
            half _ShadeStep;
            half _ShadeSoft;
            half _Ambient;
            half4 _RimColor;
            half _RimStep;
            half _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineFar;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile_local _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                // x: the outline's thinning, which is the face's (Muscles marks it there).
                float2 thin : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
                half face : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.face = saturate(input.thin.x * 2);

                return output;
            }

            // Two steps of light: lit, or the shadow tint (cold); a narrow soft edge between.
            half Step(half lambert)
            {
                return smoothstep(_ShadeStep - _ShadeSoft, _ShadeStep + _ShadeSoft, lambert * 0.5 + 0.5);
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 texel = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
                Alpha(texel.a, half4(1, 1, 1, 1), _Cutoff);
                half3 albedo = texel.rgb;

                // Both sides drawn: the back's normal faces the other way.
                half3 normal = normalize(input.normalWS) * (IS_FRONT_VFACE(face, 1, -1));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = view;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                Light main = GetMainLight(inputData.shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half shadowed = smoothstep(0.35, 0.65, main.shadowAttenuation);
                // The face is kept in the light (as drawn faces are), but for shadows cast on it.
                half lambert = dot(normal, main.direction);
                lambert = lerp(lambert, lambert * 0.3 + 0.45, input.face);
                half lit = Step(lambert) * shadowed;
                half3 shade = _ShadeColor.rgb;
                half3 light = main.color * main.distanceAttenuation * lerp(shade, half3(1, 1, 1), lit);

                // The sky's light, flat (no gradient): its average, cooled like the shadows.
                half3 ambient = max(half3(0, 0, 0), SampleSH(half3(0, 1, 0))) * _Ambient * lerp(shade, half3(1, 1, 1), 0.5);

                // Lanterns and the like, cel-shaded too.
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light extra = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    half extraLit = Step(dot(normal, extra.direction)) * extra.shadowAttenuation;
                    light += extra.color * extra.distanceAttenuation * lerp(shade * 0.35, half3(1, 1, 1), extraLit);
                LIGHT_LOOP_END
                #endif

                half3 colour = albedo * (light + ambient);

                // A cold rim along the silhouette.
                half fresnel = 1 - saturate(dot(normal, view));
                half rim = smoothstep(_RimStep - 0.03, _RimStep + 0.03, fresnel) * _RimStrength;
                half brightness = saturate(Luminance(main.color) * 0.6 + Luminance(ambient) * 2);
                colour += _RimColor.rgb * rim * brightness;

                colour = MixFog(colour, input.fog);

                return half4(colour, 1);
            }
            ENDHLSL
        }

        // The outline: the mesh pushed out along its normals, drawn from inside, flat dark.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                // x: how much thinner here (0 full, 1 none) — the face is thinner.
                float2 thin : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                // Wider with distance (up to a point): about the same few pixels near and far.
                float away = clamp(distance(positionWS, GetCameraPositionWS()), 0.4, _OutlineFar);
                positionWS += normalWS * _OutlineWidth * away * saturate(1 - input.thin.x);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_ALPHATEST_ON)
                    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                #endif

                return half4(MixFog(_OutlineColor.rgb, input.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_local _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_local _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_local _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
