Shader "Custom/BiomeBlendArrayShader"
{
    Properties
    {
        _BiomeTexArray ("Biome Texture Array", 2DArray) = "white" {}
        _WorldUVScale ("World UV Scale", Float) = 0.02
        _TriplanarSharpness ("Triplanar Sharpness", Float) = 4.0
        _BiomeCount ("Biome Count", Int) = 1
        _UnderwaterIndex ("Underwater Index", Int) = 0
        _MountainIndex ("Mountain Index", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WorldUVScale;
                float _TriplanarSharpness;
                int _BiomeCount;
                int _UnderwaterIndex;
                int _MountainIndex;
            CBUFFER_END

            TEXTURE2D_ARRAY(_BiomeTexArray);
            SAMPLER(sampler_BiomeTexArray);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1; // mesh.uv2
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float2 biomeIdx : TEXCOORD4;
                float3 weights : TEXCOORD5; // x=secondaryWeight, y=underwaterAlpha, z=mountainAlpha
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.biomeIdx = input.uv1;
                output.weights = float3(saturate(input.color.r), saturate(input.color.g), saturate(input.color.b));
                return output;
            }

            half3 SampleTriplanarArray(float3 posWS, float3 nWS, int layer)
            {
                float3 an = abs(nWS);
                float sharp = max(_TriplanarSharpness, 0.0001);
                an = pow(an, sharp);
                float denom = max(an.x + an.y + an.z, 1e-6);
                float3 w = an / denom;

                float2 uvX = posWS.zy * _WorldUVScale;
                float2 uvY = posWS.xz * _WorldUVScale;
                float2 uvZ = posWS.xy * _WorldUVScale;

                float l = (float)layer;
                half3 cx = SAMPLE_TEXTURE2D_ARRAY(_BiomeTexArray, sampler_BiomeTexArray, uvX, l).rgb;
                half3 cy = SAMPLE_TEXTURE2D_ARRAY(_BiomeTexArray, sampler_BiomeTexArray, uvY, l).rgb;
                half3 cz = SAMPLE_TEXTURE2D_ARRAY(_BiomeTexArray, sampler_BiomeTexArray, uvZ, l).rgb;
                return cx * w.x + cy * w.y + cz * w.z;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 nWS = normalize(input.normalWS);

                int maxLayer = max(_BiomeCount - 1, 0);
                int primaryIdx = clamp((int)round(input.biomeIdx.x), 0, maxLayer);
                int secondaryIdx = clamp((int)round(input.biomeIdx.y), 0, maxLayer);

                half3 cP = SampleTriplanarArray(input.positionWS, nWS, primaryIdx);
                half3 cS = SampleTriplanarArray(input.positionWS, nWS, secondaryIdx);

                half secondaryW = saturate(input.weights.x);
                half underwaterA = saturate(input.weights.y);
                half mountainA = saturate(input.weights.z);

                half3 albedo = lerp(cP, cS, secondaryW);

                if (_UnderwaterIndex >= 0)
                {
                    int uwIdx = clamp(_UnderwaterIndex, 0, maxLayer);
                    half3 cU = SampleTriplanarArray(input.positionWS, nWS, uwIdx);
                    albedo = lerp(albedo, cU, underwaterA);
                }

                if (_MountainIndex >= 0)
                {
                    int mIdx = clamp(_MountainIndex, 0, maxLayer);
                    half3 cM = SampleTriplanarArray(input.positionWS, nWS, mIdx);
                    albedo = lerp(albedo, cM, mountainA);
                }

                Light mainLight = GetMainLight(input.shadowCoord);
                half ndotl = saturate(dot(nWS, mainLight.direction));
                half3 direct = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation) * ndotl;
                half3 ambient = SampleSH(nWS);
                half3 rgb = albedo * (direct + ambient);
                rgb = MixFog(rgb, input.fogFactor);

                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
