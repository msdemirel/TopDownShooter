#!/usr/bin/env python3
"""Çevrilmesi gereken tüm metinleri toplar ve çeviri tablosundaki eksikleri raporlar.

    python3 Tools/Localization/check_strings.py            -> eksik anahtarlar / boş hücreler
    python3 Tools/Localization/check_strings.py --keys     -> tüm anahtarların listesi

Kaynaklar:
  * Koddaki Loc.T(...) / Loc.F(...) / Loc.Bind(x, ...) içindeki metinler
  * Sahnelerdeki sabit TMP yazıları (AutoLocalizer bunları çevirir)
  * Asset açıklamaları: upgrade kartları, mağaza upgrade'leri, karakterler, zorluklar
  * Değişkenle geçen etiketler (aşağıdaki EXTRA listesi): ör. AddDiff("Damage"), serileştirilmiş formatlar
İSİMLER (silah / skill / karakter / upgrade / sinerji adları) bilerek toplanmaz: İngilizce kalırlar.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TSV = os.path.join(ROOT, "Assets/Resources/Localization/strings.txt")
LANGS = ["en", "fr", "de", "tr", "zh", "it", "fi"]

# Kodda değişkenle Loc.T'ye giden metinler (regex yakalayamaz)
EXTRA = [
    # skill seviye farkı etiketleri (SkillUpgradeData.AddDiff)
    "Cooldown", "Speed", "Duration", "Damage", "Radius", "Waves", "Shots", "Heal", "Slow", "Slow Duration",
    "Fire Rate", "Chains", "Range",
    # karakter bonus etiketleri (CharacterData.BonusText)
    "Max Health", "Move Speed", "Crit Chance", "Pickup Range",
    # sahnede serileştirilmiş formatlar / notlar
    "Wave {0}", "LvL: {0}", "FREE", "Replaces a skill", "Replaces a weapon", "WEAPONS",
    # pause onay penceresi
    "RETURN TO MENU?", "QUIT WITHOUT SAVING?", "MAIN MENU", "QUIT",
    # ana menü "devam et" penceresi
    "RUN IN PROGRESS", "CONTINUE", "NEW RUN",
    "<color=#94B0C2>Starting a new run ends the saved one (its Core is still awarded).</color>",
    # kalite adları
    "Very Low", "Low", "Medium", "High", "Very High", "Ultra",
]

# Sahnede görünen ama çevrilmeyecek yazılar (oyun adı, yer tutucular, sayılar)
SKIP = {"TOP DOWN", "SHOOTER", "v1.0", "Option A", "Sample text", "New Text", "BEST 0", "TOTAL 0",
        "NEW UNLOCK: -", "WAVE 1  -  RUN", "WAVE 1  -  RUN #1", "WAVES +0   KILLS +0", "Exp: ", "Health:", "LvL:", "NO RUNS YE", "BUY"}


def code_keys():
    keys = set()
    for f in glob.glob(os.path.join(ROOT, "Assets/Scripts/**/*.cs"), recursive=True):
        src = open(f, encoding="utf-8").read()
        for m in re.finditer(r"Loc\.(T|F|Bind)\(", src):
            # ilk argümanı parantez dengesiyle al
            i, depth, start = m.end(), 1, m.end()
            args, cur = [], ""
            while i < len(src) and depth:
                c = src[i]
                if c == '"':   # metin: kaçışları atla
                    j = i + 1
                    while src[j] != '"' or src[j - 1] == "\\": j += 1
                    cur += src[i:j + 1]; i = j + 1; continue
                if c in "([{": depth += 1
                elif c in ")]}": depth -= 1
                if depth == 1 and c == "," : args.append(cur); cur = ""
                elif depth: cur += c
                i += 1
            args.append(cur)
            arg = args[1] if m.group(1) == "Bind" and len(args) > 1 else args[0]
            if arg.strip().startswith("$"): continue   # interpolasyonlu: anahtar değil
            for lit in re.findall(r'(?<!\$)"((?:[^"\\]|\\.)*)"', arg):
                keys.add(lit.encode().decode("unicode_escape").encode("latin-1").decode("utf-8"))
    return keys


def scene_keys():
    import yaml
    keys = set()
    for f in glob.glob(os.path.join(ROOT, "Assets/Scenes/*.unity")):
        lines = open(f, encoding="utf-8").read().split("\n")
        for i, line in enumerate(lines):
            if not line.startswith("  m_text: "): continue
            block = [line]   # çok satırlı YAML değeri: devam satırları daha içeriden başlar
            for nxt in lines[i + 1:]:
                if re.match(r"^\s*[A-Za-z_]+:( |$)|^---|^  - ", nxt): break
                block.append(nxt)
            v = yaml.safe_load("\n".join(block))["m_text"]
            v = "" if v is None else str(v)
            if v and v not in SKIP and not re.fullmatch(r"[\d:%./ +\-]*", v): keys.add(v)
    return keys


def asset_keys():
    keys = set()
    pats = ["Assets/Prefabs/Data/*.asset", "Assets/Prefabs/Data/Meta/*.asset"]
    for p in pats:
        for f in glob.glob(os.path.join(ROOT, p)):
            t = open(f, encoding="utf-8").read()
            m = re.search(r"\n  description: (.*?)\n  (?:icon|effect|color|enemyHealth|skinSprites)", t, re.S)
            if m:
                v = m.group(1).strip()
                if v[:1] in "'\"": v = v[1:-1].replace("''", "'")
                v = re.sub(r"\s*\n\s*", " ", v)
                if v: keys.add(v)
            if "Difficulty_" in f:   # zorluk ADLARI çevrilir (NORMAL / HARD / NIGHTMARE)
                keys.add(re.search(r"\n  title: (.*)", t).group(1).strip())
    # sinerji açıklamaları (adları değil)
    syn = open(os.path.join(ROOT, "Assets/Scripts/Player/SkillSynergies.cs"), encoding="utf-8").read()
    keys.update(re.findall(r'description = "([^"]+)"', syn))
    return keys


def load_tsv():
    rows = {}
    if not os.path.exists(TSV): return rows
    lines = open(TSV, encoding="utf-8").read().split("\n")
    header = lines[0].split("\t")
    for line in lines[1:]:
        if not line.strip() or line.startswith("#"): continue
        cells = line.split("\t")
        en = cells[0].replace("\\n", "\n")
        rows[en] = {h: (cells[i].replace("\\n", "\n") if i < len(cells) else "") for i, h in enumerate(header)}
    return rows


def all_keys():
    return sorted((code_keys() | scene_keys() | asset_keys() | set(EXTRA)) - SKIP - {""})


def main():
    keys = all_keys()
    if "--keys" in sys.argv:
        for k in keys: print(repr(k))
        print(len(keys), "anahtar"); return
    sq = lambda x: re.sub(r"\s+", " ", x).strip()   # Loc gibi: boşluk farkı önemsiz
    table = {sq(k): v for k, v in load_tsv().items()}
    keys = sorted({sq(k) for k in keys})
    missing = [k for k in keys if k not in table]
    empty = [(k, l) for k in keys if k in table for l in LANGS[1:] if not table[k].get(l)]
    unused = [k for k in table if k not in keys]
    for k in missing: print("EKSİK   ", repr(k))
    for k, l in empty: print(f"BOŞ [{l}]", repr(k))
    for k in unused: print("KULLANILMIYOR", repr(k))
    print(f"{len(keys)} anahtar, {len(missing)} eksik, {len(empty)} boş hücre, {len(unused)} kullanılmayan")


if __name__ == "__main__":
    main()
