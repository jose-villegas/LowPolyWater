using UnityEngine;
using System.Collections;
using System;

[RequireComponent(typeof(MeshFilter), typeof(Renderer))]
public class LowPolyWater : MonoBehaviour
{
    [SerializeField]
    private SineWaves _sineWaves = null;
    [SerializeField, Tooltip("Useful for testing sine wave setups")]
    private bool _updateMaterialPerFrame;
    private Material _lowPolyWater = null;

    // Use this for initialization
    void Start()
    {
        // no waves info
        if (_sineWaves == null)
        {
            enabled = false;
            return;
        }

        _lowPolyWater = GetComponent<Renderer>().material;

        if (!_lowPolyWater)
        {
            enabled = false;
            return;
        }
			
        // set general uniforms
        _lowPolyWater.SetInt("_Waves", _sineWaves.Length);
        _lowPolyWater.SetFloat("_TimeScale", _sineWaves.Timescale);
        // set simulation waves parameters
        for (int i = 0; i < _sineWaves.Length; i++)
        {
            var a = _sineWaves[i].amplitude;
            var f = 2.0f * Mathf.PI / _sineWaves[i].waveLength;
            var p = _sineWaves[i].speed * f;
            float radA = _sineWaves[i].travelAngle * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Sin(radA), Mathf.Cos(radA));
            var s = _sineWaves[i].sharpness;
            _lowPolyWater.SetVector("_SineWave0" + i, new Vector4(a, f, p, 0));
            _lowPolyWater.SetVector("_SineWave1" + i, new Vector4(d.x, d.y, s, 0));
        }
    }

    void Update()
    {
        if (_updateMaterialPerFrame && Application.isPlaying)
        {
            // set general uniforms
            _lowPolyWater.SetInt("_Waves", _sineWaves.Length);
            _lowPolyWater.SetFloat("_TimeScale", _sineWaves.Timescale);
            // set simulation waves parameters
            for (int i = 0; i < _sineWaves.Length; i++)
            {
                var a = _sineWaves[i].amplitude;
                var f = 2.0f * Mathf.PI / _sineWaves[i].waveLength;
                var p = _sineWaves[i].speed * f;
                float radA = _sineWaves[i].travelAngle * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Sin(radA), Mathf.Cos(radA));
                var s = _sineWaves[i].sharpness;
                _lowPolyWater.SetVector("_SineWave0" + i, new Vector4(a, f, p, 0));
                _lowPolyWater.SetVector("_SineWave1" + i, new Vector4(d.x, d.y, s, 0));
            }
        }
    }
}
