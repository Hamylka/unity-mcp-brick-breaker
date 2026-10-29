// Additive see-through neon shell (the phasing ball). Bright at the rim, nearly empty in the middle, so whatever is
// behind it (bricks, the floor grid) shows through and the object reads as a ghost of energy rather than a solid.
// Faint scrolling bands add a shimmer. Colors are HDR for the bloom; set _Color per object with a MaterialPropertyBlock.
Shader "BrickBreaker/Neon Ghost"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.6, 0.6, 3, 1)
        _RimPower ("Rim Power", Range(0.3, 8)) = 1.6
        _Fill ("Center Fill", Range(0, 1)) = 0.12
        _BandStrength ("Band Strength", Range(0, 1)) = 0.35
        _BandDensity ("Band Density", Range(1, 60)) = 14
        _BandSpeed ("Band Speed", Range(-20, 20)) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "NeonGhost"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower;
                half _Fill;
                half _BandStrength;
                half _BandDensity;
                half _BandSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float bandCoord : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
                output.bandCoord = input.positionOS.y; // object space, so the bands ride with the ball
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0h - facing, _RimPower);
                half bands = 1.0h - _BandStrength * (0.5h + 0.5h * sin(input.bandCoord * _BandDensity * 6.2832h + _Time.y * _BandSpeed));
                half3 color = _Color.rgb * (rim + _Fill) * bands;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
