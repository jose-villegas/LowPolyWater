using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(Renderer))]
public class LowPolyWater : MonoBehaviour
{
    [SerializeField]
    private SineWaves _sineWaves = null;
    [SerializeField, Tooltip("Useful for testing sine wave setups")]
    private bool _updateMaterialPerFrame = false;
    [Header("Quad Detail"), SerializeField, InspectorButton("SubdivideMesh")]
    private bool _subdivideMesh;

    private Material _lowPolyWater = null;
#if UNITY_5_4_OR_NEWER
	private Vector4[] _SineWave0;
	private Vector4[] _SineWave1;
#endif

    /// <summary>
    /// Subdivides each quad of the mesh for smaller quads
    /// </summary>
    private void SubdivideMesh()
    {
        Mesh current = GetComponent<MeshFilter>().sharedMesh;
        Mesh newMesh = Instantiate(current);
        newMesh.name = current.name;
        MeshHelper.Subdivide4(newMesh);
        GetComponent<MeshFilter>().sharedMesh = newMesh;
    }

    // Use this for initialization
    private void Start()
    {
        // no waves info
        if (_sineWaves == null)
        {
            enabled = false;
            return;
        }

        _lowPolyWater = GetComponent<Renderer>().sharedMaterial;

        if (!_lowPolyWater)
        {
            enabled = false;
            return;
        }
			
        // set general uniforms
        _lowPolyWater.SetInt("_Waves", _sineWaves.Length);
        _lowPolyWater.SetFloat("_TimeScale", _sineWaves.Timescale);
#if UNITY_5_4_OR_NEWER
		// reserve space for uniform array
		_SineWave0 = new Vector4[_sineWaves.Length];
		_SineWave1 = new Vector4[_sineWaves.Length];
#endif
        // set simulation waves parameters
        for (int i = 0; i < _sineWaves.Length; i++)
        {
            var a = _sineWaves[i].amplitude;
            var f = 2.0f * Mathf.PI / _sineWaves[i].waveLength;
            var p = _sineWaves[i].speed * f;
            float radA = _sineWaves[i].travelAngle * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Sin(radA), Mathf.Cos(radA));
            var s = _sineWaves[i].sharpness;
#if UNITY_5_4_OR_NEWER
			_SineWave0[i] = new Vector4(a, f, p, 0);
			_SineWave1[i] = new Vector4(d.x, d.y, s, 0);
#elif
            _lowPolyWater.SetVector("_SineWave0" + i, new Vector4(a, f, p, 0));
            _lowPolyWater.SetVector("_SineWave1" + i, new Vector4(d.x, d.y, s, 0));
#endif
        }
#if UNITY_5_4_OR_NEWER
		// pass parameters
		_lowPolyWater.SetVectorArray("_SineWave0", _SineWave0);
		_lowPolyWater.SetVectorArray("_SineWave1", _SineWave1);
#endif
    }

    private void Update()
    {
        if (_updateMaterialPerFrame)
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
#if UNITY_5_4_OR_NEWER
				_SineWave0[i].Set(a, f, p, 0);
				_SineWave1[i].Set(d.x, d.y, s, 0);
#elif
				_lowPolyWater.SetVector("_SineWave0" + i, new Vector4(a, f, p, 0));
				_lowPolyWater.SetVector("_SineWave1" + i, new Vector4(d.x, d.y, s, 0));
#endif
            }
#if UNITY_5_4_OR_NEWER
			// pass parameters
			_lowPolyWater.SetVectorArray("_SineWave0", _SineWave0);
			_lowPolyWater.SetVectorArray("_SineWave1", _SineWave1);
#endif
        }
    }

}
