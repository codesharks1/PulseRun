Shader "PulseRun/GateEnergy"
{
    Properties
    {
        _BaseColor("Energy Color", Color) = (0.7,0.1,1.5,1)
        _FlowSpeed("Flow Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _FlowSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float edge=pow(saturate(1-min(input.uv.x,1-input.uv.x)*8),3);
                float scan=pow(saturate(sin(input.uv.y*35-_Time.y*_FlowSpeed*3)),18);
                float lattice=step(.985,frac(input.uv.x*14));
                float alpha=_BaseColor.a*(.012+.1*edge+.035*scan+.015*lattice);
                return half4(_BaseColor.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
