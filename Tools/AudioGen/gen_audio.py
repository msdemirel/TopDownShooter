#!/usr/bin/env python3
"""Oyunun tüm ses efektleri + müzikleri (retro/chiptune, prosedürel) -> Assets/Audio/Generated_v2/

Eski sesler (Assets/Audio/SFX, Music) DOKUNULMAZ. Tekrar çalıştırmak güvenli: aynı adlara yazar,
.meta varsa korunur (guid değişmez). Rastgelelik sabit tohumla: her çalıştırma aynı sesi üretir.

    SFX/<ad>.wav     44.1kHz mono 16-bit (adlar SfxId enum'unun snake_case hali: EnemyDeath -> enemy_death)
    Music/menu.wav   ~43 sn döngü  (90 BPM, sakin)
    Music/game.wav   ~51 sn döngü  (150 BPM, hareketli: pompalayan bas + sidechain + sürekli melodi)
    Music/boss.wav   ~24 sn döngü  (160 BPM, gergin)

Çalıştır:  python3 Tools/AudioGen/gen_audio.py      (sonra Unity'de Ctrl+R)
"""
import math
import os
import random
import struct
import uuid
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets/Audio/Generated_v2")
SR = 44100
TAU = 2 * math.pi

# ================================================================ temel yapı taşları
def note(n):
    """'A4', 'C#5' -> Hz. Sayı verilirse MIDI numarası."""
    if isinstance(n, (int, float)):
        return 440.0 * 2 ** ((n - 69) / 12)
    names = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
    semi = names[n[0]]
    i = 1
    if n[i] == "#": semi += 1; i += 1
    elif n[i] == "b": semi -= 1; i += 1
    octave = int(n[i:])
    return 440.0 * 2 ** ((semi + (octave + 1) * 12 - 69) / 12)


def midi(n):
    return 69 + 12 * math.log2(note(n) / 440.0)


def osc(kind, phase):
    p = phase % 1.0
    if kind == "sine": return math.sin(TAU * p)
    if kind == "square": return 1.0 if p < 0.5 else -1.0
    if kind == "pulse25": return 1.0 if p < 0.25 else -1.0
    if kind == "pulse12": return 1.0 if p < 0.125 else -1.0
    if kind == "saw": return 2.0 * p - 1.0
    if kind == "tri": return 4.0 * abs(p - 0.5) - 1.0
    raise ValueError(kind)


def tone(dur, f0, f1=None, kind="square", attack=0.005, decay=None, vol=1.0,
         vib=0.0, vib_rate=6.0, curve=1.0, release=0.02):
    """Pitch kaydırmalı (f0 -> f1) zarflı ton. decay verilirse üstel söner."""
    n = int(dur * SR)
    out = [0.0] * n
    f1 = f0 if f1 is None else f1
    phase = 0.0
    for i in range(n):
        t = i / SR
        k = (i / n) ** curve
        f = f0 + (f1 - f0) * k
        if vib: f *= 1 + vib * math.sin(TAU * vib_rate * t)
        phase += f / SR
        env = min(1.0, t / attack) if attack > 0 else 1.0
        if decay: env *= math.exp(-t / decay)
        rel = (dur - t)
        if rel < release: env *= max(0.0, rel / release)
        out[i] = osc(kind, phase) * env * vol
    return out


_rng = random.Random(1234)


def noise(dur, attack=0.002, decay=None, vol=1.0, lp=None, lp_end=None, hp=None, release=0.02, crush=0):
    """Beyaz gürültü; lp: tek kutuplu alçak geçiren (Hz, lp_end'e kayabilir), hp: yüksek geçiren."""
    n = int(dur * SR)
    out = [0.0] * n
    y = 0.0
    hy = 0.0
    hx = 0.0
    held = 0.0
    for i in range(n):
        t = i / SR
        x = _rng.uniform(-1, 1)
        if crush and i % crush: x = held
        else: held = x
        if lp:
            cut = lp if lp_end is None else lp + (lp_end - lp) * (i / n)
            a = 1 - math.exp(-TAU * cut / SR)
            y += a * (x - y)
            x = y
        if hp:
            a = math.exp(-TAU * hp / SR)
            hy = a * (hy + x - hx)
            hx = x
            x = hy
        env = min(1.0, t / attack) if attack > 0 else 1.0
        if decay: env *= math.exp(-t / decay)
        rel = dur - t
        if rel < release: env *= max(0.0, rel / release)
        out[i] = x * env * vol
    return out


