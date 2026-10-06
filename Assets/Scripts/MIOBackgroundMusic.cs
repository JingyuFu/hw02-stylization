using UnityEngine;

// Plays the user-selected recording; independent of camera/style animation.
[DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
public sealed class MIOBackgroundMusic : MonoBehaviour
{
    [Range(0,1)] public float volume = .45f;
    public float fadeInSeconds = .75f;
    AudioSource source;
    float elapsed;
    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true; source.spatialBlend = 0; source.playOnAwake = false;
    }
    void OnEnable()
    {
        if (source == null) source = GetComponent<AudioSource>();
        elapsed = 0; source.volume = fadeInSeconds > 0 ? 0 : volume;
        if (source.clip != null) source.Play();
    }
    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        source.volume = volume * (fadeInSeconds > 0 ? Mathf.Clamp01(elapsed / fadeInSeconds) : 1);
        if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
    }
    void OnDisable() { if (source != null) source.Stop(); }
    public void ToggleMute()
    {
        if (source == null) source = GetComponent<AudioSource>();
        source.mute = !source.mute;
    }
}
