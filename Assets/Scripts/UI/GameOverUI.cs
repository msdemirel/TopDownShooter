using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Oyuncu ölünce Game Over panelini açar ve oyunu dondurur.
// Oyunun istatistiklerini (RunStats) gösterir, rekorları (BestRecords) günceller:
// değerler sayarak dolar, kırılan rekorların yanında "NEW!" rozeti belirir.
// Kalıcı ilerleme (MetaProgress): kazanılan Core ve dökümü + bu oyunla açılan içerikler.
//
// Kurulum: Menü > TopDownShooter > UI > Game Over Panelini Kur
// (paneli, ikonları, butonları ve buradaki referansları otomatik bağlar).
//
// Butonların OnClick'i:  Restart -> Restart,  Main Menu -> GoToMainMenu,  Quit -> QuitGame
public class GameOverUI : MonoBehaviour
{
    // Tek bir istatistik kartının yazıları.
    [System.Serializable]
    public class StatRow
    {
        [Tooltip("Bu oyunun değeri (büyük yazı).")]
        public TMP_Text value;
        [Tooltip("Rekor değeri (\"BEST 07:10\"). Opsiyonel.")]
        public TMP_Text best;
        [Tooltip("Rekor kırılınca görünen rozet. Opsiyonel.")]
        public GameObject newBestBadge;
    }

    [Header("Referanslar")]
    [Tooltip("Ölünce açılacak panel. Sahnede başta KAPALI (inactive) olmalı.")]
    [SerializeField] GameObject panel;

    [Tooltip("Boş bırakılırsa 'Player' tag'inden bulunur.")]
    [SerializeField] Health playerHealth;

    [Tooltip("Boş bırakılırsa bu objede aranır, yoksa eklenir.")]
    [SerializeField] RunStats runStats;

    [Header("Görünüm (opsiyonel)")]
    [Tooltip("Açılırken hafifçe büyüyerek belirecek pencere.")]
    [SerializeField] RectTransform window;
    [Tooltip("Panelin saydamlığı (açılırken fade-in).")]
    [SerializeField] CanvasGroup canvasGroup;
    [Tooltip("Başlığın altındaki özet yazısı (\"WAVE 7  -  RUN #12\").")]
    [SerializeField] TMP_Text subtitleText;
    [Tooltip("En az bir rekor kırılınca görünen büyük \"NEW RECORD!\" şeridi.")]
    [SerializeField] GameObject newRecordBanner;

    [Header("İstatistikler")]
    [SerializeField] StatRow timeRow = new StatRow();
    [SerializeField] StatRow waveRow = new StatRow();
    [SerializeField] StatRow killsRow = new StatRow();
    [SerializeField] StatRow levelRow = new StatRow();
    [SerializeField] StatRow coinsRow = new StatRow();
    [SerializeField] StatRow damageRow = new StatRow();

    [Header("Core (kalıcı ilerleme, opsiyonel)")]
    [Tooltip("Kazanılan Core (\"+61\"), diğer sayılarla birlikte sayarak dolar.")]
    [SerializeField] TMP_Text coreEarnedText;
    [Tooltip("Döküm: \"WAVES +30  KILLS +31  BOSS +15\".")]
    [SerializeField] TMP_Text coreBreakdownText;
    [Tooltip("Oyundan sonraki toplam bakiye: \"TOTAL 245\".")]
    [SerializeField] TMP_Text coreTotalText;
    [Tooltip("Bu oyunla içerik açıldıysa görünen bölüm (\"NEW UNLOCK\" + ikonlar).")]
    [SerializeField] GameObject unlockSection;
    [SerializeField] TMP_Text unlockText;
    [Tooltip("Açılan içeriklerin ikonları buraya dizilir (yatay).")]
    [SerializeField] RectTransform unlockIcons;
    [SerializeField] float unlockIconSize = 44f;

    [Header("Animasyon")]
    [Tooltip("Panel açılmadan önce ölüm anının görülmesi için bekleme (gerçek zaman, sn).")]
    [SerializeField] float showDelay = 0.6f;
    [SerializeField] float fadeDuration = 0.3f;
    [Tooltip("Sayıların 0'dan değerine sayarak dolma süresi (sn).")]
    [SerializeField] float countUpDuration = 1.2f;

    [Header("Sahne")]
    [Tooltip("Main Menu butonunun yükleyeceği sahne. Build Profiles > Scene List'te EKLİ olmalı.")]
    [SerializeField] string mainMenuSceneName = "MainMenu";

    [Header("Klavye / Gamepad (opsiyonel)")]
    [Tooltip("Panel açılınca seçili gelecek buton (ör. Restart).")]
    [SerializeField] GameObject firstSelected;

    bool shown;

    void Awake()
    {
        if (runStats == null) runStats = GetComponent<RunStats>();
        if (runStats == null) runStats = gameObject.AddComponent<RunStats>();
    }

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
        if (shown) return;
        shown = true;

