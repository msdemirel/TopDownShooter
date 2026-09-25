#!/usr/bin/env python3
"""Denge turu (E) — değerleri asset'lere yazar. Bu dosya aynı zamanda denge tablosunun belgesidir.

Çalıştır:  python3 Tools/Balance/apply_balance.py      (sonra Unity'de Ctrl+R)
Tekrar çalıştırmak güvenli: aynı değerleri yazar.

EKONOMİ (loot düzeltmesinden sonra, 10 tasarlanmış dalga, hepsi toplanırsa beklenen):
    dalga:      1    2    3    4    5    6    7    8    9   10
    level:      2    4    5    6    8    9   10   12   14   15   (exp eğrisi 4 + 3/level, değişmedi)
    toplam $:   3    8   16   24   37   48   64   88  114  151
Silahlara ulaşmak ZOR olmalı (2. tur): 10. dalgaya kadar ~150 coin ile ~3-4 silah alınır
(ilki ~3. dalga, sonra her 2 dalgada bir). Reroll da aynı paradan: silah mı reroll mu kararı.
Ayrıca silah kartı her panelde çıkmaz: UpgradeManager.weaponCardChance = 0.45 (Inspector'da).
Geç açılan silah coin başına daha az DPS verir ama SLOT başına daha çok (6 slot + birleştirme).
"""
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DATA = os.path.join(ROOT, "Assets/Prefabs/Data")
PREFABS = os.path.join(ROOT, "Assets/Prefabs")

# ---------------------------------------------------------------- 1. LOOT
# Hata: bu 8 düşmanın loot adetleri 0-0 idi (LootDropper Random.Range(0, 0+1) = 0) -> hiç düşürmüyorlardı.
# Oranlar FlyEnemy/Exploder'a göre (exp %50-55, para %20-25, can %3), role göre ayarlı.
EXP = "{fileID: 34416263852694475, guid: 3cba8cb1424ab3847a4af5e557479638, type: 3}"
MONEY = "{fileID: 3672976162837707703, guid: a2814ca6af7faf446b66258790fdbaab, type: 3}"
HEALTH = "{fileID: 3709485075238305286, guid: b9b7d3ebb199eaa4fae616491c6aaa10, type: 3}"

#            prefab            exp (şans, min, max)  para              can
LOOT = {
    "Swarmling":    ((45, 1, 1), (10, 1, 1), (2, 1, 1)),    # sürü: çok gelir, az verir
    "Scuttler":     ((55, 1, 1), (22, 1, 1), (3, 1, 1)),
    "Ranged_Enemy": ((55, 1, 1), (22, 1, 1), (3, 1, 1)),
    "Scatter":      ((55, 1, 1), (22, 1, 1), (3, 1, 1)),
    "Charger":      ((55, 1, 1), (30, 1, 1), (5, 1, 1)),    # tehlikeli: daha değerli
    "Turret_Enemy": ((55, 1, 1), (30, 1, 1), (5, 1, 1)),
    "Juggernaut":   ((50, 1, 2), (40, 1, 3), (10, 1, 1)),   # elit (sonsuz mod 12+)
    "Hive":         ((50, 1, 2), (40, 1, 3), (10, 1, 1)),   # elit (sonsuz mod 15+)
}

# ---------------------------------------------------------------- 2. SİLAH FİYAT / KİLİT
#                 kart dosyası (Data/)           fiyat  açılma dalgası
WEAPON_CARDS = {
    "[W_UP] Bat.asset":           (12, 1),   # başlangıç silahı: birleştirme malzemesi
    "[W_UP] Sword.asset":         (15, 1),   # (lastWave 8 aynen kalır)
    "[W_UP]Pistol.asset":         (22, 2),
    "[W_UP] SMG.asset":           (25, 3),
    "[W_UP] Katana.asset":        (30, 3),
    "[W_UP] Greatsword.asset":    (32, 4),
    "[W_UP] Revolver.asset":      (36, 4),
    "[W_UP] Shotgun.asset":       (42, 5),
    "[W_UP] Laser Sword.asset":   (52, 6),
    "[W_UP] Assault Rifle.asset": (58, 7),
    "[W_UP] Sniper Rifle.asset":  (68, 8),
    "[W_UP] Minigun.asset":       (90, 10),
}

# ---------------------------------------------------------------- 3. SİLAH DEĞERLERİ
# Greatsword: 18 hasar x 1.6 = ~30 DPS, 20 coin -> en zayıf silahtı (Katana 46 DPS / 18 coin).
WEAPON_STATS = {
    "Guns/Greatsword.asset": {"damage": 24},   # ~40 DPS, geniş yay + uzun erişim
    "Guns/Sword.asset": {"damage": 12},        # test değeri 100'dü (~157 DPS); 12 x 1.5 = ~19 DPS
}

# ---------------------------------------------------------------- 4. KARAKTERLER
# Scout: -15 can = temel 50 canın %30'u (fazla ceza). Juggernaut +50 / -%12 hız dengeli bırakıldı.
CHARACTER_STATS = {
    "Meta/Character_SCOUT.asset": {"maxHealth": -10},
}


def set_field(text, field, value, path):
    new, n = re.subn(rf"(\n  {field}: )[^\n]*", rf"\g<1>{value}", text, count=1)
    if n != 1:
        raise SystemExit(f"{path}: '{field}' alanı bulunamadı")
    return new


def apply_loot():
    for prefab, (exp, money, health) in LOOT.items():
        path = os.path.join(PREFABS, prefab + ".prefab")
        text = open(path).read()
        i = text.find("Assembly-CSharp::LootDropper")
        if i < 0:
            raise SystemExit(f"{path}: LootDropper yok")
        start = text.index("\n  drops:", i)
        end = text.index("\n  nothingChance:", start)
        entries = ""
        for ref, (chance, lo, hi) in ((EXP, exp), (MONEY, money), (HEALTH, health)):
            entries += (f"\n  - pickupPrefab: {ref}\n    dropChance: {chance}"
                        f"\n    minCount: {lo}\n    maxCount: {hi}")
        text = text[:start] + "\n  drops:" + entries + text[end:]
        nothing = 100 - exp[0] - money[0] - health[0]
        text = re.sub(r"(\n  nothingChance: )[^\n]*", rf"\g<1>{nothing}", text[: text.index("\n--- !u!", i)], count=1) \
            + text[text.index("\n--- !u!", i):]
        open(path, "w").write(text)
        print(f"loot   {prefab:14s} exp {exp[0]}%  para {money[0]}%  can {health[0]}%  hiç {nothing}%")


def apply_assets(table, label):
    for rel, fields in table.items():
        path = os.path.join(DATA, rel)
        text = open(path).read()
        for f, v in fields.items():
            text = set_field(text, f, v, path)
        open(path, "w").write(text)
        print(f"{label:6s} {rel:32s} {fields}")


def main():
    apply_loot()
    apply_assets({k: {"cost": c, "unlockWave": w} for k, (c, w) in WEAPON_CARDS.items()}, "kart")
    apply_assets(WEAPON_STATS, "silah")
    apply_assets(CHARACTER_STATS, "karakt")


if __name__ == "__main__":
    main()
