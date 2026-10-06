Shader "Hidden/MIO/Fixed Edge Mask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Fixed Roberts Cross Detection"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "MIOOutlines.hlsl"
            float _DepthThreshold, _NormalThreshold, _WobblePixels, _WobbleSpeed, _SketchFPS, _PreviewTime;
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 offset = MIOWobble(uv, _Time.y, _WobblePixels, _WobbleSpeed, _SketchFPS, _PreviewTime);
                // These detection footprints stay fixed when the exposed line widths change.
                return float4(MIODepthEdge(uv + offset, 1.5, _DepthThreshold),
                              MIONormalEdge(uv, 1.0, _NormalThreshold), 0, 1);
            }
            ENDHLSL
        }
    }
}
