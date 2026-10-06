//UNITY_SHADER_NO_UPGRADE
#ifndef MIO_MEMORY_PAPER_INCLUDED
#define MIO_MEMORY_PAPER_INCLUDED
#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#endif

// Original procedural artwork: soft washes and imagined paired-eye glyphs.
// No pixels are copied from the reference illustration.
float MPHash(float2 p)
{
    float3 p3=frac(float3(p.xyx)*0.1031);
    p3+=dot(p3,p3.yzx+33.33);
    return frac((p3.x+p3.y)*p3.z);
}
float MPNoise(float2 p)
{
    float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(MPHash(i),MPHash(i+float2(1,0)),f.x),lerp(MPHash(i+float2(0,1)),MPHash(i+1),f.x),f.y);
}
float MPWash(float2 p)
{
    return .55*MPNoise(p)+.27*MPNoise(p*2.03+7.1)+.13*MPNoise(p*4.07+19.8)+.05*MPNoise(p*8.11+37.2);
}
float3 MPLinear(float3 c)
{
    #ifdef UNITY_COLORSPACE_GAMMA
    return c;
    #else
    return SRGBToLinear(c);
    #endif
}
float3 MPDisplay(float3 c)
{
    #ifdef UNITY_COLORSPACE_GAMMA
    return c;
    #else
    return LinearToSRGB(c);
    #endif
}
float MPSegment(float2 p, float2 a, float2 b)
{
    float2 v=b-a;
    return length(p-a-v*saturate(dot(p-a,v)/dot(v,v)));
}
float MPGlyph(float2 p, float seed, float aa)
{
    // Slight bends make the repeated motif feel drawn, not typeset.
    p+=.018*float2(sin(p.y*12+seed*7),sin(p.x*9+seed*13));
    float d=min(abs(length(p-float2(-.20,-.18))-.14),abs(length(p-float2(.20,-.18))-.14));
    d=min(d,abs(length(p-float2(-.20,-.18))-.049));
    d=min(d,abs(length(p-float2(.20,-.18))-.049));
    d=min(d,MPSegment(p,float2(-.34,-.18),float2(-.34,.32)));
    d=min(d,MPSegment(p,float2(.34,-.18),float2(.34,.32)));
    d=min(d,MPSegment(p,float2(-.34,.32),float2(-.1,.37)));
    d=min(d,MPSegment(p,float2(-.1,.37),float2(.34,.32)));
    d=min(d,MPSegment(p,float2(-.07,.02),float2(-.07,.18)));
    d=min(d,MPSegment(p,float2(-.07,.18),float2(.12,.15)));
    return 1-smoothstep(.016-aa,.026+aa,d);
}
float MPGlyphField(float2 uv, float aspect, float scale, float seed, float density)
{
    float2 p=float2(uv.x*aspect,uv.y)*scale;
    float2 cell=floor(p); float result=0;
    [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
    {
        float2 id=cell+float2(x,y);
        float h=MPHash(id+seed), h2=MPHash(id+seed+36.2);
        float2 center=id+.5+.4*float2(h-.5,h2-.5);
        float bottom=1-saturate(center.y/scale);
        float likelihood=lerp(.13,.94,pow(bottom,1.6))*density;
        float size=lerp(.55,1.05,h2);
        float2 q=(p-center)/size;
        float angle=(h-.5)*.45, cs=cos(angle), sn=sin(angle);
        q=float2(cs*q.x-sn*q.y,sn*q.x+cs*q.y);
        // Derivatives of randomized cells would create visible grid seams.
        // Compute antialiasing from the known pixel footprint instead.
        float glyph=MPGlyph(q,h,scale/(_ScreenParams.y*size)*.75)*step(h,likelihood);
        result=max(result,glyph*lerp(.45,1,h2));
    }
    return result;
}
void MIOMemoryBackdrop_float(float3 Source, float2 UV, float Strength, float WashStrength,
    float GlyphStrength, float GlyphDensity, out float3 Out)
{
    Out=Source;
    #ifndef SHADERGRAPH_PREVIEW
    // This pass is drawn before opaque geometry, preserving native MSAA coverage.
    if(Strength<=0) return;
    float aspect=_ScreenParams.x/_ScreenParams.y;
    float2 p=float2((UV.x-.5)*aspect,UV.y-.5);
    float2 warp=float2(MPWash(p*2.7+7),MPWash(p*2.7+41))-.5;
    float wash=MPWash(p*3.6+warp*1.5+12.9);
    float strokes=MPWash(float2(p.x*6.0+p.y*.9,p.y*2.6)+warp+6);
    float clouds=smoothstep(.27,.72,lerp(wash,strokes,.38));
    float3 sage=float3(.58,.70,.66), mint=float3(.76,.85,.73), cream=float3(.89,.91,.79);
    float3 color=lerp(sage,mint,clouds);
    float haze=exp(-dot(p*float2(2.6,1.45)+float2(.08,.22),p*float2(2.6,1.45)+float2(.08,.22)));
    color=lerp(color,cream,.56*haze);
    color=lerp(color,float3(.70,.84,.70),.30*smoothstep(.45,.02,UV.y));
    color=lerp(float3(.74,.83,.77),color,WashStrength);
    // Broad, dry brush breakup is underneath the final paper-grain pass.
    float chalk=MPNoise(p*155+warp*4)*.6+MPNoise(p*float2(57,211))*.4;
    color+=(chalk-.5)*.045*WashStrength;
    float glyphs=max(MPGlyphField(UV,aspect,7.6,3.1,GlyphDensity),
                     MPGlyphField(UV+float2(.016,.057),aspect,12.2,39.6,GlyphDensity)*.62);
    float quietCenter=lerp(1,.42,exp(-dot(p*float2(4.2,3),p*float2(4.2,3))));
    float broken=.55+.45*MPNoise(p*240);
    color=lerp(color,float3(.92,.95,.79),glyphs*GlyphStrength*quietCenter*broken);
    Out=lerp(Source,MPLinear(saturate(color)),saturate(Strength));
    #endif
}
void MIOPaper_float(float3 Source, float2 UV, float Strength, float GrainScale,
    float FiberStrength, out float3 Out)
{
    Out=Source;
    #ifndef SHADERGRAPH_PREVIEW
    if(Strength<=0) return;
    float2 p=UV*float2(_ScreenParams.x/_ScreenParams.y,1)*1080*max(.1,GrainScale);
    float grain=.56*MPNoise(p*.81)+.29*MPNoise(p*.37+13)+.15*MPHash(floor(p*1.53));
    float tooth=smoothstep(.49,.78,grain);
    float fibers=MPNoise(float2(p.x*.047,p.y*.87)+float2(MPNoise(p*.02)*2,0));
    float pulp=MPWash(p*.0083+73);
    float3 pigment=MPDisplay(Source);
    float3 paper=float3(.965,.961,.885);
    // Raised fibers catch less pigment; the recesses retain slightly darker pigment.
    float3 textured=lerp(pigment,paper,tooth*.31);
    textured*=1-(1-grain)*.075-(fibers-.5)*.055*FiberStrength;
    textured+=(pulp-.5)*.036;
    Out=lerp(Source,MPLinear(saturate(textured)),saturate(Strength));
    #endif
}
#endif
