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

        [Header(Background Key)]
        // The atlas has no alpha channel; its people sit on a flat brown. We alpha-clip pixels
        // close to that brown so the stands show through. _KeyColor is the brown in LINEAR space
        // (the atlas is sRGB, so the sampled colour is linear too) - default is atlas RGB 54/47/43.
        _KeyColor      ("Background Colour (linear)", Vector)       = (0.037, 0.028, 0.024, 0)
        _KeyTolerance  ("Key Tolerance", Range(0, 0.2))             = 0.02
        _KeySoftness   ("Key Edge Softness", Range(0, 0.1))         = 0.006

        [Header(Idle Motion)]
        _BobAmplitude   ("Bob Amplitude (m)", Range(0, 0.25))       = 0.035
        _BobFrequency   ("Bob Frequency (Hz)", Range(0, 3))         = 0.8
        _IdleVariety    ("Idle Variety (0..1)", Range(0, 1))        = 0.6
        _IdlePeriod     ("Idle Reshuffle Period (s)", Range(2, 30)) = 8

        [Header(Cheer)]
        // Extra motion when the crowd is excited (a boundary or a wicket). Driven at runtime by
        // the global _CrowdExcitement, so these are the ceilings the cheer ramps up toward.
        _CheerBobGain   ("Cheer Bob Gain", Range(0, 6))             = 3.5
        _CheerFreqGain  ("Cheer Freq Gain", Range(0, 4))            = 1.5
        _JumpHeight     ("Cheer Jump Height (m)", Range(0, 1))      = 0.35
        _JumpFrequency  ("Cheer Jump Freq (Hz)", Range(0, 4))       = 1.6

        [Header(Wave)]
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
            // Alpha-tested, not blended: we clip the brown background but still write depth and
            // render in one pass with no sorting - the cheap way to key out a background on a
            // tile GPU. The stands (opaque, Geometry queue) draw first, so they show through.
            "RenderType"        = "TransparentCutout"
            "RenderPipeline"    = "UniversalPipeline"
            "Queue"             = "AlphaTest"
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
                float4 _KeyColor;
                float  _KeyTolerance;
                float  _KeySoftness;
                float4 _FlashColor;
                float  _BobAmplitude;
                float  _BobFrequency;
                float  _IdleVariety;
                float  _IdlePeriod;
                float  _CheerBobGain;
                float  _CheerFreqGain;
                float  _JumpHeight;
                float  _JumpFrequency;
                float  _WaveWidth;
                float  _WaveHeight;
                float  _WaveRowLag;
                float  _FlashStrength;
                float  _FlashDensity;
                float  _FlashRate;
                float  _PeoplePerRepeat;
            CBUFFER_END

            // Runtime drive, set as GLOBAL uniforms by CrowdController (never per-material, so the
            // SRP Batcher keeps batching and both stands react from one Shader.SetGlobalFloat).
            //   _CrowdExcitement  0..1  overall cheer level (bob gain, freq, jumping, flashes)
            //   _CrowdWaveEnabled 0/1   gate for the travelling wave
            //   _CrowdWavePhase   rad   moving centre of the wave, swept by the controller
            //   _CrowdFlash       0..1  camera-flash burst amount (night)
            float _CrowdExcitement;
            float _CrowdWaveEnabled;
            float _CrowdWavePhase;
            float _CrowdFlash;

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

                float exc = saturate(_CrowdExcitement);
                float phase = IN.color.r * CROWD_TWO_PI;
                float angle = IN.color.b * CROWD_TWO_PI;

                // --- randomised idle: each arc of the stand gets its own beat, reshuffled every
                // _IdlePeriod seconds, so the resting crowd never bobs in lockstep. Costs one hash.
                float section = floor(IN.color.b * 24.0);           // ~24 arcs around the bowl
                float epoch   = floor(_Time.y / max(_IdlePeriod, 0.5));
                float rnd     = CrowdHash21(float2(section, epoch)); // 0..1, stable within an epoch
                float idleAmp  = 1.0 + (rnd - 0.5) * 2.0 * _IdleVariety;   // 1 +/- variety
                float idleFreq = 1.0 + (rnd - 0.5) * _IdleVariety;
                float idlePhase = phase + rnd * CROWD_TWO_PI;

                // --- bob: whole quad moves together, so nobody shears. Amplitude and speed both
                // ramp up with excitement toward the cheer ceilings. ---
                float amp  = _BobAmplitude * idleAmp * (1.0 + _CheerBobGain * exc);
                float freq = _BobFrequency * idleFreq * (1.0 + _CheerFreqGain * exc);
                float bob  = sin(_Time.y * freq * CROWD_TWO_PI + idlePhase) * amp;

                // --- jump: at high excitement they hop upward (rectified sine, phase-staggered so
                // it reads as a bouncing crowd rather than one synchronised leap). ---
                float hop = max(0.0, sin(_Time.y * _JumpFrequency * CROWD_TWO_PI + idlePhase));
                float jump = hop * hop * _JumpHeight * exc;

                // --- travelling wave: gaussian bump centred on the global _CrowdWavePhase ---
                float centre = _CrowdWavePhase + IN.color.g * _WaveRowLag;
                float d = abs(angle - centre);
                d = min(d, CROWD_TWO_PI - d);                       // shortest way round
                float wave = exp(-(d * d) / (2.0 * _WaveWidth * _WaveWidth))
                             * _WaveHeight * saturate(_CrowdWaveEnabled);

                positionOS.y += bob + jump + wave;

                OUT.positionCS = TransformObjectToHClip(positionOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 CrowdFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 raw = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Key out the flat brown background so the stands behind show through. Distance in
                // (linear) colour space to _KeyColor; anything within _KeyTolerance is discarded,
                // with a short soft edge to take the jaggies off the silhouettes.
                float keyDist = distance(raw.rgb, _KeyColor.rgb);
                float coverage = smoothstep(_KeyTolerance, _KeyTolerance + _KeySoftness, keyDist);
                clip(coverage - 0.5);

                half4 albedo = raw * _BaseColor;

                // Occasional camera flashes, one person at a time. A cheer (_CrowdFlash) fires many
                // more at once and a little brighter. Free when both strengths are 0 (e.g. daytime).
                float flashStrength = _FlashStrength + _CrowdFlash * 2.0;
                if (flashStrength > 0.0)
                {
                    float2 cell = floor(float2(IN.uv.x * _PeoplePerRepeat, IN.uv.y * 64.0));
                    float t = floor(_Time.y * _FlashRate);
                    float r = CrowdHash21(cell + t * 17.13);
                    // Cheering drops the threshold, so a much larger fraction of the crowd flashes.
                    float density = _FlashDensity - saturate(_CrowdFlash) * 0.05;
                    float flash = step(density, r) * flashStrength;
                    albedo.rgb += _FlashColor.rgb * flash;
                }

                return albedo;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
