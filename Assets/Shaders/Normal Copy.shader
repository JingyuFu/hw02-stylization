Shader "Hidden/Normal Copy"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 viewNormal : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.viewNormal = COMPUTE_VIEW_NORMAL;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(normalize(i.viewNormal) * 0.5 + 0.5, 0);
            }
            ENDCG
        }
        // Resolve native signed world normals into the independent view-normal buffer.
        Pass
        {
            Name "ResolveViewNormals"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ResolveNormals
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_MIOLuminousMask);
            half4 ResolveNormals(Varyings input) : SV_Target
            {
                float3 n = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).xyz;
                float valid = step(0.1, dot(n, n));
                if (valid < 0.5) return 0;
                float3 viewNormal = TransformWorldToViewDir(n, true);
                float3 luminous = SAMPLE_TEXTURE2D_X(_MIOLuminousMask, sampler_PointClamp, input.texcoord).xyz;
                // Alpha: 0 background, .25 luminous geometry, 1 ink-eligible geometry.
                float eligibility = dot(luminous, luminous) > 0.1 ? 0.25 : 1.0;
                return float4(viewNormal * 0.5 + 0.5, eligibility);
            }
            ENDHLSL
        }
    }
}
