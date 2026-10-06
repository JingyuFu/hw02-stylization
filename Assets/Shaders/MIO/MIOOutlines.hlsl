//UNITY_SHADER_NO_UPGRADE
#ifndef MIO_OUTLINES_INCLUDED
#define MIO_OUTLINES_INCLUDED
#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
TEXTURE2D(_MIONormalBuffer);
SAMPLER(sampler_MIONormalBuffer);
float4 _MIONormalBuffer_TexelSize;
TEXTURE2D(_MIOFixedEdgeMask);
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
float MIOEyeDepth(float2 uv)
{
    float raw = SampleSceneDepth(saturate(uv));
    if (unity_OrthoParams.w > 0.5)
    {
        #if UNITY_REVERSED_Z
        raw = 1.0 - raw;
        #endif
        return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
    }
    return LinearEyeDepth(raw, _ZBufferParams);
}
float4 MIONormal(float2 uv)
{
    float4 s = SAMPLE_TEXTURE2D_LOD(_MIONormalBuffer, sampler_MIONormalBuffer, saturate(uv), 0);
    return float4(s.xyz * 2.0 - 1.0, s.a);
}
float MIOHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float MIONoise(float2 p)
{
    float2 i = floor(p), f = frac(p); f = f*f*(3.0-2.0*f);
    return lerp(lerp(MIOHash(i), MIOHash(i+float2(1,0)), f.x), lerp(MIOHash(i+float2(0,1)), MIOHash(i+1), f.x), f.y);
}
float2 MIOWobble(float2 uv, float time, float pixels, float speed, float fps, float preview)
{
    time = preview >= 0 ? preview : time;
    time = floor(time * max(fps,1.0)) / max(fps,1.0);
    float2 p = uv * _MIONormalBuffer_TexelSize.zw;
    float2 n = float2(MIONoise(p/42.0 + time*speed), MIONoise(p/39.0 - time*speed + 19.7));
    return (n*2-1)*pixels*_MIONormalBuffer_TexelSize.xy;
}
float MIOWidenEdge(float2 uv, float width, float baseWidth, float2 channel)
{
    float2 texel = _MIONormalBuffer_TexelSize.xy;
    float radius = max(0, (width-baseWidth)*0.5);
    float result = dot(SAMPLE_TEXTURE2D_LOD(_MIOFixedEdgeMask, sampler_LinearClamp, uv, 0).rg, channel);
    // Dilation of an already detected mask, rather than a wider depth/normal filter.
    [unroll] for (int y=-1; y<=1; y++) [unroll] for (int x=-1; x<=1; x++)
    {
        float2 direction = float2(x,y) * ((x != 0 && y != 0) ? 0.707107 : 1.0);
        float edge = dot(SAMPLE_TEXTURE2D_LOD(_MIOFixedEdgeMask, sampler_LinearClamp, saturate(uv+direction*radius*texel), 0).rg, channel);
        result = max(result, edge);
    }
    return result*saturate(width/max(baseWidth,0.01));
}
float MIODepthEdge(float2 uv, float width, float threshold)
{
    float2 h = _MIONormalBuffer_TexelSize.xy * max(width, 0.0) * 0.5;
    float a = MIOEyeDepth(uv + float2(-h.x,-h.y)), b = MIOEyeDepth(uv + float2(h.x,h.y));
    float c = MIOEyeDepth(uv + float2(-h.x,h.y)), d = MIOEyeDepth(uv + float2(h.x,-h.y));
    float difference = sqrt((a-b)*(a-b) + (c-d)*(c-d));
    float relative = difference / max(0.1, min(min(a,b),min(c,d)));
    float4 masks = float4(MIONormal(uv+float2(-h.x,-h.y)).w, MIONormal(uv+float2(h.x,h.y)).w,
                         MIONormal(uv+float2(-h.x,h.y)).w, MIONormal(uv+float2(h.x,-h.y)).w);
    float nearest = min(min(a,b),min(c,d));
    float eligibility = nearest == a ? masks.x : nearest == b ? masks.y : nearest == c ? masks.z : masks.w;
    // At MSAA silhouettes, fall back to an adjacent covered normal sample.
    if (eligibility < 0.1) eligibility = max(max(masks.x,masks.y),max(masks.z,masks.w));
    float t = max(threshold, 0.00001);
    return smoothstep(t, t * 1.6, relative) * step(0.01, width) * step(0.5, eligibility);
}
float MIONormalEdge(float2 uv, float width, float threshold)
{
    float2 h = _MIONormalBuffer_TexelSize.xy * max(width, 0.0) * 0.5;
    float4 a = MIONormal(uv + float2(-h.x,-h.y)), b = MIONormal(uv + float2(h.x,h.y));
    float4 c = MIONormal(uv + float2(-h.x,h.y)), d = MIONormal(uv + float2(h.x,-h.y));
    float difference = sqrt(dot(a.xyz-b.xyz,a.xyz-b.xyz) + dot(c.xyz-d.xyz,c.xyz-d.xyz));
    // Internal creases only: the independent depth edge handles the silhouette.
    float valid = step(0.5,min(min(a.w,b.w),min(c.w,d.w))), t = max(threshold, 0.00001);
    return smoothstep(t, t * 1.5, difference) * valid * step(0.01, width);
}
#endif
void MIOOutline_float(float3 Source, float2 UV, float Time, float3 InkColor,
    float Strength, float DepthWidth, float NormalWidth, float DepthThreshold,
    float NormalThreshold, float DepthStrength, float NormalStrength,
    float WobblePixels, float WobbleSpeed, float SketchFPS, float PreviewTime,
    float DebugView, out float3 Out)
{
    #ifdef SHADERGRAPH_PREVIEW
    Out = Source;
    #else
    float depth = MIOWidenEdge(UV, DepthWidth, 1.5, float2(1,0));
    // Internal seams stay steady while the outer contour has stepped pencil wobble.
    float normal = MIOWidenEdge(UV, NormalWidth, 1.0, float2(0,1));
    float ink = max(depth * saturate(DepthStrength), normal * saturate(NormalStrength));
    Out = lerp(Source, InkColor, saturate(Strength) * ink);
    if (DebugView > 0.5 && DebugView < 1.5) Out = saturate(MIOEyeDepth(UV)/_ProjectionParams.z).xxx;
    else if (DebugView > 1.5 && DebugView < 2.5) { float4 n = MIONormal(UV); Out = (n.xyz*0.5+0.5)*step(0.1,n.w); }
    else if (DebugView > 2.5 && DebugView < 3.5) Out = depth.xxx;
    else if (DebugView > 3.5 && DebugView < 4.5) Out = normal.xxx;
    else if (DebugView > 4.5) Out = ink.xxx;
    #endif
}
#endif
