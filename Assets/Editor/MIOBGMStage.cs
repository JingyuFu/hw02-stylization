using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MIOBGMStage
{
    const string ClipPath="Assets/Audio/MIO/Opening.mp3";
    const string Output="Screenshots/MIO/Audio";
    const string PlayKey="MIO.BGM.PlayCheck";
    static MIOBGMStage() { if(SessionState.GetBool(PlayKey,false))EditorApplication.playModeStateChanged+=PlayState; }
    [MenuItem("HW02 MIO/13 - Add the selected BGM")]
    public static void FromMenu() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())ConfigureAndValidate(); }
    public static void ConfigureAndValidate()
    {
        Directory.CreateDirectory(Output);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=(AudioImporter)AssetImporter.GetAtPath(ClipPath);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
        importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);clip.LoadAudioData();
        var samples=new float[clip.samples*clip.channels];
        if(!clip.GetData(samples,0)||clip.frequency!=48000||clip.channels!=2||clip.length<36||!samples.Any(s=>Mathf.Abs(s)>.01f))throw new InvalidOperationException("Expected audible 48kHz stereo music longer than the 36-second video.");
        foreach(var scene in new[]{"Assets/Scenes/MIO Interactive Study.unity","Assets/Scenes/MIO Showcase.unity"})
        {
            EditorSceneManager.OpenScene(scene);var camera=Camera.main;
            var listeners=UnityEngine.Object.FindObjectsOfType<AudioListener>();
            if(listeners.Length==0)camera.gameObject.AddComponent<AudioListener>();
            else if(listeners.Length>1)throw new InvalidOperationException("Multiple audio listeners in "+scene);
            var source=camera.GetComponent<AudioSource>();if(source==null)source=camera.gameObject.AddComponent<AudioSource>();
            source.clip=clip;source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.volume=.45f;source.mute=false;
            var music=camera.GetComponent<MIOBackgroundMusic>();if(music==null)music=camera.gameObject.AddComponent<MIOBackgroundMusic>();
            music.volume=.45f;music.fadeInSeconds=.75f;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        File.WriteAllText(Output+"/import-validation.txt","PASS: selected Opening.mp3 imports and decodes into non-silent PCM\n"+
            "Sample rate: "+clip.frequency+"; channels: "+clip.channels+"; length: "+clip.length+" seconds\n"+
            "PASS: both final scenes contain the selected clip, a single listener, looping 2D AudioSource and mute controller\n"+
            "Runtime volume: 0.45; fade-in: 0.75 seconds. Full source loops independently of the 36-second camera sequence.\n");
        File.WriteAllText("Assets/Audio/MIO/Music Credits.txt","User-selected recording: Opening\nArtist/composer: Nicolas Gueguen\nAlbum: MIO: Memories in Orbit (Original Game Soundtrack)\n"+
            "Attribution from the supplied MP3's embedded metadata. Original file: BGM/01. Opening.mp3; imported copy: Assets/Audio/MIO/Opening.mp3. These files are byte-identical.\n"+
            "Metadata copyright: 2026 Douze Dixiemes and Focus Entertainment, Kromatik, under exclusive license to Kid Katana Records.\n"+
            "The recording is third-party soundtrack material supplied by the user, not original project music. No standalone redistribution license is asserted.\n"+
            "Unity plays the full clip in a loop. The showcase video uses the first 36 seconds at 45% volume with 0.75s fade-in and 1.8s fade-out. M toggles mute; P/R affect the camera only.\n");
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("MIO BGM CONFIGURATION COMPLETE");
    }
    public static void ConfigureAndExport() { ConfigureAndValidate();MIOShowcaseStage.ExportVideo(); }
    public static void ValidatePlayMode()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MIO Interactive Study.unity");SessionState.SetBool(PlayKey,true);EditorApplication.playModeStateChanged+=PlayState;EditorApplication.isPlaying=true;
    }
    static void PlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=PlayAssertions;
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(PlayKey);Debug.Log("MIO BGM PLAY VALIDATION COMPLETE");EditorApplication.Exit(0);
        }
    }
    static void PlayAssertions()
    {
        try
        {
            var music=Camera.main.GetComponent<MIOBackgroundMusic>();var source=Camera.main.GetComponent<AudioSource>();
            if(!source.isPlaying||source.mute||!source.loop)throw new InvalidOperationException("Music did not start playing in Play mode.");
            music.ToggleMute();if(!source.mute)throw new InvalidOperationException("Mute handler failed.");
            music.ToggleMute();if(source.mute)throw new InvalidOperationException("Unmute handler failed.");
            var style=Camera.main.GetComponent<MIOStyleSwitcher>();int mode=style.CurrentMode;style.HandleKeyDown(KeyCode.M);
            if(style.CurrentMode!=mode)throw new InvalidOperationException("M interferes with style switching.");
            music.enabled=false;if(source.isPlaying)throw new InvalidOperationException("Disabling music did not stop playback.");
            music.enabled=true;if(!source.isPlaying)throw new InvalidOperationException("Re-enabling music did not restart playback.");
            File.WriteAllText(Output+"/play-mode-validation.txt","PASS: AudioSource starts playing in actual Unity Play mode\nPASS: mute/unmute handler toggles source mute\nPASS: M is ignored by the style controller\nPASS: disabling/re-enabling the music component stops/restarts playback\nPhysical OS keyboard and speaker listening are not automated by this check.\n");
            EditorApplication.isPlaying=false;
        }
        catch(Exception e){SessionState.EraseBool(PlayKey);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
