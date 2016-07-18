using UnityEngine;
using System.Collections;

public class LowPolyWater : MonoBehaviour
{
    [Range(1, 32)]
    public int waves = 4;
    public float height = 0;
    public float timeScale = 1.0f;

	// Use this for initialization
	void Start ()
	{
	    var material = Resources.Load("Materials/LowPolyWater", typeof (Material)) as Material;

	    if (!material) { return; }
	    // set wave count
        material.SetInt("_Waves", waves);
        material.SetFloat("_Height", height);
        material.SetFloat("_TimeScale", timeScale);

        // assign random directions
	    for (int i = 0; i < waves; i++)
	    {
	        var angle = Random.Range(-Mathf.PI/3.0f, Mathf.PI/3.0f);
            material.SetVector("_Direction" + i, new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), 0.0f, 0.0f));
        }
	}
}
