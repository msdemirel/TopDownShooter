#!/usr/bin/env python3
"""HUD + upgrade card sprite set v2  ->  Assets/Art/Generated_v2/

Eski set (Assets/Art/Generated/) DOKUNULMAZ; bu script sadece Generated_v2'ye yazar.
Tekrar calistirmak guvenli: ayni dosya adlarina yazar, .meta varsa korunur
(guid degismez, sahnedeki referanslar bozulmaz).

Olculer sahnedeki gercek ekran boyutlarindan (1920x1080 referans) turetildi.
Cogu Image 'Simple' tipte ve X/Y olcegi farkli oldugu icin sprite'lar ekranda
gorunecekleri EN-BOY ORANINDA cizildi: 1 sprite pikseli ~ 2 ekran pikseli.

    Cards/card_frame.png   229x235  UpgradeCardBG   (457x470 ekranda, yanlarda 8px saydam pay)
    Cards/coin_small.png    16x16   UpgradePanel.coinSprite (fiyat yanindaki coin)
    HUD/wave_panel.png      81x63   WaveHUD         (162x126)
    HUD/level_badge.png     58x58   Level           (117x117)
    HUD/money_plate.png     97x30   Money           (195x61)
    HUD/hp_frame.png       124x22   Health/BG       (249x45)
    HUD/hp_fill.png        109x8    Health/Bar      (218x16, Filled)
    HUD/cooldown.png        25x25   SkillSlot/CooldownOverlayImage (50x50, Filled)
    HUD/skill_slot.png      32x32   SkillHUD.slotFrame (ikonun arkasi, 64x64)
    HUD/skill_bar_frame.png 48x48   SkillHUD.barFrame (9-slice, kutularin toplam alanina gore)
    HUD/button_frame.png    32x20   genel buton, reroll (9-slice, kenar 8)
    HUD/icon_core.png       24x24   kalici para "Core" ikonu (Game Over, magaza)
    HUD/minimap_*.png               MinimapUI (220x220 ekranda; cerceve 110, zemin/tarama 96,
                                    oyuncu 9, dusman 5, kenar oku 7x5 - gri tonlu, rengi tint verir)
    Icons/*.png             64x64   upgrade/skill ikonlari (32px orijinalden Scale2x + isik/golge)

Calistir:  python3 Tools/SpriteGen/gen_hud_v2.py
"""
import math
import os
import uuid
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC_ICONS = os.path.join(ROOT, "Assets/Art/Generated/Icons/Upgrades")
EXTRA_ICONS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "src_icons")
OUT = os.path.join(ROOT, "Assets/Art/Generated_v2")

# ---------------------------------------------------------------- palet
OUTL = (9, 11, 22)          # dis cizgi
M0 = (26, 31, 54)           # metal koyu (alt/sag)
M1 = (43, 52, 86)           # metal orta
M2 = (66, 79, 124)          # metal acik (ust/sol)
M3 = (104, 122, 178)        # parlama
F_TOP = (24, 30, 53)        # zemin gradyani
F_BOT = (13, 16, 30)
WELL = (9, 11, 21)          # oyuk (socket / slot ici)
HDR_TOP = (37, 46, 80)
HDR_BOT = (27, 33, 59)
CY = (70, 236, 248)
CY_D = (24, 132, 166)
CY_DD = (18, 72, 102)
MG = (255, 64, 140)
MG_D = (150, 30, 90)
GOLD = (255, 207, 112)
GOLD_L = (255, 240, 180)
GOLD_D = (205, 130, 40)
GOLD_DD = (120, 66, 22)


