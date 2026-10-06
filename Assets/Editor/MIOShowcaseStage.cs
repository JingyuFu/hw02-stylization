using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIOShowcaseStage
{
    const string ScenePath = "Assets/Scenes/MIO Showcase.unity";
    const string Output = "Screenshots/MIO/Showcase";
    const string VideoPath = "Videos/MIO/MIO-Turnaround-1080p.mp4";
    static MIOShowcaseOrbit orbit;
    static Material outline;
    static Material[] filaments;
    static MeshRenderer[] renderers;
    static readonly List<string> report = new List<string>();
    static readonly float[] ReviewTimes = { 0, 5.2f, 7.22f, 10, 12.78f, 14.8f, 18, 21, 24.6f, 27.5f, 28, 30.5f, 35.5f };
    public static void RebuildAndExport() { BuildAndPreview(); ExportVideo(); }

    [MenuItem("HW02 MIO/10 - Build and preview the 360 showcase")]
    public static void FromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAndPreview();
    }
    [MenuItem("HW02 MIO/11 - Export 36 second 1080p showcase video")]
    public static void ExportFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ExportVideo();
    }
    public static void BuildAndPreview()
    {
        Directory.CreateDirectory(Output); Directory.CreateDirectory("Videos/MIO"); report.Clear();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/MIO Interactive Study.unity");
        orbit = Camera.main.gameObject.AddComponent<MIOShowcaseOrbit>();
        Camera.main.orthographicSize = 4.3f; // Margin for the elevated side-view filaments.
        Camera.main.GetComponent<MIOStyleSwitcher>().showControls = false;
        orbit.Restart(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        FindMaterials();
        using (var capture = new FrameCapture(1600, 900))
        {
            foreach (float t in ReviewTimes)
            {
                Sample(t); var image = capture.Render();
                File.WriteAllBytes(Output + "/" + t.ToString("00.00",System.Globalization.CultureInfo.InvariantCulture) + "s.png", image.EncodeToPNG());
                ValidateBounds();
                report.Add("PASS: t="+t+"s, orbit="+MIOShowcaseOrbit.AngleAt(t).ToString("F1")+" degrees, style="+Camera.main.GetComponent<MIOStyleSwitcher>().CurrentMode+", hero stays within frame.");
            }
        }
        if (Mathf.Abs(MIOShowcaseOrbit.AngleAt(18)-MIOShowcaseOrbit.AngleAt(2)-360)>.001f)
            throw new InvalidOperationException("Orbit does not complete a full revolution.");
        report.Add("PASS: the uninterrupted color segment completes exactly 360 degrees.");
        CheckShaders(); report.Add("PASS: all scene shaders render without compiler errors.");
        // Validate the installed Unity encoder before spending time on all 1080 frames.
        using (var capture = new FrameCapture(640,360))
        using (var encoder = new MediaEncoder(Path.Combine(Path.GetTempPath(),"mio-showcase-encoder-check.mp4"), Track(640,360)))
            for(int i=0;i<30;i++) { Sample(i/30f); if(!encoder.AddFrame(capture.Render())) throw new InvalidOperationException("MediaEncoder rejected a preview frame."); }
        report.Add("PASS: Unity's H.264/MP4 encoder accepted all 30 preview frames.");
        Reset(); WriteNotes();
        File.WriteAllText(Output+"/showcase-validation.txt",string.Join("\n",report));
        Debug.Log("MIO SHOWCASE PREVIEW COMPLETE\n"+string.Join("\n",report));
    }
    public static void ExportVideo()
    {
        Directory.CreateDirectory(Output); Directory.CreateDirectory("Videos/MIO");
        EditorSceneManager.OpenScene(ScenePath); orbit=Camera.main.GetComponent<MIOShowcaseOrbit>(); FindMaterials();
        const int width=1920, height=1080, fps=30, frames=1080;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using (var capture=new FrameCapture(width,height))
            using (var encoder=new MediaEncoder(VideoPath,Track(width,height)))
            {
                for(int frame=0;frame<frames;frame++)
                {
                    float t=(float)frame/fps; Sample(t); ValidateBounds(); var image=capture.Render();
                    if(!encoder.AddFrame(image)) throw new InvalidOperationException("Encoder rejected frame "+frame);
                    if(frame==0) File.WriteAllBytes(Output+"/poster-1080p.png",image.EncodeToPNG());
                    if(frame%60==0) Debug.Log("MIO VIDEO: "+frame+"/"+frames+" frames; timeline "+t.ToString("F1")+"s; elapsed "+watch.Elapsed.TotalSeconds.ToString("F1")+"s");
                    if(!Application.isBatchMode && frame%15==0 && EditorUtility.DisplayCancelableProgressBar("MIO 1080p showcase","Rendering frame "+frame+" / "+frames,(float)frame/frames))
                        throw new OperationCanceledException("Video export cancelled. The partially rendered MP4 remains at "+VideoPath);
                }
            }
            CheckShaders();
            File.WriteAllText(Output+"/video-export.txt","Unity 2022.3.62f2 / D3D11 / MediaEncoder\n"+
                "1920 x 1080, 30 fps, 1080 frames, 36 seconds, MP4/H.264, no audio\n"+
                "0-2s: color front. 2-18s: full 360-degree color orbit. 18-20s: front.\n"+
                "20-25s: graphite, with a gentle three-quarter move after 23s.\n25-32s: Dream Palette. 32-36s: return to color and front.\n"+
                "Every frame was rendered from the Unity scene. Hidden shader preview times advance at 1/30s per frame; all reset to live time afterward.\n"+
                "All 1080 encoder AddFrame calls succeeded; shader checks passed.\n"+
                "File bytes: "+new FileInfo(VideoPath).Length+"\nExport elapsed seconds: "+watch.Elapsed.TotalSeconds.ToString("F1")+"\n");
            WriteNotes();
            Debug.Log("MIO SHOWCASE VIDEO COMPLETE: "+VideoPath+"; "+new FileInfo(VideoPath).Length+" bytes; "+watch.Elapsed.TotalSeconds.ToString("F1")+" seconds elapsed.");
        }
        finally { EditorUtility.ClearProgressBar(); Reset(); }
    }
    static VideoTrackAttributes Track(uint width,uint height) => new VideoTrackAttributes
    { width=width, height=height, frameRate=new MediaRational(30), includeAlpha=false, bitRateMode=VideoBitrateMode.High };
    static void FindMaterials()
    {
        outline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MIO/MIO Animated Outlines.mat");
        renderers=UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
        filaments=renderers.Select(r=>r.sharedMaterial)
            .Where(m=>m!=null && AssetDatabase.GetAssetPath(m.shader).EndsWith("MIO Living Filaments.shadergraph")).Distinct().ToArray();
    }
    static void Sample(float t)
    {
        orbit.Evaluate(t); outline.SetFloat("_PreviewTime",t); foreach(var material in filaments) material.SetFloat("_PreviewTime",t);
    }
    static void Reset()
    {
        orbit.Restart(); outline.SetFloat("_PreviewTime",-1); foreach(var material in filaments) material.SetFloat("_PreviewTime",-1);
        var buffer=AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Buffers/Normal Buffer.renderTexture");
        buffer.Release(); buffer.width=1920; buffer.height=1080; EditorUtility.SetDirty(buffer);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath); AssetDatabase.SaveAssets();
    }
    static void CheckShaders()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:Shader",new[] {"Assets/Shaders/MIO"}))
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            var errors=ShaderUtil.GetShaderMessages(shader).Where(e=>e.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            if(errors.Length>0) throw new InvalidOperationException(shader.name+": "+string.Join("\n",errors.Select(e=>e.message)));
        }
    }
    static void ValidateBounds()
    {
        var camera=Camera.main;
        foreach(var renderer in renderers)
        {
            var b=renderer.bounds;
            for(int n=0;n<8;n++)
            {
                var v=b.center+Vector3.Scale(b.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));
                var p=camera.WorldToViewportPoint(v);
                if(p.x<.015f || p.x>.985f || p.y<.015f || p.y>.985f || p.z<=0)
                    throw new InvalidOperationException("Showcase bounds may be clipped: "+renderer.name+" "+p);
            }
        }
    }
    sealed class FrameCapture : IDisposable
    {
        readonly Camera camera;
        readonly RenderTexture oldTarget,oldActive,target;
        readonly Texture2D image;
        bool warmed;
        public FrameCapture(int width,int height)
        {
            camera=Camera.main; oldTarget=camera.targetTexture; oldActive=RenderTexture.active;
            target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=4 };
            image=new Texture2D(width,height,TextureFormat.RGBA32,false); camera.targetTexture=target;
        }
        public Texture2D Render()
        {
            if(!warmed) { camera.Render(); warmed=true; }
            camera.Render(); RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(false);
            return image;
        }
        public void Dispose()
        {
            camera.targetTexture=oldTarget; RenderTexture.active=oldActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void WriteNotes()
    {
        File.WriteAllText("Assets/Shaders/MIO/Showcase Notes.txt",
            "HW02 submission video / repeatable 360-degree showcase\n\n"+
            "Open Assets/Scenes/MIO Showcase.unity and press Play. The 36-second camera sequence loops. P pauses/resumes; R restarts; Space changes the material manually and overrides scheduled style cuts until restart/the next loop. The existing MIO Interactive Study scene remains available for the fixed front-view interaction.\n\n"+
            "The color scene completes one uninterrupted 360-degree orbit from 2s to 18s. There is a small camera elevation change to reveal volume. Graphite is demonstrated from 20s to 25s, Dream Palette is shown from 25s to 32s, followed by a return to color/front. Vertex animation and stepped outline animation advance throughout.\n\n"+
            "Videos/MIO/MIO-Turnaround-1080p.mp4 is the 36-second, 1920x1080, 30 fps submission recording. It contains 1080 deterministic Unity-rendered frames and no audio. It is a scene recording, not a transformation of a still image. The physical keyboard is not filmed; scheduled cuts invoke the same material selection used by the interactive scene.\n\n"+
            "Use HW02 MIO > 11 - Export 36 second 1080p showcase video to reproduce the recording in the Unity Editor. The exporter uses the built-in UnityEditor.Media.MediaEncoder and needs no third-party Unity package. Output is relative to the project root.\n\n"+
            "Screenshots/MIO/Showcase contains inspection frames and validation. Model side/back forms are the original 3D interpretation of a single front-view concept reference.\n\n"+
            "Remaining submission work: user-selected BGM, final review and PR.\n\n"+
            "Concept art: Raphaelle Colin, Mio: Memories in Orbit - Shii, https://raphaelle_colin.artstation.com/projects/BkbRor\n"+
            "Encoder API reference: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Media.MediaEncoder.html\n");
    }
}
