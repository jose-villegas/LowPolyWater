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

    // ---- ocean skybox ---------------------------------------------------------------

    const string ProceduralSky = "Assets/Demo/Materials/CustomSkybox.mat";
    const int SkyFaceSize = 1024;

    [MenuItem("Tools/Low Poly Water/Bake Ocean Skybox")]
    public static void BakeOceanSkyboxMenu()
    {
        BakeOceanSkybox();
    }

    /// <summary>
    /// Captures the procedural sky into a cubemap and makes the scene's skybox an ocean
    /// that continues the water mesh out to the horizon, matched to the mesh's level,
    /// colour and facet size. Re-run after a bake so the colour picks up the baked ambient.
    /// </summary>
    public static void BakeOceanSkybox()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var folder = Path.GetDirectoryName(scene.path).Replace('\\', '/');
        var cubePath = folder + "/" + scene.name + "Sky.png";
        var materialPath = folder + "/" + scene.name + "OceanSkybox.mat";

        // 1. The sky, as the procedural skybox draws it.
        var previous = RenderSettings.skybox;
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(ProceduralSky);
        var go = new GameObject("Sky Capture") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = 0;
        cam.allowHDR = false;
        var cube = new Cubemap(SkyFaceSize, TextureFormat.RGBA32, false);
        cam.RenderToCubemap(cube);
        Object.DestroyImmediate(go);
        RenderSettings.skybox = previous;

        // 2. A horizontal strip of faces (+X -X +Y -Y +Z -Z), which Unity imports as a cubemap.
        var strip = new Texture2D(SkyFaceSize * 6, SkyFaceSize, TextureFormat.RGBA32, false);
        var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                            CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
        for (int f = 0; f < faces.Length; f++)
        {
            var pixels = cube.GetPixels(faces[f]);
            // Cubemap faces are stored top row first; Texture2D rows start at the bottom.
            for (int y = 0; y < SkyFaceSize; y++)
            {
                var row = new Color[SkyFaceSize];
                System.Array.Copy(pixels, (SkyFaceSize - 1 - y) * SkyFaceSize, row, 0, SkyFaceSize);
                strip.SetPixels(f * SkyFaceSize, y, SkyFaceSize, 1, row);
            }
        }
        File.WriteAllBytes(cubePath, strip.EncodeToPNG());
        Object.DestroyImmediate(strip);
        Object.DestroyImmediate(cube);
        AssetDatabase.ImportAsset(cubePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(cubePath);
        importer.textureShape = TextureImporterShape.TextureCube;
        importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;   // no banding in the gradients
        importer.SaveAndReimport();

        // 3. The ocean, matched to the water mesh.
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("LowPolyWater/Skybox Ocean"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_Sky", AssetDatabase.LoadAssetAtPath<Cubemap>(cubePath));

        var water = Object.FindFirstObjectByType<LowPolyWater>();
        if (water != null)
        {
            var renderer = water.GetComponent<Renderer>();
            var waterMaterial = renderer.sharedMaterial;
            var bounds = renderer.bounds;

            // Mean surface height: both wave models lift the mesh by about the summed amplitude.
            float lift = 0;
            if (water.Waves != null)
                for (int i = 0; i < water.Waves.Length; i++)
                    lift += water.Waves[i].amplitude;
            material.SetFloat("_WaterLevel", water.transform.position.y + lift);

            // The colour the water shader gives deep water: its deep colour, lit by sky and sun.
            var deep = waterMaterial.HasProperty("_DeepColor") ? waterMaterial.GetColor("_DeepColor") : waterMaterial.GetColor("_AlbedoColor");
            // Light it the way the river is lit: from the baked light probes over the water if
            // it uses them, otherwise from the scene's ambient probe.
            var probe = RenderSettings.ambientProbe;
            if (renderer.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off && LightmapSettings.lightProbes != null)
                LightProbes.GetInterpolatedProbe(bounds.center + Vector3.up, renderer, out probe);
            var ambient = new Color[1];
            probe.Evaluate(new[] { Vector3.up }, ambient);
            var light = ambient[0];
            // ShadeSH9 hands shaders gamma values in a Gamma colour space project; the probe is linear.
            if (PlayerSettings.colorSpace == ColorSpace.Gamma)
                light = light.gamma;
            if (RenderSettings.sun != null)
                light += RenderSettings.sun.color * RenderSettings.sun.intensity * Mathf.Max(0, -RenderSettings.sun.transform.forward.y);
            material.SetColor("_WaterColor", new Color(deep.r * light.r, deep.g * light.g, deep.b * light.b, 1));
            material.SetFloat("_Reflectivity", waterMaterial.GetFloat("_Reflectivity"));

            // Fresnel takes over past the mesh's far corners.
            float reach = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            material.SetVector("_Center", new Vector4(bounds.center.x, 0, bounds.center.z, 0));
            material.SetFloat("_FresnelStart", reach);
            material.SetFloat("_FresnelEnd", reach * 5);

            // Facets as big as the mesh's triangles.
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            float cells = Mathf.Max(1, Mathf.Sqrt(mesh.vertexCount) - 1);
            material.SetFloat("_FacetSize", bounds.size.x / cells);
        }
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        // The scene keeps the procedural sky for ambient light, reflections and the bake;
        // only the camera draws the ocean (the water's reflection camera copies it).
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(ProceduralSky);
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            var skybox = mainCamera.GetComponent<Skybox>();
            if (skybox == null)
                skybox = mainCamera.gameObject.AddComponent<Skybox>();
            skybox.material = material;
            EditorUtility.SetDirty(skybox);
        }
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DemoTools] Ocean skybox: " + materialPath);
    }

    // ---- wind -------------------------------------------------------------------------

    // Wind bend per material of the terrain's tree prototypes (m of sway per m^2 of height).
    // Materials not listed keep their shader: rocks, logs and stumps stand still.
    static readonly (string prefix, float weight)[] FoliageWeights =
    {
        ("trees_", 0.003f),        // the tall low poly trees: tops sway about 0.25 m in gusts
        ("Leaves1", 0.3f),         // grass blades
        ("Red", 0.3f),             // flower petals, with their grass stems
        ("Bush Leaves", 0.06f),    // bushes are wider and stiffer than grass
    };

    // Prototypes whose shared material needs its own copy before it can bend differently.
    static readonly (string prefab, string material, string copy)[] FoliageCopies =
    {
        ("Bush_2", "Leaves1", "Bush Leaves"),
        ("Rock_1_Mossy", "Leaves1", "Moss"),   // moss on a rock stays put
    };

    const float CanopyHeight = 5.0f;           // where the baked tree shadows come from

    [MenuItem("Tools/Low Poly Water/Configure Wind")]
    public static void ConfigureWindMenu()
    {
        ConfigureWind();
    }

    /// <summary>
    /// Adds a wind to the open scene, moves the terrain's grass, flowers, bushes and trees
    /// to the wind-bent foliage shader, and gives the terrain the shader that sways the
    /// baked tree shadows along with them.
    /// </summary>
    public static void ConfigureWind()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Object.FindFirstObjectByType<LowPolyWind>() == null)
            new GameObject("Wind").AddComponent<LowPolyWind>();

        var foliage = Shader.Find("LowPolyWater/Foliage");
        float treeWeight = 0;
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            foreach (var prototype in terrain.terrainData.treePrototypes)
            {
                if (prototype.prefab == null)
                    continue;
                bool prefabChanged = false;
                foreach (var renderer in prototype.prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var m = materials[i];
                        if (m == null)
                            continue;
                        foreach (var copy in FoliageCopies)
                        {
                            if (copy.prefab != prototype.prefab.name || copy.material != m.name)
                                continue;
                            var path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m)) + "/" + copy.copy + ".mat";
                            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(m), path);
                            materials[i] = m = AssetDatabase.LoadAssetAtPath<Material>(path);
                            prefabChanged = true;
                            // A copy taken after its source went to the foliage shader would
                            // inherit its bend; copies without their own weight stand still.
                            if (System.Array.FindIndex(FoliageWeights, w => m.name.StartsWith(w.prefix)) < 0
                                && m.HasProperty("_WindWeight"))
                            {
                                m.SetFloat("_WindWeight", 0);
                                m.SetFloat("_Flutter", 0);
                                EditorUtility.SetDirty(m);
                            }
                        }
                        foreach (var (prefix, weight) in FoliageWeights)
                        {
                            if (!m.name.StartsWith(prefix))
                                continue;
                            if (m.shader != foliage)
                                m.shader = foliage;   // keeps _MainTex and _Color
                            m.SetFloat("_WindWeight", weight);
                            m.SetFloat("_Wrap", 0);   // backlit sides stay dark, as with Soft Occlusion
                            m.enableInstancing = true;
                            EditorUtility.SetDirty(m);
                            if (prefix == "trees_")
                                treeWeight = weight;
                        }
                    }
                    if (prefabChanged)
                        renderer.sharedMaterials = materials;
                }
                if (prefabChanged)
                    PrefabUtility.SavePrefabAsset(prototype.prefab);
            }
            terrain.terrainData.RefreshPrototypes();

            // Foliage has no billboards: draw the meshes at every distance.
            terrain.treeBillboardDistance = Mathf.Max(terrain.treeBillboardDistance, 1000);
            terrain.drawInstanced = true;
            terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, 1000);

            var materialPath = Path.GetDirectoryName(scene.path).Replace('\\', '/') + "/" + scene.name + "Terrain.mat";
            var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (terrainMaterial == null)
            {
                terrainMaterial = new Material(Shader.Find("LowPolyWater/Terrain"));
                AssetDatabase.CreateAsset(terrainMaterial, materialPath);
            }
            terrainMaterial.SetVector("_TerrainSize", terrain.terrainData.size);
            terrainMaterial.SetFloat("_ShadowCasterHeight", CanopyHeight);
            terrainMaterial.SetFloat("_ShadowSway", treeWeight * CanopyHeight * CanopyHeight);
            terrainMaterial.enableInstancing = true;
            EditorUtility.SetDirty(terrainMaterial);
            terrain.materialTemplate = terrainMaterial;
            terrain.Flush();
            EditorUtility.SetDirty(terrain);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DemoTools] Wind configured");
    }

    /// <summary>
    /// The whole demo setup: wind, ocean skybox, lighting, bake, then the ocean colour again
    /// with the baked ambient, and a capture.
    /// </summary>
    public static void ConfigureAndBakeFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        ConfigureWind();
        BakeOceanSkybox();
        ConfigureLighting();
        if (!Bake())
            EditorApplication.Exit(1);
        BakeOceanSkybox();
        ApplyKeywordOverrides();
        Capture(Arg("-out", Path.Combine(CaptureFolder, "baked.png")), 1920, 1080);
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
    /// Renders an animation of the main camera as numbered PNGs, stepping the wave clock:
    /// -frames 96 -fps 24 -width 960 -height 540 -out Captures/sequence/frame
    /// </summary>
    public static void CaptureSequenceFromCommandLine()
    {
        EditorSceneManager.OpenScene(Arg("-scene", DemoScene));
        ApplyKeywordOverrides();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int frames = int.Parse(Arg("-frames", "96"), inv);
        float fps = float.Parse(Arg("-fps", "24"), inv);
        int width = int.Parse(Arg("-width", "960"), inv);
        int height = int.Parse(Arg("-height", "540"), inv);
        var prefix = Arg("-out", Path.Combine(CaptureFolder, "sequence", "frame"));
        const float start = 10.0f;  // seconds; any positive value, it only picks where the waves are
        try
        {
            for (int i = 0; i < frames; i++)
            {
                // _Time.x units are seconds / 20.
                Shader.SetGlobalFloat("_LowPolyWaterTime", (start + i / fps) / 20.0f);
                Capture(prefix + "_" + i.ToString("D3") + ".png", width, height);
            }
        }
        finally
        {
            Shader.SetGlobalFloat("_LowPolyWaterTime", 0.0f);
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
