using UnityEditor;
using UnityEngine;

// SkillUpgradeData'nın Inspector görünümünü özelleştirir:
// Levels bölümünde YALNIZCA seçili skillType'ın alanları gösterilir
// (Dash seçiliyken Blast Damage görünmez vb.) ve her alanın birimi etikette yazar.
// Bu dosya Editor klasöründe olduğu için build'e dahil edilmez.
[CustomEditor(typeof(SkillUpgradeData))]
public class SkillUpgradeDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // levels dışındaki her şeyi normal çiz (title, description, icon, skillType, effectPrefab)
        DrawPropertiesExcluding(serializedObject, "m_Script", "levels");

        var skillType = (SkillType)serializedObject.FindProperty("skillType").enumValueIndex;
        SerializedProperty levels = serializedObject.FindProperty("levels");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Seviyeler", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Tüm skill değerleri DÜZ değerdir (yüzde değil): süreler saniye, " +
            "hasar can puanı, yarıçap dünya birimi, hız birim/sn.", MessageType.Info);

        float improve = serializedObject.FindProperty("autoImprovePerLevel").floatValue;

        // Seviye sayısı (en az 1). Büyütülünce yeni seviyeler bir öncekinden
        // autoImprovePerLevel oranında iyileştirilerek OTOMATİK üretilir.
        int oldSize = levels.arraySize;
        int newSize = EditorGUILayout.IntField("Seviye Sayısı", oldSize);
        if (newSize != oldSize)
        {
            levels.arraySize = Mathf.Max(1, newSize);
            for (int i = oldSize; i < levels.arraySize; i++)
                if (i > 0) ImproveFromPrevious(levels, i, improve);
        }

        // Tek tuşla toparlama: 1. seviyeyi elle ayarla, gerisini baştan ürettir.
        if (levels.arraySize > 1 &&
            GUILayout.Button($"Seviye 2+ değerlerini 1. seviyeden yeniden üret (%{improve * 100f:0} iyileşme)"))
        {
            for (int i = 1; i < levels.arraySize; i++)
                ImproveFromPrevious(levels, i, improve);
        }

        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty lv = levels.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(i == 0 ? "Seviye 1 (ilk alım)" : $"Seviye {i + 1}",
                                       EditorStyles.boldLabel);

            Draw(lv, "cooldown", "Cooldown (sn)");

            switch (skillType)
            {
                case SkillType.Dash:
                    Draw(lv, "dashSpeed", "Dash Hızı (birim/sn)");
                    Draw(lv, "dashDuration", "Dash Süresi (sn)");
                    break;

                case SkillType.AreaBlast:
                    Draw(lv, "blastDamage", "Hasar (can puanı)");
                    Draw(lv, "blastRadius", "Yarıçap (dünya birimi)");
                    break;

                case SkillType.Shield:
                    Draw(lv, "shieldDuration", "Kalkan Süresi (sn)");
                    break;

                case SkillType.PulseWave:
                    Draw(lv, "waveCount", "Dalga Sayısı (adet)");
                    Draw(lv, "waveInterval", "Dalgalar Arası (sn)");
                    Draw(lv, "waveDamage", "Dalga Başına Hasar (can puanı)");
                    Draw(lv, "waveRadius", "Yarıçap (dünya birimi)");
                    Draw(lv, "waveExpandTime", "Yayılma Süresi (sn)");
                    break;

                case SkillType.Burst:
                    Draw(lv, "burstCount", "Mermi Sayısı (nokta)");
                    Draw(lv, "burstDamage", "Mermi Başına Hasar (can puanı)");
                    Draw(lv, "burstSpeed", "Mermi Hızı (birim/sn)");
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    static void Draw(SerializedProperty parent, string field, string label)
    {
        EditorGUILayout.PropertyField(parent.FindPropertyRelative(field), new GUIContent(label));
    }

    // index'teki seviyeyi, bir öncekinden türetir: değerler f oranında iyileşir.
    // Cooldown DÜŞER (azalması iyi), diğer her şey ARTAR. Tüm alanlar ölçeklenir ki
    // skillType sonradan değiştirilirse de tutarlı kalsın.
    static void ImproveFromPrevious(SerializedProperty levels, int index, float f)
    {
        SerializedProperty prev = levels.GetArrayElementAtIndex(index - 1);
        SerializedProperty cur = levels.GetArrayElementAtIndex(index);

        Scale(prev, cur, "cooldown", 1f - f);
        Scale(prev, cur, "dashSpeed", 1f + f);
        Scale(prev, cur, "dashDuration", 1f + f);
        Scale(prev, cur, "blastDamage", 1f + f);
        Scale(prev, cur, "blastRadius", 1f + f);
        Scale(prev, cur, "shieldDuration", 1f + f);
        // PulseWave: hasar ve yarıçap büyür; adet/aralık/yayılma süresi elle ayarlanır
        Scale(prev, cur, "waveDamage", 1f + f);
        Scale(prev, cur, "waveRadius", 1f + f);
        // Burst: hasar ve hız büyür; mermi sayısı (int) elle ayarlanır
        Scale(prev, cur, "burstDamage", 1f + f);
        Scale(prev, cur, "burstSpeed", 1f + f);
    }

    static void Scale(SerializedProperty from, SerializedProperty to, string field, float factor)
    {
        float v = from.FindPropertyRelative(field).floatValue * factor;
        // İki ondalığa yuvarla: 18.749999 gibi değerler asset'e yazılmasın
        to.FindPropertyRelative(field).floatValue = Mathf.Round(v * 100f) / 100f;
    }
}