def mix(*layers, at=None):
    """Katmanları topla. at: her katmanın başlangıç zamanı (sn) listesi."""
    at = at or [0.0] * len(layers)
    length = max(int(a * SR) + len(l) for l, a in zip(layers, at))
    out = [0.0] * length
    for l, a in zip(layers, at):
        o = int(a * SR)
        for i, v in enumerate(l):
            out[o + i] += v
    return out


def seq(parts):
    """[(katman, başlangıç_sn), ...] -> tek katman."""
    return mix(*[p for p, _ in parts], at=[a for _, a in parts])


def gain(x, g):
    return [v * g for v in x]


def lowpass(x, cut):
    a = 1 - math.exp(-TAU * cut / SR)
    y = 0.0
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        y += a * (v - y)
        out[i] = y
    return out


def bitcrush(x, step=4, levels=32):
    out = [0.0] * len(x)
    held = 0.0
    for i, v in enumerate(x):
        if i % step == 0: held = round(v * levels) / levels
        out[i] = held
    return out


def echo(x, delay=0.12, fb=0.35, taps=3):
    d = int(delay * SR)
    out = list(x) + [0.0] * d * taps
    for k in range(1, taps + 1):
        g = fb ** k
        for i, v in enumerate(x):
            out[i + d * k] += v * g
    return out


def finish(x, peak=0.9, fade=0.004):
    """Yumuşak kırpma + normalize + uçlarda tık önleyici fade."""
    x = [math.tanh(v * 1.2) for v in x]
    m = max(1e-6, max(abs(v) for v in x))
    x = [v * peak / m for v in x]
    f = int(fade * SR)
    for i in range(min(f, len(x))):
        x[i] *= i / f
        x[-1 - i] *= i / f
    return x


def write_wav(path, x):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in x))
    write_meta(path)


META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: {load}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: {quality}
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: {bg}
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(path):
    meta = path + ".meta"
    guid = uuid.uuid4().hex
    if os.path.exists(meta):
        for line in open(meta):
            if line.startswith("guid: "):
                guid = line.split()[1]
    music = os.sep + "Music" + os.sep in path
    # SFX: DecompressOnLoad (anında çalar). Müzik: Streaming (bellekte büyük durmasın)
    with open(meta, "w") as f:
        f.write(META.format(guid=guid, load=2 if music else 0, quality=0.7 if music else 1,
                            bg=1 if music else 0))


