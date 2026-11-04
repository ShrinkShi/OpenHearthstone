# 卡牌实现状态

`[x] verified` = 有充分行为核验；`[ ] incomplete` = 明确有缺失；`[?] uncertain` = 已接入但仍有待核验行为。

当前目标池 489 张：原九职业与恶魔猎手历史池 431 张，死亡骑士上市池 58 张。

目标 489 张均通过服务器合法动作执行的基础测试；20 个抉择分支另行逐一执行。另有伤害、尸体、符文、技能、冻结、吸血、武器、持续伤害等专项。基础执行测试不能证明所有随机池、触发顺序和边界正确。

上游 Classic/Mechanics/Hero Powers/Secrets 共 281 项测试通过。本项目另有 577 项规则、TCP、视觉比较测试。尚未将每张历史牌的全部数值与行为矩阵独立补齐，因此不能把所有牌标为 verified。

历史脚本修正已验证：BT_427 死亡计数；BT_753 法力锁定与叠加/过期；BT_801 目标、吸血和双方手牌两端流放减费；BT_142 英雄增益与过期；BT_351 自身成长；BT_510 被攻击触发。修正保留其他上游脚本属性。

已知未完成：RLK_122 天灾军团目前随机池只包含本项目注册的死亡骑士亡灵，尚未扩展并核验完整的 2022 历史生成池。其他发现/随机生成池也需要跨历史卡池语义审计。

