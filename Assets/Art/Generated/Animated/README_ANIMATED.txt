ANIMATED SPRITE SHEETS (flipbook strips)
=========================================
Her PNG yatay bir ŞERİT: kareler soldan sağa dizili. Kare boyutu = görselin YÜKSEKLİĞİ.
YENI: efektler artik YUMUSAK stil -> Filter Mode = BILINEAR (Point DEGIL).
  VFX/         -> 64x64 kareler (8 veya 6 kare)
  Projectiles/ -> 64x64 kareler (6 kare, LOOP)
  SkillFX/     -> 96x96 kareler (8 veya 6 kare)

UNITY IMPORT
------------
  Texture Type : Sprite (2D and UI)
  Sprite Mode  : MULTIPLE
  Filter Mode  : Point (no filter)
  Compression  : None
  Sprite Editor -> Slice -> Grid By Cell Size -> 64x64 (SkillFX icin 96x96) -> Slice -> Apply
  (Bu, sheet'i kare kare alt-sprite'lara boler.)

OYNATMA — iki yol
-----------------
A) SpriteAnimation.cs (Assets/Scripts/Effects/) — ONERILEN, basit:
   - Bos obje + SpriteRenderer + SpriteAnimation ekle, prefab yap.
   - Dilimlenmis kareleri 'Frames' dizisine surukle (sirayla).
   - fps ~12. LOOP acik: surekli efektler. LOOP kapali + destroyOnEnd:
     tek seferlik efekt (bitince kendini yok eder).

B) Unity Animator: dilimlenmis alt-sprite'larin HEPSINI birden Scene'e surukle
   -> Unity otomatik Animation clip + Animator olusturur. Sample rate'i ayarla.

LOOP ONERISI
------------
  LOOP (surekli):     proj_fireball, proj_plasma, proj_energy_orb, proj_saw,
                      vfx_spark, vfx_glow_soft, fx_shield_bubble, fx_heal_aura
  TEK SEFERLIK:       vfx_explosion, vfx_muzzle, vfx_hit, vfx_ring_shock,
                      vfx_nova_ring, vfx_smoke, fx_blast, fx_pulse_ring, fx_dash_streak

NEREDE KULLANILIR
-----------------
  proj_*           -> Projectile prefab (donen/titresen mermi). Loop acik.
  vfx_muzzle       -> silah ates edince namlu ucunda (tek seferlik).
  vfx_hit          -> mermi/vurus degince.
  vfx_explosion    -> exploder patlamasi / olum.
  vfx_smoke        -> olum/patlama dumani.
  fx_shield_bubble -> Shield skill effectPrefab (loop, sureyle yok olur).
  fx_blast/pulse   -> AreaBlast / PulseWave skill efekti.
  fx_heal_aura     -> iyilesme.
  fx_dash_streak   -> Dash izi.
