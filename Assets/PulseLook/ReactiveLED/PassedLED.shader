Shader "PulseRun/PassedLED"
{
    Properties
    {
        [HDR] _IdleColor("Before passage", Color) = (1.5,0.08,0.7,1)
        [HDR] _PassedColor("After passage", Color) = (1.8,0.9,0.08,1)
        _ProgressZ("Passed position", Float) = -10000
        _OriginZ("Marker origin", Float) = 0
        _Period("Marker spacing", Float) = 4
        _DashLength("Marker length", Float) = 0.9
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _IdleColor;
                half4 _PassedColor;
                float _ProgressZ, _OriginZ, _Period, _DashLength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half fogFactor:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.fogFactor=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float period=max(.1,_Period);
                float index=floor((input.positionWS.z-_OriginZ+period*.5)/period);
                float center=_OriginZ+index*period;
                clip(_DashLength*.5-abs(input.positionWS.z-center));
                clip(center-_OriginZ+.001);
                float passed=smoothstep(0,.6,_ProgressZ-center);
                half3 color=lerp(_IdleColor.rgb,_PassedColor.rgb,passed);
                return half4(MixFog(color,input.fogFactor),1);
            }
            ENDHLSL
        }
    }
}
