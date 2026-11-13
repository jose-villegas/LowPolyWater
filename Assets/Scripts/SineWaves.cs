using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Data", menuName = "Custom/Sine Waves", order = 1)]
public class SineWaves : ScriptableObject
{
    public enum WaveModel
    {
        /// <summary>Vertical sum of sines with sharpened crests (the original look).</summary>
        SumOfSines,
        /// <summary>Gerstner waves: vertices also move sideways, giving pointed crests.</summary>
        Gerstner,
    }

    [Serializable]
    public struct SineWave
    {
        public float amplitude;
        public float waveLength;
        public float speed;
        [Range(0, 360)]
        public float travelAngle;
        [Tooltip("Sum of sines only: crest sharpening exponent.")]
        public float sharpness;
    }

    [SerializeField]
    private WaveModel _model = WaveModel.SumOfSines;
    [SerializeField, Range(0, 1), Tooltip("Gerstner only: 0 = rolling sines, 1 = crests about to loop over.")]
    private float _steepness = 0.6f;
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

    public WaveModel Model
    {
        get { return _model; }
    }

    public float Steepness
    {
        get { return _steepness; }
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
