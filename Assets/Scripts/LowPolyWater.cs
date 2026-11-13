using UnityEngine;

/// <summary>
/// Uploads a <see cref="SineWaves"/> setup to the water material. Runs in edit mode
/// too, so the surface animates in the Scene view and in editor captures.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(Renderer))]
public class LowPolyWater : MonoBehaviour
{
    const int MaxWaves = 8;
    const string GerstnerKeyword = "_WAVES_GERSTNER";

    static readonly int WavesId = Shader.PropertyToID("_Waves");
    static readonly int TimeScaleId = Shader.PropertyToID("_TimeScale");
    static readonly int SineWave0Id = Shader.PropertyToID("_SineWave0");
    static readonly int SineWave1Id = Shader.PropertyToID("_SineWave1");

    [SerializeField]
    private SineWaves _sineWaves = null;
    [SerializeField, Tooltip("Re-upload the wave setup every frame. Useful while tuning a SineWaves asset.")]
    private bool _updateMaterialPerFrame = false;
    [Header("Quad Detail"), SerializeField, InspectorButton("SubdivideMesh")]
    private bool _subdivideMesh;

    /// <summary>The wave setup this surface animates with.</summary>
    public SineWaves Waves
    {
        get { return _sineWaves; }
    }

    // Fixed size: Unity locks a material array's length the first time it is set.
    private readonly Vector4[] _sineWave0 = new Vector4[MaxWaves];
    private readonly Vector4[] _sineWave1 = new Vector4[MaxWaves];

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

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Update()
    {
        if (_updateMaterialPerFrame)
            Apply();
    }

    // The water shader reads the scene depth under it for absorption, refraction and foam.
    private void OnWillRenderObject()
    {
        var cam = Camera.current;
        if (cam != null)
            cam.depthTextureMode |= DepthTextureMode.Depth;
    }

    /// <summary>Writes the wave parameters into the shared water material.</summary>
    public void Apply()
    {
        var material = GetComponent<Renderer>().sharedMaterial;
        if (_sineWaves == null || material == null)
            return;

        int count = Mathf.Min(_sineWaves.Length, MaxWaves);
        for (int i = 0; i < count; i++)
        {
            var wave = _sineWaves[i];
            float frequency = 2.0f * Mathf.PI / Mathf.Max(wave.waveLength, 1e-4f);
            float phase = wave.speed * frequency;
            float angle = wave.travelAngle * Mathf.Deg2Rad;
            // GPU Gems 1.2: Q = steepness / (w * A * waves) keeps the summed crests from looping.
            float q = _sineWaves.Steepness / Mathf.Max(frequency * wave.amplitude * count, 1e-4f);
            _sineWave0[i] = new Vector4(wave.amplitude, frequency, phase, q);
            _sineWave1[i] = new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), wave.sharpness, 0);
        }

        if (_sineWaves.Model == SineWaves.WaveModel.Gerstner)
            material.EnableKeyword(GerstnerKeyword);
        else
            material.DisableKeyword(GerstnerKeyword);

        material.SetInt(WavesId, count);
        material.SetFloat(TimeScaleId, _sineWaves.Timescale);
        material.SetVectorArray(SineWave0Id, _sineWave0);
        material.SetVectorArray(SineWave1Id, _sineWave1);
    }
}
