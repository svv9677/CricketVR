Shader "CricketVR/PitchBlend"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _DetailAlbedoMap("Detail Albedo", 2D) = "gray" {}
        _DetailStrength("Detail Strength", Range(0,1)) = 0.5
        _Smoothness("Smoothness", Range(0,1)) = 0.0
        _Metallic("Metallic", Range(0,1)) = 0.0
        // Width (in 0..1 quad UV) of the alpha fade band on each edge.
        _FadeWidthX("Fade Width X (UV)", Range(0,0.5)) = 0.06
        _FadeWidthY("Fade Width Y (UV)", Range(0,0.5)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back
            // Depth bias so the (near-coplanar) pitch always wins the depth test
            // against the ground's baked pitch rectangle just below it. Lets the
            // pitch sit flush with the ground without z-fighting ("grey") at
            // grazing angles/distance.
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // Required for VR single-pass instanced (stereo) rendering. Without
            // this the base rendered fine in the (mono) editor scene view but went
            // fully transparent in Play mode under XR stereo.
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DetailAlbedoMap_ST;
                // NOTE: keep every UnityPerMaterial member as float/float4. Mixing
                // `half` scalars in here breaks SRP Batcher CBUFFER packing, which
                // made _FadeWidthX/_FadeWidthY read as garbage in Play mode and the
                // whole pitch base render fully transparent.
                float4 _BaseColor;
                float _DetailStrength;
                float _Smoothness;
                float _Metallic;
                float _FadeWidthX;
                float _FadeWidthY;
            CBUFFER_END

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailAlbedoMap); SAMPLER(sampler_DetailAlbedoMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;   // raw quad UV (0..1) for fade
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                // Use the mesh UV0. The base uses a corrected mesh
                // (PitchTop_BaseUV) whose UV0 is a clean uniform 0..1 planar map,
                // fixing the original blocky tiling. IMPORTANT: do NOT derive UVs
                // from positionOS here — the base is Batching Static, so under
                // static batching positionOS is baked to world space and the UV
                // (and edge fade) would break in Play mode.
                OUT.uv         = IN.uv;

                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(OUT.normalWS, OUT.vertexSH);
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half EdgeAlpha(float2 uv)
            {
                float ex = min(uv.x, 1.0 - uv.x);
                float ey = min(uv.y, 1.0 - uv.y);
                float ax = (_FadeWidthX <= 1e-5) ? 1.0 : saturate(ex / _FadeWidthX);
                float ay = (_FadeWidthY <= 1e-5) ? 1.0 : saturate(ey / _FadeWidthY);
                float a = min(ax, ay);
                return smoothstep(0.0, 1.0, a);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 baseUV = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV) * _BaseColor;

                float2 detUV = IN.uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                half3 det = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detUV).rgb;
                half3 albedo = baseTex.rgb * lerp(half3(1.0, 1.0, 1.0), det * 2.0, _DetailStrength);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(IN.lightmapUV);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1.0;
                // Surface texture alpha is ignored on purpose: only the edge
                // feather controls transparency, so the interior stays fully
                // opaque and hides the ground's baked-in markings.
                surfaceData.alpha = EdgeAlpha(IN.uv);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = surfaceData.alpha;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
