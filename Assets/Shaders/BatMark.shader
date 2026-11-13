// The ball's prints on the bat's face (BatMarks): one mesh of small quads, each a soft print
// texture tinted and faded by its vertex colour (the ball's leather, and how hard it was struck),
// alpha blended over the wood, drawn a hair in front of it (Offset) so it never flickers into it.
// Lit by the scene's main light and ambient like the face it sits on, so a red print does not glow
// at night. Single-pass instanced and multiview safe (Quest stereo).
Shader "CricketVR/BatMark"
{
    Properties
    {
        _BaseMap ("Print", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "BatMark"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half3 light : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                half3 n = TransformObjectToWorldNormal(v.normalOS);
                Light sun = GetMainLight();
                // Either side of the face: the print is on whichever side faces the light.
                half facing = abs(dot(n, sun.direction));
                o.light = sun.color * (0.35h + 0.65h * facing) + SampleSH(n);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 print = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 rgb = i.color.rgb * saturate(i.light);
                return half4(rgb, print.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
