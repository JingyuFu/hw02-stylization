using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MIOPaperStage
{
    const string RendererPath = "Assets/Render Settings/MIO Paper Renderer.asset";
    const string ScenePath = "Assets/Scenes/MIO Paper Study.unity";
    const string Output = "Screenshots/MIO/Paper";
    static Material outline, backdrop, paper;
    static FullScreenFeature backgroundPass, paperPass;
    static Material[] filaments;
    static readonly List<string> report = new List<string>();

    [MenuItem("HW02 MIO/8 - Build patterned background and paper study")]
    public static void FromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAndValidate();
    }
    public static void BuildAndValidate()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        MIOFoundation.CreateHatching(true);
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/MIOCharacter" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (AssetDatabase.GetAssetPath(m.shader).EndsWith("MIO Toon.shadergraph")) MIOShaderStage.ConfigureHatching(m);
        }
        MIOOutlineStage.BuildAndValidate();
        BuildPaperAndValidate();
    }
    // Allows a visual iteration without repeating all earlier outline tests.
    public static void BuildPaperAndValidate()
    {
        report.Clear(); Directory.CreateDirectory(Output);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MIO/MIO Animated Outlines.mat");
        backdrop = MakeMaterial("MIO Memory Backdrop"); paper = MakeMaterial("MIO Paper Grain");
        Defaults();
        if (!File.Exists(RendererPath)) AssetDatabase.CopyAsset("Assets/Render Settings/MIO Outline Renderer.asset", RendererPath);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        MIOOutlineStage.ConfigureEdgeMask(renderer, outline);
        backgroundPass = Feature(renderer, "MIO Painted Background", backdrop, 250);
        paperPass = Feature(renderer, "MIO Paper Finish", paper, (int)RenderPassEvent.AfterRenderingTransparents + 2);
        var rendererSO = new SerializedObject(renderer); var map = rendererSO.FindProperty("m_RendererFeatureMap");
        map.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < map.arraySize; i++)
        { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long local); map.GetArrayElementAtIndex(i).longValue = local; }
        rendererSO.ApplyModifiedPropertiesWithoutUndo(); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Render Settings/URP-Custom.asset");
        var so = new SerializedObject(pipeline); var list = so.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;
        if (index < 0) { index = list.arraySize; list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = renderer; }
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MIO Outline Study.unity");
        Camera.main.GetUniversalAdditionalCameraData().SetRenderer(index);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        filaments = UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Select(r => r.sharedMaterial)
            .Where(m => m != null && AssetDatabase.GetAssetPath(m.shader).EndsWith("MIO Living Filaments.shadergraph")).Distinct().ToArray();
        Validate(); WriteNotes();
        var buffer = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Buffers/Normal Buffer.renderTexture");
        buffer.Release(); buffer.width=1920; buffer.height=1080; EditorUtility.SetDirty(buffer);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("MIO PAPER STAGE COMPLETE\n" + string.Join("\n", report));
    }
    static Material MakeMaterial(string name)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MIO/" + name + ".shadergraph");
        if (shader == null) throw new InvalidOperationException("Missing shader: " + name);
        string path = "Assets/Materials/MIO/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m,path); }
        m.shader=shader; EditorUtility.SetDirty(m); return m;
    }
    static FullScreenFeature Feature(UniversalRendererData renderer, string name, Material material, int passEvent)
    {
        var feature = renderer.rendererFeatures.OfType<FullScreenFeature>().FirstOrDefault(f => f.name == name);
        if (feature == null) { feature = ScriptableObject.CreateInstance<FullScreenFeature>(); feature.name=name; AssetDatabase.AddObjectToAsset(feature,renderer); renderer.rendererFeatures.Add(feature); }
        feature.settings.material=material; feature.settings.materialPass=0; feature.settings.renderPassEvent=(RenderPassEvent)passEvent;
        // Neither painted background nor paper samples depth. In particular the
        // background must not force a non-MSAA depth prepass before opaque geometry.
        feature.settings.requiresDepth=false;
        feature.SetActive(true); feature.Create(); EditorUtility.SetDirty(feature); return feature;
    }
    static void Defaults()
    {
        outline.SetFloat("_DepthWidth",3); outline.SetFloat("_NormalWidth",1.8f); outline.SetFloat("_OutlineStrength",.94f);
        outline.SetFloat("_DebugView",0); outline.SetFloat("_PreviewTime",-1);
        backdrop.SetFloat("_Strength",1); backdrop.SetFloat("_WashStrength",1);
        backdrop.SetFloat("_GlyphStrength",.57f); backdrop.SetFloat("_GlyphDensity",1);
        paper.SetFloat("_Strength",.78f); paper.SetFloat("_GrainScale",1); paper.SetFloat("_FiberStrength",.8f);
        foreach (var m in new[] { outline,backdrop,paper }) EditorUtility.SetDirty(m);
    }
    static void Validate()
    {
        var camera=Camera.main; var position=camera.transform.position; var rotation=camera.transform.rotation;
        float size=camera.orthographicSize;
        try
        {
            Time(0); paperPass.SetActive(false); backgroundPass.SetActive(false);
            outline.SetFloat("_DepthWidth",1.5f); outline.SetFloat("_NormalWidth",1); outline.SetFloat("_OutlineStrength",.88f);
            var thin=Capture("01-thin-outlines.png"); var fixedBefore=ReadRT((RenderTexture)Shader.GetGlobalTexture("_MIOFixedEdgeMask"));
            outline.SetFloat("_DepthWidth",3); outline.SetFloat("_NormalWidth",1.8f); outline.SetFloat("_OutlineStrength",.94f);
            var thicker=Capture("02-thicker-ink.png"); var fixedAfter=ReadRT((RenderTexture)Shader.GetGlobalTexture("_MIOFixedEdgeMask"));
            Same(fixedBefore,fixedAfter,"Widening lines leaves the detected depth/normal edge mask exactly unchanged");
            Changed(thin,thicker,1000,"Wider ink visibly changes existing line coverage");
            backgroundPass.SetActive(true); backdrop.SetFloat("_Strength",0);
            NearSame(thicker,Capture("00-backdrop-zero.png"),"Zero backdrop strength preserves the previous composite within one 8-bit level");
            backdrop.SetFloat("_Strength",1); backdrop.SetFloat("_GlyphStrength",0);
            var washes=Capture("03-painted-washes.png");
            backdrop.SetFloat("_GlyphStrength",.57f); var patterned=Capture("04-patterned-background.png");
            Changed(washes,patterned,10000,"Pale glyphs are visible over the painted washes");
            var normals=ReadRT(AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Buffers/Normal Buffer.renderTexture"));
            int foreground=0;
            // Check pixels well inside geometry, away from partially covered silhouettes.
            for(int y=3;y<997;y++) for(int x=3;x<1597;x++)
            {
                int i=y*1600+x;
                bool interior=true;
                for(int dy=-3;dy<=3 && interior;dy++) for(int dx=-3;dx<=3;dx++)
                    if(normals[i+dy*1600+dx].a<128) { interior=false; break; }
                if(!interior) continue;
                foreground++;
                if(Diff(thicker[i],patterned[i])>8) throw new InvalidOperationException("Background paint touched a foreground interior.");
            }
            Require(foreground>10000,"Background is drawn before geometry; " + foreground + " foreground interior pixels are preserved");
            paperPass.SetActive(true); paper.SetFloat("_Strength",0);
            NearSame(patterned,Capture("05-paper-zero.png"),"Zero paper strength preserves the patterned scene within one 8-bit level");
            paper.SetFloat("_Strength",.78f); var final=Capture("06-paper-finish.png");
            int heroChanges=0, backgroundChanges=0;
            for(int i=0;i<final.Length;i++) if(Diff(final[i],patterned[i])>8) { if(normals[i].a>20) heroChanges++; else backgroundChanges++; }
            Require(heroChanges>5000 && backgroundChanges>100000,"Paper grain covers the hero ("+heroChanges+" pixels) and background ("+backgroundChanges+" pixels)");
            StablePaper(final,Capture("06b-paper-still.png"));
            Time(1.3f); Changed(final,Capture("07-live-motion.png"),1000,"Filament and outline animation remain active under paper"); Time(0);
            camera.transform.position=new Vector3(-1.8f,2.48f,-9); camera.transform.LookAt(new Vector3(0,1.73f,0)); camera.orthographicSize=1.12f;
            paper.SetFloat("_Strength",0); Capture("08-detail-without-paper.png",1000,1000);
            paper.SetFloat("_Strength",.78f); Capture("09-ink-and-paper-detail.png",1000,1000);
            camera.transform.SetPositionAndRotation(position,rotation); camera.orthographicSize=size;
            Capture("10-widescreen.png",1600,900);
            camera.orthographic=false; camera.fieldOfView=35; Capture("11-perspective.png",1280,720); camera.orthographic=true;
            CheckShaders();
            Require(true,"Fullscreen backdrop, edge mask, outlines and paper compile and render at 1600x1000, 1600x900 and 1280x720");
            Directory.CreateDirectory(Output+"/Frames");
            for(int i=0;i<32;i++) { Time(i*.1f); Capture("Frames/"+i.ToString("D3")+".png",960,600); }
            File.WriteAllText(Output+"/paper-validation.txt",string.Join("\n",report));
        }
        finally
        {
            Defaults(); Time(-1); backgroundPass.SetActive(true); paperPass.SetActive(true);
            camera.orthographic=true; camera.transform.SetPositionAndRotation(position,rotation); camera.orthographicSize=size;
            foreach(var m in filaments) EditorUtility.SetDirty(m);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath); AssetDatabase.SaveAssets();
        }
    }
    static void CheckShaders()
    {
        foreach(var shader in new[] {outline.shader,backdrop.shader,paper.shader,AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MIO/MIO Edge Mask.shader")})
        {
            var errors=ShaderUtil.GetShaderMessages(shader).Where(e=>e.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if(errors.Length>0) throw new InvalidOperationException(shader.name+": "+string.Join("\n",errors.Select(e=>e.message)));
        }
    }
    static void Time(float t) { outline.SetFloat("_PreviewTime",t); foreach(var m in filaments) m.SetFloat("_PreviewTime",t); }
    static Color32[] ReadRT(RenderTexture rt)
    {
        if(rt==null) throw new InvalidOperationException("Missing render buffer.");
        var old=RenderTexture.active; var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,true);
        try { RenderTexture.active=rt; image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply(); return image.GetPixels32(); }
        finally { RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(image); }
    }
    static Color32[] Capture(string name,int width=1600,int height=1000)
    {
        var camera=Camera.main; var oldTarget=camera.targetTexture; var oldActive=RenderTexture.active;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=4 };
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt; camera.Render(); camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            if(name!=null) File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG()); return image.GetPixels32();
        }
        finally { camera.targetTexture=oldTarget; RenderTexture.active=oldActive; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    static int Diff(Color32 a,Color32 b) => Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
    static int Difference(Color32[] a,Color32[] b) { int n=0; for(int i=0;i<a.Length;i++) if(Diff(a[i],b[i])>8) n++; return n; }
    static void Same(Color32[] a,Color32[] b,string message) { int n=0; for(int i=0;i<a.Length;i++) if(!a[i].Equals(b[i])) n++; Require(n==0,message+": "+n+" changed pixels"); }
    static void NearSame(Color32[] a,Color32[] b,string message)
    {
        int maximum=0;
        for(int i=0;i<a.Length;i++) maximum=Math.Max(maximum,Math.Max(Math.Abs(a[i].r-b[i].r),Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b))));
        // A blit adds an sRGB roundtrip around the MSAA resolve; allow
        // at most one quantization level, not a change to shading or coverage.
        Require(maximum<=1,message+": maximum channel difference "+maximum+"/255");
    }
    static void StablePaper(Color32[] a,Color32[] b)
    {
        int maximum=0,changed=0;
        for(int i=0;i<a.Length;i++)
        {
            if(!a[i].Equals(b[i])) changed++;
            maximum=Math.Max(maximum,Math.Max(Math.Abs(a[i].r-b[i].r),Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b))));
        }
        // GPU surface lighting has a handful of 1-2 LSB variations in repeated
        // captures. Animated grain would change a large fraction of the image.
        Require(maximum<=3 && changed<128,"Frozen paper stays stable: "+changed+"/"+a.Length+" pixels differ, maximum "+maximum+"/255 (GPU tolerance <=3/255 on <128 pixels)");
    }
    static void Changed(Color32[] a,Color32[] b,int minimum,string message) { int n=Difference(a,b); Require(n>minimum,message+": "+n+" changed pixels"); }
    static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); report.Add("PASS: "+message); Debug.Log("MIO PASS: "+message); }
    static void WriteNotes()
    {
        File.WriteAllText("Assets/Shaders/MIO/Task 4 Notes.txt",
            "HW02 task 4: patterned painted background and full-screen paper\n\n"+
            "Open Assets/Scenes/MIO Paper Study.unity and press Play. The camera uses MIO Paper Renderer. This includes task 2 surface shading/filaments, task 3 depth/normal ink, and task 4 full-screen paper.\n\n"+
            "Rendering order: original procedural background (event 250, before opaque geometry); opaque character with native MSAA; separate normal buffer; fixed edge detection (449); widened animated ink (AfterRenderingTransparents, 500); full-screen paper (502). Drawing the backdrop first preserves antialiased silhouettes.\n\n"+
            "MIO Memory Backdrop.shadergraph and MIOMemoryPaper.hlsl build mint/sage/cream painted washes with domain-warped noise. Original line motifs are constructed from circles and bent segments. Their scale, rotation and opacity vary; motifs become denser toward the bottom and quieter behind the hero. These are newly authored procedural shapes inspired by the reference, not the original image pasted behind the model.\n\n"+
            "MIO Paper Grain.shadergraph applies stationary multi-scale paper tooth, fibers and uneven pigment after ALL scene elements and outlines. Therefore the paper covers both the character and the background. Paper Strength=0 disables the paper treatment. Grain Scale and Fiber Strength are exposed. Paper uses normalized screen coordinates (1080-height units) and does not flicker with Time.\n\n"+
            "Ink revision: Depth Width 1.5 -> 3; Normal Width 1 -> 1.8; Outline Strength .88 -> .94. Fixed Roberts detection footprints and thresholds remain unchanged. The separate RG mask stores depth/normal edges, and mask dilation thickens only previously detected lines. Ivory filaments remain excluded from dark ink.\n\n"+
            "Shadow revision: the seamless UV pencil texture keeps its 7-cycle frequency and per-material Shadow Scale of 2/3, while stroke half-width increases .085 -> .125. Pattern Strength increases to .90 on gold and .98 elsewhere. No extra hatch lines or shadow-threshold changes.\n\n"+
            "Controls: Assets/Materials/MIO/MIO Memory Backdrop.mat (wash strength, glyph opacity/density), MIO Paper Grain.mat (paper/grain/fibers), MIO Animated Outlines.mat (ink widths/strength/wobble).\n\n"+
            "Screenshots/MIO/Paper contains ink, wash, glyph and paper comparisons, detail/widescreen/perspective renders, animation and validation. Earlier step screenshots are historical comparisons.\n\n"+
            "Task 6 is implemented in MIO Interactive Study. Remaining submission work: final turnaround video, comprehensive README and PR.\n\n"+
            "Concept art: Raphaelle Colin, Mio: Memories in Orbit - Shii, https://raphaelle_colin.artstation.com/projects/BkbRor\n");
        string path="Assets/Shaders/MIO/Task 2 Notes.txt";
        if(File.Exists(path)) File.WriteAllText(path,File.ReadAllText(path).Replace("Task 4 full-screen effects and task 6 interaction are still pending.","Task 4 is implemented in MIO Paper Study; task 6 is implemented in MIO Interactive Study."));
    }
}
