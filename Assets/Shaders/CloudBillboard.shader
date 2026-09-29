// The sky's clouds (ProceduralClouds): one textured, camera-facing quad per cloud.
// Unlit and transparent, because a cloud is light passing through water droplets rather than a
// surface catching it -- lighting a cloud plate with a diffuse term is what gives the hard
// terminator that reads as polystyrene.
//   * _Tint carries the sun: ProceduralClouds works out per cloud how much of the sun it faces
//     and brightens or shades it there, so no two clouds in the sky are the same white;
//   * _Haze fades a cloud into _HazeColor, the colour of the sky at the horizon. Low clouds lose
//     contrast and sit back; high ones stay crisp. This is the depth cue that a flat white sky
//     is missing;
//   * _Opacity thins the whole plate. The plates come in at full strength, which is right for a
//     cloud overhead and too heavy for a distant one.
// Per-cloud values ride in an instancing buffer so the whole sky is one batch.
// Single-pass instanced and multiview safe (Quest stereo): instance id in, stereo output set up.
Shader "CricketVR/CloudBillboard"
{
    Properties
    {
        _BaseMap ("Cloud plate", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Horizon haze colour", Color) = (0.72, 0.80, 0.89, 1)
        _Haze ("Haze", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "CloudBillboard"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off          // transparent: never occlude, sort back-to-front by distance
            ZTest LEqual        // but still hide behind the stadium roof and floodlights
            Cull Off            // the quad may be mirrored, which flips its winding
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HazeColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Haze)
                UNITY_DEFINE_INSTANCED_PROP(float, _Opacity)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                float4 st = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseMap_ST);
                OUT.uv = IN.uv * st.xy + st.zw;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 plate = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                half4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
                half haze = UNITY_ACCESS_INSTANCED_PROP(Props, _Haze);
                half4 hazeColour = UNITY_ACCESS_INSTANCED_PROP(Props, _HazeColor);
                half opacity = UNITY_ACCESS_INSTANCED_PROP(Props, _Opacity);

                half3 rgb = plate.rgb * tint.rgb;
                rgb = lerp(rgb, hazeColour.rgb, haze);

                // Haze eats the edges as well as the colour: a distant cloud loses its crisp
                // silhouette first. Without this the shape stays sharp while the colour washes
                // out, which looks like a faded sticker rather than distance.
                half alpha = plate.a * tint.a * opacity * lerp(1.0, 0.72, haze);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
