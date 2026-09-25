using UnityEditor;
using UnityEngine;

// WeaponData'nın Inspector'ında yalnızca seçili weaponType'a ait alanları gösterir:
// Ranged'de mermi alanları, Melee'de kılıç alanları. Ortak alanlar (hasar, fireRate,
// range, kritik) her zaman görünür. Bu dosya Editor klasöründe — build'e girmez.
[CustomEditor(typeof(WeaponData))]
public class WeaponDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Draw("weaponName");
        Draw("sprite");
        Draw("icon");
        Draw("weaponType");

        var type = (WeaponType)serializedObject.FindProperty("weaponType").enumValueIndex;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ortak", EditorStyles.boldLabel);
        Draw("damage");
        Draw("fireRate");
        Draw("range", type == WeaponType.Melee ? "Erişim (Reach)" : "Menzil (Range)");

        EditorGUILayout.Space();
        if (type == WeaponType.Ranged)
        {
            EditorGUILayout.LabelField("Mermi", EditorStyles.boldLabel);
            Draw("projectilePrefab");
            Draw("projectileSpeed");
            Draw("projectileCount");
            Draw("spreadAngle");
            Draw("inaccuracyAngle");
            Draw("pierce");
        }
        else
        {
            EditorGUILayout.LabelField("Kılıç", EditorStyles.boldLabel);
            Draw("meleeArc");
            Draw("lungeDistance");
            Draw("swingTime");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Kritik Vuruş", EditorStyles.boldLabel);
        Draw("critChance");
        Draw("critMultiplier");

        serializedObject.ApplyModifiedProperties();
    }

    void Draw(string field, string label = null)
    {
        SerializedProperty p = serializedObject.FindProperty(field);
        if (p == null) return;
        if (label == null) EditorGUILayout.PropertyField(p, true);
        else EditorGUILayout.PropertyField(p, new GUIContent(label), true);
    }
}
