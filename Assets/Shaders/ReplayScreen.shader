// The big replay screen. Shows the replay camera's picture (_View, the RenderTexture CameraReplay
// renders the reconstructed delivery into while a replay plays). _On = 0 shows the screen
// switched off (black). Unlit and opaque: it never needs lighting and must not z-fight or sort
// against the stands behind it.
Shader "CricketVR/ReplayScreen"
{
    Properties
    {
        _View ("View", 2D) = "black" {}
        _On ("On", Float) = 0
        _Brightness ("Brightness", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ReplayScreen"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            TEXTURE2D(_View);
            SAMPLER(sampler_View);
            CBUFFER_START(UnityPerMaterial)
                float _On;
                float _Brightness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_On < 0.5)
                    return half4(0, 0, 0, 1);
                half3 c = SAMPLE_TEXTURE2D(_View, sampler_View, IN.uv).rgb;
                return half4(c * _Brightness, 1);
            }
            ENDHLSL
        }
    }
}
