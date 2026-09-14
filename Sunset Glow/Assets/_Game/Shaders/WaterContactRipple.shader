Shader "SunsetGlow/WaterContactRipple"
{
    Properties
    {
        [HideInInspector] _AffectDeformation("Deformation", Float) = 1
        [HideInInspector] _AffectsFoam("Foam", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "ShaderGraphTargetId"="WaterDecalSubTarget" }
        Pass
        {
            Name "DeformationAndFoam"
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(id);
                o.uv = GetFullScreenTriangleTexCoord(id);
                return o;
            }
            float4 Frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2.0 - 1.0);
                // Signed, zero-at-edge concentric height pulses. HDRP derives the real water normal.
                float envelope = smoothstep(.12, .32, r) * (1.0 - smoothstep(.7, .98, r));
                float height = sin(r * 25.132741) * envelope;
                return float4(height, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