# ================================================================ SES EFEKTLERİ
def sfx():
    s = {}
    # ---- silah / vuruş
    s["shoot"] = seq([(tone(0.09, 1100, 320, "pulse25", decay=0.04, vol=0.7), 0),
                      (noise(0.03, decay=0.01, vol=0.5, hp=2000), 0)])
    s["melee_swing"] = noise(0.16, attack=0.02, vol=0.8, lp=600, lp_end=4500, hp=300, release=0.06)
    s["hit"] = seq([(noise(0.06, decay=0.02, vol=0.8, lp=3000), 0),
                    (tone(0.07, 180, 90, "sine", decay=0.03, vol=0.8), 0)])
    s["crit"] = seq([(noise(0.05, decay=0.015, vol=0.7, lp=5000), 0),
                     (tone(0.18, 1800, 1750, "sine", decay=0.05, vol=0.45), 0),
                     (tone(0.18, 2700, 2650, "sine", decay=0.04, vol=0.3), 0),
                     (tone(0.08, 220, 100, "square", decay=0.03, vol=0.35), 0)])
    s["enemy_death"] = bitcrush(seq([(tone(0.22, 520, 110, "square", decay=0.09, vol=0.55), 0),
                                     (noise(0.18, decay=0.06, vol=0.45, lp=2500), 0)]), step=3)
    s["explosion"] = seq([(noise(0.7, decay=0.2, vol=1.0, lp=3000, lp_end=200), 0),
                          (tone(0.5, 90, 38, "sine", decay=0.18, vol=1.0), 0),
                          (noise(0.12, decay=0.03, vol=0.6, hp=1500), 0)])
    s["hurt"] = seq([(tone(0.22, 320, 150, "square", decay=0.1, vol=0.6, vib=0.08, vib_rate=30), 0),
                     (noise(0.1, decay=0.04, vol=0.5, lp=2000), 0)])
    s["player_death"] = seq([(tone(1.1, 420, 55, "square", decay=0.45, vol=0.6, vib=0.05, vib_rate=8, curve=0.6), 0),
                             (noise(0.9, decay=0.35, vol=0.5, lp=1500, lp_end=150), 0.05)])

    # ---- toplama / ilerleme
    s["pickup_coin"] = seq([(tone(0.06, note("B5"), kind="square", vol=0.45, release=0.01), 0),
                            (tone(0.14, note("E6"), kind="square", decay=0.06, vol=0.45), 0.055)])
    s["pickup_exp"] = tone(0.07, note("E6"), note("A6"), "sine", decay=0.03, vol=0.6)
    s["pickup_health"] = seq([(tone(0.1, note("C5"), note("G5"), "tri", vol=0.6), 0),
                              (tone(0.16, note("C6"), kind="tri", decay=0.07, vol=0.5), 0.08)])
    lv = ["C5", "E5", "G5", "C6", "E6"]
    s["level_up"] = echo(seq([(tone(0.09, note(n), kind="pulse25", decay=0.08, vol=0.5), i * 0.065)
                              for i, n in enumerate(lv)]), 0.09, 0.3, 2)

    # ---- skill'ler
    s["dash"] = seq([(noise(0.2, attack=0.01, vol=0.9, lp=800, lp_end=6000, hp=400, release=0.08), 0),
                     (tone(0.16, 300, 900, "tri", decay=0.08, vol=0.25), 0)])
    s["blast"] = seq([(noise(0.9, decay=0.25, vol=1.0, lp=4000, lp_end=150), 0),
                      (tone(0.6, 110, 32, "sine", decay=0.22, vol=1.0), 0),
                      (tone(0.25, 300, 60, "saw", decay=0.08, vol=0.35), 0)])
    s["shield"] = echo(seq([(tone(0.45, note("C5"), note("C6"), "tri", attack=0.03, decay=0.3, vol=0.5, vib=0.01), 0),
                            (tone(0.45, note("G5"), note("G6"), "sine", attack=0.03, decay=0.3, vol=0.35), 0.03)]),
                       0.08, 0.3, 2)
    s["shield_block"] = seq([(tone(0.15, 2200, 2100, "sine", decay=0.04, vol=0.5), 0),
                             (tone(0.12, 1470, kind="tri", decay=0.04, vol=0.35), 0)])
    s["pulse"] = tone(0.4, 420, 90, "sine", decay=0.18, vol=0.9, vib=0.06, vib_rate=18)
    s["burst"] = seq([(noise(0.4, attack=0.01, decay=0.15, vol=0.9, lp=1200, lp_end=5000), 0),
                      (noise(0.3, decay=0.1, vol=0.35, crush=40, hp=1000), 0.03),
                      (tone(0.25, 180, 70, "saw", decay=0.1, vol=0.3), 0)])
    s["heal"] = echo(seq([(tone(0.3, note(n), kind="tri", decay=0.15, vol=0.45), i * 0.07)
                          for i, n in enumerate(["C6", "E6", "G6", "C7"])]), 0.1, 0.35, 2)
    frost = [(tone(0.45, _rng.uniform(2500, 5200), kind="sine", attack=0.01, decay=0.12, vol=0.18), _rng.uniform(0, 0.25))
             for _ in range(14)]
    s["frost"] = seq(frost + [(noise(0.55, attack=0.02, decay=0.2, vol=0.5, hp=3000), 0)])
    s["overdrive"] = seq([(tone(0.55, 120, 620, "saw", attack=0.02, vol=0.5, curve=0.7, release=0.1), 0),
                          (tone(0.55, 121, 627, "saw", attack=0.02, vol=0.4, curve=0.7, release=0.1), 0),
                          (tone(0.2, note("A5"), kind="square", decay=0.1, vol=0.3), 0.45)])
    zap = []
    t = 0.0
    while t < 0.28:
        zap.append((tone(0.03, _rng.uniform(400, 2400), kind="square", vol=0.45, release=0.005), t))
        t += 0.02
    s["lightning"] = seq(zap + [(noise(0.3, decay=0.1, vol=0.6, hp=2500), 0)])

    # ---- oyun olayları
    s["merge"] = echo(seq([(tone(0.12, note(n), kind="pulse25", decay=0.1, vol=0.45), i * 0.05)
                           for i, n in enumerate(["G5", "B5", "D6", "G6", "B6"])] +
                          [(tone(0.5, note("G6"), kind="sine", decay=0.25, vol=0.4), 0.25)]), 0.07, 0.3, 2)
    s["synergy"] = echo(seq([(tone(0.8, note(n), kind="tri", attack=0.02, decay=0.4, vol=0.35, vib=0.006), i * 0.03)
                             for i, n in enumerate(["C5", "E5", "G5", "B5", "D6"])]), 0.13, 0.35, 3)
    s["purchase"] = seq([(noise(0.04, decay=0.01, vol=0.5, hp=3000), 0),
                         (tone(0.07, note("E6"), kind="square", vol=0.4, release=0.01), 0.02),
                         (tone(0.2, note("B6"), kind="square", decay=0.08, vol=0.4), 0.08)])
    s["reroll"] = seq([(noise(0.035, decay=0.01, vol=0.7, lp=4000, hp=500), i * 0.045) for i in range(5)] +
                      [(tone(0.08, note("A5"), note("E6"), "tri", vol=0.35), 0.22)])
    s["unlock"] = echo(seq([(tone(0.13, note(n), kind="square", decay=0.12, vol=0.38), a)
                            for n, a in [("C5", 0), ("E5", 0.1), ("G5", 0.2)]] +
                           [(tone(0.55, note("C6"), kind="pulse25", decay=0.3, vol=0.4, vib=0.01), 0.3),
                            (tone(0.55, note("E6"), kind="tri", decay=0.3, vol=0.3), 0.3)]), 0.1, 0.3, 2)
    s["revive"] = echo(seq([(tone(1.0, note(n), note(n) * 2, "tri", attack=0.1, decay=0.6, vol=0.3, curve=0.5), i * 0.05)
                            for i, n in enumerate(["C4", "G4", "C5", "E5"])]), 0.15, 0.35, 3)
    s["wave_start"] = seq([(tone(0.45, note(n), kind="square", attack=0.04, decay=0.3, vol=0.3), 0)
                           for n in ["A4", "C#5", "E5"]] +
                          [(noise(0.3, attack=0.05, decay=0.1, vol=0.2, lp=1500), 0)])
    s["countdown_tick"] = tone(0.06, note("A5"), kind="pulse25", decay=0.025, vol=0.5)
    s["boss_warning"] = seq([(tone(0.22, 620, 820, "saw", vol=0.45, release=0.03), i * 0.25) for i in range(4)] +
                            [(tone(1.0, 55, 50, "square", attack=0.05, decay=0.5, vol=0.5), 0)])
    s["game_over"] = echo(seq([(tone(0.35, note(n), kind="square", decay=0.25, vol=0.4), i * 0.22)
                               for i, n in enumerate(["A4", "F4", "D4", "A3"])] +
                              [(tone(0.9, note("A2"), kind="tri", decay=0.5, vol=0.5), 0.88)]), 0.14, 0.3, 2)
    s["new_record"] = echo(seq([(tone(0.12, note(n), kind="pulse25", decay=0.1, vol=0.4), a)
                                for n, a in [("G5", 0), ("G5", 0.12), ("G5", 0.24), ("E6", 0.36)]] +
                               [(tone(0.6, note("C6"), kind="square", decay=0.35, vol=0.35), 0.52),
                                (tone(0.6, note("E6"), kind="tri", decay=0.35, vol=0.3), 0.52)]), 0.1, 0.3, 2)

    # ---- arayüz
    s["ui_hover"] = tone(0.03, note("E6"), kind="tri", decay=0.012, vol=0.35)
    s["ui_click"] = seq([(tone(0.05, note("A5"), note("E6"), "pulse25", decay=0.02, vol=0.45), 0),
                         (noise(0.015, vol=0.3, hp=4000), 0)])
    s["ui_back"] = tone(0.07, note("E5"), note("A4"), "pulse25", decay=0.03, vol=0.45)
    s["ui_error"] = tone(0.16, 140, 120, "square", decay=0.08, vol=0.45, vib=0.1, vib_rate=40)
    s["ui_start"] = seq([(noise(0.4, attack=0.02, vol=0.5, lp=500, lp_end=6000, hp=300, release=0.15), 0)] +
                        [(tone(0.35, note(n), kind="pulse25", attack=0.01, decay=0.2, vol=0.3), 0.18)
                         for n in ["C5", "G5", "C6"]])
    return s


