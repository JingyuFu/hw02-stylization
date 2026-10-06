//UNITY_SHADER_NO_UPGRADE
#ifndef MIO_DREAM_PALETTE_INCLUDED
#define MIO_DREAM_PALETTE_INCLUDED
#include "MIOLighting.hlsl"

float DreamHash(float3 p) { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
float DreamNoise(float3 p)
{
    float3 i=floor(p),f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(lerp(DreamHash(i),DreamHash(i+float3(1,0,0)),f.x),
        lerp(DreamHash(i+float3(0,1,0)),DreamHash(i+float3(1,1,0)),f.x),f.y),
        lerp(lerp(DreamHash(i+float3(0,0,1)),DreamHash(i+float3(1,0,1)),f.x),
        lerp(DreamHash(i+float3(0,1,1)),DreamHash(i+1),f.x),f.y),f.z);
}
float3 DreamPaint(float light, float3 baseColor, float3 highlight, float3 shadow,
    float3 accent, float3 textureColor, float textureStrength, float variation, float wash)
{
    // Hue changes with illumination and pigment pooling, rather than scaling RGB.
    float coolPool=smoothstep(.28,.72,wash)*(1-smoothstep(.42,.88,light));
    float warmBloom=smoothstep(.42,.75,1-wash)*smoothstep(.35,.85,light);
    float3 color=lerp(baseColor,accent,coolPool*variation);
    color=lerp(color,highlight,warmBloom*variation*.6);
    // A user-supplied RGB pigment map contributes chroma while its value is
    // remapped through warm highlights / cool shadows to respect lighting.
    float3 painted=lerp(shadow,textureColor,smoothstep(.02,.80,light)*.8+.2);
    color=lerp(color,painted,saturate(textureStrength));
    float pooling=smoothstep(.57,.73,wash)*(1-smoothstep(.68,.92,wash));
    return lerp(color,lerp(shadow,accent,.25),pooling*variation*.14);
}
void DreamPalette_float(float Diffuse,float Unshadowed,float3 Highlight,float3 Midtone,
    float3 Shadow,float ShadowThreshold,float HighlightThreshold,float ThreeBands,float Smoothness,
    float3 PositionOS,float3 Pigment,float3 Accent,float TextureStrength,float Variation,float Scale,
    out float3 Color,out float3 UnshadowedColor)
{
    float3 p=PositionOS*max(.1,Scale);
    float3 warp=float3(DreamNoise(p+7),DreamNoise(p+31),DreamNoise(p+53));
    float wash=.65*DreamNoise(p+warp*.8)+.25*DreamNoise(p*2.07+11)+.1*DreamNoise(p*4.13+19);
    Color=ToonPalette(Diffuse,Highlight,Midtone,Shadow,ShadowThreshold,HighlightThreshold,ThreeBands,Smoothness);
    UnshadowedColor=ToonPalette(Unshadowed,Highlight,Midtone,Shadow,ShadowThreshold,HighlightThreshold,ThreeBands,Smoothness);
    Color=DreamPaint(Diffuse,Color,Highlight,Shadow,Accent,Pigment,TextureStrength,Variation,wash);
    UnshadowedColor=DreamPaint(Unshadowed,UnshadowedColor,Highlight,Shadow,Accent,Pigment,TextureStrength,Variation,wash);
}
#endif
