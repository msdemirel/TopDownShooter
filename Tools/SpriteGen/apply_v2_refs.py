#!/usr/bin/env python3
"""Generated_v2 sprite'larini sahneye ve upgrade data asset'lerine baglar.

Sadece kapsamdaki objeler degisir (upgrade kartlari + oyun ici HUD). Eski
Generated/ sprite'lari yerinde kalir; geri donmek icin git ile bu iki dosya
grubunu geri almak yeterli. Tekrar calistirmak guvenli (zaten bagliysa atlar).
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
V2 = os.path.join(ROOT, "Assets/Art/Generated_v2")
SCENE = os.path.join(ROOT, "Assets/Scenes/MainGame.unity")
DATA = os.path.join(ROOT, "Assets/Prefabs/Data")
SINGLE = 21300000      # Single sprite modundaki sprite'in fileID'si


def guid(rel):
    for line in open(os.path.join(V2, rel + ".meta")):
        if line.startswith("guid: "):
            return line.split()[1]
    raise SystemExit("guid yok: " + rel)


def ref(rel):
    return "{fileID: %d, guid: %s, type: 3}" % (SINGLE, guid(rel))


# ---------------------------------------------------------------- sahne
txt = open(SCENE).read()
parts = re.split(r"(\n--- !u!\d+ &\d+[^\n]*)", txt)
# parts: [header, sep, body, sep, body, ...] -> fileID -> index
index = {}
for i in range(1, len(parts), 2):
    fid = re.search(r"&(\d+)", parts[i]).group(1)
    index[fid] = i + 1
names = {}
for fid, i in index.items():
    m = re.search(r"\n  m_Name: (.*)", parts[i])
    if parts[i - 1].startswith("\n--- !u!1 ") and m:
        names[fid] = m.group(1)
parent_of = {}
go_tr = {}
for fid, i in index.items():
    if parts[i - 1].startswith("\n--- !u!224 "):
        go = re.search(r"m_GameObject: \{fileID: (\d+)", parts[i]).group(1)
        par = re.search(r"m_Father: \{fileID: (\d+)", parts[i]).group(1)
        go_tr[go] = fid
        parent_of[fid] = par
tr_go = {v: k for k, v in go_tr.items()}


def path_of_go(go):
    p = []
    while go in names:
        p.append(names[go])
        tr = go_tr.get(go)
        par = parent_of.get(tr, "0")
        go = tr_go.get(par)
    return "/".join(reversed(p))


def components(path_regex, must_contain):
    """path_regex'e uyan GameObject'lerin, must_contain iceren MonoBehaviour'lari."""
    out = []
    for fid, i in index.items():
        if not parts[i - 1].startswith("\n--- !u!114 "):
            continue
        m = re.search(r"m_GameObject: \{fileID: (\d+)", parts[i])
        if m and re.fullmatch(path_regex, path_of_go(m.group(1))) and must_contain in parts[i]:
            out.append(i)
    return out


changes = 0


def set_sprite(path_regex, old_internal, new_rel, expect):
    """Image.m_Sprite'i degistirir. old_internal: beklenen eski ui_kit fileID (guvenlik)."""
    global changes
    hits = components(path_regex, "m_Sprite:")
    new = "m_Sprite: " + ref(new_rel)
    done = 0
    for i in hits:
        body = parts[i]
        if new in body:
            done += 1
            continue
        pat = r"m_Sprite: \{fileID: %s, guid: \w+, type: 3\}" % re.escape(str(old_internal))
        if not re.search(pat, body):
            continue
        parts[i] = re.sub(pat, new, body, count=1)
        done += 1
        changes += 1
    if done != expect:
        sys.exit(f"{path_regex}: {expect} bekleniyordu, {done} bulundu - hicbir sey yazilmadi")


def set_field(path_regex, marker, field, new_rel):
    global changes
    hits = components(path_regex, marker)
    if len(hits) != 1:
        sys.exit(f"{path_regex}/{field}: 1 bekleniyordu, {len(hits)} bulundu")
    i = hits[0]
    new = f"\n  {field}: " + ref(new_rel)
    if new in parts[i]:
        return
    if re.search(rf"\n  {field}: \{{[^}}]*\}}", parts[i]):
        parts[i] = re.sub(rf"\n  {field}: \{{[^}}]*\}}", new, parts[i], count=1)
    else:
        # alan sahnede hic yok (yeni eklenen SerializeField) -> script referansindan sonra ekle
        parts[i] = re.sub(r"(\n  m_EditorClassIdentifier:[^\n]*)", r"\1" + new.replace("\\", "\\\\"), parts[i], count=1)
    changes += 1


CARDS = r"Canvas/UpgradePanel/UpgradeCard\d?/UpgradeCardBG"
set_sprite(CARDS, 2092007752, "Cards/card_frame.png", 3)
set_sprite(r"Canvas/WaveHUD", 2092007752, "HUD/wave_panel.png", 1)
set_sprite(r"Canvas/Level", -1905640581, "HUD/level_badge.png", 1)
set_sprite(r"Canvas/Money", -1988510349, "HUD/money_plate.png", 1)
set_sprite(r"Canvas/Health/BG", 142052604, "HUD/hp_frame.png", 1)
set_sprite(r"Canvas/Health/Bar", -1957001338, "HUD/hp_fill.png", 1)
set_sprite(r"Canvas/SkillBar/SkillSlot( \(\d\))?/CooldownOverlayImage", 134242439, "HUD/cooldown.png", 3)

