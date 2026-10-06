//UNITY_SHADER_NO_UPGRADE
#ifndef MIO_GRAPHITE_INCLUDED
#define MIO_GRAPHITE_INCLUDED
#include "MIOMemoryPaper.hlsl"
#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
TEXTURE2D(_MIONormalBuffer);
#endif

float MGStroke(float2 p, float2 direction, float spacing, float width, float seed)
{
    float2 tangent=float2(-direction.y,direction.x);
    float along=dot(p,tangent);
    float bend=(MPNoise(float2(along*.023,seed))-.5)*1.6;
    bend+=.22*sin(along*.11+seed);
    float v=dot(p,direction)+bend+seed*3.7;
    float distance=abs(frac(v/spacing)-.5)*spacing;
    float aa=max(.3,fwidth(v)*.55);
    float stroke=1-smoothstep(width*.5-aa,width*.5+aa,distance);
    float dry=.72+.28*MPNoise(p*.48+seed);
    return stroke*dry;
}
void MIOGraphite_float(float3 Source, float2 UV, float Contrast, float HatchSpacing,
    float HatchStrength, float PencilWidth, float PaperStrength, out float3 Out)
{
    Out=Source;
    #ifndef SHADERGRAPH_PREVIEW
    float3 rgb=MPDisplay(Source);
    float luminance=saturate((dot(rgb,float3(.2126,.7152,.0722))-.5)*Contrast+.5);
    float alpha=SAMPLE_TEXTURE2D_LOD(_MIONormalBuffer,sampler_LinearClamp,UV,0).a;
    float body=smoothstep(.6,.95,alpha);
    float filament=smoothstep(.03,.20,alpha)*(1-smoothstep(.28,.70,alpha));
    float contour=1-smoothstep(.22,.34,luminance);
    float2 p=UV*float2(_ScreenParams.x/_ScreenParams.y,1)*1080;
    float spacing=max(3,HatchSpacing);

    // Four luminance thresholds produce five graphite value bands. This is a
    // new tonal construction, not a desaturation of the color-paper material.
    float tone=.18+.28*smoothstep(.21,.25,luminance)
                   +.25*smoothstep(.41,.45,luminance)
                   +.16*smoothstep(.63,.67,luminance)
                   +.10*smoothstep(.82,.86,luminance);
    float shade=saturate((.93-luminance)/.72);
    float primary=MGStroke(p,float2(.574,.819),spacing,PencilWidth,2.3);
    float crossing=MGStroke(p,float2(-.743,.669),spacing*1.23,PencilWidth*.85,8.1);
    float deep=MGStroke(p,float2(.985,.174),spacing*1.43,PencilWidth*.72,14.8);
    float hatch=primary*smoothstep(.06,.20,shade)*.83;
    hatch=max(hatch,crossing*smoothstep(.34,.58,shade)*.83);
    hatch=max(hatch,deep*smoothstep(.69,.86,shade)*.92);
    tone=lerp(tone,.105,saturate(hatch*HatchStrength));

    // A quiet, pale sketchbook background retains the original painted motifs.
    float background=saturate(.927+(luminance-.75)*.25);
    float graphite=lerp(background,tone,max(body,contour));
    // The ivory ribbons become pencil lines so their silhouettes survive on white paper.
    graphite=lerp(graphite,.29+.065*MPNoise(p*.17),filament);

    float grain=.58*MPNoise(p*.81)+.27*MPNoise(p*.37+13)+.15*MPHash(floor(p*1.53));
    float tooth=smoothstep(.49,.78,grain);
    float fiber=MPNoise(float2(p.x*.047,p.y*.87));
    float papered=lerp(graphite,.98,tooth*.23);
    papered*=1-(1-grain)*.035-(fiber-.5)*.026;
    papered+=(MPWash(p*.0083+73)-.5)*.018;
    graphite=lerp(graphite,papered,saturate(PaperStrength));
    Out=MPLinear(saturate(graphite).xxx);
    #endif
}
#endif