        Time.timeScale = 0f;   // oyunu dondur (UI çalışmaya devam eder)

        // Rekorları HEMEN kaydet: oyuncu paneli beklemeden sahneden çıksa da kaybolmasın
        RunSave.Delete();   // ölen oyun devam ettirilemez

        RunResult run = runStats.Snapshot();
        var lockedBefore = MetaProgress.CurrentlyLocked();   // rekorlar güncellenmeden önce
        var charsBefore = MetaProgress.CurrentlyLockedCharacters();
        BestRecords.NewBestFlags flags = BestRecords.Submit(run, out RunResult previous);
        MetaProgress.EndRun(run, lockedBefore, charsBefore);  // Core + toplamlar + yeni açılanlar

        StartCoroutine(ShowRoutine(run, previous, flags));
    }

    // Oyun donuk olduğu için tüm bekleme/animasyonlar GERÇEK zamanla (unscaled) çalışır.
    IEnumerator ShowRoutine(RunResult run, RunResult previous, BestRecords.NewBestFlags flags)
    {
        if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);
        AudioManager.Play(SfxId.GameOver);

        if (panel != null) panel.SetActive(true);
        PrepareTexts(run, previous, flags);
        Select(firstSelected);

        // Fade-in + pencere hafifçe büyüyerek oturur
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            float k = fadeDuration > 0f ? t / fadeDuration : 1f;
            if (canvasGroup != null) canvasGroup.alpha = k;
            if (window != null) window.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, EaseOut(k));
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (window != null) window.localScale = Vector3.one;

        // Sayılar birlikte sayarak dolar
        for (float t = 0f; t < countUpDuration; t += Time.unscaledDeltaTime)
        {
            SetValues(run, EaseOut(countUpDuration > 0f ? t / countUpDuration : 1f));
            yield return null;
        }
        SetValues(run, 1f);

        // Yeni açılan içerikler: rozetlerden hemen önce, küçük bir "pop" ile
        yield return ShowUnlocks(MetaProgress.LastReport);

        // Kırılan rekorların rozetleri en sonda belirir (vurgu olsun)
        SetBadge(timeRow, flags.time);
        SetBadge(waveRow, flags.wave);
        SetBadge(killsRow, flags.kills);
        SetBadge(levelRow, flags.level);
        SetBadge(coinsRow, flags.coins);
        SetBadge(damageRow, flags.damage);
        if (newRecordBanner != null) newRecordBanner.SetActive(flags.Any);
        if (flags.Any) AudioManager.Play(SfxId.NewRecord);
    }

    void PrepareTexts(RunResult run, RunResult previous, BestRecords.NewBestFlags flags)
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (newRecordBanner != null) newRecordBanner.SetActive(false);

        if (subtitleText != null)
        {
            var diff = Difficulty.Current;
            var ch = CharacterSelection.Current;
            string diffTag = diff != null ? $"  -  <color=#{ColorUtility.ToHtmlStringRGB(diff.color)}>{Loc.T(diff.title)}</color>" : "";
            string charTag = ch != null ? $"  -  <color=#{ColorUtility.ToHtmlStringRGB(ch.color)}>{ch.title}</color>" : "";
            subtitleText.text = $"{Loc.F("WAVE {0}", run.wave)}{diffTag}{charTag}  -  {Loc.F("RUN #{0}", BestRecords.TotalRuns)}";
        }

        // "BEST" = bu oyun dahil en iyi değer (rekor kırıldıysa yeni değer)
        SetBest(timeRow, FormatTime(Mathf.Max(run.timeSurvived, previous.timeSurvived)));
        SetBest(waveRow, Mathf.Max(run.wave, previous.wave).ToString());
        SetBest(killsRow, Mathf.Max(run.kills, previous.kills).ToString());
        SetBest(levelRow, Mathf.Max(run.level, previous.level).ToString());
        SetBest(coinsRow, Mathf.Max(run.coins, previous.coins).ToString());
        SetBest(damageRow, FormatBig(Mathf.Max(run.damage, previous.damage)));

        foreach (var r in new[] { timeRow, waveRow, killsRow, levelRow, coinsRow, damageRow })
            if (r.newBestBadge != null) r.newBestBadge.SetActive(false);

        var report = MetaProgress.LastReport;
        if (coreBreakdownText != null)
        {
            coreBreakdownText.text = report == null ? "" :
                $"{Loc.T("WAVES")} +{report.coreFromWaves}   {Loc.T("KILLS")} +{report.coreFromKills}" +
                (report.coreFromBosses > 0 ? $"   {Loc.T("BOSS")} +{report.coreFromBosses}" : "") +
                (report.coreFromDifficulty > 0 && report.difficulty != null
                    ? $"   <color=#{ColorUtility.ToHtmlStringRGB(report.difficulty.color)}>{Loc.T(report.difficulty.title)} " +
                      $"x{report.difficulty.coreMultiplier:0.##} +{report.coreFromDifficulty}</color>"
                    : "");
        }
        if (coreTotalText != null) coreTotalText.text = Loc.F("TOTAL {0}", MetaProgress.Core.ToString("N0"));
        if (unlockSection != null) unlockSection.SetActive(false);

        SetValues(run, 0f);
    }

    IEnumerator ShowUnlocks(MetaProgress.RunReport report)
    {
        if (unlockSection == null || report == null
            || report.newlyUnlocked.Count + report.newCharacters.Count == 0) yield break;

        // Karakterler önce (büyük haber), sonra silah/skill'ler
        var names = new System.Collections.Generic.List<string>();
        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (var c in report.newCharacters)
        {
            names.Add($"<color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.title}</color>");
            sprites.Add(c.previewFrames != null && c.previewFrames.Length > 0 ? c.previewFrames[0] : null);
        }
        foreach (var u in report.newlyUnlocked) { names.Add(DisplayName(u)); sprites.Add(u.icon); }
        if (unlockText != null)
            unlockText.text = $"{Loc.T(names.Count > 1 ? "NEW UNLOCKS:" : "NEW UNLOCK:")} <color=#F4F4F4>{string.Join(", ", names)}</color>";

        if (unlockIcons != null)
        {
            for (int i = unlockIcons.childCount - 1; i >= 0; i--) Destroy(unlockIcons.GetChild(i).gameObject);
            for (int i = 0; i < sprites.Count; i++)
            {
                var go = new GameObject("Unlock", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(unlockIcons, false);
                rt.sizeDelta = Vector2.one * unlockIconSize;
                // Ortalanmış yatay dizi
                float x = (i - (sprites.Count - 1) * 0.5f) * (unlockIconSize + 8f);
                rt.anchoredPosition = new Vector2(x, 0f);
                var img = go.GetComponent<Image>();
                img.sprite = sprites[i];
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
        }

        unlockSection.SetActive(true);
        AudioManager.Play(SfxId.Unlock);
        var srt = unlockSection.transform;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.25f;
            srt.localScale = Vector3.one * (1f + 0.3f * (1f - k) * (1f - k));   // büyükten normale
            yield return null;
        }
        srt.localScale = Vector3.one;
    }

    // Silah kartları "Buy +1 Katana" başlığını taşır; kilit bildiriminde silahın adı yeter.
    static string DisplayName(UpgradeData u)
        => u is WeaponUpgradeData w && w.weapon != null ? w.weapon.weaponName : u.title;

    // k: 0 -> 1 arası sayma ilerlemesi
    void SetValues(RunResult run, float k)
    {
        SetValue(timeRow, FormatTime(run.timeSurvived * k));
        SetValue(waveRow, Mathf.RoundToInt(run.wave * k).ToString());
        SetValue(killsRow, Mathf.RoundToInt(run.kills * k).ToString());
        SetValue(levelRow, Mathf.RoundToInt(run.level * k).ToString());
        SetValue(coinsRow, Mathf.RoundToInt(run.coins * k).ToString());
        SetValue(damageRow, FormatBig(run.damage * k));

        var report = MetaProgress.LastReport;
        if (coreEarnedText != null)
            coreEarnedText.text = $"+{Mathf.RoundToInt((report != null ? report.CoreTotal : 0) * k)}";
    }

    static void SetValue(StatRow r, string s) { if (r.value != null) r.value.text = s; }
    static void SetBest(StatRow r, string s) { if (r.best != null) r.best.text = Loc.F("BEST {0}", s); }
    static void SetBadge(StatRow r, bool on) { if (r.newBestBadge != null) r.newBestBadge.SetActive(on); }

    // 332.4 -> "05:32", 1 saati geçerse "1:05:32"
    static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }

    // 950 -> "950", 12345 -> "12.3K", 2500000 -> "2.5M"
    static string FormatBig(float v)
    {
        if (v >= 1_000_000f) return (v / 1_000_000f).ToString("0.#") + "M";
        if (v >= 10_000f) return (v / 1000f).ToString("0.#") + "K";
        return Mathf.RoundToInt(v).ToString();
    }

    static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

    static void Select(GameObject go)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    // ---- Buton metotları (Inspector'dan OnClick'e bağlanır) ----

    public void Restart()
    {
        // ÖNEMLİ: timeScale sahne yüklenince kendiliğinden sıfırlanmaz — global bir değerdir.
        // Burada 1'e çekmezsek yeni oyun donuk başlar.
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"[GameOverUI] '{mainMenuSceneName}' sahnesi Build Profiles listesinde yok — " +
                           "eklemeden yüklenemez.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        GameSettings.Save();   // ayarlar diske yazılmadan kapanmasın

#if UNITY_EDITOR
        // Application.Quit() Editor'da HİÇBİR ŞEY yapmaz; Play modunu burada durduruyoruz.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