# ================================================================ MÜZİK
def drum_kick(vol=1.0):
    return mix(tone(0.22, 150, 42, "sine", decay=0.07, vol=vol, curve=0.4),
               noise(0.01, vol=0.3 * vol, lp=3000))


def drum_snare(vol=1.0):
    return mix(noise(0.18, decay=0.05, vol=0.8 * vol, lp=6000, hp=700),
               tone(0.1, 220, 160, "tri", decay=0.04, vol=0.4 * vol))


def drum_hat(vol=1.0, open_=False):
    return noise(0.12 if open_ else 0.04, decay=0.04 if open_ else 0.01, vol=0.35 * vol, hp=7000)


class Track:
    """Basit sıralayıcı: vuruş bazlı nota yerleştirme + dikişsiz döngü (kuyruklar başa sarılır)."""

    def __init__(self, bpm, bars, beats_per_bar=4):
        self.beat = 60.0 / bpm
        self.length = int(self.beat * beats_per_bar * bars * SR)
        self.buf = [0.0] * self.length

    def add(self, layer, beat_pos, vol=1.0):
        o = int(beat_pos * self.beat * SR)
        n = self.length
        for i, v in enumerate(layer):
            self.buf[(o + i) % n] += v * vol   # kuyruk döngünün başına sarılır: dikiş duyulmaz

    def dur(self, beats):
        return beats * self.beat


