using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// This stage is a material study, not the final character or outline effect.
public static class MIOFoundation
{
    const string Graph = "Assets/Shaders/MIO/MIO Toon.shadergraph";
    const string ScenePath = "Assets/Scenes/MIO Material Study.unity";
    const string TexturePath = "Assets/Textures/MIO/MIO Hatching.png";
    const string Output = "Screenshots/MIO";

    [MenuItem("HW02 MIO/1 - Create material study (only if missing)")]
    public static void CreateStudy()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Material study already exists. Open it from Assets/Scenes; creation does not overwrite your work.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/Materials/MIO");
        Directory.CreateDirectory("Assets/Textures/MIO");
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        CreateHatching();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Graph);
        if (shader == null) throw new InvalidOperationException("MIO Shader Graph could not be imported.");
        var purple = MakeMaterial(shader, "MIO Purple", "DEA5EF", "B86AD7", "8652AF");
        var yellow = MakeMaterial(shader, "MIO Yellow", "FFF78B", "F4DC55", "B58BC2");
        var cyan = MakeMaterial(shader, "MIO Cyan", "DBF1DF", "A3D6CC", "798EB8");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 2.05f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 40;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("C5DDCA");
        camera.allowHDR = false;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = true;
        AddSphere("Purple - UV hatching test", new Vector3(0, 0, 0), 2.2f, purple);
        AddSphere("Yellow - palette test", new Vector3(-2.15f, 0, 0), 1.55f, yellow);
        AddSphere("Cyan - palette test", new Vector3(2.15f, 0, 0), 1.55f, cyan);
        var sun = new GameObject("Key - Directional").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.65f, 0.6f));
        sun.intensity = 0.92f;
        sun.shadows = LightShadows.Soft;
        sun.shadowBias = 0.035f;
        sun.shadowNormalBias = 0.1f;
        var fill = new GameObject("Fill - Point (toggle to test additional lights)").AddComponent<Light>();
        fill.type = LightType.Point;
        fill.transform.position = new Vector3(2.6f, 0.4f, -1.7f);
        fill.intensity = 1.4f;
        fill.range = 6;
        fill.shadows = LightShadows.Soft;
        fill.shadowBias = 0.035f;
        fill.shadowNormalBias = 0.1f;
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("MIO foundation scene created: " + ScenePath);
    }

    static Material MakeMaterial(Shader shader, string name, string light, string mid, string dark)
    {
        string path = "Assets/Materials/MIO/" + name + ".mat";
        if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite existing material: " + path);
        var material = new Material(shader) { name = name };
        material.SetColor("_Highlight", Hex(light));
        material.SetColor("_Midtone", Hex(mid));
        material.SetColor("_Shadow", Hex(dark));
        material.SetFloat("_ThreeBands", 1);
        material.SetFloat("_ShadowThreshold", 0.32f);
        material.SetFloat("_HighlightThreshold", 0.7f);
        material.SetFloat("_Smoothness", 0.035f);
        material.SetFloat("_ShadowScale", 3);
        material.SetFloat("_PatternStrength", 0.65f);
        material.SetTexture("_ShadowPattern", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void AddSphere(string name, Vector3 position, float size, Material material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * size;
        sphere.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
    }

    static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }

    public static void CreateHatching(bool overwrite = false)
    {
        if (File.Exists(TexturePath) && !overwrite) return;
        // All frequencies are integers over UV [0,1): edges tile continuously.
        // Fine periodic variation gives the diagonal pencil strokes irregular width.
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, false, true);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float wave = 0.09f * Mathf.Sin(2 * Mathf.PI * (2 * u + v))
                       + 0.025f * Mathf.Sin(2 * Mathf.PI * (7 * u - 3 * v));
            float cycle = Mathf.Repeat(7 * (u + v) + wave, 1);
            float distance = Mathf.Min(cycle, 1 - cycle);
            // Broad enough to remain legible in a full-character shot after mipmapping.
            float width = 0.125f + 0.024f * Mathf.Sin(2 * Mathf.PI * (9 * u - 5 * v));
            float ink = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(width, width + 0.027f, distance));
            float grain = 0.88f + 0.12f * Mathf.Sin(2 * Mathf.PI * (31 * u + 19 * v));
            float paper = 1 - ink * grain;
            pixels[y * size + x] = new Color(paper, paper, paper, 1);
        }
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    [MenuItem("HW02 MIO/2 - Capture and validate material study")]
    public static void ValidateStudy()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;
        Material[] temporary = null;
        try
        {
            EditorSceneManager.OpenScene(ScenePath);
            var renderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            temporary = renderers.Select(r => new Material(r.sharedMaterial)).ToArray();
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = temporary[i];
            Directory.CreateDirectory(Output);
            var baseline = Capture(Output + "/material-study.png");
            var point = UnityEngine.Object.FindObjectsOfType<Light>().Single(l => l.type == LightType.Point);
            point.enabled = false;
            RequireChange("Additional point light changes rendered lighting", baseline, Capture());
            point.enabled = true;
            foreach (var material in temporary) material.SetFloat("_PatternStrength", 0);
            RequireChange("Object surface hatching is visible", baseline, Capture(Output + "/without-hatching.png"));
            foreach (var material in temporary) material.SetFloat("_PatternStrength", 0.65f);
            foreach (var material in temporary) material.SetFloat("_ShadowThreshold", 0.6f);
            RequireChange("Palette threshold changes band coverage", baseline, Capture());
            foreach (var material in temporary) material.SetFloat("_ShadowThreshold", 0.32f);
            foreach (var material in temporary) material.SetFloat("_ShadowScale", 5);
            RequireChange("Shadow Scale changes texture tiling", baseline, Capture());
            foreach (var material in temporary) material.SetFloat("_ShadowScale", 3);
            var main = UnityEngine.Object.FindObjectsOfType<Light>().Single(l => l.type == LightType.Directional);
            main.enabled = false;
            RequireChange("Main directional light contributes", baseline, Capture());
            main.enabled = true;
            ValidateUVAnchoring(renderers, temporary, point);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Graph);
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.message)));
            if (!shader.isSupported) throw new InvalidOperationException("MIO shader is unsupported.");
            Debug.Log("MIO ALL FOUNDATION CHECKS PASSED");
        }
        finally
        {
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous) ? ScenePath : previous);
            if (temporary != null) foreach (var material in temporary) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    static void ValidateUVAnchoring(MeshRenderer[] renderers, Material[] materials, Light point)
    {
        // Turn a rotationally symmetric UV sphere while lighting is unchanged.
        // Plain shading stays almost identical; mesh-attached ink must rotate.
        point.enabled = false;
        var sphere = renderers.Single(r => r.name.StartsWith("Purple"));
        foreach (var renderer in renderers) renderer.enabled = renderer == sphere;
        sphere.sharedMaterial.SetFloat("_ShadowThreshold", 0.95f);
        sphere.sharedMaterial.SetFloat("_PatternStrength", 0);
        var a = Capture();
        sphere.transform.Rotate(0, 90, 0);
        int plain = Difference(a, Capture());
        sphere.transform.Rotate(0, -90, 0);
        sphere.sharedMaterial.SetFloat("_PatternStrength", 1);
        a = Capture();
        sphere.transform.Rotate(0, 90, 0);
        int textured = Difference(a, Capture());
        if (textured < plain + 1000) throw new InvalidOperationException("UV anchoring check failed: plain=" + plain + ", ink=" + textured);
        Debug.Log("MIO PASS: Hatching rotates with the mesh (plain=" + plain + ", ink=" + textured + " pixels)");
    }

    static int Difference(Color32[] a, Color32[] b)
    {
        int count = 0;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 12) count++;
        return count;
    }

    static void RequireChange(string label, Color32[] baseline, Color32[] other)
    {
        int count = Difference(baseline, other);
        if (count < 150) throw new InvalidOperationException(label + ": only " + count + " pixels changed.");
        Debug.Log("MIO PASS: " + label + " (" + count + " pixels)");
    }

    static Color32[] Capture(string path = null)
    {
        const int width = 1400, height = 840;
        var camera = Camera.main;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            if (path != null) File.WriteAllBytes(path, image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }

    public static void CreateAndValidate()
    {
        if (!File.Exists(ScenePath)) CreateStudy();
        ValidateStudy();
    }
}
