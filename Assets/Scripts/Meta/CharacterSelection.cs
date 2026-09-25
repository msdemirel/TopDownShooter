using UnityEngine;

// Seçili karakter (oyunlar arası hatırlanır). Ana menüdeki karakter ekranı Select çağırır;
// oyunda PlayerWeapons (başlangıç silahı) ve MetaApplier (bonuslar + görünüm) Current'ı okur.
public static class CharacterSelection
{
    const string KeySelected = "character_selected";
    static CharacterData current;

    // Katalog yoksa (kurulum yapılmadı) null: oyuncu prefab'daki haliyle oynar.
    public static CharacterData Current
    {
        get
        {
            if (current != null) return current;
            var list = MetaProgress.Catalog != null ? MetaProgress.Catalog.characters : null;
            if (list == null || list.Count == 0) return null;

            string id = PlayerPrefs.GetString(KeySelected, "");
            foreach (var c in list)
                if (c != null && c.id == id && c.IsUnlocked) return current = c;
            return current = list[0];   // seçilmemiş ya da kilitli: ilk karakter
        }
    }

    public static void Select(CharacterData c)
    {
        if (c == null || !c.IsUnlocked) return;
        current = c;
        PlayerPrefs.SetString(KeySelected, c.id);
        PlayerPrefs.Save();
    }

    public static void ResetForTesting()
    {
        PlayerPrefs.DeleteKey(KeySelected);
        current = null;
    }
}