# Akor tanımları (kök + aralıklar yarım ton)
CHORDS = {"Am": ("A", [0, 3, 7]), "F": ("F", [0, 4, 7]), "C": ("C", [0, 4, 7]), "G": ("G", [0, 4, 7]),
          "Em": ("E", [0, 3, 7]), "Dm": ("D", [0, 3, 7]), "Bb": ("Bb", [0, 4, 7]), "A": ("A", [0, 4, 7]),
          "E": ("E", [0, 4, 7])}


def chord_notes(name, octave):
    root, iv = CHORDS[name]
    base = midi(root + str(octave))
    return [base + i for i in iv]


def music_menu():
    t = Track(bpm=90, bars=16)
    prog = ["Am", "F", "C", "G"] * 4
    melody = [None, "E5", None, "C5", "D5", None, "E5", None, "C5", None, "A4", None, "B4", None, "G4", None]
    for bar, ch in enumerate(prog):
        b0 = bar * 4
        # yumuşak pad (üçgen + hafif vibrato)
        for m in chord_notes(ch, 4):
            t.add(tone(t.dur(4), note(m), kind="tri", attack=0.4, vol=0.10, vib=0.004, vib_rate=4, release=0.5), b0)
        # bas
        t.add(tone(t.dur(2), note(chord_notes(ch, 2)[0]), kind="tri", attack=0.02, decay=0.9, vol=0.28), b0)
        t.add(tone(t.dur(2), note(chord_notes(ch, 2)[0]), kind="tri", attack=0.02, decay=0.9, vol=0.22), b0 + 2)
        # arpej (sekizlik, yankılı)
        arp = chord_notes(ch, 5)
        for k in range(8):
            m = arp[[0, 1, 2, 1][k % 4]] + (12 if k == 6 else 0)
            t.add(tone(t.dur(0.45), note(m), kind="pulse25", decay=0.12, vol=0.07), b0 + k * 0.5)
        # hafif perküsyon (2. yarıda)
        if bar >= 8:
            t.add(drum_kick(0.5), b0)
            t.add(drum_hat(0.5), b0 + 1.5)
            t.add(drum_kick(0.4), b0 + 2.5)
            t.add(drum_hat(0.5), b0 + 3.5)
        # melodi (son 8 bar)
        if bar >= 8:
            n = melody[(bar - 8) * 2 % 16]
            if n: t.add(tone(t.dur(1.8), note(n), kind="square", attack=0.02, decay=0.6, vol=0.07, vib=0.005), b0)
            n = melody[((bar - 8) * 2 + 1) % 16]
            if n: t.add(tone(t.dur(1.8), note(n), kind="square", attack=0.02, decay=0.6, vol=0.07, vib=0.005), b0 + 2)
    return lowpass(t.buf, 7000)


