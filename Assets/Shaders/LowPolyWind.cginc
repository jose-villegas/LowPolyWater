#ifndef LOWPOLY_WIND_INCLUDED
#define LOWPOLY_WIND_INCLUDED

#include "LowPolyTime.cginc"

// Wind field uploaded by LowPolyWind.cs.
// _LowPolyWind:       xy = direction the wind blows toward (world XZ, unit), z = strength,
//                     w = distance between gust fronts (m)
// _LowPolyWindMotion: x = speed the gust fronts travel downwind (m/s), y = flutter frequency
float4 _LowPolyWind;
float4 _LowPolyWindMotion;

// Gust intensity at a point, 0..1: fronts that roll downwind, broken up across it.
float WindGust(float2 xz)
{
    float2 dir = _LowPolyWind.xy;
    float k = 6.2831853 / max(_LowPolyWind.w, 1.0);
    float t = LowPolySeconds() * _LowPolyWindMotion.x * k;
    float along = dot(xz, dir) * k - t;
    float across = dot(xz, float2(-dir.y, dir.x)) * k * 0.37;
    float g = sin(along + 0.9 * sin(across + t * 0.21)) * 0.5 + 0.5;
    return g * g;
}

// World XZ sway of a point `height` m above a plant's base, per unit of bend weight.
// Plants lean a little in steady wind and further as each gust passes.
float2 WindSway(float2 baseXZ, float height)
{
    return _LowPolyWind.xy * (_LowPolyWind.z * (0.35 + WindGust(baseXZ)) * height * height);
}

#endif