set_field(r"Canvas/UpgradePanel", "coinSprite:", "coinSprite", "Cards/coin_small.png")
set_field(r"Canvas/UpgradePanel", "swapSprite:", "swapSprite", "Icons/ui_skill_swap.png")
set_field(r"Canvas/SkillBar", "swapPulseScale:", "slotFrame", "HUD/skill_slot.png")

# Soldaki kartin (UpgradeCard) ikonu digerlerinden ~8 ekran px solda duruyordu; yuvaya hizala
for fid, i in index.items():
    if parts[i - 1].startswith("\n--- !u!224 ") and "m_AnchoredPosition: {x: -826, y: 204}" in parts[i]:
        go = re.search(r"m_GameObject: \{fileID: (\d+)", parts[i]).group(1)
        if path_of_go(go) == "Canvas/UpgradePanel/UpgradeCard/Icon":
            parts[i] = parts[i].replace("m_AnchoredPosition: {x: -826, y: 204}",
                                        "m_AnchoredPosition: {x: -815, y: 204}")
            changes += 1

# ---------------------------------------------------------------- minimap
MINI = r"Canvas/Minimap"
for field, rel in (("frameSprite", "HUD/minimap_frame.png"), ("backgroundSprite", "HUD/minimap_bg.png"),
                   ("playerSprite", "HUD/minimap_player.png"), ("enemySprite", "HUD/minimap_enemy.png"),
                   ("edgeSprite", "HUD/minimap_edge.png"), ("sweepSprite", "HUD/minimap_sweep.png")):
    set_field(MINI, "worldRadius:", field, rel)


def set_value(path_regex, marker, field, value):
    """Sahnede kayitli basit bir degeri degistirir (sprite'lara uygun renk/boyut)."""
    global changes
    hits = components(path_regex, marker)
    if len(hits) != 1:
        sys.exit(f"{path_regex}/{field}: 1 bekleniyordu, {len(hits)} bulundu")
    i = hits[0]
    new = f"\n  {field}: {value}"
    if new in parts[i]:
        return
    if not re.search(rf"\n  {field}: [^\n]*", parts[i]):
        sys.exit(f"{field} sahnede yok")
    parts[i] = re.sub(rf"\n  {field}: [^\n]*", lambda m: new, parts[i], count=1)
    changes += 1


# zemin sprite'i kendi renklerini tasiyor -> beyaz tint; sprite'lar 2x piksel olcegine gore boyut
set_value(MINI, "worldRadius:", "backgroundColor", "{r: 1, g: 1, b: 1, a: 0.95}")
set_value(MINI, "worldRadius:", "playerDotSize", "18")
set_value(MINI, "worldRadius:", "enemyDotSize", "10")
set_value(MINI, "worldRadius:", "edgeAlpha", "0.7")

open(SCENE, "w").write("".join(parts))
print(f"sahne: {changes} degisiklik")

# ---------------------------------------------------------------- upgrade data ikonlari
ICONS = {
    "[SKL] FireBurst.asset": "skill_fire_burst",
    "[SKL] PulseWave.asset": "skill_pulse_wave",
    "[SKL]AreaBlast.asset": "skill_area_blast",
    "[SKL]ChainLightning.asset": "skill_chain_lightning",
    "[SKL]Dash.asset": "skill_dash",
    "[SKL]FrostNova.asset": "skill_frost_nova",
    "[SKL]Overdrive.asset": "skill_overdrive",
    "[SKL]SecondWind.asset": "skill_heal",
    "[SKL]Shield.asset": "skill_shield",
    "[UP]CritChance.asset": "stat_crit_chance",
    "[UP]Damage.asset": "stat_damage",          # eskiden yanlislikla bot ikonu
    "[UP]FireRate.asset": "stat_fire_rate",     # eskiden yanlislikla bot ikonu
    "[UP]Health.asset": "stat_max_health",
    "[UP]MagnetRange.asset": "stat_magnet_range",
    "[UP]Speed.asset": "stat_move_speed",
    "[W_UP] Sword II.asset": "weapon_sword",
    "[W_UP] Sword.asset": "weapon_sword",
    "[W_UP]Pistol.asset": "weapon_pistol",
}
n = 0
for fname, icon in ICONS.items():
    p = os.path.join(DATA, fname)
    t = open(p).read()
    new = "\n  icon: " + ref(f"Icons/{icon}.png")
    t2 = re.sub(r"\n  icon: \{[^}]*\}", new, t, count=1)
    if t2 == t and new not in t:
        sys.exit("icon alani yok: " + fname)
    if t2 != t:
        open(p, "w").write(t2)
        n += 1
print(f"data: {n} ikon guncellendi")