def rgba(c, a=255):
    return (c[0], c[1], c[2], a)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(c, k):
    """k>0 acar, k<0 koyulastirir."""
    if k >= 0:
        return mix(c, (255, 255, 255), k)
    return mix(c, (0, 0, 0), -k)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.px = self.im.load()

    def set(self, x, y, c, a=255):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = rgba(c, a) if len(c) == 3 else c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return (0, 0, 0, 0)

    def hline(self, x0, x1, y, c, a=255):
        for x in range(x0, x1 + 1):
            self.set(x, y, c, a)

    def vline(self, x, y0, y1, c, a=255):
        for y in range(y0, y1 + 1):
            self.set(x, y, c, a)

    def rect(self, x0, y0, x1, y1, c, a=255):
        for y in range(y0, y1 + 1):
            self.hline(x0, x1, y, c, a)

    def save(self, rel):
        path = os.path.join(OUT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.im.save(path)
        return path


# ---------------------------------------------------------------- sekil yardimcilari
def chamfer_inside(x, y, x0, y0, x1, y1, ch, corners=(1, 1, 1, 1)):
    """Pah kirilmis dikdortgenin icinde mi? corners = (sol-ust, sag-ust, sag-alt, sol-alt)."""
    if x < x0 or x > x1 or y < y0 or y > y1:
        return False
    tl, tr, br, bl = corners
    if tl and (x - x0) + (y - y0) < ch:
        return False
    if tr and (x1 - x) + (y - y0) < ch:
        return False
    if br and (x1 - x) + (y1 - y) < ch:
        return False
    if bl and (x - x0) + (y1 - y) < ch:
        return False
    return True


def bevel_panel(cv, x0, y0, x1, y1, ch, fill_top, fill_bot, corners=(1, 1, 1, 1),
                metal=2, grid=True):
    """Dis cizgi + metal cerceve (ust/sol acik, alt/sag koyu) + gradyan zemin.

    Katmanlar disaridan iceri: 1px OUTL, 'metal' px metal, 1px ic golge, zemin.
    """
    def inside(x, y, inset):
        return chamfer_inside(x, y, x0 + inset, y0 + inset, x1 - inset, y1 - inset,
                              max(0, ch - inset // 2), corners)

    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if not inside(x, y, 0):
                continue
            if not inside(x, y, 1):
                cv.set(x, y, OUTL)
            elif not inside(x, y, 1 + metal):
                # metal: isik sol-ustten geliyor
                lit = not inside(x - 1, y - 1, 1) or not inside(x - 1, y, 1) or not inside(x, y - 1, 1)
                dark = not inside(x + 1, y + 1, 1) or not inside(x + 1, y, 1) or not inside(x, y + 1, 1)
                if lit and not dark:
                    c = M2
                elif dark and not lit:
                    c = M0
                else:
                    c = M1
                # dis halkada en ust sira parlasin
                if lit and not dark and not inside(x, y - 1, 1):
                    c = M3 if (x + y) % 7 else M2
                cv.set(x, y, c)
            elif not inside(x, y, 2 + metal):
                cv.set(x, y, OUTL)
            else:
                t = (y - y0) / max(1, (y1 - y0))
                c = mix(fill_top, fill_bot, t)
                if grid and (x - x0) % 6 == 3 and (y - y0) % 6 == 3:
                    c = shade(c, 0.07)
                cv.set(x, y, c)


def fade_line(cv, x0, x1, y, c_mid, c_end, a_end=255):
    """Ortasi parlak, uclari sonuk yatay cizgi."""
    n = x1 - x0
    for x in range(x0, x1 + 1):
        t = abs((x - x0) / n - 0.5) * 2  # 0 ortada, 1 uclarda
        cv.set(x, y, mix(c_mid, c_end, t ** 1.5), int(255 + (a_end - 255) * t))


def bracket(cv, x, y, dx, dy, n, c):
    """Kose koseli parantezi: (x,y) kose, dx/dy yon (+1/-1)."""
    for i in range(n):
        cv.set(x + dx * i, y, c)
        cv.set(x, y + dy * i, c)


def rivet(cv, x, y):
    cv.set(x, y, M3)
    cv.set(x + 1, y, M1)
    cv.set(x, y + 1, M1)
    cv.set(x + 1, y + 1, OUTL)


def circle_fill(cv, cx, cy, r, color_fn):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
            if d <= r:
                c = color_fn(x, y, d)
                if c is not None:
                    cv.set(x, y, c)


def coin(cv, ox, oy, size):
    """Pixel altin coin (size = 14 veya 16)."""
    r = size / 2 - 0.5
    cx, cy = ox + size / 2, oy + size / 2

    def col(x, y, d):
        if d > r - 1:
            return OUTL
        if d > r - 2.2:
            # kenar: sol-ust acik, sag-alt koyu
            return GOLD if (x - cx) + (y - cy) < 0 else GOLD_D
        # yuz: hafif gradyan
        t = ((y + 0.5) - (cy - r)) / (2 * r)
        c = mix(GOLD_L, GOLD, min(1, t * 1.6)) if t < 0.5 else mix(GOLD, GOLD_D, (t - 0.5) * 1.4)
        return c

    circle_fill(cv, cx, cy, r, col)
    # ic halka + ortada kabartma
    ir = r - 3.2
    for y in range(oy, oy + size):
        for x in range(ox, ox + size):
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
            if ir - 0.6 < d <= ir + 0.4:
                cv.set(x, y, GOLD_D if (x - cx) + (y - cy) < 0 else GOLD_L)
    # ortada dikey cubuk (para sembolu)
    mx = ox + size // 2 - 1
    for y in range(oy + size // 2 - 2, oy + size // 2 + 2):
        cv.set(mx, y, GOLD_DD)
        cv.set(mx + 1, y, GOLD_D)
    # parlama
    cv.set(ox + size // 2 - 3, oy + 3, (255, 255, 255))
    cv.set(ox + size // 2 - 4, oy + 4, GOLD_L)


def heart(cv, ox, oy):
    """9x8 kalp."""
    shape = [
        ".XX...XX.",
        "XhhX.XrrX",
        "XhrrXrrrX",
        "XrrrrrrdX",
        ".XrrrrdX.",
        "..XrrdX..",
        "...XdX...",
        "....X....",
    ]
    cmap = {"X": OUTL, "h": (255, 170, 185), "r": (255, 52, 84), "d": (170, 18, 50)}
    for j, row in enumerate(shape):
        for i, ch in enumerate(row):
            if ch in cmap:
                cv.set(ox + i, oy + j, cmap[ch])


# ================================================================ CARD
def make_card_frame():
    # Sahnedeki (1920x1080) yerlesimden, sprite pikseli cinsinden:
    #   Title merkezi y~37, Icon merkezi (~43, ~42) boyut 24, Decription merkezi y~117,
    #   CostText merkezi y~207. Kartlar sahnede ~26 ekran px ust uste bindigi icin
    #   cerceve yanlardan MX px saydam pay birakir (kartlar arasinda bosluk kalsin).
    W, H = 229, 235
    MX = 8
    x0, x1 = MX, W - 1 - MX
    cv = Canvas(W, H)
    bevel_panel(cv, x0, 0, x1, H - 1, 9, F_TOP, F_BOT, metal=3)

    il, ir = x0 + 5, x1 - 5          # ic zeminin sol/sag siniri
    cx = W // 2

    def is_fill(x, y):
        p = cv.get(x, y)
        return p[3] and p[:3] not in (OUTL, M0, M1, M2, M3)

    # --- ust serit: kucuk "tab" + magenta led (baslik bandinin ustu bos kalmasin)
    cv.hline(cx - 22, cx + 22, 5, CY_DD)
    fade_line(cv, cx - 16, cx + 16, 6, CY, CY_DD)
    cv.rect(x1 - 12, 4, x1 - 11, 5, MG)
    cv.set(x1 - 12, 4, (255, 170, 210))

    # --- baslik bandi (y 12..62): Title y~37'de ortalanir
    HB0, HB1 = 12, 62
    for y in range(HB0, HB1 + 1):
        t = (y - HB0) / (HB1 - HB0)
        for x in range(il, ir + 1):
            if is_fill(x, y):
                cv.set(x, y, mix(HDR_TOP, HDR_BOT, t))
    cv.hline(il, ir, HB0 - 1, OUTL)
    cv.hline(il, ir, HB0, shade(HDR_TOP, 0.18))
    fade_line(cv, il, ir, HB1 + 1, CY, CY_DD)
    cv.hline(il, ir, HB1 + 2, OUTL)
    cv.hline(il, ir, HB1 + 3, shade(F_TOP, -0.25))
    for y in range(28, 47):               # bandin uclarinda cyan centik
        cv.set(il, y, CY_D)
        cv.set(ir, y, CY_D)

    # --- ikon yuvasi: merkez (43, 42), ikon 24px
    icx, icy = 43, 42
    sx0, sy0, sx1, sy1 = icx - 16, icy - 16, icx + 16, icy + 16
    for y in range(sy0, sy1 + 1):
        for x in range(sx0, sx1 + 1):
            if not chamfer_inside(x, y, sx0, sy0, sx1, sy1, 5):
                continue
            if not chamfer_inside(x, y, sx0 + 1, sy0 + 1, sx1 - 1, sy1 - 1, 5):
                cv.set(x, y, OUTL)
            elif not chamfer_inside(x, y, sx0 + 2, sy0 + 2, sx1 - 2, sy1 - 2, 5):
                cv.set(x, y, M2 if (x - sx0) + (y - sy0) < (sx1 - x) + (sy1 - y) else M0)
            elif not chamfer_inside(x, y, sx0 + 3, sy0 + 3, sx1 - 3, sy1 - 3, 5):
                cv.set(x, y, OUTL)
            else:
                d = ((x - icx) ** 2 + (y - icy) ** 2) ** 0.5
                cv.set(x, y, mix((26, 48, 76), WELL, min(1, d / 16)))
    bracket(cv, sx0 + 4, sy0 + 4, 1, 1, 4, CY)
    bracket(cv, sx1 - 4, sy1 - 4, -1, -1, 4, CY)
    cv.set(sx1 - 4, sy0 + 4, CY_D)
    cv.set(sx0 + 4, sy1 - 4, CY_D)

    # --- govde: aciklama bolgesinin yanlarinda kesik cizgiler + koseli parantezler
    fy = 192
    for y in range(76, 160):
        if (y // 3) % 2 == 0:
            cv.set(il + 3, y, CY_DD)
            cv.set(ir - 3, y, CY_DD)
    bracket(cv, il + 3, 70, 1, 1, 6, CY)
    bracket(cv, ir - 3, 70, -1, 1, 6, CY)
    bracket(cv, il + 3, 165, 1, -1, 6, CY_D)
    bracket(cv, ir - 3, 165, -1, -1, 6, CY_D)

    # --- alt fiyat bolumu (CostText merkezi y~207)
    cv.hline(il, ir, fy, OUTL)
    fade_line(cv, il + 1, ir - 1, fy + 1, M2, M0)
    for y in range(fy + 2, H - 5):
        for x in range(il, ir + 1):
            if is_fill(x, y):
                cv.set(x, y, mix((17, 21, 38), (11, 13, 25), (y - fy) / (H - fy)))
    px0, py0, px1, py1 = cx - 42, 198, cx + 42, 216
    for y in range(py0, py1 + 1):
        for x in range(px0, px1 + 1):
            if not chamfer_inside(x, y, px0, py0, px1, py1, 4):
                continue
            if not chamfer_inside(x, y, px0 + 1, py0 + 1, px1 - 1, py1 - 1, 4):
                cv.set(x, y, OUTL)
            elif not chamfer_inside(x, y, px0 + 2, py0 + 2, px1 - 2, py1 - 2, 4):
                cv.set(x, y, M1 if y < (py0 + py1) / 2 else M0)
            else:
                cv.set(x, y, mix((10, 12, 22), (16, 20, 36), (y - py0) / (py1 - py0)))
    cv.hline(px0 + 5, px1 - 5, py0 + 1, M2)
    for i in range(3):                    # plakanin yanlarinda ok uclari
        cv.vline(px0 - 6 + i, 207 - (2 - i), 207 + (2 - i), CY_D if i < 2 else CY)
        cv.vline(px1 + 6 - i, 207 - (2 - i), 207 + (2 - i), CY_D if i < 2 else CY)

    cv.rect(x0 + 10, H - 6, x0 + 11, H - 5, MG_D)
    rivet(cv, x0 + 2, H // 2 + 20)
    rivet(cv, x1 - 3, H // 2 + 20)
    return cv.save("Cards/card_frame.png")


def make_coin_small():
    cv = Canvas(16, 16)
    coin(cv, 0, 0, 16)
    return cv.save("Cards/coin_small.png")


# ================================================================ HUD
def make_wave_panel():
    # Ekranin tepesine asili sekme: ust koseler duz, alt koseler pahli.
    W, H = 81, 63
    cv = Canvas(W, H)
    bevel_panel(cv, 0, 0, W - 1, H - 1, 10, F_TOP, F_BOT, corners=(0, 0, 1, 1), metal=2)
    # alt kenarda cyan vurgu
    fade_line(cv, 12, W - 13, H - 7, CY, CY_DD)
    cv.hline(30, W - 31, H - 6, CY_D)
    # yan centikler
    for y in range(18, 34):
        if y % 2 == 0:
            cv.set(4, y, CY_D)
            cv.set(W - 5, y, CY_D)
    rivet(cv, 6, 5)
    rivet(cv, W - 8, 5)
    cv.rect(W // 2 - 1, 3, W // 2, 3, MG)
    return cv.save("HUD/wave_panel.png")


def make_level_badge():
    # Ortadaki daireyi PlayerHUD'un LevelClock'u (radial exp saati) doldurur.
    W = H = 58
    cv = Canvas(W, H)
    bevel_panel(cv, 0, 0, W - 1, H - 1, 12, F_TOP, F_BOT, metal=2, grid=False)
    cx = cy = W / 2
    # dairesel oyuk + cyan halka
    for y in range(H):
        for x in range(W):
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
            if d <= 21.5:
                cv.set(x, y, mix((20, 30, 52), WELL, d / 21.5))
            elif d <= 22.6:
                cv.set(x, y, OUTL)
            elif d <= 23.8:
                ang_lit = (x - cx) + (y - cy) < 0
                cv.set(x, y, CY if ang_lit else CY_D)
            elif d <= 24.8:
                cv.set(x, y, OUTL)
    # halkada 4 tik (saat yonleri)
    for (x, y) in ((29, 4), (28, 4), (29, 53), (28, 53), (4, 29), (4, 28), (53, 29), (53, 28)):
        cv.set(x, y, M3)
    rivet(cv, 6, 6)
    rivet(cv, W - 8, 6)
    rivet(cv, 6, H - 8)
    rivet(cv, W - 8, H - 8)
    return cv.save("HUD/level_badge.png")


def make_money_plate():
    W, H = 97, 30
    cv = Canvas(W, H)
    bevel_panel(cv, 0, 0, W - 1, H - 1, 6, F_TOP, F_BOT, corners=(0, 1, 1, 0), metal=2)
    # coin yuvasi
    for y in range(4, H - 4):
        for x in range(4, 27):
            cv.set(x, y, mix((22, 26, 44), (14, 17, 30), (y - 4) / (H - 8)))
    cv.vline(27, 4, H - 5, OUTL)
    cv.vline(28, 4, H - 5, M1)
    coin(cv, 8, 8, 14)
    # yazi alaninin altinda altin cizgi
    fade_line(cv, 34, W - 8, H - 6, GOLD_D, GOLD_DD)
    return cv.save("HUD/money_plate.png")


def make_hp_frame():
    # Bar'in oturdugu oyuk: x 3..112, y 7..15 (sahnedeki Health/Bar dikdortgeni).
    W, H = 124, 22
    cv = Canvas(W, H)
    bevel_panel(cv, 0, 0, W - 1, H - 1, 5, F_TOP, F_BOT, corners=(0, 1, 1, 0), metal=2, grid=False)
    cv.rect(3, 6, 113, 16, OUTL)
    cv.rect(4, 7, 112, 15, (30, 8, 18))          # bos can = koyu bordo
    cv.hline(4, 112, 7, (18, 4, 10))
    for x in range(14, 112, 11):                  # bos kisimda silik bolmeler
        cv.vline(x, 8, 15, (44, 14, 26))
    cv.vline(114, 3, H - 4, M1)
    heart(cv, 115, 7)
    return cv.save("HUD/hp_frame.png")


def make_hp_fill():
    W, H = 109, 8
    cv = Canvas(W, H)
    rows = [(255, 150, 165), (255, 72, 98), (255, 46, 77), (238, 34, 66),
            (214, 24, 56), (184, 16, 48), (150, 10, 40), (112, 6, 30)]
    for y, c in enumerate(rows):
        cv.hline(0, W - 1, y, c)
    # bolmeler (her 11 px) - fill kesilince de duzgun gorunur
    for x in range(10, W, 11):
        for y in range(1, H - 1):
            cv.set(x, y, shade(rows[y], -0.28))
    # parlak ust seritte kucuk yansima kiriklari
    for x in range(2, W, 11):
        cv.hline(x, x + 3, 1, (255, 205, 212))
    return cv.save("HUD/hp_fill.png")


def make_cooldown():
    # Filled/Radial overlay: yari saydam koyu disk + ince cyan kenar.
    S = 25
    cv = Canvas(S, S)
    c = S / 2
    for y in range(S):
        for x in range(S):
            d = ((x + 0.5 - c) ** 2 + (y + 0.5 - c) ** 2) ** 0.5
            if d <= 10.6:
                cv.set(x, y, mix((12, 18, 34), (4, 6, 14), d / 10.6), 205)
            elif d <= 11.6:
                cv.set(x, y, CY_D, 230)
            elif d <= 12.4:
                cv.set(x, y, OUTL, 220)
    return cv.save("HUD/cooldown.png")


def make_skill_slot():
    S = 32
    cv = Canvas(S, S)
    bevel_panel(cv, 0, 0, S - 1, S - 1, 6, (22, 28, 50), (11, 14, 27), metal=2, grid=False)
    bracket(cv, 5, 5, 1, 1, 3, CY)
    bracket(cv, S - 6, S - 6, -1, -1, 3, CY)
    cv.set(S - 6, 5, CY_D)
    cv.set(5, S - 6, CY_D)
    return cv.save("HUD/skill_slot.png")


def make_skill_bar_frame():
    # SkillHUD.barFrame: kutularin arkasindaki panel. 9-SLICE (kenar 14px, SLICED_BORDERS):
    # kod kutularin toplam alanina gore boyutlandirir. Alt koseler duz (ekranin altina oturur).
    S = 48
    cv = Canvas(S, S)
    bevel_panel(cv, 0, 0, S - 1, S - 1, 9, (24, 30, 53), (13, 16, 30), corners=(1, 1, 0, 0),
                metal=2, grid=False)
    cv.hline(9, S - 10, 5, CY_D)            # ust kenarda cyan cizgi (9-slice'ta yatay uzar)
    bracket(cv, 6, 8, 1, 1, 4, CY)
    bracket(cv, S - 7, 8, -1, 1, 4, CY)
    rivet(cv, 4, S - 8)
    rivet(cv, S - 6, S - 8)
    return cv.save("HUD/skill_bar_frame.png")


def make_button_frame():
    # Genel buton (reroll vb.). 9-SLICE, kenar 8px: pahli koseler + ust parlak cizgi + alt cyan cizgi.
    W, H = 32, 20
    cv = Canvas(W, H)
    bevel_panel(cv, 0, 0, W - 1, H - 1, 5, (40, 50, 86), (22, 27, 48), metal=2, grid=False)
    cv.hline(8, W - 9, H - 5, CY_D)
    bracket(cv, 4, 4, 1, 1, 3, CY)
    bracket(cv, W - 5, H - 5, -1, -1, 3, CY)
    return cv.save("HUD/button_frame.png")


def make_core_icon():
    # Kalici para "Core": cyan/mor enerji kristali (pixel art, 24x24).
    rows = [
        "..........XX..........",
        ".........XhhX.........",
        "........XhhcmX........",
        ".......XhhccmmX.......",
        "......XhhcccmmmX......",
        ".....XhhcccccmmmX.....",
        "....XhhccccccmmmmX....",
        "...XhhcccwcccmmmmmX...",
        "..XhhccccwwccmmmmmdX..",
        ".XhhcccccwcccmmmmmddX.",
        "XhhccccccccccmmmmmdddX",
        "XggggggggggggggggggggX",
        "XppcccccccccmmmmmmdddX",
        ".XppccccccccmmmmmdddX.",
        "..XppcccccccmmmmddddX.",
        "...XppccccccmmmdddX...",
        "....XppcccccmmdddX....",
        ".....XppccccmmddX.....",
        "......XppcccmddX......",
        ".......XppccmdX.......",
        "........XppmdX........",
        ".........XpdX.........",
        "..........XX..........",
    ]
    cmap = {"X": OUTL, "h": (200, 255, 255), "c": CY, "m": (60, 150, 220), "d": (70, 60, 160),
            "w": (255, 255, 255), "p": (120, 220, 255),
            "g": (24, 110, 150)}
    cv = Canvas(24, 24)
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch in cmap:
                cv.set(i + 1, j, cmap[ch])
    return cv.save("HUD/icon_core.png")


SLICED_BORDERS = {"skill_bar_frame.png": 14, "button_frame.png": 8}


# ================================================================ MINIMAP
# Minimap sol altta 220x220 ekran px. Cerceve halkasi 7 sprite px (14 ekran px) ->
# MinimapUI.frameInset = 14; ic daire 192 ekran px = minimap_bg 96x96.
# Oyuncu/dusman/ok sprite'lari GRI tonlu cizilir: rengi MinimapUI'daki renkler (tint) verir.
def make_minimap_frame():
    S = 110
    cv = Canvas(S, S)
    c = S / 2
    for y in range(S):
        for x in range(S):
            dx, dy = x + 0.5 - c, y + 0.5 - c
            d = (dx * dx + dy * dy) ** 0.5
            lit = dx + dy < 0          # isik sol-ustten
            if 54 < d <= 55:
                cv.set(x, y, OUTL)
            elif 51.5 < d <= 54:
                cv.set(x, y, M2 if lit else M0)
            elif 50 < d <= 51.5:
                cv.set(x, y, M1)
            elif 49 < d <= 50:
                cv.set(x, y, OUTL)
            elif 48 < d <= 49:
                cv.set(x, y, CY if lit else CY_D)
    # ust kenarda parlama
    for x in range(40, 71):
        for y in range(0, 6):
            p = cv.get(x, y)
            if p[3] and p[:3] == M2:
                cv.set(x, y, M3)
                break
    # 8 yonde tik (N/E/S/W uzun, capraz kisa)
    import math
    for k in range(8):
        a = k * math.pi / 4
        r0, r1 = (50.5, 54) if k % 2 == 0 else (51.5, 53.5)
        col = M3 if k % 2 == 0 else M1
        steps = 8
        for i in range(steps + 1):
            r = r0 + (r1 - r0) * i / steps
            cv.set(int(c + math.sin(a) * r), int(c - math.cos(a) * r), col)
    # kuzey isareti: halkanin tepesinde cyan ucgen
    for j in range(4):
        cv.hline(int(c) - 3 + j, int(c) + 2 - j, 1 + j, CY if j else CY_D)
    # capraz perciner
    for (x, y) in ((17, 17), (91, 17), (17, 91), (91, 91)):
        rivet(cv, x, y)
    return cv.save("HUD/minimap_frame.png")


def make_minimap_bg():
    S = 96
    cv = Canvas(S, S)
    c = S / 2
    for y in range(S):
        for x in range(S):
            dx, dy = x + 0.5 - c, y + 0.5 - c
            d = (dx * dx + dy * dy) ** 0.5
            if d > 48:
                continue
            t = d / 48
            col = mix((20, 34, 60), (9, 13, 26), t ** 1.3)
            # menzil halkalari (1/3 ve 2/3), kesik kesik
            for rr in (16, 32):
                if abs(d - rr) < 0.55 and (int((math.atan2(dy, dx) + 3.2) * rr / 3) % 2 == 0):
                    col = mix(col, CY_D, 0.45)
            # arti isareti
            if (abs(dx) < 0.6 or abs(dy) < 0.6) and d > 3:
                col = mix(col, CY_D, 0.3)
            # nokta izgarasi
            if x % 8 == 4 and y % 8 == 4:
                col = shade(col, 0.1)
            cv.set(x, y, col)
    return cv.save("HUD/minimap_bg.png")


def make_minimap_sweep():
    # Donen radar taramasi: yukari bakan kenar parlak, saat yonunun tersine sonen dilim.
    S = 96
    cv = Canvas(S, S)
    c = S / 2
    span = math.radians(70)
    for y in range(S):
        for x in range(S):
            dx, dy = x + 0.5 - c, y + 0.5 - c
            d = (dx * dx + dy * dy) ** 0.5
            if d > 47.5 or d < 1:
                continue
            ang = math.atan2(dx, -dy)            # 0 = yukari, saat yonunde artar
            behind = (-ang) % (2 * math.pi)      # tarama cizgisinin ne kadar gerisinde
            if behind > span:
                continue
            k = 1 - behind / span
            a = int(120 * k ** 2.2)
            if behind < 0.03:
                a = 190
            cv.set(x, y, CY, a)
    return cv.save("HUD/minimap_sweep.png")


def make_minimap_player():
    # Yukari bakan ok (hareket yonune doner). Beyaz govde + koyu kenar -> tint ile renklenir.
    rows = [
        "....X....",
        "...XWX...",
        "..XWWWX..",
        "..XWLWX..",
        ".XWWLWWX.",
        ".XWLLLWX.",
        "XWLLXLLWX",
        "XLLX.XLLX",
        "XXX...XXX",
    ]
    cmap = {"X": (40, 40, 40), "W": (255, 255, 255), "L": (190, 190, 190)}
    cv = Canvas(9, 9)
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch in cmap:
                cv.set(i, j, cmap[ch])
    return cv.save("HUD/minimap_player.png")


def make_minimap_enemy():
    rows = [
        ".XXX.",
        "XWLLX",
        "XLLLX",
        "XLLDX",
        ".XXX.",
    ]
    cmap = {"X": (45, 45, 45), "W": (255, 255, 255), "L": (225, 225, 225), "D": (160, 160, 160)}
    cv = Canvas(5, 5)
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch in cmap:
                cv.set(i, j, cmap[ch])
    return cv.save("HUD/minimap_enemy.png")


def make_minimap_edge():
    # Menzil disindaki dusman icin yukari bakan kucuk ok (kod dusmana dogru cevirir).
    rows = [
        "...X...",
        "..XWX..",
        ".XWWWX.",
        "XWLLLWX",
        "XXXXXXX",
    ]
    cmap = {"X": (45, 45, 45), "W": (255, 255, 255), "L": (200, 200, 200)}
    cv = Canvas(7, 5)
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch in cmap:
                cv.set(i, j, cmap[ch])
    return cv.save("HUD/minimap_edge.png")


# ================================================================ IKONLAR
def scale2x(im):
    """EPX/Scale2x: pixel art'i 2x buyutur, diyagonalleri yumusatir, renk uydurmaz."""
    w, h = im.size
    src = im.load()
    out = Image.new("RGBA", (w * 2, h * 2))
    dst = out.load()

    def P(x, y):
        x = min(max(x, 0), w - 1)
        y = min(max(y, 0), h - 1)
        return src[x, y]

    for y in range(h):
        for x in range(w):
            p = P(x, y)
            a, b, c, d = P(x, y - 1), P(x + 1, y), P(x - 1, y), P(x, y + 1)
            e0 = e1 = e2 = e3 = p
            if c == a and c != d and a != b:
                e0 = a
            if a == b and a != c and b != d:
                e1 = b
            if d == c and d != b and c != a:
                e2 = c
            if b == d and b != a and d != c:
                e3 = d
            dst[2 * x, 2 * y] = e0
            dst[2 * x + 1, 2 * y] = e1
            dst[2 * x, 2 * y + 1] = e2
            dst[2 * x + 1, 2 * y + 1] = e3
    return out


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def enhance_icon(src_path):
    base = Image.open(src_path).convert("RGBA")
    # 32px -> ortada 28px'lik alana sigsin diye 1px kenar birak (golge + parilti icin yer)
    big = scale2x(base)                       # 64x64
    w, h = big.size
    px = big.load()

    def opaque(x, y):
        return 0 <= x < w and 0 <= y < h and px[x, y][3] > 128

    # dis cizgi rengi = ikondaki en koyu opak renk
    dark = min((px[x, y][:3] for y in range(h) for x in range(w) if px[x, y][3] > 128), key=lum)

    def is_outline(c):
        return lum(c[:3]) < lum(dark) + 18

    lit = big.copy()
    lp = lit.load()
    for y in range(h):
        for x in range(w):
            c = px[x, y]
            if c[3] <= 128 or is_outline(c):
                continue
            # sol-ust tarafi disari / cizgiye bakan pikseller: kenar isigi
            up_l = [(x - 1, y), (x, y - 1), (x - 2, y), (x, y - 2)]
            dn_r = [(x + 1, y), (x, y + 1), (x + 2, y), (x, y + 2)]
            edge_ul = any(not opaque(a, b) or is_outline(px[a, b]) for a, b in up_l[:2])
            edge_dr = any(not opaque(a, b) or is_outline(px[a, b]) for a, b in dn_r[:2])
            rgb = c[:3]
            if edge_ul and not edge_dr:
                rgb = shade(rgb, 0.28)
            elif edge_dr and not edge_ul:
                rgb = shade(rgb, -0.22)
            lp[x, y] = (rgb[0], rgb[1], rgb[2], c[3])

    # baskin renk (parilti icin): en doygun renklerin ortalamasi
    cols = [px[x, y][:3] for y in range(h) for x in range(w)
            if px[x, y][3] > 128 and not is_outline(px[x, y])]
    cols.sort(key=lambda c: max(c) - min(c), reverse=True)
    top = cols[: max(1, len(cols) // 3)]
    glow_c = shade(tuple(sum(c[i] for c in top) // len(top) for i in range(3)), 0.25)
    if lum(glow_c) < 120:
        # Gri/koyu ikonlar (silahlar) koyu zeminde kaybolmasin: parlamayi acik cyan'a cek
        glow_c = mix(glow_c, (120, 205, 240), 0.65)

    alpha = lit.getchannel("A")
    glow = Image.new("RGBA", (w, h), glow_c + (0,))
    glow.putalpha(alpha.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(2.5))
                  .point(lambda a: int(a * 0.38)))
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sh_a = Image.new("L", (w, h), 0)
    sh_a.paste(alpha.point(lambda a: 110 if a > 128 else 0), (1, 2))
    shadow.putalpha(sh_a)

    out = Image.alpha_composite(glow, shadow)
    out = Image.alpha_composite(out, lit)
    return out


ICON_NAMES = [
    "skill_area_blast", "skill_chain_lightning", "skill_dash", "skill_fire_burst",
    "skill_frost_nova", "skill_heal", "skill_overdrive", "skill_pulse_wave", "skill_shield",
    "stat_crit_chance", "stat_damage", "stat_magnet_range", "stat_max_health",
    "stat_move_speed", "ui_skill_swap", "weapon_pistol", "weapon_sword",
]
# eski commit'lerden geri alinanlar (src_icons/): Attack Speed kendi ikonunu alsin
EXTRA_NAMES = ["stat_fire_rate", "weapon_sword2", "weapon_smg", "weapon_shotgun", "weapon_revolver",
               "weapon_rifle", "weapon_sniper", "weapon_minigun", "weapon_laser_sword", "weapon_katana",
               "weapon_bat", "meta_wisdom", "meta_funds", "meta_second_chance", "meta_dice"]

# Sword II: Sword ikonunun renk varyanti (ayni sprite'i kullandiklari icin HUD'da ayirt edilsin).
SWORD2_RECOLOR = {
    (244, 244, 244): (255, 236, 180),   # bicak parlak
    (148, 176, 194): (255, 168, 72),    # bicak orta -> kizgin turuncu
    (86, 108, 134): (186, 84, 40),      # bicak golge
    (255, 205, 117): (214, 62, 88),     # siper altin -> kirmizi
    (93, 39, 93): (60, 68, 110),        # kabza
}


def make_sword2_source():
    im = Image.open(os.path.join(SRC_ICONS, "weapon_sword.png")).convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            c = px[x, y]
            if c[3] and c[:3] in SWORD2_RECOLOR:
                px[x, y] = SWORD2_RECOLOR[c[:3]] + (c[3],)
    os.makedirs(EXTRA_ICONS, exist_ok=True)
    im.save(os.path.join(EXTRA_ICONS, "weapon_sword2.png"))


# Paketteki silah sprite'larindan (Assets/Art/1.png dilimleri) HUD/kart ikonlari.
SHEET = os.path.join(ROOT, "Assets/Art/1.png")
WEAPON_SLICES = {
    "weapon_smg": "1_60", "weapon_shotgun": "1_57", "weapon_revolver": "1_68",
    "weapon_rifle": "1_55", "weapon_sniper": "1_49", "weapon_minigun": "1_52",
    "weapon_laser_sword": "1_58", "weapon_katana": "1_26", "weapon_bat": "1_41",
}


def make_weapon_sources():
    """Dilimi kare tuvale ortalar (2px bosluk) -> src_icons/. Boyut silaha gore degisir; HUD sigdirir.
    Uzun silahlar 38 derece capraz cevrilir (mevcut Sword/Pistol ikonlariyla ayni duzen)."""
    import re
    meta = open(SHEET + ".meta").read()
    rects = {n: tuple(map(int, r)) for n, *r in re.findall(
        r"name: (\S+)\n\s+rect:\n\s+serializedVersion: 2\n\s+x: (\d+)\n\s+y: (\d+)\n\s+width: (\d+)\n\s+height: (\d+)", meta)}
    sheet = Image.open(SHEET).convert("RGBA")
    os.makedirs(EXTRA_ICONS, exist_ok=True)
    for name, sl in WEAPON_SLICES.items():
        x, y, w, h = rects[sl]
        crop = sheet.crop((x, sheet.height - y - h, x + w, sheet.height - y))   # Unity y'si alttan
        if w > h * 1.4:
            # Uzun silahlar yatayken kare ikonda cok kucuk kalir: Sword/Pistol ikonlari gibi
            # capraz cevir. 8x buyut -> dondur -> kucult (RotSprite benzeri, pikseller dagilmaz).
            k = 8
            big = crop.resize((w * k, h * k), Image.NEAREST).rotate(38, Image.NEAREST, expand=True)
            crop = big.resize((big.width // k, big.height // k), Image.NEAREST)
            crop = crop.crop(crop.getbbox())
            w, h = crop.size
        # Paket renkleri oyun dunyasi icin koyu: ikonda govdeyi ac (dis cizgi koyu kalsin)
        cp = crop.load()
        for yy in range(h):
            for xx in range(w):
                c = cp[xx, yy]
                if c[3] and lum(c[:3]) > 45:
                    cp[xx, yy] = shade(c[:3], 0.22) + (c[3],)
        n = max(w, h) + 4
        tile = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        tile.paste(crop, ((n - w) // 2, (n - h) // 2))
        tile.save(os.path.join(EXTRA_ICONS, name + ".png"))


def make_meta_sources():
    """Magaza (meta) ikonlarinin 32px kaynaklari -> src_icons/."""
    os.makedirs(EXTRA_ICONS, exist_ok=True)
    gen = os.path.join(ROOT, "Assets/Art/Generated")

    # Wisdom: level ikonu (yildiz + ok), Starting Funds: coin
    Image.open(os.path.join(gen, "Icons/GameOver/ui_level.png")).convert("RGBA").save(
        os.path.join(EXTRA_ICONS, "meta_wisdom.png"))
    Image.open(os.path.join(gen, "Pickups/pickup_coin.png")).convert("RGBA").save(
        os.path.join(EXTRA_ICONS, "meta_funds.png"))

    # Second Chance: can kalbinin altin hali
    heart = Image.open(os.path.join(SRC_ICONS, "stat_max_health.png")).convert("RGBA")
    hp = heart.load()
    for y in range(heart.height):
        for x in range(heart.width):
            c = hp[x, y]
            if c[3] and lum(c[:3]) > 60:
                l = lum(c[:3]) / 255
                gold = mix(GOLD_D, GOLD_L, min(1, l * 1.2)) if l < 0.9 else (255, 255, 235)
                hp[x, y] = gold + (c[3],)
    heart.save(os.path.join(EXTRA_ICONS, "meta_second_chance.png"))

    # Lucky Dice: iki yuzu gorunen beyaz zar (5 noktali on yuz)
    rows = [
        "......XXXXXXXXXXXXXXXX......",
        ".....XssssssssssssssssX.....",
        "....XssssssssssssssssddX....",
        "...XXXXXXXXXXXXXXXXXXddX....",
        "...XwwwwwwwwwwwwwwwwXddX....",
        "...XwppwwwwwwwwwwppwXddX....",
        "...XwppwwwwwwwwwwppwXddX....",
        "...XwwwwwwwwwwwwwwwwXddX....",
        "...XwwwwwwwwwwwwwwwwXddX....",
        "...XwwwwwwwppwwwwwwwXddX....",
        "...XwwwwwwwppwwwwwwwXddX....",
        "...XwwwwwwwwwwwwwwwwXddX....",
        "...XwwwwwwwwwwwwwwwwXddX....",
        "...XwppwwwwwwwwwwppwXdX.....",
        "...XwppwwwwwwwwwwppwXdX.....",
        "...XggggggggggggggggXX......",
        "...XXXXXXXXXXXXXXXXXX.......",
    ]
    cmap = {"X": (26, 28, 44), "w": (244, 244, 244), "s": (205, 214, 226), "d": (148, 160, 186),
            "g": (190, 198, 214), "p": (229, 83, 60)}
    dice = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    dp = dice.load()
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch in cmap:
                dp[i + 2, j + 7] = cmap[ch] + (255,)
    # Bos kenarlari kirp, kare tuvale ortala: diger ikonlar kadar alan kaplasin
    dice = dice.crop(dice.getbbox())
    n = max(dice.size) + 2
    tile = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    tile.paste(dice, ((n - dice.width) // 2, (n - dice.height) // 2))
    tile.save(os.path.join(EXTRA_ICONS, "meta_dice.png"))


def make_icons():
    make_sword2_source()
    make_weapon_sources()
    make_meta_sources()
    paths = []
    for n in ICON_NAMES:
        im = enhance_icon(os.path.join(SRC_ICONS, n + ".png"))
        path = os.path.join(OUT, "Icons", n + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        im.save(path)
        paths.append(path)
    for n in EXTRA_NAMES:
        im = enhance_icon(os.path.join(EXTRA_ICONS, n + ".png"))
        path = os.path.join(OUT, "Icons", n + ".png")
        im.save(path)
        paths.append(path)
    return paths


# ================================================================ .meta
META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: {filter}
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(path, filter_mode):
    """Yeni .meta yazar; varsa guid'i korur (referanslar kopmasin).
    SLICED_BORDERS'taki dosyalara 9-slice kenari yazilir."""
    meta = path + ".meta"
    guid = uuid.uuid4().hex
    if os.path.exists(meta):
        for line in open(meta):
            if line.startswith("guid: "):
                guid = line.split()[1]
                break
    with open(meta, "w") as f:
        text = META.format(guid=guid, filter=filter_mode)
        b = SLICED_BORDERS.get(os.path.basename(path))
        if b:
            text = text.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}",
                                f"spriteBorder: {{x: {b}, y: {b}, z: {b}, w: {b}}}")
        f.write(text)


FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_folder_meta(folder):
    meta = folder.rstrip("/") + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w") as f:
            f.write(FOLDER_META.format(guid=uuid.uuid4().hex))


def main():
    pixel = [make_card_frame(), make_coin_small(), make_wave_panel(), make_level_badge(),
             make_money_plate(), make_hp_frame(), make_hp_fill(), make_cooldown(),
             make_skill_slot(), make_skill_bar_frame(), make_button_frame(),
             make_core_icon()]
    pixel += [make_minimap_frame(), make_minimap_bg(), make_minimap_player(),
              make_minimap_enemy(), make_minimap_edge()]
    smooth = [make_minimap_sweep()]
    icons = make_icons()

    write_folder_meta(OUT)
    for sub in ("Cards", "HUD", "Icons"):
        write_folder_meta(os.path.join(OUT, sub))
    for p in pixel:
        write_meta(p, 0)      # Point: keskin pixel art
    for p in icons + smooth:
        write_meta(p, 1)      # Bilinear: 64px ikon ~48px'e kuculuyor, Point'te pikseller dusuyor
    print(f"{len(pixel) + len(smooth)} HUD/kart + {len(icons)} ikon -> {OUT}")


if __name__ == "__main__":
    main()
