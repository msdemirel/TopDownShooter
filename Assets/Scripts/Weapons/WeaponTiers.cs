using UnityEngine;

// Silah birleştirme kademeleri. Aynı silahtan aynı kademede iki tane olunca PlayerWeapons
// onları birleştirip bir üst kademeye çıkarır (I + I -> II, II + II -> III ...).
// Birleşmiş silah iki ayrı kopyadan biraz güçlü + bir slot boşaltır: birleştirmek cazip olsun.
// Denge için tek yer: çarpanları buradan değiştir.
public static class WeaponTiers
{
    public const int MaxTier = 4;

    // Index = kademe - 1
    static readonly float[] Damage = { 1f, 2.1f, 4.3f, 8.8f };
    static readonly float[] FireRate = { 1f, 1.1f, 1.2f, 1.3f };
    static readonly string[] Roman = { "I", "II", "III", "IV" };
    static readonly Color[] Colors =
    {
        new Color32(0xF4, 0xF4, 0xF4, 0xFF),   // I   beyaz
        new Color32(0x73, 0xEF, 0xF7, 0xFF),   // II  cyan
        new Color32(0xC7, 0x8B, 0xFF, 0xFF),   // III mor
        new Color32(0xFF, 0xCD, 0x75, 0xFF),   // IV  altın
    };

    static int Idx(int tier) => Mathf.Clamp(tier, 1, MaxTier) - 1;

    public static float DamageMultiplier(int tier) => Damage[Idx(tier)];
    public static float FireRateMultiplier(int tier) => FireRate[Idx(tier)];
    public static string RomanNumeral(int tier) => Roman[Idx(tier)];
    public static Color Color(int tier) => Colors[Idx(tier)];
    public static string Hex(int tier) => "#" + ColorUtility.ToHtmlStringRGB(Colors[Idx(tier)]);
}
