using UnityEngine;

// HW02 task 6: the camera selects a genuinely different fullscreen material.
// Dream mode also replaces renderer material assignments. Shared material assets
// stay untouched, and disabling the component restores the original assignments.
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class MIOStyleSwitcher : MonoBehaviour
{
    [Tooltip("0: Color Paper. 1: Graphite. Optional 2: paper finish with Dream surface materials.")]
    public Material[] materials = new Material[2];
    public bool showControls = true;
    [Tooltip("Optional third style: replace these surface materials with Dream Palette variants.")]
    public Transform surfaceRoot;
    public Material[] originalSurfaceMaterials;
    public Material[] dreamSurfaceMaterials;
    Renderer[] surfaceRenderers;
    Material[][] savedSurfaces;
    int currentMode;
    GUIStyle hintStyle;

    public int CurrentMode => currentMode;
    public Material ActiveMaterial => materials != null && materials.Length > 0
        ? materials[Mathf.Clamp(currentMode, 0, materials.Length - 1)] : null;

    public string CurrentName => currentMode == 2 ? "MIO dream palette" : currentMode == 1 ? "Graphite" : "Color paper";
    void OnEnable() { currentMode = 0; }
    void OnDisable() { RestoreSurfaces(); surfaceRenderers = null; savedSurfaces = null; currentMode = 0; }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) HandleKeyDown(KeyCode.Space);
    }
    // Shared by the real input path and the play-mode rendering validation.
    public void HandleKeyDown(KeyCode key)
    {
        if (key != KeyCode.Space || materials == null || materials.Length < 2) return;
        SetMode((currentMode + 1) % materials.Length);
    }
    public void SetMode(int mode)
    {
        currentMode = materials == null || materials.Length == 0 ? 0 : Mathf.Clamp(mode, 0, materials.Length - 1);
        if (surfaceRoot == null) return;
        if (surfaceRenderers == null)
        {
            surfaceRenderers = surfaceRoot.GetComponentsInChildren<Renderer>(true);
            savedSurfaces = new Material[surfaceRenderers.Length][];
            for (int i = 0; i < surfaceRenderers.Length; i++) savedSurfaces[i] = surfaceRenderers[i].sharedMaterials;
        }
        if (currentMode != 2) { RestoreSurfaces(); return; }
        for (int i = 0; i < surfaceRenderers.Length; i++)
        {
            if (surfaceRenderers[i] == null) continue;
            var selected = (Material[])savedSurfaces[i].Clone();
            for (int j = 0; j < selected.Length; j++)
                for (int k = 0; originalSurfaceMaterials != null && dreamSurfaceMaterials != null && k < Mathf.Min(originalSurfaceMaterials.Length, dreamSurfaceMaterials.Length); k++)
                    if (selected[j] == originalSurfaceMaterials[k] && dreamSurfaceMaterials[k] != null) { selected[j] = dreamSurfaceMaterials[k]; break; }
            surfaceRenderers[i].sharedMaterials = selected;
        }
    }
    void RestoreSurfaces()
    {
        if (surfaceRenderers == null) return;
        for (int i = 0; i < surfaceRenderers.Length; i++) if (surfaceRenderers[i] != null) surfaceRenderers[i].sharedMaterials = savedSurfaces[i];
    }
    void OnGUI()
    {
        if (!showControls || !Application.isPlaying) return;
        if (hintStyle == null)
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 0, 0) };
        var oldColor = GUI.color;
        var box = new Rect(20, Screen.height - 58, Mathf.Min(470, Screen.width - 40), 36);
        GUI.color = new Color(1, 1, 1, .76f); GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(.14f, .12f, .17f, 1);
        GUI.Label(box, "SPACE  Next style / " + CurrentName, hintStyle);
        GUI.color = oldColor;
    }
}
