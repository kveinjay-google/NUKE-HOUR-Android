#!/usr/bin/env python3
"""AI 人格配置管理：读取 launcher_ai_profiles.json，生成 mods/ra2/rules/ai-profiles.yaml。

OpenRA 的 AI 人格是纯数据驱动的：Player 角色上每个 ModularBot@后缀 + 一组
RequiresCondition 绑定的 BotModule 即为一个人格，遭遇战大厅的电脑槽位下拉框
会列出全部人格（Name 字段直接显示，找不到 fluent 键时回退显示原文，所以
可以直接写中文名）。本模块按用户保存的数值配置生成对应 yaml 块。
"""

import json
import os
import re
import uuid

ROOT = os.path.dirname(os.path.abspath(__file__))
PROFILES_JSON = os.path.join(ROOT, "launcher_ai_profiles.json")
OUTPUT_YAML = os.path.join(ROOT, "mods", "ra2", "rules", "ai-profiles.yaml")

TICKS_PER_SECOND = 25
PROFILE_SCHEMA_VERSION = 2

# 可调旋钮（key -> 默认值），秒数在生成时换算成 tick
KNOB_DEFAULTS = {
    "squad_size": 5,            # 进攻小队最低兵力
    "squad_random": 30,         # 小队规模随机加成上限
    "rush_interval_s": 24,      # 两次进攻尝试的间隔（秒）
    "min_attack_delay_s": 0,    # 成队最小间隔（秒）
    "build_active_delay_s": 1.0,   # 建筑生产间隔（秒，越小暴兵越快）
    "build_inactive_delay_s": 5,   # 建筑空闲检查间隔（秒）
    "min_cash": 500,            # 停工现金门槛（低于此数暂停排队生产）
    "refinery_limit": 4,        # 矿场上限
    "harvester_limit": 8,       # 矿车上限
    "mcv_count": 1,             # 维持基地车数量（>1 会扩张开分矿）
    "unit_prod_interval_s": 0.4,  # 造兵决策间隔（秒）
    "new_production_cash_threshold": 2000,  # 追加生产建筑的现金门槛
    "barracks_limit": 4,        # 兵营上限
    "weap_limit": 5,            # 战车工厂上限
    "airpad_limit": 4,          # 空军生产建筑上限
    "naval_limit": 3,           # 船厂上限
}

BUILTIN_PRESETS = [
    {
        "id": "profturtle", "builtin": True, "name": "龟缩防御",
        "squad_size": 10, "squad_random": 10, "rush_interval_s": 24,
        "min_attack_delay_s": 4, "build_active_delay_s": 0.48,
        "build_inactive_delay_s": 2.4, "min_cash": 400,
        "refinery_limit": 6, "harvester_limit": 16, "mcv_count": 2,
        "unit_prod_interval_s": 0.4, "new_production_cash_threshold": 2500,
        "barracks_limit": 4, "weap_limit": 4, "airpad_limit": 3, "naval_limit": 2,
    },
    {
        "id": "profbalanced", "builtin": True, "name": "均衡标准",
        "squad_size": 5, "squad_random": 30, "rush_interval_s": 9.6,
        "min_attack_delay_s": 0, "build_active_delay_s": 0.4,
        "build_inactive_delay_s": 2, "min_cash": 200,
        "refinery_limit": 6, "harvester_limit": 14, "mcv_count": 2,
        "unit_prod_interval_s": 0.4, "new_production_cash_threshold": 2000,
        "barracks_limit": 4, "weap_limit": 5, "airpad_limit": 4, "naval_limit": 3,
    },
    {
        "id": "profrush", "builtin": True, "name": "狂暴 Rush",
        "squad_size": 3, "squad_random": 5, "rush_interval_s": 2.4,
        "min_attack_delay_s": 0, "build_active_delay_s": 0.24,
        "build_inactive_delay_s": 0.8, "min_cash": 0,
        "refinery_limit": 5, "harvester_limit": 12, "mcv_count": 2,
        "unit_prod_interval_s": 0.2, "new_production_cash_threshold": 1000,
        "barracks_limit": 6, "weap_limit": 6, "airpad_limit": 4, "naval_limit": 3,
    },
]

