using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class MIOInteractivityStage
{
    const string RendererPath="Assets/Render Settings/MIO Interactive Renderer.asset";
    const string ScenePath="Assets/Scenes/MIO Interactive Study.unity";
    const string Output="Screenshots/MIO/Interactivity";
    static MIOStyleSwitcher style;
    static Material graphite, paper, outline;
    static Material[] filaments;
    static FullScreenFeature finish;
    static readonly List<string> report=new List<string>();
    static readonly Dictionary<string,string> originalMaterialFiles=new Dictionary<string,string>();
    const string PlayTestKey="MIO.Interactivity.PlayTest";
    const string PlaySnapshot="Temp/MIOStyleAssetSnapshot.json";
    [Serializable] class AssetSnapshot { public string[] paths; public string[] contents; }
    static MIOInteractivityStage()
    {
        // Reconnect only an explicitly requested validation run across Unity's
        // normal domain reload when entering and leaving Play mode.
        if(SessionState.GetBool(PlayTestKey,false)) EditorApplication.playModeStateChanged+=PlayModeState;
    }

    [MenuItem("HW02 MIO/9 - Build interactive color and graphite study")]
    public static void FromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAndValidate();
    }
    public static void BuildAndValidate()
    {
        report.Clear(); Directory.CreateDirectory(Output);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        paper=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MIO/MIO Paper Grain.mat");
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MIO/MIO Graphite Sketch.shadergraph");
        if (shader==null) throw new InvalidOperationException("Graphite graph was not imported.");
        const string materialPath="Assets/Materials/MIO/MIO Graphite Sketch.mat";
        graphite=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(graphite==null) { graphite=new Material(shader); AssetDatabase.CreateAsset(graphite,materialPath); }
        graphite.SetFloat("_Contrast",1.08f); graphite.SetFloat("_HatchSpacing",8.5f);
        graphite.SetFloat("_HatchStrength",.90f); graphite.SetFloat("_PencilWidth",1.05f); graphite.SetFloat("_PaperStrength",.8f);
        EditorUtility.SetDirty(graphite);
        // URP 14 AfterRenderingTransparents is 500. Finish after the ink pass,
        // so both paper and graphite include the composited silhouette lines.
        var paperRenderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Render Settings/MIO Paper Renderer.asset");
        var paperFinish=paperRenderer.rendererFeatures.OfType<FullScreenFeature>().Single(f=>f.name=="MIO Paper Finish");
        paperFinish.settings.renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.AfterRenderingTransparents+2);
        paperFinish.Create(); EditorUtility.SetDirty(paperFinish); paperRenderer.SetDirty(); EditorUtility.SetDirty(paperRenderer);
        AssetDatabase.SaveAssets();
        if(!File.Exists(RendererPath)) AssetDatabase.CopyAsset("Assets/Render Settings/MIO Paper Renderer.asset",RendererPath);
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        finish=renderer.rendererFeatures.OfType<FullScreenFeature>().Single(f=>f.name=="MIO Paper Finish" || f.name=="MIO Style Finish");
        finish.name="MIO Style Finish"; finish.settings.material=paper; finish.settings.useCameraStyle=true;
        finish.settings.renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.AfterRenderingTransparents+2);
        finish.Create(); EditorUtility.SetDirty(finish); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Render Settings/URP-Custom.asset");
        var so=new SerializedObject(pipeline); var list=so.FindProperty("m_RendererDataList"); int index=-1;
        for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer) index=i;
        if(index<0) { index=list.arraySize; list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue=renderer; }
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MIO Paper Study.unity");
        Camera.main.GetUniversalAdditionalCameraData().SetRenderer(index);
        style=Camera.main.gameObject.AddComponent<MIOStyleSwitcher>(); style.materials=new[] {paper,graphite}; style.showControls=true;
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
        FindAnimationMaterials(); Validate(); WriteNotes();
        var buffer=AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Buffers/Normal Buffer.renderTexture");
        buffer.Release(); buffer.width=1920; buffer.height=1080; EditorUtility.SetDirty(buffer);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("MIO INTERACTIVITY STAGE COMPLETE\n"+string.Join("\n",report));
    }
    static void FindAnimationMaterials()
    {
        outline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MIO/MIO Animated Outlines.mat");
        filaments=UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Select(r=>r.sharedMaterial)
            .Where(m=>m!=null && AssetDatabase.GetAssetPath(m.shader).EndsWith("MIO Living Filaments.shadergraph")).Distinct().ToArray();
    }
    static void Time(float t) { outline.SetFloat("_PreviewTime",t); foreach(var m in filaments) m.SetFloat("_PreviewTime",t); }
    static void Validate()
    {
        var camera=Camera.main; var position=camera.transform.position; var rotation=camera.transform.rotation; float size=camera.orthographicSize;
        try
        {
            Time(0);
            Require(style.materials.Length==2 && style.materials[0].shader!=style.materials[1].shader,"Controller references two distinct shader materials");
            style.enabled=false; var fallback=Capture(null); style.enabled=true;
            var color=Capture("01-color-paper.png"); Same(fallback,color,"Default interactive mode matches the original paper pass");
            style.HandleKeyDown(KeyCode.Return); Require(style.CurrentMode==0,"Unrelated key does not change style");
            style.HandleKeyDown(KeyCode.Space); Require(style.CurrentMode==1 && style.ActiveMaterial==graphite,"Space selects the graphite material");
            var sketch=Capture("02-graphite-sketch.png");
            CheckShader();
            Require(Difference(color,sketch)>700000,"Color and graphite have a substantial full-frame visual difference");
            int colored=sketch.Count(c=>Math.Max(c.r,Math.Max(c.g,c.b))-Math.Min(c.r,Math.Min(c.g,c.b))>1);
            Require(colored==0,"Graphite output is monochrome across all pixels");
            graphite.SetFloat("_HatchStrength",0); var bands=Capture("03-tones-without-crosshatching.png");
            graphite.SetFloat("_HatchStrength",.9f);
            Require(Difference(sketch,bands)>10000,"New crosshatching materially changes the tonal-band result");
            style.HandleKeyDown(KeyCode.Space); var restored=Capture("04-restored-color.png");
            Same(color,restored,"Second Space restores the original color scene");
            for(int i=0;i<20;i++) style.HandleKeyDown(KeyCode.Space);
            Require(style.CurrentMode==0 && finish.settings.material==paper,"Repeated switching preserves the renderer asset's default material");
            Same(color,Capture(null),"Repeated switches do not accumulate effects");
            style.HandleKeyDown(KeyCode.Space); style.enabled=false;
            Same(color,Capture(null),"Disabling the controller safely falls back to color paper");
            style.enabled=true; if(style.CurrentMode==0) style.HandleKeyDown(KeyCode.Space);
            Time(1.2f); Require(Difference(sketch,Capture("05-graphite-in-motion.png"))>1000,"Existing filament/outline animation survives graphite mode"); Time(0);
            camera.transform.position=new Vector3(-1.8f,2.48f,-9); camera.transform.LookAt(new Vector3(0,1.73f,0)); camera.orthographicSize=1.12f;
            Capture("06-graphite-detail.png",1000,1000); style.HandleKeyDown(KeyCode.Space); Capture("07-color-detail.png",1000,1000);
            camera.transform.SetPositionAndRotation(position,rotation); camera.orthographicSize=size;
            style.HandleKeyDown(KeyCode.Space); Capture("08-graphite-widescreen.png",1600,900);
            camera.orthographic=false; camera.fieldOfView=35; Capture("09-graphite-perspective.png",1280,720); camera.orthographic=true;
            CheckShader(); Require(true,"Graphite Shader Graph compiles and renders at multiple aspect ratios and in perspective");
            Directory.CreateDirectory(Output+"/Frames");
            for(int i=0;i<40;i++)
            {
                int desired=i>=10 && i<30 ? 1 : 0;
                if(style.CurrentMode!=desired) style.HandleKeyDown(KeyCode.Space);
                Time(i*.12f); Capture("Frames/"+i.ToString("D3")+".png",960,600);
            }
            File.WriteAllText(Output+"/render-validation.txt",string.Join("\n",report));
        }
        finally
        {
            Time(-1); if(style.CurrentMode!=0) style.HandleKeyDown(KeyCode.Space); style.enabled=true;
            graphite.SetFloat("_HatchStrength",.9f); EditorUtility.SetDirty(graphite);
            camera.orthographic=true; camera.transform.SetPositionAndRotation(position,rotation); camera.orthographicSize=size;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
    static Color32[] Capture(string name,int width=1600,int height=1000)
    {
        var camera=Camera.main; var target=camera.targetTexture; var active=RenderTexture.active;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=4 };
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt; camera.Render(); camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            if(name!=null) File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG()); return image.GetPixels32();
        }
        finally { camera.targetTexture=target; RenderTexture.active=active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
    static void CheckShader()
    {
        var errors=ShaderUtil.GetShaderMessages(graphite.shader).Where(e=>e.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
        if(errors.Length>0) throw new InvalidOperationException(string.Join("\n",errors.Select(e=>e.message)));
    }
    static int Difference(Color32[] a,Color32[] b)
    { int n=0; for(int i=0;i<a.Length;i++) if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>8) n++; return n; }
    static void Same(Color32[] a,Color32[] b,string label) { int n=Difference(a,b); Require(n==0,label+": "+n+" differences above GPU quantization tolerance"); }
    static void Require(bool pass,string message) { if(!pass) throw new InvalidOperationException(message); report.Add("PASS: "+message); Debug.Log("MIO PASS: "+message); }

    // Run in a separate hidden editor process, without -quit, to exercise the
    // actual play-mode lifecycle and the same key handler called by Update().
    public static void ValidatePlayMode()
    {
        report.Clear(); originalMaterialFiles.Clear(); Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(ScenePath);
        foreach(var path in Directory.GetFiles("Assets/Materials/MIO","*.mat")) originalMaterialFiles[path]=File.ReadAllText(path);
        originalMaterialFiles[RendererPath]=File.ReadAllText(RendererPath);
        File.WriteAllText(PlaySnapshot,JsonUtility.ToJson(new AssetSnapshot { paths=originalMaterialFiles.Keys.ToArray(), contents=originalMaterialFiles.Values.ToArray() }));
        SessionState.SetBool(PlayTestKey,true);
        EditorApplication.playModeStateChanged+=PlayModeState;
        EditorApplication.isPlaying=true;
    }
    static void PlayModeState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall+=RunPlayAssertions;
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            try
            {
                report.Clear(); report.AddRange(File.ReadAllLines(Output+"/play-mode-validation.txt"));
                var snapshot=JsonUtility.FromJson<AssetSnapshot>(File.ReadAllText(PlaySnapshot));
                originalMaterialFiles.Clear(); for(int i=0;i<snapshot.paths.Length;i++) originalMaterialFiles[snapshot.paths[i]]=snapshot.contents[i];
                foreach(var pair in originalMaterialFiles) Require(File.ReadAllText(pair.Key)==pair.Value,"Play mode leaves asset unchanged: "+pair.Key);
                Require(Camera.main.GetComponent<MIOStyleSwitcher>().CurrentMode==0,"Leaving Play returns to the default scene state");
                File.WriteAllText(Output+"/play-mode-validation.txt",string.Join("\n",report));
                SessionState.EraseBool(PlayTestKey); Debug.Log("MIO PLAY MODE VALIDATION COMPLETE"); EditorApplication.Exit(0);
            }
            catch(Exception error) { SessionState.EraseBool(PlayTestKey); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
    static void RunPlayAssertions()
    {
        try
        {
            style=Camera.main.GetComponent<MIOStyleSwitcher>(); paper=style.materials[0]; graphite=style.materials[1]; FindAnimationMaterials(); Time(0);
            Require(Application.isPlaying && style.isActiveAndEnabled && style.CurrentMode==0,"Play starts in color with an enabled keyboard controller");
            var color=Capture("10-play-mode-color.png",960,600);
            style.HandleKeyDown(KeyCode.Space); var sketch=Capture("11-play-mode-graphite.png",960,600);
            Require(style.CurrentMode==1 && Difference(color,sketch)>200000,"Space handler swaps the actual rendered material during Play");
            style.HandleKeyDown(KeyCode.Return); Require(style.CurrentMode==1,"Other keys are ignored during Play");
            style.HandleKeyDown(KeyCode.Space); Same(color,Capture(null,960,600),"Space toggles back during Play");
            style.HandleKeyDown(KeyCode.Space); style.enabled=false;
            Same(color,Capture(null,960,600),"Disabled runtime controller falls back to color");
            style.enabled=true; Require(style.CurrentMode==0,"Re-enabling the runtime controller resets it to color");
            Time(-1); File.WriteAllText(Output+"/play-mode-validation.txt",string.Join("\n",report)); EditorApplication.isPlaying=false;
        }
        catch(Exception error) { SessionState.EraseBool(PlayTestKey); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void WriteNotes()
    {
        File.WriteAllText("Assets/Shaders/MIO/Task 6 Notes.txt",
            "HW02 task 6: keyboard-driven color paper / graphite sketch\n\n"+
            "Open Assets/Scenes/MIO Interactive Study.unity, press Play, click the Game view and press Space. Each key-down switches between Color Paper and Graphite. A small bottom-left label shows the control and current mode. Earlier study scenes remain separate.\n\n"+
            "MIOStyleSwitcher.cs is attached to the main camera with two assigned materials. Update uses Input.GetKeyDown(KeyCode.Space), matching this project's existing legacy Input Manager. Its key handler advances the runtime material index. MIO Interactive Renderer's Style Finish pass resolves the selected material for this camera. Shared material and renderer assets are never edited by the runtime switch.\n\n"+
            "Color Paper uses the existing MIO Paper Grain material. Graphite uses a NEW material and a NEW Fullscreen Shader Graph, MIO Graphite Sketch, with MIOGraphite.hlsl. It reconstructs luminance as five graphite value bands, adds three differently oriented procedural pencil stroke families according to shadow depth, varies stroke curvature/dryness, turns ivory filaments into graphite lines using the existing normal-buffer mask, and finishes with neutral paper tooth/fibers. It is more than a saturation or color parameter change.\n\n"+
            "The depth/normal outlines, object-UV pencil shadows, background motifs and filament vertex animation remain active in both modes. Graphite hatch spacing/width/strength, tonal contrast and paper strength are adjustable in MIO Graphite Sketch.mat. The new crosshatching is a screen-space post effect; the original object-UV shadow texture is retained underneath.\n\n"+
            "Validation includes actual Unity renders of both materials, crosshatching-off comparison, restoration after repeated switching, animation, multiple camera aspect ratios and perspective. A separate Play-mode run invokes the same key handler used by Update, checks the rendered swap and lifecycle, and verifies that shared asset files are unchanged. Physical OS keyboard input is checked by the user in Game view; the automated test invokes the key handler directly.\n\n"+
            "Screenshots/MIO/Interactivity contains color/graphite previews, detail images, switching animation and validation reports.\n\n"+
            "Tasks 1-6 are implemented. Remaining submission work: a turnaround video, comprehensive README, and PR. Extra credit is optional.\n\n"+
            "Concept art: Raphaelle Colin, Mio: Memories in Orbit - Shii, https://raphaelle_colin.artstation.com/projects/BkbRor\n");
    }
}
