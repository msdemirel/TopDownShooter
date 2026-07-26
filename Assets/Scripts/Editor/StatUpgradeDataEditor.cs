using UnityEditor;
using UnityEngine;

// StatUpgradeData'nın Inspector görünümüne, seçili stat'a göre değerlerin
// DÜZ mü ORAN mı girileceğini söyleyen bir bilgi kutusu ekler.
// Bu dosya Editor klasöründe olduğu için build'e dahil edilmez.
[CustomEditor(typeof(StatUpgradeData))]
public class StatUpgradeDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // tierAmounts hariç her şeyi normal çiz; bilgi kutusu tam dizinin üstüne gelsin
        DrawPropertiesExcluding(serializedObject, "m_Script", "tierAmounts");

        var stat = (StatType)serializedObject.FindProperty("stat").enumValueIndex;
        EditorGUILayout.HelpBox(GetUnitInfo(stat), MessageType.Info);

        SerializedProperty tiers = serializedObject.FindProperty("tierAmounts");
        float improve = serializedObject.FindProperty("autoImprovePerTier").floatValue;

        // Kademe sayısı (en az 1). Büyütülünce yeni kademeler bir öncekinden
        // autoImprovePerTier oranında büyütülerek OTOMATİK üretilir.
        int oldSize = tiers.arraySize;
        int newSize = EditorGUILayout.IntField("Kademe Sayısı", oldSize);
        if (newSize != oldSize)
        {
            tiers.arraySize = Mathf.Max(1, newSize);
            for (int i = oldSize; i < tiers.arraySize; i++)
                if (i > 0) ScaleFromPrevious(tiers, i, improve);
        }

        // Tek tuşla toparlama: 1. kademeyi elle ayarla, gerisini baştan ürettir.
        if (tiers.arraySize > 1 &&
            GUILayout.Button($"Kademe 2+ değerlerini 1. kademeden yeniden üret (%{improve * 100f:0} artış)"))
        {
            for (int i = 1; i < tiers.arraySize; i++)
                ScaleFromPrevious(tiers, i, improve);
        }

        for (int i = 0; i < tiers.arraySize; i++)
            EditorGUILayout.PropertyField(tiers.GetArrayElementAtIndex(i),
                new GUIContent(i == 0 ? "Kademe 1 (ilk alım)" : $"Kademe {i + 1}"));

        serializedObject.ApplyModifiedProperties();
    }

    // index'teki kademeyi bir öncekinden türetir: değer f oranında büyür, 2 ondalığa yuvarlanır.
    static void ScaleFromPrevious(SerializedProperty tiers, int index, float f)
    {
        float v = tiers.GetArrayElementAtIndex(index - 1).floatValue * (1f + f);
        tiers.GetArrayElementAtIndex(index).floatValue = Mathf.Round(v * 100f) / 100f;
    }

    static string GetUnitInfo(StatType stat)
    {
        switch (stat)
        {
            case StatType.MaxHealth:
                return "DÜZ değer gir: can puanı. Ör. 20 = maks can +20 (o kadar da doldurulur).";
            case StatType.MoveSpeed:
                return "DÜZ değer gir: hız birimi. Oyuncu taban hızı ~6'dır; ör. 0.5 = +0.5 hız.";
            case StatType.Damage:
                return "ORAN gir (yüzde): 0.1 = tüm silahlara %10 hasar artışı. 1 girme — o %100 olur!";
            case StatType.FireRate:
                return "ORAN gir (yüzde): 0.1 = tüm silahlara %10 atış hızı artışı. 1 girme — o %100 olur!";
            case StatType.Heal:
                return "DÜZ değer gir: can puanı. Ör. 30 = anında 30 can doldurur.";
            case StatType.MagnetRange:
                return "DÜZ değer gir: dünya birimi. Ör. 1 = çekim menzili +1 (başlangıç ~3).";
            case StatType.CritChance:
                return "ORAN gir (yüzde): 0.05 = tüm silahlara +%5 kritik şans. 5 girme — o %500 olur!";
            default:
                return "";
        }
    }
}