# Display order is stable. Numeric runtime defaults live in AiDifficultyCatalog.
OFFICIAL_DIFFICULTIES = [
    ("beginner", "新手", "Beginner"), ("easy", "简单", "Easy"),
    ("normal", "普通", "Normal"), ("hard", "困难", "Hard"),
    ("brutal", "冷酷", "Brutal"), ("expert", "专家", "Expert"),
    ("master", "大师", "Master"), ("nightmare", "噩梦", "Nightmare"),
]


def difficulty_yaml():
    # Shared base modules; synchronized runtime profiles supply the actual tier values.
    baseline = next(p for p in BUILTIN_PRESETS if p["id"] == "profbalanced")
    return "".join(_profile_yaml(dict(baseline, id="difficulty-" + key,
                                     name="ai-difficulty-" + key))
                   for key in [d[0] for d in OFFICIAL_DIFFICULTIES] + ["custom"])


# ---------------- 以下为固定模板（复制自 ai.yaml 的 @test 人格，数值部分由配置覆盖） ----------------

_SUPPORT_POWER_BLOCK = """\
		Decisions:
			airborne:
				OrderName: AmericanParatroopers
				MinimumAttractiveness: 5
				MinimumScanTimeInterval: 100
				MaximumScanTimeInterval: 105
				Consideration@1:
					Against: Enemy
					Types: Structure
					Attractiveness: 1
					TargetMetric: None
					CheckRadius: 8c0
				Consideration@2:
					Against: Enemy
					Types: Water
					Attractiveness: -5
					TargetMetric: None
					CheckRadius: 8c0
			nukepower:
				OrderName: NukePowerInfoOrder
				MinimumAttractiveness: 3000
				MinimumScanTimeInterval: 100
				MaximumScanTimeInterval: 105
				Consideration@1:
					Against: Enemy
					Types: Structure
					Attractiveness: 1
					TargetMetric: Value
					CheckRadius: 5c0
				Consideration@2:
					Against: Ally
					Types: Air, Ground, Water
					Attractiveness: -10
					TargetMetric: Value
					CheckRadius: 7c0
"""

def _building_limits_tail(profile):
    return f"""\
			gapowr: 8
			napowr: 8
			yapowr: 8
			nanrct: 1
			gapile: {int(profile['barracks_limit'])}
			nahand: {int(profile['barracks_limit'])}
			yabrck: {int(profile['barracks_limit'])}
			gaweap: {int(profile['weap_limit'])}
			naweap: {int(profile['weap_limit'])}
			yaweap: {int(profile['weap_limit'])}
			nayard: {int(profile['naval_limit'])}
			gayard: {int(profile['naval_limit'])}
			yayard: {int(profile['naval_limit'])}
			gadept: 1
			nadept: 1
			yadept: 1
			gaairc: {int(profile['airpad_limit'])}
			amradr: {int(profile['airpad_limit'])}
			naradr: 1
			yadome: 1
			gatech: 1
			natech: 1
			yatech: 1
			yagrnd: 1
			gagap: 2
"""

_BUILDING_FRACTIONS = """\
		BuildingFractions:
			gapowr: 20
			napowr: 20
			yapowr: 20
			nanrct: 1
			garefn: 15
			narefn: 15
			yarefn: 15
			gapile: 1
			nahand: 1
			yabrck: 1
			gaweap: 6
			naweap: 6
			yaweap: 6
			nayard: 1
			gayard: 1
			yayard: 1
			gaairc: 1
			amradr: 1
			naradr: 1
			yadome: 1
			gatech: 1
			natech: 1
			yatech: 1
			naclon: 1
			gadept: 1
			nadept: 1
			yadept: 1
			yagrnd: 1
			gapill: 10
			nalasr: 10
			yaggun: 10
			yapsyt: 4
			nasam: 4
			naflak: 4
			tesla: 4
			atesla: 4
			gtgcan: 1
			gagap: 1
			namisl: 1
			yagntc: 1
			yappet: 1
"""

_UNITS_TO_BUILD = """\
		UnitsToBuild:
			e1: 90
			e2: 90
			init: 90
			brute: 10
			virus: 5
			engineer: 1
			dog: 1
			flakt: 10
			shk: 10
			ivan: 3
			jumpjet: 2
			deso: 1
			tany: 1
			yuri: 1
			yurix: 1
			snipe: 5
			cmin: 10
			harv: 10
			smin: 10
			htnk: 50
			htk: 20
			mtnk: 50
			ltnk: 50
			ytnk: 20
			caos: 10
			tele: 10
			mind: 4
			disk: 5
			fv: 20
			sref: 10
			mgtk: 10
			apoc: 10
			tnkd: 15
			ttnk: 15
			dest: 20
			aegis: 20
			dlph: 5
			carrier: 3
			sub: 20
			hyd: 20
			sqd: 5
			yhvr: 5
			bsub: 3
"""

