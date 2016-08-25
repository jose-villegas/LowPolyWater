using UnityEngine;
using System.Collections;
using System;

public class LowPolyWater : MonoBehaviour
{
	[SerializeField]
	private float _height = 0;
	[SerializeField]
	private float _timescale = 1.0f;
	[SerializeField]
	private SineWaves _sineWaves;

	// Use this for initialization
	void Start ()
	{
		// no waves info
		if (_sineWaves == null) {
			enabled = false;
			return;
		}

		var material = Resources.Load("Materials/LowPolyWater", typeof(Material)) as Material;

		if (!material) {
			return;
		}
		// set general uniforms
		material.SetInt("_Waves", _sineWaves.Length);
		material.SetFloat("_Height", _height);
		material.SetFloat("_TimeScale", _timescale);
		// set simulation waves parameters
		for (int i = 0; i < _sineWaves.Length; i++) {
			material.SetFloat("_Amplitude" + i, _sineWaves [i].amplitude);
			material.SetFloat("_Frequency" + i, _sineWaves[i].frequency);
			material.SetFloat("_Phase" + i, _sineWaves[i].phase);
			var dir = new Vector4(_sineWaves[i].travelDirection.x, _sineWaves[i].travelDirection.y, 0, 0);
			material.SetVector("_TravelDirection" + i, dir);
		}
	}
}
