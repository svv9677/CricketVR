// The shot replay's ghosts (ShotReplay): the bat, the ball, the ball's trail and the contact ring.
// Unlit, additive and transparent so it reads as light rather than as an object, never needs
// lighting, and costs one pass with no texture on the Quest's GPU.
//   * fresnel rim: silhouettes glow, faces toward the eye stay faint, so the swing's shape reads
//     even when the ghost bat is edge-on;
//   * scanlines move up in world Y, so they stay put as the bat turns (object-space lines crawl);
//   * _Flicker is 0 by default: a flickering light in the headset is uncomfortable;
//   * _UseVertexColor multiplies in the vertex colour, for LineRenderers (their colour gradient
//     fades the trail). Meshes leave it off: an absent colour stream is not guaranteed to be white.
// Single-pass instanced and multiview safe (Quest stereo): instance id in, stereo output set up.
Shader "CricketVR/Hologram"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.25, 0.95, 1.0, 1)
        _Alpha ("Base opacity", Range(0, 1)) = 0.18
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim strength", Range(0, 4)) = 1.4
        _ScanDensity ("Scanlines per metre", Float) = 40
        _ScanSpeed ("Scanline speed (m/s)", Float) = 0.25
        _ScanStrength ("Scanline strength", Range(0, 1)) = 0.35
        _Flicker ("Flicker", Range(0, 1)) = 0
        [Toggle] _UseVertexColor ("Use vertex colour", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One      // additive: overlaps brighten, nothing needs sorting
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                half _RimPower;
                half _RimStrength;
                float _ScanDensity;
                float _ScanSpeed;
                half _ScanStrength;
                half _Flicker;
                half _UseVertexColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                // Per-eye camera position: computed here, where the eye index is already set up.
                OUT.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                OUT.color = lerp(half4(1, 1, 1, 1), IN.color, _UseVertexColor);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // LineRenderers carry no normals (zero vector): no rim there, not a NaN.
                float nLen = dot(IN.normalWS, IN.normalWS);
                float vLen = dot(IN.viewDirWS, IN.viewDirWS);
                half rim = 0;
                if (nLen > 1e-6 && vLen > 1e-8)
                {
                    half facing = abs(dot(IN.normalWS * rsqrt(nLen), IN.viewDirWS * rsqrt(vLen)));
                    rim = pow(1 - saturate(facing), _RimPower) * _RimStrength;
                }

                float scan = frac(IN.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed * _ScanDensity);
                half scanLine = 1 - _ScanStrength * smoothstep(0.35, 0.5, abs(scan - 0.5));

                half flicker = 1 - _Flicker * 0.5 * (1 + sin(_Time.y * 67.0) * sin(_Time.y * 23.0));

                half intensity = (_Alpha + rim) * scanLine * flicker;
                half a = saturate(intensity * _Color.a * IN.color.a);
                return half4(_Color.rgb * IN.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