def sidechain(buf, beat_sec, depth=0.6, release=0.22):
    """Her vuruşta (kick) sesi kısıp geri açar: dans müziği 'pompalama' hissi."""
    period = int(beat_sec * SR)
    out = [0.0] * len(buf)
    for i, v in enumerate(buf):
        t = (i % period) / SR
        out[i] = v * (1 - depth * math.exp(-t / (release * beat_sec)))
    return out


def music_game():
    """Oyun içi: 150 BPM, 32 bar (~51 sn), dikişsiz döngü.
    Bölümler (8'er bar): A giriş-enerji / B melodi / C yükselen (breakdown'suz) / D melodi + tiz oktav.
    Davul ve synth ayrı 'bus'larda: synth'lere kick'e bağlı sidechain uygulanır."""
    bpm, bars = 150, 32
    drums = Track(bpm, bars)
    synth = Track(bpm, bars)
    prog = ["Am", "F", "C", "G"]
    # 2 barlık melodi motifleri (sekizlik): None = sus
    lead_b = ["A5", None, "C6", "A5", "E6", None, "D6", "C6",   "B5", None, "G5", "B5", "D6", "C6", "B5", "G5"]
    lead_d = ["E6", "E6", "D6", "C6", "D6", None, "E6", "G6",  "A6", None, "G6", "E6", "D6", "C6", "D6", "E6"]
    for bar in range(bars):
        ch = prog[bar % 4]
        b0 = bar * 4
        section = bar // 8
        last_of_phrase = bar % 8 == 7

        # ---- davul
        for beat in range(4):
            drums.add(drum_kick(1.0), b0 + beat)
            if beat in (1, 3): drums.add(drum_snare(0.85), b0 + beat)
            for s16 in range(4):
                accent = 0.75 if s16 == 2 else 0.4
                drums.add(drum_hat(accent), b0 + beat + s16 * 0.25)
            drums.add(drum_hat(0.55, open_=True), b0 + beat + 0.5) if section >= 1 else None
        if last_of_phrase:
            # snare dolgusu: son vuruşta onaltılık rulo
            for k in range(4):
                drums.add(drum_snare(0.45 + 0.15 * k), b0 + 3 + k * 0.25)

        # ---- bas: pompalayan (vuruş aralarında kök, onaltılıklarda oktav)
        root = chord_notes(ch, 2)[0]
        for k in range(16):
            if k % 4 == 0: continue                      # kick'in üstü boş: bas nefes alsın
            m = root + (12 if k % 4 == 2 else 0)
            synth.add(tone(synth.dur(0.22), note(m), kind="saw", decay=0.07, vol=0.2), b0 + k * 0.25)
            synth.add(tone(synth.dur(0.22), note(m), kind="pulse25", decay=0.07, vol=0.12), b0 + k * 0.25)

        # ---- arpej: onaltılık, bölüm ilerledikçe oktav açılır
        arp = chord_notes(ch, 4) + [chord_notes(ch, 4)[0] + 12]
        up = 12 if section >= 2 else 0
        for k in range(16):
            m = arp[[0, 1, 2, 3, 2, 1, 3, 2][k % 8]] + up
            synth.add(tone(synth.dur(0.2), note(m), kind="square", decay=0.045, vol=0.055), b0 + k * 0.25)

        # ---- pad
        for m in chord_notes(ch, 4):
            synth.add(tone(synth.dur(4), note(m), kind="saw", attack=0.05, vol=0.03, release=0.2), b0)

        # ---- melodi: B ve D bölümleri + C'de seyrek
        line = lead_b if section in (1, 2) else lead_d if section == 3 else None
        if line and not (section == 2 and bar % 2 == 1):
            half = (bar % 2) * 8
            for k in range(8):
                n = line[half + k]
                if n is None: continue
                synth.add(tone(synth.dur(0.48), note(n), kind="pulse25", attack=0.005, decay=0.25, vol=0.085,
                               vib=0.006, vib_rate=6), b0 + k * 0.5)
                if section == 3:   # son bölümde bir oktav yukarı ikinci ses: parlaklık
                    synth.add(tone(synth.dur(0.3), note(n) * 2, kind="square", decay=0.1, vol=0.03), b0 + k * 0.5)

        # ---- riser: her 8 barın sonunda yükselen gürültü (bir sonraki bölüme hazırlık)
        if last_of_phrase:
            synth.add(noise(synth.dur(4), attack=synth.dur(3.8), vol=0.25, lp=500, lp_end=9000, hp=300,
                            release=0.02), b0)

    mixbuf = [d + s for d, s in zip(drums.buf, sidechain(synth.buf, drums.beat))]
    return lowpass(mixbuf, 10000)


