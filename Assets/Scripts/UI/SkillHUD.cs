using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Kazanılan skillerin ikonlarını, tuşlarını ve cooldown durumunu gösterir.
// Slotlar başta gizlidir; skill kazanıldıkça sırayla görünür olur.
// Cooldown overlay'i her karede PlayerSkills'ten okunur (WaveHUD kalıbı).
public class SkillHUD : MonoBehaviour
{
    // Ekrandaki tek bir skill kutusu.
    [System.Serializable]
    public class SlotUI
    {
        [Tooltip("Kutunun tamamı (skill kazanılana kadar gizlenir).")]
        public GameObject root;
        [Tooltip("Skill ikonu (SkillUpgradeData.icon buraya basılır).")]
        public Image icon;
        [Tooltip("Cooldown karartması: Image tipi 'Filled' olmalı. 1 = az önce kullanıldı.")]
        public Image cooldownOverlay;
        [Tooltip("Kalan cooldown saniyesini gösteren yazı (ör. \"3\"). Hazırken boşalır. Opsiyonel.")]
        public TMP_Text cooldownText;
        [Tooltip("Tuş etiketi (\"Space\", \"E\"...). Opsiyonel.")]
        public TMP_Text keyText;
    }

    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa 'Player' tag'inden bulunur.")]
    [SerializeField] PlayerSkills playerSkills;

    [Tooltip("Sıra PlayerSkills'teki slot tuşlarıyla aynı olmalı (0. kutu 0. tuş).")]
    [SerializeField] SlotUI[] slots;

    void Start()
    {
        if (playerSkills == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerSkills = p.GetComponent<PlayerSkills>();
        }

        if (playerSkills == null)
        {
            Debug.LogWarning("[SkillHUD] Player'da PlayerSkills bulunamadı.", this);
            enabled = false;
            return;
        }

        playerSkills.OnSkillAdded += HandleSkillAdded;

        // Başta tüm kutular gizli (skill kazanılınca açılır)
        foreach (var s in slots)
            if (s != null && s.root != null) s.root.SetActive(false);
    }

    void OnDestroy()
    {
        if (playerSkills != null) playerSkills.OnSkillAdded -= HandleSkillAdded;
    }

    void HandleSkillAdded(int slot, SkillUpgradeData skill)
    {
        if (slot < 0 || slot >= slots.Length || slots[slot] == null) return;

        SlotUI ui = slots[slot];
        if (ui.root != null) ui.root.SetActive(true);
        if (ui.icon != null) ui.icon.sprite = skill.icon;
        if (ui.keyText != null) ui.keyText.text = playerSkills.GetKeyName(slot);
    }

    void Update()
    {
        // Sadece dolu slotları güncelle
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || playerSkills.GetSkill(i) == null) continue;

            if (slots[i].cooldownOverlay != null)
                slots[i].cooldownOverlay.fillAmount = playerSkills.GetCooldownNormalized(i);

            if (slots[i].cooldownText != null)
            {
                float remaining = playerSkills.GetCooldownRemaining(i);
                // Yukarı yuvarla: "3,2,1" gibi görünsün; hazırken (0) boş kalsın
                slots[i].cooldownText.text = remaining > 0.05f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : "";
            }
        }
    }
}
