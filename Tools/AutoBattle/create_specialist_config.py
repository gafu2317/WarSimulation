#!/usr/bin/env python3
"""Create terrain-specific searches and controlled personality-pair comparisons."""

import argparse
import json
import pathlib

import create_league_config as coverage
import league_search as league


MAPS = (
    ("AuthoredMap 9", "平地", "山・川・森林領域なし。緩い起伏、散布木30本、岩12個。"),
    ("AuthoredMap 7", "丘陵", "左右の大きな丘と中央の重なった小丘、森林4領域。川・湖なし。"),
    ("AuthoredMap 6", "森林・湿地", "森林8領域、湿地25配置、通常湖1配置。山・川なし。"),
    ("AuthoredMap 3", "川・橋", "川2本、橋2本、森林6領域、左端に大きな丘1配置。"),
    ("AuthoredMap 5", "雪・氷", "雪28配置、凍結湖2配置、大きな丘2配置。森林領域・川なし。"),
)
BASE_SEED = 202610130
FOLLOWER_BASE = ((1, 0), (3, 0), (2, 0), (5, 0), (6, 9))
PAIR_BLOCKS = (
    ("寂しがり＋戦闘狂", coverage.BASE, 2, 16, 0, 2),
    ("寂しがり＋献身的", coverage.BASE, 2, 16, 4, 9),
    ("便乗屋＋猪突猛進", FOLLOWER_BASE, 1, 21, 0, 19),
    ("便乗屋＋戦闘狂", FOLLOWER_BASE, 1, 21, 0, 2),
    ("便乗屋＋狡猾", FOLLOWER_BASE, 1, 21, 0, 7),
    ("高所好き＋高所好き", coverage.BASE, 2, 24, 3, 24),
    ("高所好き＋影薄い", coverage.BASE, 2, 24, 3, 25),
    ("門番＋門番", coverage.BASE, 1, 20, 2, 20),
    ("門番＋猪突猛進", coverage.BASE, 1, 20, 0, 19),
    ("献身的＋戦闘狂", coverage.BASE, 4, 9, 0, 2),
    ("復讐鬼＋影薄い", coverage.BASE, 2, 22, 3, 25),
    ("陽キャ＋寂しがり", coverage.BASE, 0, 1, 2, 16),
)
ENEMIES = (
    ("剣陽キャ", ((1, 1), (2, 9), (3, 0), (5, 9), (6, 9))),
    ("聖書戦闘狂", ((1, 2), (2, 9), (3, 0), (5, 2), (6, 9))),
    ("魔石速攻", ((1, 19), (1, 19), (3, 19), (3, 19), (6, 9))),
)


def create_candidates():
    candidates = coverage.create_candidates(count=96, seed=BASE_SEED)
    existing = {league.candidate_key(candidate["Roles"]): candidate for candidate in candidates}
    pairs = []
    for name, template, slot_a, personality_a, slot_b, personality_b in PAIR_BLOCKS:
        base = list(template)
        for slot in (slot_a, slot_b):
            base[slot] = (base[slot][0], 0)
        pair = {"Name": name, "SlotA": slot_a, "SlotB": slot_b,
                "PersonalityA": personality_a, "PersonalityB": personality_b}
        for variant, a_on, b_on in (("Base", False, False), ("A", True, False),
                                    ("B", False, True), ("AB", True, True)):
            roles = list(base)
            if a_on:
                roles[slot_a] = (roles[slot_a][0], personality_a)
            if b_on:
                roles[slot_b] = (roles[slot_b][0], personality_b)
            candidate = {"Name": f"組合せ・{name}・{variant}", "Roles": coverage.roles_json(roles)}
            key = league.candidate_key(candidate["Roles"])
            if key not in existing:
                existing[key] = candidate
                candidates.append(candidate)
            elif existing[key]["Roles"] != candidate["Roles"]:
                raise ValueError(f"Pair control has a conflicting registration order: {name}/{variant}")
            pair[variant] = key
        pairs.append(pair)
    return candidates, pairs


def create_config(build):
    if build.get("Success") is not True:
        raise ValueError("Search Player must build successfully before creating a config.")
    available = {entry["Name"]: entry for entry in build.get("Maps", [])}
    scenarios = []
    for name, _, _ in MAPS:
        entry = available.get(name, {})
        for reversed_position, field in ((False, "NormalAvailable"), (True, "ReversedAvailable")):
            if entry.get(field) is not True:
                raise ValueError(f"Required map orientation is unavailable: {name}/{field}")
            scenarios.append({"Id": name + ("_reversed" if reversed_position else "_normal"),
                              "MapName": name, "StonePositionsReversed": reversed_position})
    candidates, pairs = create_candidates()
    config = {
        "Candidates": candidates,
        "Enemies": [{"Name": name, "Roles": coverage.roles_json(roles)} for name, roles in ENEMIES],
        "Scenarios": scenarios,
        "ScreenScenarios": scenarios,
        "ScreenMatches": 1,
        "FinalMatches": 3,
        "FinalCandidates": 10,
        "LeagueMatches": 3,
        "LeagueCandidates": 6,
        "BaseSeed": BASE_SEED,
        "TimeoutSeconds": 600,
        "TimeScale": 6,
        "PreserveFixedDeltaTime": True,
        "DisableDiagnostics": True,
        "Conditions": {
            "PartySize": 5,
            "RoleCoverage": "6 weapons x all 12 current personalities, plus 12 fixed-slot pair controls",
            "TagalongOrder": "Fixed registration order; follower always has a preceding leader.",
            "PersonalityStats": "AttentionSeeker all x1.1; Gatekeeper HP x1.2; Cunning AGI x1.2; Reckless AGI x0.8.",
            "Scope": "Terrain and combination specialists within sampled candidates; not exhaustive.",
        },
        "Experiment": {
            "Pairs": pairs,
            "Maps": [{"MapName": name, "Label": label, "Description": description}
                     for name, label, description in MAPS],
            "ExpectedBuildGuid": build["BuildGuid"],
            "ConfirmMatches": 3,
            "PerMapTop": 1,
            "ConfirmPairCount": 3,
            "BaselineKey": league.candidate_key(coverage.roles_json(coverage.BASE)),
        },
        "Batch": {"Executable": ".unity/CombatAutoBattleSearch/CombatAutoBattleSearch.app",
                  "ParallelProcesses": 4, "MatchesPerJob": 3, "JobWorkerCount": 1},
    }
    league.normalize_config(config, argparse.Namespace(parallel=None, executable=None))
    return config


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-metadata", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    config = create_config(json.loads(args.build_metadata.read_text(encoding="utf-8")))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    matches = len(config["Candidates"]) * len(config["Enemies"]) * len(config["Scenarios"])
    print(f"{len(config['Candidates'])} candidates, 12 pair controls, 10 scenarios, "
          f"{matches} screening matches: {args.output}")


if __name__ == "__main__":
    main()
