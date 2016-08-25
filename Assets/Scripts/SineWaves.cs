using UnityEngine;
using System.Collections;
using System;

[CreateAssetMenu(fileName = "Data", menuName = "Custom/Sine Waves", order = 1)]
public class SineWaves : ScriptableObject 
{
	[Serializable]
	public struct SineWave
	{
		public float amplitude;
		public float frequency;
		public float phase;
		public Vector2 travelDirection;
	}

	[SerializeField, Range(1, 8)]
	private int _waves = 4;
	[SerializeField]
	private SineWave[] _wavesInfo;

	[InspectorButton("AssignRandomDirections")]
	public bool assignRandomDirections;

	public SineWave this[int index] 
	{
		get { return _wavesInfo[index]; }
	}

	public int Length
	{
		get { return _wavesInfo.Length; }
	}

	void OnValidate()
	{
		if(_wavesInfo == null) _wavesInfo = new SineWave[_waves];

		if (_wavesInfo.Length != _waves) 
		{
			Array.Resize(ref _wavesInfo, _waves);
		}
	}
		
	void AssignRandomDirections()
	{
		for (int i = 0; i < _wavesInfo.Length; i++) 
		{
			var angle = UnityEngine.Random.Range(-Mathf.PI / 3.0f, Mathf.PI / 3.0f);
			_wavesInfo[i].travelDirection.x = Mathf.Cos(angle);
			_wavesInfo[i].travelDirection.y = Mathf.Sin(angle);
		}
	}
}