_PROTECTION_TYPES = ("gacnst, gapowr, gapile, garefn, gaairc, amradr, gaweap, gayard, gadept, gatech, "
                     "gapill, nasam, gtgcan, gaorep, gaspysat, gagap, gaweat, gacsph, atesla, nacnst, "
                     "napowr, nahand, narefn, naradr, naweap, nayard, nadept, nanrct, natech, naclon, "
                     "napsis, nairon, namisl, naflak, tesla, nalasr, yacnst, yapowr, yabrck, yarefn, "
                     "yaweap, yadome, yaggun, yatech, yagrnd, yadept, yayard, yapsyt, natbnk, yagntc, "
                     "yappet, amcv, cmin, smcv, harv, pcv, smin, slav")


def _ticks(seconds):
    return max(1, int(round(float(seconds) * TICKS_PER_SECOND)))


def _sanitize_name(name):
    """去掉会破坏 MiniYaml / fluent 显示的字符。"""
    name = re.sub(r"[:#\t\r\n{}]", " ", str(name)).strip()
    return name[:24] or "自定义 AI"


def _sanitize_id(raw):
    slug = re.sub(r"[^a-z0-9]", "", str(raw).lower())
    return slug[:20] or uuid.uuid4().hex[:8]


def new_profile_id():
    return "prof" + uuid.uuid4().hex[:6]


def normalize_profile(p):
    out = dict(KNOB_DEFAULTS)
    out.update({k: v for k, v in p.items() if k in KNOB_DEFAULTS})
    out["id"] = _sanitize_id(p.get("id", new_profile_id()))
    out["name"] = _sanitize_name(p.get("name", "自定义 AI"))
    out["builtin"] = bool(p.get("builtin", False))
    return out


def load_profiles():
    """读取用户配置；首次运行或文件损坏时用出厂预设播种。"""
    try:
        with open(PROFILES_JSON, "r", encoding="utf-8") as f:
            data = json.load(f)
        source_version = int(data.get("version", 1))
        raw_profiles = data.get("profiles", [])
        presets = {p["id"]: normalize_profile(p) for p in BUILTIN_PRESETS}
        profiles = []
        for raw_profile in raw_profiles:
            profile_id = _sanitize_id(raw_profile.get("id", new_profile_id()))
            if source_version < PROFILE_SCHEMA_VERSION and profile_id in presets:
                profiles.append(dict(presets[profile_id]))
            else:
                profiles.append(normalize_profile(raw_profile))

        if profiles:
            # 保证出厂预设始终存在（用户可能误删）
            have = {p["id"] for p in profiles}
            for profile_id, preset in presets.items():
                if profile_id not in have:
                    profiles.append(dict(preset))

            if source_version < PROFILE_SCHEMA_VERSION:
                save_profiles(profiles)
            return profiles
    except (OSError, ValueError, KeyError):
        pass
    return [normalize_profile(p) for p in BUILTIN_PRESETS]


