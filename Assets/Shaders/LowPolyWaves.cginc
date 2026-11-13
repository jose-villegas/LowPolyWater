#ifndef LOWPOLY_WAVES_INCLUDED
#define LOWPOLY_WAVES_INCLUDED

// Wave setup uploaded by LowPolyWater.cs from a SineWaves asset.
// _SineWave0: x = amplitude, y = angular frequency (2pi / wavelength),
//             z = phase speed (speed * frequency), w = Gerstner steepness Q
// _SineWave1: xy = travel direction, z = crest sharpness (sum of sines only)
// Reference: GPU Gems, chapter 1, "Effective Water Simulation from Physical Models".
float4 _SineWave0[8];
float4 _SineWave1[8];
int _Waves;
float _TimeScale;
// Global clock override in _Time.x units (seconds / 20). Zero means use _Time;
// the editor capture tools set it to render animation frames deterministically.
float _LowPolyWaterTime;

float WavePhase(int i, float2 xz)
{
    float time = _LowPolyWaterTime > 0 ? _LowPolyWaterTime : _Time.x;
    return dot(_SineWave1[i].xy, xz) * _SineWave0[i].y + time * _TimeScale * _SineWave0[i].z;
}

// Displaces a point of the flat water mesh (object space, surface on the XZ plane).
float3 DisplaceWater(float3 p)
{
    float3 offset = 0;
    for (int i = 0; i < _Waves; i++)
    {
        float A = _SineWave0[i].x;
        float t = WavePhase(i, p.xz);
    #if defined(_WAVES_GERSTNER)
        // Gerstner: points orbit, bunching vertices into sharp crests and wide troughs.
        // Lifted by A so the surface spans [0, 2A] like the sum of sines below,
        // which keeps the shoreline where the scene was built for.
        float Q = _SineWave0[i].w;
        offset.xz += Q * A * _SineWave1[i].xy * cos(t);
        offset.y += A * (sin(t) + 1.0);
    #else
        // Sum of sines with sharpened crests (the original model).
        offset.y += 2.0 * A * pow((sin(t) + 1.0) * 0.5, _SineWave1[i].z);
    #endif
    }
    return p + offset;
}

#endif
