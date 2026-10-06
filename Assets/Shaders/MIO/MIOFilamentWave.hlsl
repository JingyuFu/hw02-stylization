//UNITY_SHADER_NO_UPGRADE
#ifndef MIO_FILAMENT_WAVE_INCLUDED
#define MIO_FILAMENT_WAVE_INCLUDED

// CurveData.xyz is the object-space gradient of normalized curve distance;
// CurveData.w is normalized distance from the attached root. UV0 stays available
// for surface texture sampling. Both end caps carry the same data as their ring.
void FilamentWave_float(float3 PositionOS, float3 NormalOS, float3 TangentOS,
    float4 CurveData, float Time, float Amplitude, float Speed, float WaveCount,
    float Phase, float Side, float PreviewTime,
    out float3 Position, out float3 Normal, out float3 Tangent)
{
    float time = PreviewTime >= 0.0 ? PreviewTime : Time;
    float u = saturate(CurveData.w);
    float s = saturate(u / 0.16);
    float mask = s * s * (3.0 - 2.0 * s);
    float maskDerivative = (u > 0.0 && u < 0.16) ? 6.0 * s * (1.0 - s) / 0.16 : 0.0;
    float frequency = clamp(WaveCount, 0.25, 2.0) * 6.283185307;
    float p = frequency * u - time * max(Speed, 0.0) + Phase;
    float side = Side < 0.0 ? -1.0 : 1.0;
    float3 wave = float3(0.35 * side * sin(p + 1.1), sin(p), 0.65 * cos(p + 0.4));
    float3 derivative = frequency * float3(0.35 * side * cos(p + 1.1), cos(p), -0.65 * sin(p + 0.4));
    float amplitude = clamp(Amplitude, 0.0, 0.3);
    Position = PositionOS + amplitude * mask * wave;

    // Inverse-transpose of J = I + (dOffset/du) (grad u)^T.
    // Updating the basis keeps lighting and later normal-buffer outlines in sync.
    float3 d = amplitude * (maskDerivative * wave + mask * derivative);
    float3 g = CurveData.xyz;
    float denominator = max(0.1, 1.0 + dot(g, d));
    Normal = normalize(NormalOS - g * (dot(d, NormalOS) / denominator));
    Tangent = normalize(TangentOS + d * dot(g, TangentOS));
}

#endif
