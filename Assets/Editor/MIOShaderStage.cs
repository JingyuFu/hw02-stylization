using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HW02 task 2: improved toon lighting and a derived, vertex-animated shader.
public static class MIOShaderStage
{
    const string Materials = "Assets/Materials/MIOCharacter/";
    const string Prefab = "Assets/Models/MIOCharacter/MIO Character.prefab";
    const string Scene = "Assets/Scenes/MIO Shader Study.unity";
    const string Output = "Screenshots/MIO/Shaders";
    const string Toon = "Assets/Shaders/MIO/MIO Toon.shadergraph";
    const string Flow = "Assets/Shaders/MIO/MIO Living Filaments.shadergraph";
    static readonly List<string> report = new List<string>();
    static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

    [MenuItem("HW02 MIO/5 - Apply rim and living filament shaders")]
    public static void FromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Apply MIO shaders", "Update the generated MIO materials and prefab, then create the shader study scene?", "Apply", "Cancel")) return;
        BuildAndValidate();
    }

    // Also called after a full character rebuild, keeping generated assets consistent.
    public static void ApplyToCharacter(GameObject character)
    {
        var palette = character.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial)
            .Where(m => m != null && AssetDatabase.GetAssetPath(m.shader) == Toon).Distinct().ToArray();
        foreach (var m in palette)
        {
            m.SetColor("_RimColor", Color.Lerp(m.GetColor("_Highlight"), Hex("FFF5FF"), .6f));
            m.SetFloat("_RimStrength", m.name.Contains("Golden") ? .42f : .65f);
            m.SetFloat("_RimStart", .55f);
            m.SetFloat("_RimSoftness", .22f);
            ConfigureHatching(m);
            EditorUtility.SetDirty(m);
        }
        var left = FlowMaterial("09 - Living filaments left", -1, .35f);
        var right = FlowMaterial("10 - Living filaments right", 1, 1.05f);
        int count = 0;
        foreach (var renderer in character.GetComponentsInChildren<MeshRenderer>())
        {
            if (!IsFilament(renderer.name)) continue;
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            PrepareCurveData(mesh, renderer.name.Contains("fine companion") ? 6 : 8);
            renderer.sharedMaterial = renderer.name.StartsWith("Left") ? left : right;
            renderer.renderingLayerMask = 128; // Outline mask: preserve ivory filaments.
            count++;
        }
        if (count != 12) throw new InvalidOperationException("Expected 12 flowing filaments; got " + count);
    }

    static bool IsFilament(string name) => name.EndsWith("main filament") || name.EndsWith("fine companion");

    // Per-material UV tiling keeps pencil strokes visible at the presentation scale.
    public static void ConfigureHatching(Material material)
    {
        bool gold = material.name.Contains("Golden") || material.name.Contains("Collar");
        float scale = material.name.Contains("Celadon") || material.name.Contains("Collar") ? 2f : 3f;
        material.SetFloat("_PatternStrength", gold ? .90f : .98f);
        material.SetFloat("_ShadowScale", scale);
        EditorUtility.SetDirty(material);
    }

    static Material FlowMaterial(string name, float side, float phase)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Flow);
        if (shader == null) throw new InvalidOperationException("Missing living filament shader.");
        var path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        var ivory = Hex("FFFEEE");
        material.SetColor("_Highlight", ivory); material.SetColor("_Midtone", ivory); material.SetColor("_Shadow", ivory);
        material.SetFloat("_PatternStrength", 0); material.SetFloat("_ShadowScale", 4);
        material.SetTexture("_ShadowPattern", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/MIO/MIO Hatching.png"));
        material.SetColor("_RimColor", ivory); material.SetFloat("_RimStrength", 0);
        material.SetFloat("_FlowAmplitude", .14f); material.SetFloat("_FlowSpeed", 1.15f);
        material.SetFloat("_WaveCount", 1.1f); material.SetFloat("_FlowPhase", phase);
        material.SetFloat("_FlowSide", side); material.SetFloat("_PreviewTime", -1);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void PrepareCurveData(Mesh mesh, int sides)
    {
        int stride = sides + 1, rings = (mesh.vertexCount - 2) / stride;
        if (rings * stride + 2 != mesh.vertexCount) throw new InvalidOperationException("Unexpected tube topology: " + mesh.name);
        var vertices = mesh.vertices; var uv = mesh.uv;
        float length = uv[(rings - 1) * stride].y;
        if (length < 1) throw new InvalidOperationException("Invalid filament length: " + mesh.name);
        var centers = new Vector3[rings];
        for (int r = 0; r < rings; r++)
        {
            for (int j = 0; j < sides; j++) centers[r] += vertices[r * stride + j];
            centers[r] /= sides;
        }
        var data = new List<Vector4>(new Vector4[vertices.Length]);
        for (int r = 0; r < rings; r++)
        {
            var gradient = (centers[Math.Min(rings - 1, r + 1)] - centers[Math.Max(0, r - 1)]).normalized / length;
            var v = new Vector4(gradient.x, gradient.y, gradient.z, uv[r * stride].y / length);
            for (int j = 0; j <= sides; j++) data[r * stride + j] = v;
        }
        // End caps must follow their ring, including the pinned root.
        data[vertices.Length - 2] = data[0]; data[vertices.Length - 1] = data[(rings - 1) * stride];
        uv[vertices.Length - 2] = new Vector2(.5f, 0); uv[vertices.Length - 1] = new Vector2(.5f, length);
        mesh.uv = uv; mesh.SetUVs(1, data);
        mesh.RecalculateBounds(); var bounds = mesh.bounds;
        bounds.Expand(.66f); mesh.bounds = bounds; // Shader clamps maximum displacement to .3 object units.
        EditorUtility.SetDirty(mesh);
    }

    public static void BuildAndValidate()
    {
        report.Clear(); Directory.CreateDirectory(Output);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try { ApplyToCharacter(root); ValidateFilaments(root); PrefabUtility.SaveAsPrefabAsset(root, Prefab); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MIO Character Study.unity");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Scene);
        ValidateAndCapture();
        WriteNotes();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("MIO SHADER STAGE COMPLETE\n" + string.Join("\n", report));
    }

    static void ValidateFilaments(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>().Where(r => IsFilament(r.name)))
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var data = new List<Vector4>(); mesh.GetUVs(1, data);
            if (data.Count != mesh.vertexCount || data[0].w != 0 || data[data.Count - 2].w != 0 || data[data.Count - 1].w != 1)
                throw new InvalidOperationException("Curve data/cap mismatch: " + mesh.name);
            if (data.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z) || v.w < 0 || v.w > 1))
                throw new InvalidOperationException("Invalid deformation data: " + mesh.name);
            var geometric = new Bounds(mesh.vertices[0], Vector3.zero);
            foreach (var v in mesh.vertices) geometric.Encapsulate(v);
            if ((mesh.bounds.size - geometric.size).y < .65f) throw new InvalidOperationException("Insufficient animated bounds.");
        }
        Pass("All 12 filaments have curve data, pinned roots, matching cap data and expanded culling bounds.");
    }

    static void ValidateAndCapture()
    {
        var renderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
        var materials = renderers.Select(r => r.sharedMaterial).Where(m => m != null).Distinct().ToArray();
        var toon = materials.Where(m => AssetDatabase.GetAssetPath(m.shader) == Toon).ToArray();
        var flow = materials.Where(m => AssetDatabase.GetAssetPath(m.shader) == Flow).ToArray();
        if (toon.Length != 6 || flow.Length != 2) throw new InvalidOperationException("Unexpected scene material assignments.");
        var savedRim = toon.Select(m => m.GetFloat("_RimStrength")).ToArray();
        var camera = Camera.main; var oldPos = camera.transform.position; var oldRot = camera.transform.rotation; var oldSize = camera.orthographicSize;
        var flowRenderers = renderers.Where(r => IsFilament(r.name)).ToArray();
        try
        {
            foreach (var m in flow) m.SetFloat("_PreviewTime", 0);
            camera.transform.position = new Vector3(-1.8f, 2.48f, -9); camera.transform.LookAt(new Vector3(0, 1.73f, 0)); camera.orthographicSize = 1.12f;
            foreach (var m in toon) m.SetFloat("_RimStrength", 0);
            var off = Capture("01-rim-off.png", 1000, 1000);
            for (int i = 0; i < toon.Length; i++) toon[i].SetFloat("_RimStrength", savedRim[i]);
            var on = Capture("02-rim-on.png", 1000, 1000);
            RequireDifference(off, on, 200, "Rim lighting changes the rendered armor");
            camera.transform.SetPositionAndRotation(oldPos, oldRot); camera.orthographicSize = oldSize;
            var frame0 = Capture("03-character-shaders.png", 1600, 1000);
            foreach (var m in flow) m.SetFloat("_PreviewTime", 1.65f);
            var frame1 = Capture("04-filaments-time-1.65.png", 1600, 1000);
            RequireDifference(frame0, frame1, 2500, "Vertex animation changes the filament silhouette");
            foreach (var m in flow) { m.SetFloat("_FlowAmplitude", 0); m.SetFloat("_PreviewTime", 0); }
            var fixed0 = Capture(null, 800, 500);
            foreach (var m in flow) m.SetFloat("_PreviewTime", 2.7f);
            var fixed1 = Capture(null, 800, 500);
            RequireSame(fixed0, fixed1, "Zero amplitude disables movement");
            foreach (var m in flow) { m.SetFloat("_FlowAmplitude", .14f); m.SetFloat("_FlowSpeed", 0); }
            var paused0 = Capture(null, 800, 500);
            foreach (var m in flow) m.SetFloat("_PreviewTime", 4.1f);
            var paused1 = Capture(null, 800, 500);
            RequireSame(paused0, paused1, "Zero speed freezes the animation");
            foreach (var m in flow) m.SetFloat("_FlowSpeed", 1.15f);
            // Removing the animated renderers isolates the rest of the scene.
            foreach (var r in flowRenderers) r.enabled = false;
            var static0 = Capture(null, 800, 500);
            foreach (var m in flow) m.SetFloat("_PreviewTime", 0);
            var static1 = Capture(null, 800, 500);
            RequireSame(static0, static1, "Helmet, body, wings and lens remain stationary");
            foreach (var r in flowRenderers) r.enabled = true;
            // Frames for a looping preview; render actual Unity camera output.
            Directory.CreateDirectory(Output + "/Frames");
            const int count = 64;
            for (int i = 0; i < count; i++)
            {
                foreach (var m in flow) m.SetFloat("_PreviewTime", i * (2 * Mathf.PI / 1.15f) / count);
                Capture("Frames/" + i.ToString("D3") + ".png", 960, 600);
            }
            foreach (var shader in materials.Select(m => m.shader).Distinct())
            {
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.message)));
            }
            Pass("Both Shader Graphs compiled and rendered without shader errors (D3D11).");
        }
        finally
        {
            for (int i = 0; i < toon.Length; i++) { toon[i].SetFloat("_RimStrength", savedRim[i]); EditorUtility.SetDirty(toon[i]); }
            foreach (var m in flow) { m.SetFloat("_FlowAmplitude", .14f); m.SetFloat("_FlowSpeed", 1.15f); m.SetFloat("_PreviewTime", -1); EditorUtility.SetDirty(m); }
            foreach (var r in flowRenderers) r.enabled = true;
            camera.transform.SetPositionAndRotation(oldPos, oldRot); camera.orthographicSize = oldSize;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Scene);
        File.WriteAllText(Output + "/shader-validation.txt", string.Join("\n", report));
    }

    static Color32[] Capture(string name, int width, int height)
    {
        var camera = Camera.main; var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            if (name != null) File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally { camera.targetTexture = target; RenderTexture.active = active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
    }

    static int Difference(Color32[] a, Color32[] b)
    {
        int changed = 0;
        for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 8) changed++;
        return changed;
    }
    static void RequireDifference(Color32[] a, Color32[] b, int minimum, string message)
    {
        int difference = Difference(a, b);
        if (difference < minimum) throw new InvalidOperationException(message + ": only " + difference + " changed pixels");
        Pass(message + ": " + difference + " changed pixels.");
    }
    static void RequireSame(Color32[] a, Color32[] b, string message)
    {
        int difference = Difference(a, b);
        if (difference != 0) throw new InvalidOperationException(message + ": " + difference + " changed pixels");
        Pass(message + ": zero changed pixels.");
    }
    static void Pass(string message) { report.Add("PASS: " + message); Debug.Log("MIO PASS: " + message); }

    static void WriteNotes()
    {
        File.WriteAllText("Assets/Shaders/MIO/Task 2 Notes.txt",
            "HW02 - Task 2: Interesting Shaders\n\n" +
            "Open Assets/Scenes/MIO Shader Study.unity and press Play.\n\n" +
            "MIO Toon.shadergraph derives from the user's Lab 03 shader. It combines main/additional lights, a three-color palette and a custom seamless hatching texture sampled in UV0 with Shadow Scale. SoftRim adds a view-dependent, lighting-weighted rim; use Rim Strength = 0 to compare. Rim Color, Start and Softness control its appearance.\n\n" +
            "MIO Living Filaments.shadergraph duplicates that improved graph and adds FilamentWave in the vertex stage (task 2 special shader, option 2). Smoothstep pins the roots; sine/cosine waves displace vertices along all three axes. UV1 carries curve distance and its gradient, while UV0 remains the surface UV. The vertex normal/tangent are transformed with the deformation Jacobian. End caps share ring data and bounds cover the full amplitude range. Only the 12 long filaments use this shader; the lens, pearl and throat strands remain static.\n\n" +
            "Materials 01-06: Rim Strength / Rim Color / Rim Start / Rim Softness.\n" +
            "Materials 09-10: Wave Amplitude / Wave Speed / Wave Count / Wave Phase. Zero amplitude restores the rest shape; zero speed freezes movement. A hidden Preview Time is used for repeatable captures, and is saved at -1 for normal Time-node animation.\n\n" +
            "Screenshots/MIO/Shaders includes on/off rim renders, two animation times and GPU validation results.\n" +
            "Pencil strokes use an original seamless texture, with stronger ink and wider strokes for full-character framing. Character Hatching Strength is 0.78-0.92, Shadow Scale is 2-3. See Screenshots/MIO/Hatching for current pencil-shadow comparisons.\n" +
            "Task 3 is implemented separately in MIO Outline Study. Task 4 is implemented in MIO Paper Study; task 6 is implemented in MIO Interactive Study.\n\n" +
            "Reference: Raphaelle Colin, Mio: Memories in Orbit - Shii, https://raphaelle_colin.artstation.com/projects/BkbRor\n" +
            "Shader Graph Custom Function documentation: https://docs.unity3d.com/Packages/com.unity.shadergraph@14.0/manual/Custom-Function-Node.html\n");
    }
}