| 状态 | CardId | 名称 | 证据与限制 |
|---|---|---|---|
| [?] uncertain | BT_035 | Chaos Strike | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_036 | Coordinated Strike | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_142 | Shadowhoof Slayer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_173 | Command the Illidari | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_175 | Twin Slice | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_235 | Chaos Nova | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_271 | Flamereaper | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_323 | Sightless Watcher | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_351 | Battlefiend | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_352 | Satyr Overseer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_354 | Blade Dance | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_355 | Wrathscale Naga | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_407 | Ur'zul Horror | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_416 | Raging Felscreamer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_427 | Feast of Souls | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_481 | Nethrandamus | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_487 | Hulking Overfiend | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_488 | Soul Split | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_490 | Consume Magic | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_495 | Glaivebound Adept | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_510 | Wrathspike Brute | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_512 | Inner Demon | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_740 | Soul Cleave | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_752 | Blur | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_753 | Mana Burn | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_801 | Eye Beam | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_814 | Illidari Felblade | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_921 | Aldrachi Warblades | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_922 | Umberwing | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | BT_937 | Altruis the Outcast | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS1_042 | Goldshire Footman | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS1_069 | Fen Creeper | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS1_112 | Holy Nova | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS1_113 | Mind Control | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS1_129 | Inner Fire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS1_130 | Holy Smite | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_003 | Mind Vision | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_004 | Power Word: Shield | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_005 | Claw | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_007 | Healing Touch | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_008 | Moonfire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_009 | Mark of the Wild | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_011 | Savage Roar | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_012 | Swipe | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_013 | Wild Growth | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_022 | Polymorph | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_023 | Arcane Intellect | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_024 | Frostbolt | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_025 | Arcane Explosion | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_026 | Frost Nova | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_027 | Mirror Image | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_028 | Blizzard | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_029 | Fireball | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_031 | Ice Lance | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_032 | Flamestrike | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_033 | Water Elemental | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_037 | Frost Shock | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_038 | Ancestral Spirit | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_039 | Windfury | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_041 | Ancestral Healing | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_042 | Fire Elemental | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_045 | Rockbiter Weapon | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_046 | Bloodlust | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_053 | Far Sight | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_057 | Shadow Bolt | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_059 | Blood Imp | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_061 | Drain Life | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_062 | Hellfire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_063 | Corruption | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_064 | Dread Infernal | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_065 | Voidwalker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_072 | Backstab | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_073 | Cold Blood | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_074 | Deadly Poison | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_075 | Sinister Strike | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_076 | Assassinate | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_077 | Sprint | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_080 | Assassin's Blade | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_084 | Hunter's Mark | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_087 | Blessing of Might | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_088 | Guardian of Kings | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_089 | Holy Light | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_091 | Light's Justice | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_092 | Blessing of Kings | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_093 | Consecration | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_094 | Hammer of Wrath | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_097 | Truesilver Champion | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_103 | Charge | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_104 | Rampage | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_105 | Heroic Strike | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_106 | Fiery War Axe | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_108 | Execute | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_112 | Arcanite Reaper | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_114 | Cleave | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_117 | Earthen Ring Farseer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_118 | Magma Rager | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_119 | Oasis Snapjaw | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_120 | River Crocolisk | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_121 | Frostwolf Grunt | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_122 | Raid Leader | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_124 | Wolfrider | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_125 | Ironfur Grizzly | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_127 | Silverback Patriarch | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_131 | Stormwind Knight | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_141 | Ironforge Rifleman | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_142 | Kobold Geomancer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_146 | Southsea Deckhand | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_147 | Gnomish Inventor | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_150 | Stormpike Commando | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_151 | Silver Hand Knight | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_155 | Archmage | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_161 | Ravenholdt Assassin | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_162 | Lord of the Arena | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_168 | Murloc Raider | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_169 | Young Dragonhawk | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_171 | Stonetusk Boar | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_172 | Bloodfen Raptor | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_173 | Bluegill Warrior | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_179 | Sen'jin Shieldmasta | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_181 | Injured Blademaster | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_182 | Chillwind Yeti | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_186 | War Golem | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_187 | Booty Bay Bodyguard | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_188 | Abusive Sergeant | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_189 | Elven Archer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_196 | Razorfen Hunter | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_197 | Ogre Magi | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_200 | Boulderfist Ogre | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_201 | Core Hound | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_203 | Ironbeak Owl | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_213 | Reckless Rocketeer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_221 | Spiteful Smith | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_222 | Stormwind Champion | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_226 | Frostwolf Warlord | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_227 | Venture Co. Mercenary | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_231 | Wisp | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_232 | Ironbark Protector | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_233 | Blade Flurry | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_234 | Shadow Word: Pain | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_235 | Northshire Cleric | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_236 | Divine Spirit | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | CS2_237 | Starving Buzzard | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_055 | Darkscale Healer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_070 | Houndmaster | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_175 | Timber Wolf | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_178 | Tundra Rhino | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_183 | Multi-Shot | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_184 | Tracking | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_185 | Arcane Shot | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_188 | Gladiator's Longbow | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | DS1_233 | Mind Blast | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_001 | Lightwarden | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_002 | The Black Knight | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_004 | Young Priestess | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_005 | Big Game Hunter | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_006 | Alarm-o-Bot | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_007 | Acolyte of Pain | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_008 | Argent Squire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_009 | Angry Chicken | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_010 | Worgen Infiltrator | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_011 | Voodoo Doctor | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_012 | Bloodmage Thalnos | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_014 | King Mukla | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_015 | Novice Engineer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_016 | Sylvanas Windrunner | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_017 | Jungle Panther | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_019 | Shattered Sun Cleric | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_020 | Scarlet Crusader | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_021 | Thrallmar Farseer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_023 | Silvermoon Guardian | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_025 | Dragonling Mechanic | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_028 | Stranglethorn Tiger | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_029 | Leper Gnome | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_032 | Sunwalker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_033 | Windfury Harpy | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_043 | Twilight Drake | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_044 | Questing Adventurer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_045 | Ancient Watcher | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_046 | Dark Iron Dwarf | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_048 | Spellbreaker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_049 | Youthful Brewmaster | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_050 | Coldlight Oracle | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_055 | Mana Addict | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_057 | Ancient Brewmaster | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_058 | Sunfury Protector | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_059 | Crazed Alchemist | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_062 | Old Murk-Eye | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_066 | Acidic Swamp Ooze | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_067 | Argent Commander | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_076 | Pint-Sized Summoner | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_080 | Secretkeeper | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_082 | Mad Bomber | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_083 | Tinkmaster Overspark | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_084 | Warsong Commander | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_085 | Mind Control Tech | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_089 | Arcane Golem | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_091 | Cabal Shadow Priest | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_093 | Defender of Argus | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_095 | Gadgetzan Auctioneer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_096 | Loot Hoarder | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_097 | Abomination | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_100 | Lorewalker Cho | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_102 | Demolisher | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_103 | Coldlight Seer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_105 | Mountain Giant | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_110 | Cairne Bloodhoof | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_112 | Gelbin Mekkatorque | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_116 | Leeroy Jenkins | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_124 | Eviscerate | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_126 | Betrayal | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_128 | Conceal | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_129 | Fan of Knives | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_130 | Noble Sacrifice | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_131 | Defias Ringleader | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_132 | Eye for an Eye | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_133 | Perdition's Blade | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_134 | SI:7 Agent | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_136 | Redemption | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_137 | Headcrack | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_144 | Shadowstep | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_145 | Preparation | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_154 | Wrath | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_155 | Mark of Nature | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_158 | Soul of the Forest | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_160 | Power of the Wild | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_161 | Naturalize | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_162 | Dire Wolf Alpha | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_164 | Nourish | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_165 | Druid of the Claw | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_166 | Keeper of the Grove | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_169 | Innervate | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_170 | Emperor Cobra | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_173 | Starfire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_178 | Ancient of War | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_179 | Icicle | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_180 | Tome of Intellect | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_181 | Call of the Void | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_182 | Pilfer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_183 | Gift of the Wild | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_184 | Righteousness | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_185 | Siegebreaker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_186 | SI:7 Infiltrator | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_187 | Arcane Devourer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_188 | Barrens Stablehand | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_189 | Brightwing | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_190 | High Inquisitor Whitemane | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_191 | Plaguebringer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_192 | Radiance | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_193 | Psychic Conjurer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_194 | Power Infusion | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_195 | Kul Tiran Chaplain | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_196 | Scarlet Subjugator | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_197 | Shadow Word: Ruin | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_198 | Natalie Seline | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_238 | Lightning Bolt | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_241 | Lava Burst | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_243 | Dust Devil | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_244 | Totemic Might | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_245 | Earth Shock | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_246 | Hex | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_247 | Stormforged Axe | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_248 | Feral Spirit | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_249 | Baron Geddon | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_250 | Earth Elemental | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_251 | Forked Lightning | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_258 | Unbound Elemental | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_259 | Lightning Storm | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_274 | Ethereal Arcanist | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_275 | Cone of Cold | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_277 | Arcane Missiles | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_278 | Shiv | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_279 | Pyroblast | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_283 | Frost Elemental | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_284 | Azure Drake | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_287 | Counterspell | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_289 | Ice Barrier | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_294 | Mirror Entity | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_295 | Ice Block | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_298 | Ragnaros the Firelord | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_301 | Felguard | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_302 | Mortal Coil | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_303 | Shadowflame | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_304 | Void Terror | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_306 | Felstalker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_308 | Soulfire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_309 | Siphon Soul | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_310 | Doomguard | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_312 | Twisting Nether | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_313 | Pit Lord | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_315 | Summoning Portal | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_316 | Power Overwhelming | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_317 | Sense Demons | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_319 | Flame Imp | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_320 | Bane of Doom | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_323 | Lord Jaraxxus | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_332 | Silence | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_334 | Shadow Madness | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_335 | Lightspawn | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_339 | Thoughtsteal | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_341 | Lightwell | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_345 | Mindgames | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_349 | Divine Favor | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_350 | Prophet Velen | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_354 | Lay on Hands | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_355 | Blessed Champion | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_360 | Humility | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_362 | Argent Protector | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_363 | Blessing of Wisdom | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_365 | Holy Wrath | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_366 | Sword of Justice | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_371 | Hand of Protection | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_379 | Repentance | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_382 | Aldor Peacekeeper | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_383 | Tirion Fordring | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_384 | Avenging Wrath | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_390 | Tauren Warrior | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_391 | Slam | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_392 | Battle Rage | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_393 | Amani Berserker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_396 | Mogu'shan Warden | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_398 | Arathi Weaponsmith | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_399 | Gurubashi Berserker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_400 | Whirlwind | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_402 | Armorsmith | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_405 | Shieldbearer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_407 | Brawl | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_408 | Mortal Strike | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_409 | Upgrade! | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_410 | Shield Slam | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_411 | Gorehowl | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_412 | Raging Worgen | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_414 | Grommash Hellscream | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_506 | Murloc Tidehunter | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_507 | Murloc Warleader | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_508 | Grimscale Oracle | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_509 | Murloc Tidecaller | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_522 | Patient Assassin | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_531 | Scavenging Hyena | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_533 | Misdirection | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_534 | Savannah Highmane | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_536 | Eaglehorn Bow | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_537 | Explosive Shot | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_538 | Unleash the Hounds | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_539 | Kill Command | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_543 | King Krush | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_544 | Flare | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_549 | Bestial Wrath | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_554 | Snake Trap | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_556 | Harvest Golem | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_557 | Nat Pagle | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_558 | Harrison Jones | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_559 | Archmage Antonidas | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_560 | Nozdormu | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_561 | Alexstrasza | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_562 | Onyxia | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_563 | Malygos | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_564 | Faceless Manipulator | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_565 | Flametongue Totem | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_567 | Doomhammer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_570 | Bite | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_571 | Force of Nature | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_572 | Ysera | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_573 | Cenarius | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_575 | Mana Tide Totem | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_577 | The Beast | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_578 | Savagery | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_581 | Sap | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_582 | Dalaran Mage | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_583 | Priestess of Elune | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_584 | Ancient Mage | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_586 | Sea Giant | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_587 | Windspeaker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_590 | Blood Knight | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_591 | Auchenai Soulpriest | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_593 | Nightblade | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_594 | Vaporize | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_595 | Cult Master | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_596 | Demonfire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_597 | Imp Master | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_603 | Cruel Taskmaster | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_604 | Frothing Berserker | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_606 | Shield Block | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_607 | Inner Rage | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_608 | Sorcerer's Apprentice | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_609 | Snipe | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_610 | Explosive Trap | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_611 | Freezing Trap | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_612 | Kirin Tor Mage | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_613 | Edwin VanCleef | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_614 | Xavius | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_616 | Mana Wraith | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_617 | Deadly Shot | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_619 | Equality | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_620 | Molten Giant | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_621 | Circle of Healing | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_622 | Shadow Word: Death | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_623 | Temple Enforcer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_624 | Holy Fire | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_625 | Shadowform | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | EX1_626 | Mass Dispel | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_003 | Sacrificial Pact | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_004 | Vanish | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_005 | Kidnapper | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_007 | Starfall | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_008 | Ancient of Lore | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_010 | Al'Akir the Windlord | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_011 | Kor'kron Elite | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_012 | Mana Wyrm | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_014 | Master of Disguise | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_016 | Captain's Parrot | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_017 | Hungry Crab | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_018 | Bloodsail Raider | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_019 | Knife Juggler | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_020 | Wild Pyromancer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_021 | Doomsayer | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_022 | Dread Corsair | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_023 | Faerie Dragon | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_024 | Captain Greenskin | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_025 | Bloodsail Corsair | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_026 | Violet Teacher | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_027 | Southsea Captain | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_029 | Millhouse Manastorm | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_030 | Deathwing | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_031 | Animal Companion | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_036 | Commanding Shout | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_037 | Master Swordsmith | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_038 | Gruul | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_040 | Hogger | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | NEW1_041 | Stampeding Kodo | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | RLK_015 | 凛风冲击 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_018 | 凋零打击 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_024 | 灵界打击 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_025 | 冰霜打击 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_034 | 心脏打击 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_038 | 冰冷触摸 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_039 | 天灾谷物 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_042 | 寒冬号角 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_048 | 反魔法护罩 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_056 | 邪恶狂热 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_057 | 黑暗突变 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_060 | 亡者大军 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_061 | 战场通灵师 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_062 | 蛛魔护群守卫 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_063 | 冰霜巨龙之怒 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_066 | 鲜血魔术师 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_067 | 堕落的灰烬使者 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_071 | 帕奇维克 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_077 | 魔心操纵者 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_079 | 喷毒尸骸 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_082 | 死亡使者萨鲁法尔 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_083 | 死亡寒冰 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_085 | 玛洛加尔领主 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_086 | 霜之哀伤 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_087 | 窒息 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_101 | 解冻 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_110 | 伊米亚破霜者 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_115 | 丑恶巨怪 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_118 | 坟墓守卫 | 基础执行通路已测，效果边界仍待核验 |
| [ ] incomplete | RLK_122 | 天灾军团 | 可执行；完整历史随机亡灵池尚未实现 |
| [?] uncertain | RLK_125 | 湮灭 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_223 | 萨萨里安 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_224 | 监督者弗里吉达拉 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_226 | 伊米亚死亡使者 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_503 | 扛包收尸人 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_504 | 僵尸新娘 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_505 | 髓骨使御者 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_511 | 寒冬先锋 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_512 | 冰川突进 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_516 | 碎骨手斧 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_705 | 墓地夜战 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_707 | 墓地之力 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_708 | 堕寒男爵 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_709 | 冷酷严冬 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_710 | 霜牙之剑 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_711 | 凶恶的血虫 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_712 | 活力分流 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_713 | 亡语者女士 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_715 | 符文熔铸 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_720 | 侏儒嚼嚼怪 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_730 | 血液沸腾 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_731 | 黑暗堕落者新兵 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_740 | 米奈希尔之力 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_744 | 缝合巨人 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_745 | 恶毒恐魔 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_752 | 霜雪塑形师 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_753 | 掘骨恶鬼 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | RLK_958 | 骷髅帮手 | 基础执行通路已测，效果边界仍待核验 |
| [?] uncertain | tt_004 | Flesheating Ghoul | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
| [?] uncertain | tt_010 | Spellbender | 出牌/后续回合/死亡执行通路已测，完整数值/分支边界待补 |
