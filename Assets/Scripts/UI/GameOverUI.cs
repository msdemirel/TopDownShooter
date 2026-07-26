using UnityEngine;
using UnityEngine.SceneManagement;

// Oyuncu ölünce Game Over panelini açar ve oyunu dondurur.
// Restart butonu sahneyi baştan yükler.
public class GameOverUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Ölünce açılacak panel. Sahnede başta KAPALI (inactive) olmalı.")]
    [SerializeField] GameObject panel;

    [Tooltip("Boş bırakılırsa 'Player' tag'inden bulunur.")]
    [SerializeField] Health playerHealth;

    void Start()
    {
        if (playerHealth == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<Health>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning("[GameOverUI] Player'ın Health'i bulunamadı.", this);
            enabled = false;
            return;
        }

        playerHealth.OnDeath += HandleDeath;

        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDeath -= HandleDeath;
    }

    void HandleDeath(Health h)
    {
        if (panel != null) panel.SetActive(true);
        Time.timeScale = 0f;   // oyunu dondur (UI çalışmaya devam eder)
    }

    // Restart butonunun OnClick'ine BU metodu bağla (Inspector'dan).
    public void Restart()
    {
        // ÖNEMLİ: timeScale sahne yüklenince kendiliğinden sıfırlanmaz — global bir değerdir.
        // Burada 1'e çekmezsek yeni oyun donuk başlar.
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
