GENERATED ASSET PACK  (v2 — KARISIK STIL)
==========================================
İki stil bir arada:
  * Efekt + mermi katmani = YUMUSAK/vektorel (glossy, anti-aliased):
      Projectiles/, VFX/, SkillFX/, Animated/*
  * UI + dunya = PIXEL ART:
      Icons/, UI/, Tiles/, Pickups/

IMPORT AYARLARI — STILE GORE FARKLI!
------------------------------------
Yumusak olanlar (Projectiles, VFX, SkillFX, Animated):
  Texture Type : Sprite (2D and UI)
  Filter Mode  : BILINEAR      <- yumusak kalsin (Point DEGIL)
  Compression  : Normal/None (tercih)
Pixel olanlar (Icons, UI, Tiles, Pickups):
  Filter Mode  : POINT (no filter)
  Compression  : None
  (Tiles: Wrap Mode = Repeat)

BOYUTLAR
--------
  Projectiles/  64x64   (saga bakar; SpriteAnimation loop ile animasyonlu)
  VFX/          64x64
  SkillFX/      96x96
  Animated/     ayni kare boyutlari (Multiple + Grid By Cell Size: 64 veya 96)

Pixel olanlar (degismedi): Icons 32, UI 9-slice, Tiles 32 seamless, Pickups 32.

Not: Yumusak efektler prosedurel uretildi (scratchpad/gen-smooth.ps1). Renk/sekil
degistirmek istersen script guncellenip yeniden basilir; ayni dosya adlarina yazar,
prefab/referanslar bozulmaz.

KULLANIM (kod tarafi hazir):
  proj_*           -> WeaponData.projectilePrefab (Projectile prefab'inin sprite'i, loop)
  vfx_muzzle       -> Weapon.muzzleEffect
  vfx_hit          -> Projectile.hitEffect
  vfx_explosion/smoke -> DeathEffectSpawner.deathVfx / ExploderEnemy.explosionVfx
  fx_*             -> SkillUpgradeData.effectPrefab
