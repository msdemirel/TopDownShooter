using UnityEngine;

// CursorManager'ın ayarları. Resources/CursorSettings.asset olarak durur.
[CreateAssetMenu(menuName = "TopDownShooter/UI/Cursor Settings", fileName = "CursorSettings")]
public class CursorSettings : ScriptableObject
{
    [Tooltip("Cursor dokusu. Import ayarı: Texture Type = Cursor (okunabilir). Boşsa sistem cursor'ı.")]
    public Texture2D pointer;
    [Tooltip("Tıklama noktası (doku pikseli, sol üstten). Ok cursor'ında ucu: (0,0).")]
    public Vector2 hotspot = Vector2.zero;
    [Tooltip("Oyun akarken cursor gizlensin mi? (Menülerde ve oyun donukken her zaman görünür.)")]
    public bool hideDuringGameplay = true;
}
