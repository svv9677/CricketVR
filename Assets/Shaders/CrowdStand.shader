// Unlit crowd shader for the stepped stadium stands.
//
// Unlit on purpose: the crowd sits in shade under the roof, it covers a large part of the
// view, and per-pixel lighting on that much screen area is the wrong place to spend a tile
// GPU's budget. All motion happens in the vertex shader and costs ~10 ALU per vertex.
//
// Per-quad animation data is baked into vertex colours by CrowdStandBuilder:
//   r = random phase        0..1   (so each column bobs on its own beat)
//   g = row index           0..1   (lets the wave lag slightly up the stand)
//   b = angle around centre 0..1   (drives the travelling wave)
Shader "CricketVR/CrowdStand"
{
    Properties
    {
        [MainTexture] _BaseMap       ("Crowd Atlas", 2D)            = "white" {}
        [MainColor]   _BaseColor     ("Tint", Color)                = (1,1,1,1)

        [Header(Idle Motion)]
        _BobAmplitude   ("Bob Amplitude (m)", Range(0, 0.25))       = 0.035
        _BobFrequency   ("Bob Frequency (Hz)", Range(0, 3))         = 0.8

        [Header(Wave)]
        _WaveEnabled    ("Wave Enabled (0 or 1)", Range(0,1))       = 0
        _WavePhase      ("Wave Phase (radians)", Float)             = 0
        _WaveWidth      ("Wave Width (radians)", Range(0.05, 2))    = 0.45
        _WaveHeight     ("Wave Height (m)", Range(0, 2))            = 0.75
        _WaveRowLag     ("Wave Row Lag (radians)", Range(0, 1))     = 0.15

        [Header(Night Camera Flashes)]
        _FlashStrength  ("Flash Strength", Range(0, 4))             = 0
        _FlashColor     ("Flash Colour", Color)                     = (1,1,1,1)
        _FlashDensity   ("Flash Density", Range(0.9, 1.0))          = 0.997
        _FlashRate      ("Flash Rate (Hz)", Range(0.5, 8))          = 3
        _PeoplePerRepeat("People Per Atlas Repeat", Float)          = 50
    }

    SubShader
    {
        Tags
        {
            "RenderType"        = "Opaque"
            "RenderPipeline"    = "UniversalPipeline"
            "Queue"             = "Geometry"
        }

        Pass
        {
            Name "CrowdUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   CrowdVertex
            #pragma fragment CrowdFragment
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define CROWD_TWO_PI 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Everything the material can change lives here, so the SRP Batcher can batch us.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _FlashColor;
                float  _BobAmplitude;
                float  _BobFrequency;
                float  _WaveEnabled;
                float  _WavePhase;
                float  _WaveWidth;
                float  _WaveHeight;
                float  _WaveRowLag;
                float  _FlashStrength;
                float  _FlashDensity;
                float  _FlashRate;
                float  _PeoplePerRepeat;
            CBUFFER_END

            float CrowdHash21(float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings CrowdVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionOS = IN.positionOS.xyz;

                // --- idle bob: whole quad moves together, so nobody shears ---
                float phase = IN.color.r * CROWD_TWO_PI;
                float bob = sin(_Time.y * _BobFrequency * CROWD_TWO_PI + phase) * _BobAmplitude;

                // --- travelling wave: gaussian bump centred on _WavePhase ---
                float angle = IN.color.b * CROWD_TWO_PI;
                float centre = _WavePhase + IN.color.g * _WaveRowLag;
                float d = abs(angle - centre);
                d = min(d, CROWD_TWO_PI - d);                       // shortest way round
                float wave = exp(-(d * d) / (2.0 * _WaveWidth * _WaveWidth))
                             * _WaveHeight * _WaveEnabled;

                positionOS.y += bob + wave;

                OUT.positionCS = TransformObjectToHClip(positionOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 CrowdFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Occasional camera flashes, one person at a time. Free when _FlashStrength = 0.
                if (_FlashStrength > 0.0)
                {
                    float2 cell = floor(float2(IN.uv.x * _PeoplePerRepeat, IN.uv.y * 64.0));
                    float t = floor(_Time.y * _FlashRate);
                    float r = CrowdHash21(cell + t * 17.13);
                    float flash = step(_FlashDensity, r) * _FlashStrength;
                    albedo.rgb += _FlashColor.rgb * flash;
                }

                return albedo;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
