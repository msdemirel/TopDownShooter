using System;
using System.Collections.Generic;
using UnityEngine;

// Dil desteği. Tüm metinler tek tabloda: Resources/Localization/strings.txt
// (.txt ama sekme ile ayrılmış TSV; ilk satır dil kodları, ilk sütun İngilizce). Excel/LibreOffice ile düzenlenebilir.
//
// ANAHTAR = İNGİLİZCE METNİN KENDİSİ: Loc.T("PLAY") -> "JOUER". Tabloda yoksa İngilizce döner,
// yani çevirisi eksik metin kırılmaz. Yer tutuculu metinler: Loc.F("Reach wave {0}", 5).
// Silah / skill / karakter / upgrade İSİMLERİ bilerek çevrilmez (tabloda yoklar).
//
// Sahnedeki sabit yazılar AutoLocalizer ile kendiliğinden çevrilir; kodda üretilenler Loc.T / Loc.F kullanır.
// Eksik anahtarları bulmak için: python3 Tools/Localization/check_strings.py
public static class Loc
{
    public static readonly string[] Codes = { "en", "fr", "de", "tr", "zh", "it", "fi" };
    // Dil menüsünde her dil kendi adıyla görünür
    public static readonly string[] NativeNames = { "English", "Français", "Deutsch", "Türkçe", "简体中文", "Italiano", "Suomi" };

    const string KeyLanguage = "language";

    public static event Action Changed;

    static Dictionary<string, string[]> table;
    static Dictionary<string, string[]> loose;   // boşlukları sadeleştirilmiş anahtar -> satır (sahne yazıları için)
    static int column;          // Codes içindeki sıra (0 = İngilizce)
    static bool ready;

    public static string Current => Codes[Mathf.Clamp(Column, 0, Codes.Length - 1)];
    public static int Column { get { Init(); return column; } }

    static void Init()
    {
        if (ready) return;
        ready = true;
        Load();
        string saved = PlayerPrefs.GetString(KeyLanguage, "");
        column = Array.IndexOf(Codes, string.IsNullOrEmpty(saved) ? FromSystem() : saved);
        if (column < 0) column = 0;
    }

    // İlk açılış: bilgisayarın diline göre (desteklenmiyorsa İngilizce)
    static string FromSystem()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return "zh";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Finnish: return "fi";
            default: return "en";
        }
    }

    static void Load()
    {
        table = new Dictionary<string, string[]>();
        loose = new Dictionary<string, string[]>();
        var asset = Resources.Load<TextAsset>("Localization/strings");
        if (asset == null) { Debug.LogWarning("[Loc] Resources/Localization/strings.txt bulunamadı."); return; }

        var lines = asset.text.Replace("\r", "").Split('\n');
        if (lines.Length == 0) return;
        var header = lines[0].Split('\t');
        // Sütun sırası dosyadan okunur: dosyadaki sıra değişse de doğru dil eşleşir
        var map = new int[Codes.Length];
        for (int c = 0; c < Codes.Length; c++) map[c] = Array.IndexOf(header, Codes[c]);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].StartsWith("#")) continue;
            var cells = lines[i].Split('\t');
            var row = new string[Codes.Length];
            for (int c = 0; c < Codes.Length; c++)
                row[c] = map[c] >= 0 && map[c] < cells.Length ? Unescape(cells[map[c]]) : "";
            if (string.IsNullOrEmpty(row[0])) continue;
            row[TurkishColumn] = TurkishCaps(row[TurkishColumn]);
            table[row[0]] = row;
            loose[Squash(row[0])] = row;
        }
    }

    static readonly int TurkishColumn = Array.IndexOf(Codes, "tr");

    // PixCon küçük harfleri büyük harf gibi çizer ama 'ı' küçük kalır, 'i' de noktasız I olur.
    // Türkçe metinde ikisini büyük karşılığına çeviriyoruz (<color=...> gibi etiketlere dokunmadan).
    static string TurkishCaps(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        bool inTag = false;
        foreach (char ch in s)
        {
            if (ch == '<') inTag = true;
            else if (ch == '>') inTag = false;
            sb.Append(inTag ? ch : ch == 'i' ? 'İ' : ch == 'ı' ? 'I' : ch);
        }
        return sb.ToString();
    }

    // Sahnede kaydedilen çok satırlı yazılarda boşluk sayısı değişebiliyor: eşleşmeyi boşluğa duyarsız yap
    static string Squash(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();

    static bool TryRow(string english, out string[] row) =>
        table.TryGetValue(english, out row) || loose.TryGetValue(Squash(english), out row);

    // Tabloda satır sonu "\n" olarak yazılır
    static string Unescape(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");

    // ---- Kullanım ----
    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english)) return english;
        Init();
        if (column == 0) return english;
        return TryRow(english, out var row) && !string.IsNullOrEmpty(row[column]) ? row[column] : english;
    }

    public static string F(string english, params object[] args)
    {
        try { return string.Format(T(english), args); }
        catch (FormatException) { return string.Format(english, args); }   // hatalı çeviri: İngilizceye düş
    }

    // Sabit etiket: çeviriyi yazar ve dil değişince günceller (kodla kurulan UI başlıkları için)
    public static void Bind(TMPro.TMP_Text t, string english) => AutoLocalizer.Track(t, english);

    public static bool Has(string english)
    {
        Init();
        return !string.IsNullOrEmpty(english) && TryRow(english, out _);
    }

    public static void Set(int index)
    {
        Init();
        index = Mathf.Clamp(index, 0, Codes.Length - 1);
        if (index == column) return;
        column = index;
        PlayerPrefs.SetString(KeyLanguage, Codes[index]);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Editörde tablo değişince yeniden okumak için
    public static void Reload()
    {
        ready = false;
        Init();
        Changed?.Invoke();
    }
}