def save_profiles(profiles):
    data = {"version": PROFILE_SCHEMA_VERSION, "profiles": [normalize_profile(p) for p in profiles]}
    with open(PROFILES_JSON, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


def _profile_yaml(p):
    cond = f"enable-ai-{p['id']}"
    building_limits_tail = _building_limits_tail(p)
    return f"""\
	ModularBot@{p['id']}:
		Name: {p['name']}
		Type: {p['id']}
		MinOrderQuotientPerTick: 2
	GrantConditionOnBotOwner@{p['id']}:
		Condition: {cond}
		Bots: {p['id']}
	SupportPowerBotModule@{p['id']}:
		RequiresCondition: {cond}
{_SUPPORT_POWER_BLOCK}	BaseBuilderBotModule@{p['id']}:
		RequiresCondition: {cond}
		DefenseQueues: Support
		ConstructionYardTypes: gacnst, nacnst, yacnst
		RefineryTypes: garefn, narefn, yarefn
		PowerTypes: gapowr, napowr, nanrct, yapowr
		BarracksTypes: gapile, nahand, yabrck
		VehiclesFactoryTypes: gaweap, naweap, yaweap
		ProductionTypes: gapile, nahand, yabrck, gaweap, naweap, yaweap, gaairc, amradr
		NavalProductionTypes: nayard, gayard, yayard
		MaxBaseRadius: 50
		StructureProductionActiveDelay: {_ticks(p['build_active_delay_s'])}
		StructureProductionInactiveDelay: {_ticks(p['build_inactive_delay_s'])}
		StructureProductionRandomBonusDelay: 4
		ProductionMinCashRequirement: {int(p['min_cash'])}
		NewProductionCashThreshold: {int(p['new_production_cash_threshold'])}
		BuildingLimits:
			garefn: {int(p['refinery_limit'])}
			narefn: {int(p['refinery_limit'])}
			yarefn: {int(p['refinery_limit'])}
{building_limits_tail}{_BUILDING_FRACTIONS}		DefenseTypes: gapill, nasam, gtgcan, atesla, naflak, tesla, nalasr, yaggun, yapsyt
	SquadManagerBotModule@{p['id']}:
		RequiresCondition: {cond}
		SquadSize: {int(p['squad_size'])}
		SquadSizeRandomBonus: {int(p['squad_random'])}
		AssignRolesInterval: 20
		RushInterval: {_ticks(p['rush_interval_s'])}
		AttackForceInterval: 30
		MinimumAttackForceDelay: {_ticks(p['min_attack_delay_s'])}
		ExcludeFromSquadsTypes: cmin, harv, amcv, smcv, pcv, smin, slav, dog, engineer
		ConstructionYardTypes: gacnst, nacnst, yacnst
		NavalUnitsTypes: dest, aegis, dlph, carrier, sub, hyd, sqd, yhvr, bsub
		NavalProductionTypes: nayard, gayard, yayard
		AirUnitsTypes: jumpjet, shad, zep, orca, beag, disk
		ProtectionTypes: {_PROTECTION_TYPES}
	UnitBuilderBotModule@{p['id']}:
		RequiresCondition: {cond}
		ProductionMinCashRequirement: {int(p['min_cash'])}
		UnitProductionInterval: {_ticks(p['unit_prod_interval_s'])}
{_UNITS_TO_BUILD}		UnitLimits:
			engineer: 1
			dog: 4
			cmin: {int(p['harvester_limit'])}
			harv: {int(p['harvester_limit'])}
			smin: {int(p['harvester_limit'])}
	McvManagerBotModule@{p['id']}:
		RequiresCondition: {cond}
		MinimumConstructionYardCount: {int(p['mcv_count'])}
		McvTypes: amcv, smcv, pcv
		ConstructionYardTypes: gacnst, nacnst, yacnst
		McvFactoryTypes: gaweap, naweap, yaweap
	HarvesterBotModule@{p['id']}:
		RequiresCondition: {cond}
		HarvesterTypes: cmin, harv, smin, slav
		RefineryTypes: garefn, narefn, yarefn
	BuildingRepairBotModule@{p['id']}:
		RequiresCondition: {cond}
	CaptureManagerBotModule@{p['id']}:
		RequiresCondition: {cond}
		CapturingActorTypes: engineer
		CapturableActorTypes: caoild, caairp, cahosp, cathosp, caoutp
		CapturableRelationships: Neutral
		CheckCaptureTargetsForVisibility: false
		MaximumCaptureTargetOptions: 15
"""


def generate_yaml(profiles):
    """把全部配置写入 ai-profiles.yaml，返回写入的人格数量。"""
    profiles = [normalize_profile(p) for p in profiles]
    parts = ["# 本文件由启动器「AI 配置」页自动生成（ai_profiles.py），请勿手改。\n",
             "Player:\n"]
    parts.append(difficulty_yaml())
    for p in profiles:
        parts.append(_profile_yaml(p))
    with open(OUTPUT_YAML, "w", encoding="utf-8") as f:
        f.writelines(parts)
    return len(profiles)


def regenerate():
    """加载配置并重新生成 yaml（启动器保存时调用）。"""
    profiles = load_profiles()
    return generate_yaml(profiles)


if __name__ == "__main__":
    n = regenerate()
    print(f"已生成 {OUTPUT_YAML}（{n} 个 AI 人格）")
