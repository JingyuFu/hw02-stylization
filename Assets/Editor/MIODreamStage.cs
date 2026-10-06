using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MIODreamStage
{
    const string Output="Screenshots/MIO/Dream";
    const string Graph="Assets/Shaders/MIO/MIO Dream Palette.shadergraph";
    const string TexturePath="Assets/Textures/MIO/MIO Dream Pigment.png";
    const string ScenePath="Assets/Scenes/MIO Interactive Study.unity";
    const string PlayKey="MIO.Dream.PlayTest";
    static readonly List<string> report=new List<string>();
    static MIOStyleSwitcher style;
    static Material[] dream, original, moving;
    static Material outline;
    static MIODreamStage() { if(SessionState.GetBool(PlayKey,false)) EditorApplication.playModeStateChanged+=PlayState; }

    [MenuItem("HW02 MIO/12 - Build and validate dream palette")]
    public static void FromMenu() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildAndValidate(); }
    public static void BuildAndValidate()
    {
        Directory.CreateDirectory(Output); report.Clear();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); CreatePigment();
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Graph);
        if(shader==null) throw new InvalidOperationException("Dream graph failed to import");
        original=Directory.GetFiles("Assets/Materials/MIOCharacter","*.mat").OrderBy(p=>p).Take(6).Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
        string[][] palette={new[]{"D6DDFF","A69EE5","65649C","7AD6E1"},new[]{"E7C8F2","AF94D9","625B92","80BFED"},
            new[]{"FFE5B7","EBAAC0","9681C3","94DDE2"},new[]{"CBF3E2","86C9DC","6C7EBC","CAB0EF"},
            new[]{"D9D2FA","A594DD","625B96","80C9E1"},new[]{"FCE5B9","E1B5C6","9073B5","A5DCDC"}};
        Directory.CreateDirectory("Assets/Materials/MIODream"); AssetDatabase.Refresh(); dream=new Material[6];
        for(int i=0;i<6;i++)
        {
            var p="Assets/Materials/MIODream/"+original[i].name+" - Dream.mat";
            dream[i]=AssetDatabase.LoadAssetAtPath<Material>(p);
            if(dream[i]==null) { dream[i]=new Material(original[i]); AssetDatabase.CreateAsset(dream[i],p); }
            var m=dream[i]; m.shader=shader;
            m.SetColor("_Highlight",Hex(palette[i][0]));m.SetColor("_Midtone",Hex(palette[i][1]));m.SetColor("_Shadow",Hex(palette[i][2]));m.SetColor("_AccentColor",Hex(palette[i][3]));
            m.SetFloat("_PigmentTextureStrength",.26f);m.SetFloat("_PigmentVariation",.65f);m.SetFloat("_PigmentScale",1.8f);m.SetFloat("_PigmentUVScale",1.5f);
            m.SetTexture("_PigmentMap",AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            m.SetFloat("_PatternStrength",.64f);m.SetFloat("_Smoothness",.14f);m.SetFloat("_RimStrength",.36f);
            m.SetColor("_RimColor",Hex("DEF3EA"));EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        foreach(var scene in new[]{ScenePath,"Assets/Scenes/MIO Showcase.unity"})
        {
            EditorSceneManager.OpenScene(scene); Configure(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        EditorSceneManager.OpenScene(ScenePath); Find();
        try { Validate(); }
        finally { style.SetMode(0); style.enabled=true; Time(-1); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets(); }
        File.WriteAllText(Output+"/dream-validation.txt",string.Join("\n",report));
        File.WriteAllText("Assets/Shaders/MIO/Dream Palette Notes.txt",
            "Extra credit candidate: MIO Dream Palette is a distinct surface Shader Graph derived from MIO Toon. RGB Pigment Map sampled in UV0 + adjustable UV scale, object-space domain-warped chromatic washes, independently colored light/middle/shadow bands, and cyan pigment pooling alter hue/saturation rather than scaling brightness. Original hatching, rim, multiple lights, and native depth/normal passes are retained.\n\n"+
            "Space cycles Color Paper -> Graphite -> MIO Dream Palette -> Color Paper in Interactive Study and Showcase. Only six solid-surface materials swap; animated ivory filaments retain their shader. Disabling the controller restores original assignments.\n\n"+
            "Palette reference: user-supplied MIO: Memories in Orbit gameplay screenshot (lavender foliage, cyan haze, peach/coral and cream highlights). This is an interpretation, not the game's original shader.\n\n"+
            "The updated 36-second turnaround demonstrates all three modes. BGM is pending the user's chosen file in the root BGM folder. Extra-credit evaluation remains subject to instructor review.\n");
        AssetDatabase.Refresh(); Debug.Log("MIO DREAM STAGE COMPLETE\n"+string.Join("\n",report));
    }
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value,out var c);return c; }
    static void CreatePigment()
    {
        const int n=512;var texture=new Texture2D(n,n,TextureFormat.RGB24,false);var pixels=new Color[n*n];
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            float u=(x+.5f)/n,v=(y+.5f)/n,t=2*Mathf.PI;
            float warp=.20f*Mathf.Sin(t*(u+v))+.10f*Mathf.Sin(t*(3*u-2*v));
            float wash=.5f+.22f*Mathf.Sin(t*(u+warp))+.17f*Mathf.Cos(t*(2*v+warp))+.1f*Mathf.Sin(t*(3*u-2*v));
            float blooms=.5f+.5f*Mathf.Sin(t*(2*u+v+.15f*Mathf.Sin(t*3*v)));
            var c=Color.Lerp(Hex("8E9CD5"),Hex("BCE7DF"),Mathf.SmoothStep(0,1,wash));
            c=Color.Lerp(c,Hex("EDBACB"),Mathf.SmoothStep(.55f,1,blooms)*.6f);
            float dry=.5f+.5f*Mathf.Sin(t*(37*u+19*v)) * Mathf.Cos(t*(11*u-31*v));
            pixels[y*n+x]=Color.Lerp(c,Hex("FFF0D4"),dry*.07f);
        }
        texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(TexturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(TexturePath);importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
    }
    static void Configure()
    {
        style=Camera.main.GetComponent<MIOStyleSwitcher>();style.enabled=true;style.SetMode(0);
        style.materials=new[]{style.materials[0],style.materials[1],style.materials[0]};
        var mesh=UnityEngine.Object.FindObjectsOfType<MeshRenderer>().First(r=>original.Contains(r.sharedMaterial));
        style.surfaceRoot=PrefabUtility.GetOutermostPrefabInstanceRoot(mesh.gameObject).transform;
        style.originalSurfaceMaterials=original;style.dreamSurfaceMaterials=dream;EditorUtility.SetDirty(style);
    }
    static void Find()
    {
        style=Camera.main.GetComponent<MIOStyleSwitcher>(); dream=style.dreamSurfaceMaterials;
        outline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MIO/MIO Animated Outlines.mat");
        moving=UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Select(r=>r.sharedMaterial).Where(m=>m.HasProperty("_FlowAmplitude")).Distinct().ToArray();
    }
    static void Time(float t) { outline.SetFloat("_PreviewTime",t);foreach(var m in moving)m.SetFloat("_PreviewTime",t); }
    static void Require(bool pass,string note) { if(!pass)throw new InvalidOperationException(note);report.Add("PASS: "+note); }
    static int Difference(Color32[] a,Color32[] b) { int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>8)n++;return n; }
    static void Validate()
    {
        Time(0);var color=Capture("01-color.png");style.HandleKeyDown(KeyCode.Space);var graphite=Capture("02-graphite.png");style.HandleKeyDown(KeyCode.Space);var painted=Capture("03-dream.png");
        Require(style.CurrentMode==2 && Difference(color,painted)>12000,"Third Space-cycle style uses six distinct Dream surface materials and changes the visible palette");
        Require(graphite.Count(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b))-Mathf.Min(c.r,Mathf.Min(c.g,c.b))>1)==0,"Graphite remains monochrome");
        foreach(var m in dream)m.SetFloat("_PigmentTextureStrength",0);
        Require(Difference(painted,Capture("04-without-rgb-texture.png"))>1000,"RGB pigment texture affects the surface independently");foreach(var m in dream)m.SetFloat("_PigmentTextureStrength",.26f);
        foreach(var m in dream)m.SetFloat("_PigmentVariation",0);
        Require(Difference(painted,Capture("05-without-chromatic-wash.png"))>1000,"Procedural chromatic wash affects the surface independently");foreach(var m in dream)m.SetFloat("_PigmentVariation",.65f);
        var fill=UnityEngine.Object.FindObjectsOfType<Light>().First(l=>l.type==LightType.Point);fill.enabled=false;
        Require(Difference(painted,Capture(null))>100,"Dream shader responds to the additional point light");fill.enabled=true;
        Time(1.2f);Require(Difference(painted,Capture(null))>1000,"Vertex and outline animation remain active in Dream mode");Time(0);
        style.HandleKeyDown(KeyCode.Space);var restored=Capture("09-restored-color.png");int restorationDifference=Difference(color,restored);
        Require(style.CurrentMode==0 && restorationDifference==0,"A complete three-style cycle restores original rendering exactly within quantization tolerance; differing pixels="+restorationDifference);
        for(int i=0;i<12;i++)style.HandleKeyDown(KeyCode.Space);
        Require(Difference(color,Capture(null))==0,"Repeated three-style cycles do not accumulate material changes");
        // OnDisable is a runtime lifecycle event: exercise restoration in the
        // separate Play-mode run, not by disabling a non-ExecuteAlways script here.
        style.SetMode(2);
        var camera=Camera.main;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize;
        camera.transform.position=new Vector3(-1.8f,2.48f,-9);camera.transform.LookAt(new Vector3(0,1.73f,0));camera.orthographicSize=1.12f;Capture("06-dream-detail.png",1000,1000);
        camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;camera.orthographic=false;Capture("07-dream-perspective.png",1280,720);camera.orthographic=true;
        var errors=ShaderUtil.GetShaderMessages(dream[0].shader).Where(e=>e.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();Require(errors.Length==0,"Dream Shader Graph compiles and renders in orthographic and perspective views: "+string.Join("; ",errors.Select(e=>e.message)));
    }
    static Color32[] Capture(string name,int width=1600,int height=900)
    {
        var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try {camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();if(name!=null)File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());return image.GetPixels32();}
        finally {camera.targetTexture=old;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
    public static void ValidatePlayMode()
    {
        EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool(PlayKey,true);EditorApplication.playModeStateChanged+=PlayState;EditorApplication.isPlaying=true;
    }
    static void PlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=PlayAssertions;
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            try {Require(Camera.main.GetComponent<MIOStyleSwitcher>().CurrentMode==0,"Exiting Play restores the default mode");SessionState.EraseBool(PlayKey);Debug.Log("MIO DREAM PLAY COMPLETE");EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
    static void PlayAssertions()
    {
        try
        {
            report.Clear();Find();Time(0);var color=Capture(null,960,540);
            style.HandleKeyDown(KeyCode.Space);style.HandleKeyDown(KeyCode.Space);var painted=Capture("08-play-mode-dream.png",960,540);
            Require(style.CurrentMode==2 && Difference(color,painted)>4000,"Play-mode input handler reaches Dream surface rendering");
            style.HandleKeyDown(KeyCode.Space);Require(Difference(color,Capture(null,960,540))==0,"Play-mode third keypress restores Color Paper");
            style.SetMode(2);style.enabled=false;Require(Difference(color,Capture(null,960,540))==0,"Disabling in Play restores original renderer materials");style.enabled=true;
            Time(-1);File.WriteAllText(Output+"/play-mode-validation.txt",string.Join("\n",report));EditorApplication.isPlaying=false;
        }
        catch(Exception e){SessionState.EraseBool(PlayKey);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
