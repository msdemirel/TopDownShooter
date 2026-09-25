using System.Collections.Generic;
using UnityEngine;

// Meta sistemin içerik listesi. Resources/MetaCatalog.asset olarak durur; MetaProgress,
// MetaApplier ve mağaza ekranı buradan okur (sahneye referans bağlamak gerekmez).
// Kurulum/güncelleme: Menü > TopDownShooter > Meta > Kurulumu Yap / Güncelle
[CreateAssetMenu(menuName = "TopDownShooter/Meta/Catalog", fileName = "MetaCatalog")]
public class MetaCatalog : ScriptableObject
{
    [Tooltip("Mağazada satılan kalıcı upgrade'ler (gösterim sırası).")]
    public List<MetaUpgradeData> upgrades = new List<MetaUpgradeData>();

    [Tooltip("Kalıcı kilidi olan (Unlock Condition != None) silah/skill/stat kartları. " +
             "Kurulum menüsü otomatik doldurur; kilitler ekranı ve 'yeni açıldı' bildirimi bunu kullanır.")]
    public List<UpgradeData> lockables = new List<UpgradeData>();
}
