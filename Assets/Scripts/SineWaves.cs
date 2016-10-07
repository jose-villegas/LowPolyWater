using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Data", menuName = "Custom/Sine Waves", order = 1)]
public class SineWaves : ScriptableObject
{
    [Serializable]
    public struct SineWave
    {
        public float amplitude;
        public float waveLength;
        public float speed;
        [Range(0, 360)]
        public float travelAngle;
        public float sharpness;
    }

    [SerializeField]
    private float _timescale = 1.0f;
    [SerializeField, Range(1, 8)]
    private int _waves = 4;
    [SerializeField]
    private SineWave[] _wavesInfo;

    [SerializeField, InspectorButton("AssignRandomDirections")]
    private bool _assignRandomDirections;

    public SineWave this [int index]
    {
        get { return _wavesInfo[index]; }
    }

    public float Timescale
    {
        get { return _timescale; }
    }

    public int Length
    {
        get { return _wavesInfo.Length; }
    }

    void OnValidate()
    {
        if (_wavesInfo == null)
            _wavesInfo = new SineWave[_waves];

        if (_wavesInfo.Length != _waves)
        {
            Array.Resize(ref _wavesInfo, _waves);
        }
    }

    void AssignRandomDirections()
    {
        for (int i = 0; i < _wavesInfo.Length; i++)
        {  
            _wavesInfo[i].travelAngle = UnityEngine.Random.Range(0, 360);
        }
    }
}
