using UnityEngine;

/// <summary>
/// Global wind for the LowPolyWater foliage and terrain shaders: a steady lean plus gust
/// fronts that roll downwind across the scene. Put one in a scene.
/// </summary>
[ExecuteAlways]
public class LowPolyWind : MonoBehaviour
{
    static readonly int WindId = Shader.PropertyToID("_LowPolyWind");
    static readonly int MotionId = Shader.PropertyToID("_LowPolyWindMotion");

    [SerializeField, Range(0, 360), Tooltip("Direction the wind blows toward, degrees clockwise from +Z.")]
    private float _direction = 70.0f;
    [SerializeField, Min(0), Tooltip("Scales every sway. 0 = calm.")]
    private float _strength = 1.0f;
    [SerializeField, Min(1), Tooltip("Distance between gust fronts (m).")]
    private float _gustWavelength = 24.0f;
    [SerializeField, Min(0), Tooltip("Speed the gust fronts travel downwind (m/s).")]
    private float _gustSpeed = 7.0f;
    [SerializeField, Min(0), Tooltip("Leaf flutter frequency (rad/s).")]
    private float _flutterFrequency = 9.0f;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void OnDisable()
    {
        Shader.SetGlobalVector(WindId, Vector4.zero);
    }

    /// <summary>Uploads the wind to every shader.</summary>
    public void Apply()
    {
        float angle = _direction * Mathf.Deg2Rad;
        Shader.SetGlobalVector(WindId, new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), _strength, _gustWavelength));
        Shader.SetGlobalVector(MotionId, new Vector4(_gustSpeed, _flutterFrequency, 0, 0));
    }
}
