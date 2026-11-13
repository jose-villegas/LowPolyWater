using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor utilities for the demo scene: capture the README view to a PNG and
/// bake its lighting. Every entry point also runs from the command line, e.g.
/// Unity -batchmode -projectPath . -executeMethod DemoTools.CaptureFromCommandLine -quit
/// </summary>
public static class DemoTools
{
    public const string DemoScene = "Assets/Demo/WaterTest2.unity";
    const string CaptureFolder = "Captures";

    [MenuItem("Tools/Low Poly Water/Capture Main Camera")]
    public static void CaptureMenu()
    {
        Capture(Path.Combine(CaptureFolder, "capture.png"), 1920, 1080);
    }

    [MenuItem("Tools/Low Poly Water/Bake Demo Lighting")]
    public static void BakeMenu()
    {
        Bake();
    }

    /// <summary>Renders the scene's main camera at the given size and writes a PNG.</summary>
    public static void Capture(string path, int width, int height)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[DemoTools] No camera tagged MainCamera in the open scene.");
            return;
        }

        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 8;
        var previous = cam.targetTexture;
        cam.targetTexture = rt;
        // Render twice: the first pass lets OnWillRenderObject fill the planar reflection.
        cam.Render();
        cam.Render();
        cam.targetTexture = previous;

        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("[DemoTools] Wrote " + Path.GetFullPath(path));
    }

    [MenuItem("Tools/Low Poly Water/Configure Demo Lighting")]
    public static void ConfigureLightingMenu()
    {
        ConfigureLighting();
    }

    /// <summary>
    /// Sets the open scene up for baked soft shadows and global illumination:
    /// a Mixed sun in Shadowmask mode (baked area-light shadows on the static terrain,
    /// realtime light on the water and trees), indirect bounces from the sun and sky,
    /// and a light probe grid so everything not lightmapped picks up the bounce light.
    /// </summary>
    public static void ConfigureLighting()
    {
        var scene = EditorSceneManager.GetActiveScene();

        var settings = Lightmapping.lightingSettings;
        if (settings == null)
        {
            settings = new LightingSettings { name = scene.name + " Lighting" };
            var path = Path.ChangeExtension(scene.path, null) + "Settings.lighting";
            AssetDatabase.CreateAsset(settings, path);
            Lightmapping.lightingSettings = settings;
        }
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.mixedBakeMode = MixedLightingMode.Shadowmask;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        settings.directionalityMode = LightmapsMode.CombinedDirectional;
        settings.lightmapResolution = 10;         // texels per unit: a 1024 map for the 100 m island
        settings.lightmapMaxSize = 2048;
        settings.lightmapPadding = 4;
        settings.lightmapCompression = LightmapCompression.HighQuality;
        settings.directSampleCount = 64;          // smooth penumbrae from the sun's disc
        settings.indirectSampleCount = 512;
        settings.environmentSampleCount = 512;
        settings.maxBounces = 3;
        settings.ao = true;
        settings.aoMaxDistance = 1.5f;
        settings.aoExponentIndirect = 1.0f;
        settings.aoExponentDirect = 0.0f;
        settings.filteringMode = LightingSettings.FilterMode.Auto;
        settings.albedoBoost = 1.0f;
        settings.indirectScale = 1.0f;
        EditorUtility.SetDirty(settings);

        // Sun: keep its README angle; bake its shadows as a soft area light.
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional)
                continue;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = LightShadows.Soft;
            light.shadowAngle = 4.0f;             // degrees; the penumbra width of baked shadows
            light.bounceIntensity = 1.0f;
            RenderSettings.sun = light;
            EditorUtility.SetDirty(light);
        }

        // The terrain is the lightmapped surface; its trees cast into its lightmap and
        // get their own baked probes.
        Bounds world = new Bounds();
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject,
                GameObjectUtility.GetStaticEditorFlags(terrain.gameObject) | StaticEditorFlags.ContributeGI);
            terrain.bakeLightProbesForTrees = true;
            terrain.deringLightProbesForTrees = true;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            // The 5.5 scene had this at 0.085, a 64x64 lightmap for the whole island.
            var so = new SerializedObject(terrain);
            so.FindProperty("m_ScaleInLightmap").floatValue = 1.0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var size = terrain.terrainData.size;
            world = new Bounds(terrain.transform.position + size * 0.5f, size);
            EditorUtility.SetDirty(terrain);
        }

        // Clouds float far above the island: no lightmaps, no shadows over the whole map.
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!GetPath(r.transform).StartsWith("Clouds"))
                continue;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~StaticEditorFlags.ContributeGI);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveGI = ReceiveGI.LightProbes;
            // Lit by the sky alone, as before: the island's probes carry sunlit-grass bounce
            // that would bleach them white.
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            PrefabUtility.RecordPrefabInstancePropertyModifications(r.gameObject);
        }

        // Light probes over the terrain for the water, details and anything dynamic.
        // (No ?? here: it skips Unity's null check on destroyed objects.)
        var probesGo = GameObject.Find("Light Probes");
        if (probesGo == null)
            probesGo = new GameObject("Light Probes");
        var group = probesGo.GetComponent<LightProbeGroup>();
        if (group == null)
            group = probesGo.AddComponent<LightProbeGroup>();
        var positions = new System.Collections.Generic.List<Vector3>();
        const float spacing = 8.0f;
        float[] heights = { 6.0f, 9.0f, 14.0f, 22.0f };
        for (float x = world.min.x; x <= world.max.x + 0.01f; x += spacing)
            for (float z = world.min.z; z <= world.max.z + 0.01f; z += spacing)
                foreach (var y in heights)
                {
                    var p = new Vector3(x, y, z);
                    // Skip probes buried in the terrain.
                    var t = Terrain.activeTerrain;
                    if (t != null && t.SampleHeight(p) + t.transform.position.y > y + 0.25f)
                        continue;
                    positions.Add(probesGo.transform.InverseTransformPoint(p));
                }
        group.probePositions = positions.ToArray();
        EditorUtility.SetDirty(group);

        Debug.Log("[DemoTools] Lighting configured: terrain " + world.size + ", " + positions.Count + " probes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ConfigureAndBakeFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        ConfigureLighting();
        BakeFromCommandLineOpen();
    }

    /// <summary>Synchronous lighting bake of the open scene with its saved settings.</summary>
    public static bool Bake()
    {
        var ok = Lightmapping.Bake();
        Debug.Log("[DemoTools] Bake " + (ok ? "succeeded" : "failed"));
        if (ok)
            EditorSceneManager.SaveOpenScenes();
        return ok;
    }

    // ---- command line entry points -------------------------------------------------

    static string Arg(string name, string fallback)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return fallback;
    }

    /// <summary>
    /// Optional, unsaved overrides on every water material, for A/B captures:
    /// -keyword _WATER_VOLUME:0 (or :1). Several are separated by commas.
    /// </summary>
    static void ApplyKeywordOverrides()
    {
        var spec = Arg("-keyword", "");
        if (spec.Length == 0)
            return;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.shader.name.StartsWith("LowPolyWater"))
                continue;
            foreach (var entry in spec.Split(','))
            {
                var kv = entry.Split(':');
                if (kv[1] == "1") m.EnableKeyword(kv[0]); else m.DisableKeyword(kv[0]);
            }
        }
    }

    public static void CaptureFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        ApplyKeywordOverrides();
        Capture(Arg("-out", Path.Combine(CaptureFolder, "capture.png")), 1920, 1080);
    }

    /// <summary>
    /// Captures the scene from several camera poses at half resolution:
    /// -poses "x,y,z,pitch,yaw;x,y,z,pitch,yaw;..." -out Captures/sweep
    /// </summary>
    public static void CapturePosesFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        var cam = Camera.main;
        var prefix = Arg("-out", Path.Combine(CaptureFolder, "sweep"));
        var poses = Arg("-poses", "").Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < poses.Length; i++)
        {
            var v = System.Array.ConvertAll(poses[i].Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            cam.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(v[3], v[4], 0));
            Capture(prefix + "_" + i + ".png", 960, 540);
        }
    }

    /// <summary>
    /// Unsaved lighting A/B: -variants "ambientIntensity,sunIntensity;..." -out Captures/variant
    /// </summary>
    public static void CaptureVariantsFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        ApplyKeywordOverrides();
        var sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) sun = l;
        var prefix = Arg("-out", Path.Combine(CaptureFolder, "variant"));
        var variants = Arg("-variants", "").Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < variants.Length; i++)
        {
            var v = System.Array.ConvertAll(variants[i].Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            RenderSettings.ambientIntensity = v[0];
            sun.intensity = v[1];
            DynamicGI.UpdateEnvironment();
            Capture(prefix + "_" + i + ".png", 960, 540);
        }
    }

    public static void BakeFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        BakeFromCommandLineOpen();
    }

    static void BakeFromCommandLineOpen()
    {
        if (!Bake())
            EditorApplication.Exit(1);
        ApplyKeywordOverrides();
        Capture(Arg("-out", Path.Combine(CaptureFolder, "baked.png")), 1920, 1080);
    }

    /// <summary>Logs every renderer, its materials and shaders, plus lights and cameras.</summary>
    public static void DumpFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        var sb = new StringBuilder("[DemoTools] Scene dump\n");
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            sb.Append("R ").Append(GetPath(r.transform)).Append(" static=")
              .Append(GameObjectUtility.GetStaticEditorFlags(r.gameObject)).Append(" :");
            foreach (var m in r.sharedMaterials)
                sb.Append(" [").Append(m ? m.name + " / " + (m.shader ? m.shader.name : "null") + (m.shader && !m.shader.isSupported ? " UNSUPPORTED" : "") : "null").Append(']');
            sb.Append('\n');
        }
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            sb.Append("T ").Append(GetPath(t.transform)).Append(" mat=").Append(t.materialTemplate ? t.materialTemplate.shader.name : "default")
              .Append(" trees=").Append(t.terrainData.treeInstanceCount).Append(" protos=").Append(t.terrainData.treePrototypes.Length)
              .Append(" lmIndex=").Append(t.lightmapIndex).Append(" lmST=").Append(t.lightmapScaleOffset)
              .Append(" lmScale=").Append(new SerializedObject(t).FindProperty("m_ScaleInLightmap").floatValue)
              .Append(" static=").Append(GameObjectUtility.GetStaticEditorFlags(t.gameObject))
              .Append(" layers=").Append(t.terrainData.terrainLayers.Length).Append('\n');
            if (t.materialTemplate)
                foreach (var n in t.materialTemplate.GetTexturePropertyNames()) { }
            foreach (var p in t.terrainData.treePrototypes)
            {
                var mr = p.prefab ? p.prefab.GetComponentInChildren<Renderer>() : null;
                sb.Append("  tree ").Append(p.prefab ? p.prefab.name : "null").Append(" shader=")
                  .Append(mr && mr.sharedMaterial ? mr.sharedMaterial.shader.name : "?").Append(" cast=").Append(mr ? mr.shadowCastingMode.ToString() : "?").Append('\n');
            }
        }
        foreach (var lm in LightmapSettings.lightmaps)
            sb.Append("LM ").Append(lm.lightmapColor ? lm.lightmapColor.width + "x" + lm.lightmapColor.height : "none").Append('\n');
        sb.Append("Quality ").Append(QualitySettings.names[QualitySettings.GetQualityLevel()]).Append(" shadowDist=").Append(QualitySettings.shadowDistance)
          .Append(" shadows=").Append(QualitySettings.shadows).Append(" shadowmaskMode=").Append(QualitySettings.shadowmaskMode)
          .Append(" pixelLights=").Append(QualitySettings.pixelLightCount).Append(" aa=").Append(QualitySettings.antiAliasing).Append('\n');
        sb.Append("AmbientInt ").Append(RenderSettings.ambientIntensity).Append(" refl=").Append(RenderSettings.reflectionIntensity)
          .Append(" probe0=").Append(RenderSettings.ambientProbe[0, 0]).Append(',').Append(RenderSettings.ambientProbe[1, 0]).Append(',').Append(RenderSettings.ambientProbe[2, 0]).Append('\n');
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            sb.Append("L ").Append(GetPath(l.transform)).Append(' ').Append(l.type).Append(" mode=").Append(l.lightmapBakeType)
              .Append(" shadows=").Append(l.shadows).Append(" int=").Append(l.intensity).Append(" col=").Append(l.color)
              .Append(" rot=").Append(l.transform.eulerAngles).Append('\n');
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            sb.Append("C ").Append(GetPath(c.transform)).Append(" pos=").Append(c.transform.position).Append(" rot=").Append(c.transform.eulerAngles)
              .Append(" fov=").Append(c.fieldOfView).Append(" path=").Append(c.renderingPath).Append(" hdr=").Append(c.allowHDR).Append('\n');
        sb.Append("Ambient ").Append(RenderSettings.ambientMode).Append(" sky=").Append(RenderSettings.skybox ? RenderSettings.skybox.shader.name : "none")
          .Append(" fog=").Append(RenderSettings.fog).Append(" colorSpace=").Append(PlayerSettings.colorSpace).Append('\n');
        Debug.Log(sb.ToString());
    }

    static string GetPath(Transform t)
    {
        return t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
    }
}
