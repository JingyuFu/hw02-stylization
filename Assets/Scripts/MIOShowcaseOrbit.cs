using UnityEngine;

// A repeatable 36-second camera sequence, also sampled by the editor exporter.
// The original interactive scene stays still; this component lives in Showcase.
[DisallowMultipleComponent, RequireComponent(typeof(Camera), typeof(MIOStyleSwitcher))]
public sealed class MIOShowcaseOrbit : MonoBehaviour
{
    public const float Duration = 36;
    public Vector3 focus = new Vector3(0, .45f, 0);
    [Min(1)] public float radius = 15;
    public bool loop = true;
    public bool showControls = true;
    float elapsed;
    bool paused, manualStyle;
    MIOStyleSwitcher style;
    GUIStyle hint;

    public float Elapsed => elapsed;
    public bool IsPaused => paused;
    void Start() { Restart(); }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
        if (Input.GetKeyDown(KeyCode.P)) paused = !paused;
        // MIOStyleSwitcher handles Space itself; manual input overrides the
        // scheduled style cuts until restart or the next loop.
        if (Input.GetKeyDown(KeyCode.Space)) manualStyle = true;
        if (!paused)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Duration)
            {
                if (loop) { elapsed %= Duration; manualStyle = false; }
                else { elapsed = Duration; paused = true; }
            }
        }
        Evaluate(elapsed, !manualStyle);
    }
    public void Restart() { elapsed = 0; paused = false; manualStyle = false; Evaluate(0, true); }
    public static float AngleAt(float seconds)
    {
        if (seconds < 2) return 0;
        if (seconds < 18) return 360 * Mathf.SmoothStep(0, 1, (seconds - 2) / 16);
        if (seconds < 23) return 360;
        if (seconds < 25) return 360 + 35 * Mathf.SmoothStep(0, 1, (seconds - 23) / 2);
        if (seconds < 31) return 395 - 70 * Mathf.SmoothStep(0, 1, (seconds - 25) / 6);
        if (seconds < 34) return 325 + 35 * Mathf.SmoothStep(0, 1, (seconds - 31) / 3);
        return 360;
    }
    public void Evaluate(float seconds, bool synchronizeStyle = true)
    {
        float t = Mathf.Clamp(seconds, 0, Duration);
        float angle = AngleAt(t);
        float elevation = 6 * Mathf.Pow(Mathf.Sin(angle * Mathf.Deg2Rad * .5f), 2);
        transform.position = focus + Quaternion.Euler(elevation, angle, 0) * (Vector3.back * radius);
        transform.LookAt(focus, Vector3.up);
        if (!synchronizeStyle) return;
        if (style == null) style = GetComponent<MIOStyleSwitcher>();
        int desired = t >= 25 && t < 32 ? 2 : t >= 20 && t < 25 ? 1 : 0;
        if (style.CurrentMode != desired) style.SetMode(desired);
    }
    void OnGUI()
    {
        if (!showControls || !Application.isPlaying) return;
        if (hint == null) hint = new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 0, 0) };
        var old = GUI.color;
        var box = new Rect(20, Screen.height - 58, Mathf.Min(580, Screen.width - 40), 36);
        GUI.color = new Color(1, 1, 1, .76f); GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(.14f, .12f, .17f, 1);
        GUI.Label(box, "SPACE  Style    P  " + (paused ? "Resume" : "Pause") + "    R  Restart" + (GetComponent<MIOBackgroundMusic>() != null ? "    M  Music" : ""), hint);
        GUI.color = old;
    }
}
