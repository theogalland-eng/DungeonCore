using UnityEngine;

/// <summary>
/// Rectangle de sélection de zone (souris). À part de MonsterSelector : la simple présence d'un OnGUI fait tourner
/// tout le système IMGUI à chaque image (allocations et temps perdus, surtout sur téléphone) ; ce composant n'est
/// activé que pendant un glisser.
/// </summary>
public class SelectionBoxGUI : MonoBehaviour
{
    public Rect screenRect;                  // coordonnées écran (origine en bas à gauche)
    public Color fill = new Color(0, 1, 0, 0.3f), border = Color.green;

    private void OnGUI()
    {
        Rect gui = new Rect(screenRect.x, Screen.height - screenRect.y - screenRect.height, screenRect.width, screenRect.height);
        GUI.color = fill;
        GUI.DrawTexture(gui, Texture2D.whiteTexture);
        GUI.color = border;
        GUI.DrawTexture(new Rect(gui.x, gui.y, gui.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x, gui.yMax - 2, gui.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.x, gui.y, 2, gui.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(gui.xMax - 2, gui.y, 2, gui.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
