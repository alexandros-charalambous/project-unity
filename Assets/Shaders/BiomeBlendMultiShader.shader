Shader "Custom/BiomeBlendMultiShader"
{
    Properties
    {
        _Tex0 ("Texture 0", 2D) = "white" {}
        _Tex1 ("Texture 1", 2D) = "white" {}
        _Tex2 ("Texture 2", 2D) = "white" {}
        _Tex3 ("Texture 3", 2D) = "white" {}
        _WorldUVScale ("World UV Scale", Float) = 0.02
        _TriplanarSharpness ("Triplanar Sharpness", Float) = 4.0
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
                float4 _Tex0_ST;
                float _WorldUVScale;
                float _TriplanarSharpness;
            CBUFFER_END

            TEXTURE2D(_Tex0); SAMPLER(sampler_Tex0);
            TEXTURE2D(_Tex1); SAMPLER(sampler_Tex1);
            TEXTURE2D(_Tex2); SAMPLER(sampler_Tex2);
            TEXTURE2D(_Tex3); SAMPLER(sampler_Tex3);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.color = input.color;
                return output;
            }

            half3 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 posWS, float3 nWS)
            {
                float3 an = abs(nWS);
                float sharp = max(_TriplanarSharpness, 0.0001);
                an = pow(an, sharp);
                float denom = max(an.x + an.y + an.z, 1e-6);
                float3 w = an / denom;

                float2 uvX = posWS.zy * _WorldUVScale; // project along X => YZ
                float2 uvY = posWS.xz * _WorldUVScale; // project along Y => XZ
                float2 uvZ = posWS.xy * _WorldUVScale; // project along Z => XY

                half3 cx = SAMPLE_TEXTURE2D(tex, samp, uvX).rgb;
                half3 cy = SAMPLE_TEXTURE2D(tex, samp, uvY).rgb;
                half3 cz = SAMPLE_TEXTURE2D(tex, samp, uvZ).rgb;
                return cx * w.x + cy * w.y + cz * w.z;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 w = saturate(input.color);
                half sum = w.r + w.g + w.b + w.a;
                if (sum > 0.0001h)
                {
                    w /= sum;
                }
                else
                {
                    w = half4(1, 0, 0, 0);
                }

                float3 nWS = normalize(input.normalWS);
                half3 c0 = SampleTriplanar(TEXTURE2D_ARGS(_Tex0, sampler_Tex0), input.positionWS, nWS);
                half3 c1 = SampleTriplanar(TEXTURE2D_ARGS(_Tex1, sampler_Tex1), input.positionWS, nWS);
                half3 c2 = SampleTriplanar(TEXTURE2D_ARGS(_Tex2, sampler_Tex2), input.positionWS, nWS);
                half3 c3 = SampleTriplanar(TEXTURE2D_ARGS(_Tex3, sampler_Tex3), input.positionWS, nWS);

                half3 albedo = c0 * w.r + c1 * w.g + c2 * w.b + c3 * w.a;

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
