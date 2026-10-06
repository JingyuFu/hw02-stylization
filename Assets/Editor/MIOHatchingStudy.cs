using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Readable pencil shadows, HW02 task 2 (Interesting Shadow).
public static class MIOHatchingStudy
{
    const string Output = "Screenshots/MIO/Hatching";
    const string Materials = "Assets/Materials/MIOCharacter/";
    static Material[] toon, flow;

    [MenuItem("HW02 MIO/6 - Capture pencil shadow comparison")]
    public static void FromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CaptureStudy();
    }

    // Used for this authored texture revision, not automatically on project load.
    public static void ApplyAndCapture()
    {
        MIOFoundation.CreateHatching(true);
        foreach (var file in Directory.GetFiles(Materials, "*.mat"))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(file);
            if (material != null && AssetDatabase.GetAssetPath(material.shader) == "Assets/Shaders/MIO/MIO Toon.shadergraph")
                MIOShaderStage.ConfigureHatching(material);
        }
        AssetDatabase.SaveAssets();
        CaptureStudy();
    }

    static void CaptureStudy()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/MIO Shader Study.unity");
        var all = UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Select(r => r.sharedMaterial).Where(m => m != null).Distinct().ToArray();
        toon = all.Where(m => AssetDatabase.GetAssetPath(m.shader) == "Assets/Shaders/MIO/MIO Toon.shadergraph").ToArray();
        flow = all.Where(m => AssetDatabase.GetAssetPath(m.shader) == "Assets/Shaders/MIO/MIO Living Filaments.shadergraph").ToArray();
        var strengths = toon.Select(m => m.GetFloat("_PatternStrength")).ToArray();
        var times = flow.Select(m => m.GetFloat("_PreviewTime")).ToArray();
        var scales = toon.Select(m => m.GetFloat("_ShadowScale")).ToArray();
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation; float size = camera.orthographicSize;
        var report = new List<string>();
        try
        {
            foreach (var m in flow) m.SetFloat("_PreviewTime", 0);
            SetStrengths(strengths);
            var full = Capture("01-full-pencil-shadows.png", 1600, 1000);
            foreach (var m in toon) m.SetFloat("_PatternStrength", 0);
            var fullOff = Capture("02-full-without-hatching.png", 1600, 1000);
            int changed = Difference(full, fullOff);
            if (changed < 1500) throw new InvalidOperationException("Pencil hatching is not sufficiently visible in the full shot: " + changed);
            report.Add("PASS: Hatching is visible at full-character framing: " + changed + " changed pixels.");
            // Hatching must add darker ink rather than brighten the surface.
            int lighter = 0;
            for (int i = 0; i < full.Length; i++)
                if (full[i].r + full[i].g + full[i].b > fullOff[i].r + fullOff[i].g + fullOff[i].b + 8) lighter++;
            if (lighter != 0) throw new InvalidOperationException("Hatching unexpectedly brightens pixels: " + lighter);
            report.Add("PASS: Pencil strokes darken the surface; no unintended brightening.");
            camera.transform.position = new Vector3(-1.8f, 2.48f, -9); camera.transform.LookAt(new Vector3(0, 1.73f, 0)); camera.orthographicSize = 1.12f;
            Capture("03-detail-without-hatching.png", 1000, 1000);
            SetStrengths(strengths);
            var detail = Capture("04-detail-pencil-shadows.png", 1000, 1000);
            for (int i = 0; i < toon.Length; i++) toon[i].SetFloat("_ShadowScale", scales[i] * 1.6f);
            var denser = Capture(null, 1000, 1000);
            if (Difference(detail, denser) < 1000) throw new InvalidOperationException("Shadow Scale does not change the UV pattern.");
            report.Add("PASS: Shadow Scale changes pencil stroke density.");
            for (int i = 0; i < toon.Length; i++) toon[i].SetFloat("_ShadowScale", scales[i]);
            camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size;
            var frame1 = Capture(null, 800, 500);
            foreach (var m in flow) m.SetFloat("_PreviewTime", 1.65f);
            var frame2 = Capture(null, 800, 500);
            if (Difference(frame1, frame2) < 1000) throw new InvalidOperationException("Filament animation was lost.");
            report.Add("PASS: Living filament animation remains active.");
            foreach (var shader in all.Select(m => m.shader).Distinct())
            {
                var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.message)));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/MIO/MIO Hatching.png");
            if (importer.wrapMode != TextureWrapMode.Repeat || importer.sRGBTexture || !importer.mipmapEnabled)
                throw new InvalidOperationException("Invalid shadow texture import settings.");
            report.Add("PASS: Shader compilation and linear, repeating, mipmapped texture import.");
            File.WriteAllText(Output + "/hatching-validation.txt", string.Join("\n", report));
            Debug.Log("MIO HATCHING CHECKS COMPLETE\n" + string.Join("\n", report));
        }
        finally
        {
            SetStrengths(strengths);
            for (int i = 0; i < toon.Length; i++) { toon[i].SetFloat("_ShadowScale", scales[i]); EditorUtility.SetDirty(toon[i]); }
            for (int i = 0; i < flow.Length; i++) { flow[i].SetFloat("_PreviewTime", times[i]); EditorUtility.SetDirty(flow[i]); }
            camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size;
            AssetDatabase.SaveAssets();
        }
    }

    static void SetStrengths(float[] strengths) { for (int i = 0; i < toon.Length; i++) toon[i].SetFloat("_PatternStrength", strengths[i]); }
    static int Difference(Color32[] a, Color32[] b)
    {
        int changed = 0;
        for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b) > 8) changed++;
        return changed;
    }
    static Color32[] Capture(string name, int width, int height)
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
}
