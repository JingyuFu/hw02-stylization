using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MIOOutlineStage
{
    const string RendererPath = "Assets/Render Settings/MIO Outline Renderer.asset";
    const string MaterialPath = "Assets/Materials/MIO/MIO Animated Outlines.mat";
    const string ScenePath = "Assets/Scenes/MIO Outline Study.unity";
    const string Output = "Screenshots/MIO/Outlines";
    static Material material;
    static FullScreenFeature fullScreen;
    static NormalFeature normals;
    static Material[] filaments;
    static readonly List<string> report = new List<string>();

    [MenuItem("HW02 MIO/7 - Build depth and normal outline study")]
    public static void FromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("MIO outline study", "Configure the outline renderer and materials, then save a new outline study scene and previews?", "Build", "Cancel")) return;
        BuildAndValidate();
    }
    public static void BuildAndValidate()
    {
        report.Clear(); Directory.CreateDirectory(Output);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var character = PrefabUtility.LoadPrefabContents("Assets/Models/MIOCharacter/MIO Character.prefab");
        try
        {
            foreach (var r in character.GetComponentsInChildren<MeshRenderer>())
                if (r.name.EndsWith("main filament") || r.name.EndsWith("fine companion")) r.renderingLayerMask = 128;
            PrefabUtility.SaveAsPrefabAsset(character, "Assets/Models/MIOCharacter/MIO Character.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(character); }
        material = MakeMaterial(MaterialPath, AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MIO/MIO Animated Outlines.shadergraph"));
        Defaults();
        var normalMaterial = MakeMaterial("Assets/Materials/MIO/MIO Normal Copy.mat", Shader.Find("Hidden/Normal Copy"));
        var buffer = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Buffers/Normal Buffer.renderTexture");
        if (!File.Exists(RendererPath)) AssetDatabase.CopyAsset("Assets/Render Settings/URP-Custom-Renderer.asset", RendererPath);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        normals = renderer.rendererFeatures.OfType<NormalFeature>().FirstOrDefault();
        if (normals == null) { normals = ScriptableObject.CreateInstance<NormalFeature>(); normals.name = "MIO Separate Normals"; AssetDatabase.AddObjectToAsset(normals, renderer); renderer.rendererFeatures.Add(normals); }
        normals.NormalsTexture = buffer; normals.normalsMaterial = normalMaterial; normals.normalsLayerMask = ~0;
        normals.SetActive(true); normals.Create(); EditorUtility.SetDirty(normals);
        fullScreen = renderer.rendererFeatures.OfType<FullScreenFeature>().FirstOrDefault();
        if (fullScreen == null) { fullScreen = ScriptableObject.CreateInstance<FullScreenFeature>(); fullScreen.name = "MIO Animated Outlines"; AssetDatabase.AddObjectToAsset(fullScreen, renderer); renderer.rendererFeatures.Add(fullScreen); }
        fullScreen.settings.material = material; fullScreen.settings.materialPass = 0;
        fullScreen.SetActive(true); fullScreen.Create(); EditorUtility.SetDirty(fullScreen);
        ConfigureEdgeMask(renderer, material);
        var rendererSO = new SerializedObject(renderer); var map = rendererSO.FindProperty("m_RendererFeatureMap");
        map.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < map.arraySize; i++)
        { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long local); map.GetArrayElementAtIndex(i).longValue = local; }
        rendererSO.ApplyModifiedPropertiesWithoutUndo(); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Render Settings/URP-Custom.asset");
        pipeline.supportsCameraDepthTexture = true;
        var pipelineSO = new SerializedObject(pipeline); var list = pipelineSO.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;
        if (index < 0) { index = list.arraySize; list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = renderer; }
        pipelineSO.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MIO Shader Study.unity");
        Camera.main.GetUniversalAdditionalCameraData().SetRenderer(index);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        Validate();
        WriteNotes();
        // The buffer asset starts at 1080p; NormalFeature resizes it for every actual camera target.
        buffer.Release(); buffer.width = 1920; buffer.height = 1080; EditorUtility.SetDirty(buffer);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("MIO OUTLINE STAGE COMPLETE\n" + string.Join("\n", report));
    }
    static Material MakeMaterial(string path, Shader shader)
    {
        if (shader == null) throw new InvalidOperationException("Shader missing: " + path);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader; EditorUtility.SetDirty(m); return m;
    }
    static void Defaults()
    {
        material.SetColor("_InkColor", new Color(.22f, .105f, .32f, 1));
        material.SetFloat("_OutlineStrength", .94f); material.SetFloat("_DepthWidth", 3);
        material.SetFloat("_NormalWidth", 1.8f); material.SetFloat("_DepthThreshold", .004f);
        material.SetFloat("_NormalThreshold", .36f); material.SetFloat("_DepthStrength", 1);
        material.SetFloat("_NormalStrength", .7f); material.SetFloat("_WobblePixels", .38f);
        material.SetFloat("_WobbleSpeed", 1.2f); material.SetFloat("_SketchFPS", 8);
        material.SetFloat("_PreviewTime", -1); material.SetFloat("_DebugView", 0);
    }
    static void Validate()
    {
        filaments = UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Select(r => r.sharedMaterial)
            .Where(m => m != null && AssetDatabase.GetAssetPath(m.shader).EndsWith("MIO Living Filaments.shadergraph")).Distinct().ToArray();
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation; float size = camera.orthographicSize;
        try
        {
            FlowTime(0); material.SetFloat("_PreviewTime", 0);
            fullScreen.SetActive(false);
            var original = Capture("01-without-post-outline.png");
            fullScreen.SetActive(true); material.SetFloat("_OutlineStrength", 0);
            var identity = Capture("02-zero-strength.png");
            CheckShaders();
            RequireSame(original, identity, "Zero-strength full-screen pass preserves camera color");
            material.SetFloat("_OutlineStrength", .94f);
            var composite = Capture("03-animated-outlines.png");
            RequireChanged(original, composite, 1200, "Post-process outlines change the image");
            material.SetFloat("_DebugView", 1); var depth = Capture("04-depth-buffer.png");
            CheckBuffers(depth, "static");
            material.SetFloat("_DebugView", 2); Capture("05-normal-buffer.png");
            material.SetFloat("_WobblePixels", 0); material.SetFloat("_DebugView", 3);
            var depthEdges = Capture("06-depth-edges.png");
            if (WhitePixels(depthEdges) < 2000) throw new InvalidOperationException("Missing depth edges: " + WhitePixels(depthEdges));
            material.SetFloat("_DebugView", 4); var normalEdges = Capture("07-normal-edges.png");
            if (WhitePixels(normalEdges) < 100) throw new InvalidOperationException("Missing normal edges: " + WhitePixels(normalEdges));
            Pass("Independent depth and normal edge maps both contain edges.");
            material.SetFloat("_DebugView", 3); material.SetFloat("_DepthWidth", 5);
            var thick = Capture(null);
            if (WhitePixels(thick) <= WhitePixels(depthEdges) * 1.25f) throw new InvalidOperationException("Width does not increase line coverage.");
            Pass("Increasing depth width increases outline coverage.");
            material.SetFloat("_DepthWidth", 3); material.SetFloat("_DebugView", 0);
            var steady = Capture(null); material.SetFloat("_PreviewTime", 1.2f);
            RequireSame(steady, Capture(null), "Zero wobble gives stable outlines with frozen geometry");
            material.SetFloat("_WobblePixels", .38f); material.SetFloat("_PreviewTime", 0);
            var wobble0 = Capture(null); material.SetFloat("_PreviewTime", .75f);
            RequireChanged(wobble0, Capture("08-outline-wobble.png"), 150, "Contour animation works independently of vertex animation");
            FlowTime(1.65f); material.SetFloat("_DebugView", 1);
            CheckBuffers(Capture(null), "animated filaments");
            material.SetFloat("_DebugView", 2); Capture("09-moving-normal-buffer.png");
            material.SetFloat("_DebugView", 0); Capture("10-moving-filament-outlines.png");
            camera.orthographic = false; camera.fieldOfView = 35;
            Capture("11-perspective.png", 1280, 720);
            if (normals.NormalsTexture.width != 1280 || normals.NormalsTexture.height != 720) throw new InvalidOperationException("Normal buffer did not resize.");
            material.SetFloat("_DebugView", 1); CheckBuffers(Capture(null, 1280, 720), "perspective camera");
            Pass("Normal buffer matches camera resolution (1600x1000 and 1280x720); perspective depth also works.");
            camera.orthographic = true; material.SetFloat("_DebugView", 0); FlowTime(0); material.SetFloat("_PreviewTime", 0);
            camera.transform.position = new Vector3(-1.8f, 2.48f, -9); camera.transform.LookAt(new Vector3(0, 1.73f, 0)); camera.orthographicSize = 1.12f;
            Capture("12-outline-detail.png", 1000, 1000);
            camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size;
            Directory.CreateDirectory(Output + "/Frames");
            for (int i = 0; i < 40; i++)
            {
                float t = i / 10f; FlowTime(t); material.SetFloat("_PreviewTime", t);
                Capture("Frames/" + i.ToString("D3") + ".png", 960, 600);
            }
            CheckShaders(); Pass("Outline graph and Normal Copy compile and render without shader errors.");
            File.WriteAllText(Output + "/outline-validation.txt", string.Join("\n", report));
        }
        finally
        {
            Defaults(); FlowTime(-1); fullScreen.SetActive(true);
            camera.orthographic = true; camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size;
            EditorUtility.SetDirty(material); foreach (var m in filaments) EditorUtility.SetDirty(m);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
    static void FlowTime(float time) { foreach (var m in filaments) m.SetFloat("_PreviewTime", time); }
    public static void ConfigureEdgeMask(UniversalRendererData renderer, Material outline)
    {
        var feature = renderer.rendererFeatures.OfType<MIOEdgeMaskFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<MIOEdgeMaskFeature>(); feature.name = "MIO Fixed Edge Mask";
            AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
        }
        feature.outlineMaterial = outline;
        feature.maskShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MIO/MIO Edge Mask.shader");
        feature.passEvent = (RenderPassEvent)449; feature.SetActive(true); feature.Create(); EditorUtility.SetDirty(feature);
    }
    static void CheckShaders()
    {
        foreach (var shader in new[] { material.shader, normals.normalsMaterial.shader })
        {
            var errors = ShaderUtil.GetShaderMessages(shader).Where(e => e.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.message)));
        }
    }
    static void CheckBuffers(Color32[] depth, string label)
    {
        var rt = normals.NormalsTexture; var active = RenderTexture.active;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
        Color32[] pixels;
        try { RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply(); pixels = texture.GetPixels32(); }
        finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(texture); }
        int union = 0, intersection = 0, normalCount = 0;
        for (int i = 0; i < depth.Length; i++)
        {
            bool d = depth[i].r < 245, n = pixels[i].a > 20;
            if (d || n) union++; if (d && n) intersection++; if (n) normalCount++;
        }
        float iou = (float)intersection / Math.Max(1, union);
        if (normalCount < 1000 || iou < .87f) throw new InvalidOperationException("Depth/normal silhouette mismatch (" + label + "): IoU=" + iou + ", normals=" + normalCount);
        Pass("Separate depth/normal silhouettes align for " + label + ": IoU=" + iou.ToString("F3") + ".");
    }
    static Color32[] Capture(string name, int width = 1600, int height = 1000)
    {
        var camera = Camera.main; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            if (name != null) File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    static int Difference(Color32[] a, Color32[] b)
    { int n = 0; for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>8) n++; return n; }
    static int WhitePixels(Color32[] pixels) => pixels.Count(p => p.r > 100);
    static void RequireChanged(Color32[] a, Color32[] b, int count, string message)
    { int d = Difference(a,b); if (d < count) throw new InvalidOperationException(message + ": only " + d + " pixels"); Pass(message + ": " + d + " changed pixels."); }
    static void RequireSame(Color32[] a, Color32[] b, string message)
    { int d = Difference(a,b); if (d != 0) throw new InvalidOperationException(message + ": " + d + " pixels differ"); Pass(message + ": zero changed pixels."); }
    static void Pass(string text) { report.Add("PASS: " + text); Debug.Log("MIO PASS: " + text); }
    static void WriteNotes()
    {
        File.WriteAllText("Assets/Shaders/MIO/Task 3 Notes.txt",
            "HW02 task 3: animated depth/normal outlines\n\n" +
            "Open Assets/Scenes/MIO Outline Study.unity and press Play. Camera uses MIO Outline Renderer; earlier study scenes keep their original renderer.\n\n" +
            "Full Screen Feature.cs fixes the starter's missing blit-back, using URP 14 RTHandles and Blitter. MIO Animated Outlines.shadergraph is a URP Fullscreen Shader Graph; its Custom Function implements Roberts Cross edge detection over separate camera depth and normal textures. Depth edges and normal creases have independent width, threshold and strength. Ink color follows the reference's dark violet.\n\n" +
            "NormalFeature is connected to the supplied Normal Buffer render texture and a Normal Copy material. It draws each material's native DepthNormals pass (so vertex-animated filaments match), then Normal Copy resolves signed world normals to encoded view normals. This is a NORMAL-ONLY texture, separate from camera depth. Its resolution updates to the current camera target automatically.\n\n" +
            "Contour samples are warped by smoothly interpolated noise with stepped time (8 fps); internal normal edges remain steady. Wobble amplitude and speed are adjustable. This effect is distinct from the physical mesh seams and UV pencil-shadow texture. Fixed Edge Mask detects at the original fixed 1.5px depth/1px normal footprints; exposed widths dilate that mask, so thicker ink does not detect additional creases.\n\n" +
            "To preserve the concept art's ivory filaments, their rendering layer is 128. NormalFeature captures their animated normals but marks their alpha .25, keeping them out of dark ink. Ordinary geometry alpha is 1; background is 0. Set Unoutlined Rendering Layers to 0 to outline everything. Depth remains in its separate camera texture.\n\n" +
            "Material: Assets/Materials/MIO/MIO Animated Outlines.mat. Debug View: 0 final, 1 depth, 2 normals, 3 depth edges, 4 normal edges, 5 combined edges. Set Outline Strength to 0 for comparison. Hidden Preview Time is -1 for live animation.\n\n" +
            "Screenshots/MIO/Outlines contains separate buffer/edge previews, identity comparison, moving-silhouette checks and rendering validation.\n\n" +
            "Task 4 is implemented separately in MIO Paper Study. Task 6 is implemented in MIO Interactive Study. Remaining submission work: final turnaround video/README/PR.\n\n" +
            "URP reference: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/customize/blit-to-rthandle.html\n" +
            "Concept art: Raphaelle Colin, https://raphaelle_colin.artstation.com/projects/BkbRor\n");
    }
}
