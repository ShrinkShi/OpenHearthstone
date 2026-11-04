"""导出原始卡框图集与模型；不扫描后直接批量解包。"""
import json
from .export_runtime import RuntimeExport

ex = RuntimeExport()
registry = ex.folder / 'registry.json'
if registry.exists():
    ex.registry = {r['id']: r['path'] for r in json.loads(registry.read_text())['entries']}
for name in ['Card_Inhand_Minion_Mage', 'Card_Inhand_Ability_Mage', 'Card_Inhand_Generic',
             'GenFX_Card_InPlay_AllyFrame', 'GenFX_Card_InHand_BannerAtlas', 'GenFX_RarityGems',
             'Card_Back_Default_D', 'GenFX_Gem_Mana_D', 'GenFX_Gem_Attack_D', 'GenFX_Gem_Health_D',
             'GenFX_Card_Inplay_D', 'GenFX_HeroFrame', 'Hero_Power_Frame', 'Board_EndTurnButton']:
    ex.named('UI_' + name.upper(), name)
for class_name in ['DeathKnight', 'DemonHunter', 'Druid', 'Hunter', 'Mage', 'Paladin', 'Priest', 'Rogue', 'Shaman', 'Warlock', 'Warrior']:
    for category in ['Minion', 'Ability', 'Weapon', 'Hero']:
        name = 'Card_Inhand_' + category + '_' + class_name
        if ex.imp.store.db.execute('SELECT 1 FROM assets WHERE type="Texture2D" AND name=? COLLATE NOCASE', (name,)).fetchone():
            ex.named('FRAME_' + category.upper() + '_' + class_name.upper(), name)
ex.named('UI_CARD_BACK', 'Card_Back_Default_D')
ex.named('UI_MANA', 'GenFX_Gem_Mana_D')
ex.named('VFX_FIREBALL_BOLT', 'GenFX_Fireball_1Hot')
ex.named('SFX_UI_CLICK', 'box_hub_button_press_2', 'AudioClip')
ex.named('SFX_UI_HOVER', 'box_large_button_mouse_over', 'AudioClip')
ex.scene('MODEL_CARD_MINION', 'GenFX_Card_InHand_Ally_v3', 'essential_base_global-mesh-0.unity3d')
ex.scene('MODEL_CARD_SPELL', 'Card_InHand_Ability', 'essential_base_global-mesh-0.unity3d')
ex.scene('MODEL_CARD_WEAPON', 'Card_InHand_Weapon_v3', 'essential_base_global-mesh-0.unity3d')
ex.scene('MODEL_MINION', 'GenFX_Card_InPlay_Ally_v2', 'essential_base_global-mesh-0.unity3d')
ex.scene('MODEL_HERO', 'GenFX_Card_Hero_v3', 'essential_base_global-mesh-0.unity3d')
for kind in ['Mana', 'Attack', 'Health']:
    ex.scene('MODEL_GEM_' + kind.upper(), 'Gem_' + kind, 'essential_base_global-prefab-0.unity3d')
ex.scene('MODEL_CARD_BACK', 'Card_Back_Default', 'essential_base_global-prefab-0.unity3d')
ex.scene('MODEL_HERO_POWER', 'Card_InPlay_HeroPowerV2', 'essential_base_global-mesh-0.unity3d')
ex.scene('SCENE_BOARD', 'Board_STR', 'essential_base_qualityhigh_global-content-0.unity3d')
ex.scene('SCENE_MAIN_MENU', 'TheBox', 'essential_base_box_global-prefab-0.unity3d')
for pattern in ['%GenFX_Card_InPlay%', '%ManaGem%', '%CardBack%']:
    rows = ex.imp.store.db.execute('SELECT DISTINCT name FROM assets WHERE name LIKE ? AND type="Texture2D" LIMIT 30', (pattern,)).fetchall()
    print(pattern, [r[0] for r in rows])
ex.save()
