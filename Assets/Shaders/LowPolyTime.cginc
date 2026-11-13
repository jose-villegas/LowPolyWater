#ifndef LOWPOLY_TIME_INCLUDED
#define LOWPOLY_TIME_INCLUDED

// Global clock override in _Time.x units (seconds / 20). Zero means use _Time.
// The editor capture tools set it so animation frames render deterministically.
float _LowPolyWaterTime;

// Seconds, from the override when it is set.
float LowPolySeconds()
{
    return _LowPolyWaterTime > 0 ? _LowPolyWaterTime * 20.0 : _Time.y;
}

#endif
