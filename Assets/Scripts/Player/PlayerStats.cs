using System;
using UnityEngine;

// Oyuncunun exp / level / para durumu.
// Exp toplandıkça level atlar; her level UpgradeManager'a bir seçim hakkı açar.
public class PlayerStats : MonoBehaviour
{
    [Header("Level")]
    [Tooltip("Level 1'den 2'ye geçmek için gereken exp.")]
    [SerializeField] int baseExpToLevel = 5;
    [Tooltip("Her levelde gereken exp bu kadar artar (lineer zorlaşma).")]
    [SerializeField] int expIncreasePerLevel = 3;

    int level = 1;
    int exp;        // MEVCUT level içinde biriken exp (level atlayınca sıfırlanır)
    int totalExp;   // oyun boyunca toplanan toplam exp
    int money;
    float expRemainder;   // çarpanlı exp'in küsuratı kaybolmasın (1 exp x 1.1 -> birikir)

    // Kalıcı "Wisdom" upgrade'i: toplanan exp bu oranla çarpılır.
    public float ExpMultiplier { get; set; } = 1f;

    public int Level => level;
    public int Exp => exp;
    public int TotalExp => totalExp;
    public int Money => money;

    // Bir sonraki level için gereken exp. En az 1 döner: 0 olsaydı AddExp'teki
    // while sonsuz döngüye girerdi.
    public int ExpToNextLevel => Mathf.Max(1, baseExpToLevel + (level - 1) * expIncreasePerLevel);

    public event Action<int, int> OnExpChanged;   // (mevcut exp, gereken exp)
    public event Action<int> OnLevelUp;           // yeni level — her level için bir kez
    public event Action<int> OnMoneyChanged;

    // Kayıttan devam (RunSave): level atlama olayı TETİKLENMEZ (panel açılmasın), HUD güncellenir.
    public void RestoreState(int lvl, int curExp, int total, int cash)
    {
        level = Mathf.Max(1, lvl);
        exp = Mathf.Max(0, curExp);
        totalExp = Mathf.Max(0, total);
        money = Mathf.Max(0, cash);
        OnExpChanged?.Invoke(exp, ExpToNextLevel);
        OnMoneyChanged?.Invoke(money);
    }

    public void AddExp(int amount)
    {
        if (amount <= 0) return;

        float scaled = amount * ExpMultiplier + expRemainder;
        amount = Mathf.FloorToInt(scaled);
        expRemainder = scaled - amount;
        if (amount <= 0) return;

        exp += amount;
        totalExp += amount;

        // Tek seferde birden fazla level atlanabilir (ör. büyük bir exp yığını)
        while (exp >= ExpToNextLevel)
        {
            exp -= ExpToNextLevel;
            level++;
            OnLevelUp?.Invoke(level);
        }

        OnExpChanged?.Invoke(exp, ExpToNextLevel);
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        money += amount;
        OnMoneyChanged?.Invoke(money);
    }

    // Parayı harcamayı dener. Yeterliyse düşer ve true döner; yetmezse hiçbir şey yapmaz.
    public bool TrySpendMoney(int amount)
    {
        if (amount <= 0) return true;      // bedava
        if (money < amount) return false;  // yetersiz

        money -= amount;
        OnMoneyChanged?.Invoke(money);
        return true;
    }
}