def music_boss():
    t = Track(bpm=160, bars=16)   # oyun müziğinden (150) hızlı: boss daha gergin
    prog = ["Dm", "Bb", "C", "A"]
    for bar in range(16):
        ch = prog[bar % 4]
        b0 = bar * 4
        # agresif davul: dörtlük kick + çift snare
        for beat in range(4):
            t.add(drum_kick(1.0), b0 + beat)
            if beat in (1, 3): t.add(drum_snare(0.9), b0 + beat)
            t.add(drum_hat(0.6), b0 + beat + 0.5)
            if bar >= 8: t.add(drum_kick(0.6), b0 + beat + 0.75)
        # distorsiyonlu bas (saw, onaltılık)
        root = chord_notes(ch, 2)[0]
        for k in range(16):
            m = root + [0, 0, 12, 0, 0, 12, 0, 7][k % 8]
            t.add(tone(t.dur(0.23), note(m), kind="saw", decay=0.08, vol=0.2), b0 + k * 0.25)
        # gerilim: yarım ton kayan tiz ostinato
        top = chord_notes(ch, 5)
        for k in range(8):
            m = top[k % 3] + (1 if k == 7 else 0)
            t.add(tone(t.dur(0.45), note(m), kind="square", decay=0.1, vol=0.065), b0 + k * 0.5)
        # alarm benzeri uzun nota (her 4 barda)
        if bar % 4 == 0:
            t.add(tone(t.dur(4), note(chord_notes(ch, 4)[0]), kind="saw", attack=0.3, vol=0.06,
                       vib=0.01, vib_rate=3, release=0.4), b0)
    return lowpass(t.buf, 8000)


# ================================================================ ana akış
def main():
    effects = sfx()
    for name, x in effects.items():
        write_wav(os.path.join(OUT, "SFX", name + ".wav"), finish(x, peak=0.85))
    print(f"{len(effects)} ses efekti")

    for name, fn in (("menu", music_menu), ("game", music_game), ("boss", music_boss)):
        x = fn()
        # müzik: normalize ama tanh kırpmasız (döngü dikişi bozulmasın), fade YOK
        m = max(1e-6, max(abs(v) for v in x))
        write_wav(os.path.join(OUT, "Music", name + ".wav"), [v * 0.8 / m for v in x])
        print(f"müzik {name}: {len(x) / SR:.1f} sn")

    for d in (OUT, os.path.join(OUT, "SFX"), os.path.join(OUT, "Music")):
        meta = d + ".meta"
        if not os.path.exists(meta):
            with open(meta, "w") as f:
                f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
                        "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
                        % uuid.uuid4().hex)


if __name__ == "__main__":
    main()
