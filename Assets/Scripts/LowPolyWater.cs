using UnityEngine;
using System.Collections;
using System;

public class LowPolyWater : MonoBehaviour
{
	[SerializeField]
	private SineWaves _sineWaves = null;
	[SerializeField]
	private Material _lowPolyWater = null;

	// Use this for initialization
	void Start()
	{
		// no waves info
		if (_sineWaves == null) {
			enabled = false;
			return;
		}

		_lowPolyWater = Resources.Load("Materials/LowPolyWater", typeof(Material)) as Material;

		if (!_lowPolyWater) {
			enabled = false;
			return;
		}
			
		// set general uniforms
		_lowPolyWater.SetInt("_Waves", _sineWaves.Length);
        _lowPolyWater.SetFloat("_TimeScale", _sineWaves.Timescale);
		// set simulation waves parameters
		for (int i = 0; i < _sineWaves.Length; i++) {
			var vals = new Vector4(_sineWaves[i].amplitude, _sineWaves[i].frequency,
				           _sineWaves[i].phase, _sineWaves[i].travelAngle * Mathf.Deg2Rad);
			_lowPolyWater.SetVector("_SineWave" + i, vals);
		}
	}

	void Update()
	{
	}
}
