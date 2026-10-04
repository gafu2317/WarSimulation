#!/usr/bin/env python3
"""Create a balanced five-member search using the current Player's available maps."""

import argparse
import json
import pathlib
import random


PERSONALITIES = (0, 1, 2, 7, 9, 16, 19, 20, 21, 22, 24, 25)
BASE = ((1, 2), (2, 9), (3, 0), (5, 9), (6, 9))
ENEMIES = (
    ("混成", BASE),
    ("魔石速攻", ((1, 19), (1, 19), (3, 19), (3, 19), (6, 9))),
    ("遠距離・高所", ((3, 24), (3, 24), (4, 7), (5, 9), (6, 16))),
    ("回復耐久", ((1, 2), (2, 9), (4, 9), (6, 9), (6, 9))),
    ("魔石防衛", ((1, 2), (2, 20), (3, 20), (5, 20), (6, 9))),
    ("妨害・潜行", ((1, 7), (3, 25), (4, 25), (4, 22), (6, 9))),
)


def roles_json(roles):
    return [{"Weapon": weapon, "Personality": personality} for weapon, personality in roles]


def arrange_followers(roles):
    followers = [role for role in roles if role[1] == 21]
    leaders = [role for role in roles if role[1] != 21]
    if not leaders:
        leaders.append((roles[0][0], 0))
        followers.pop()
    return tuple(leaders[:1] + followers + leaders[1:])


def create_candidates(count=96, seed=20261004):
    candidates = []
    seen = set()

    def add(roles, name):
        roles = arrange_followers(roles)
        key = tuple(sorted(roles))
        if key not in seen:
            seen.add(key)
            candidates.append({"Name": name, "Roles": roles_json(roles)})

    for name, roles in ENEMIES:
        add(roles, name)
    add(((1, 19), (2, 16), (3, 19), (5, 9), (5, 16)), "過去候補・混成")
    add(((1, 19), (1, 19), (1, 19), (4, 9), (4, 9)), "過去候補・剣速攻")
    add(((3, 2), (3, 2), (3, 2), (4, 16), (6, 16)), "過去候補・杖火力")
    slots = {1: 0, 2: 1, 3: 2, 4: 3, 5: 3, 6: 4}
    for weapon in range(1, 7):
        for personality in PERSONALITIES:
            roles = list(BASE)
            roles[slots[weapon]] = (weapon, personality)
            add(roles, f"武器{weapon}・性格{personality}")
    for weapon in range(1, 7):
        personality = {1: 19, 2: 20, 3: 24, 4: 25, 5: 9, 6: 9}[weapon]
        add(((weapon, personality),) * 5, f"武器{weapon}集中")
    rng = random.Random(seed)
    while len(candidates) < count:
        roles = [(rng.randrange(1, 7), rng.choice(PERSONALITIES)) for _ in range(5)]
        add(roles, f"混合探索{len(candidates) + 1}")
    if len(candidates) > count:
        raise ValueError(f"Candidate count must be at least {len(candidates)} for full role coverage.")
    return candidates


def create_config(build):
    if build.get("Success") is not True:
        raise ValueError("Search Player must build successfully before creating a config.")
    scenarios = []
    for entry in build["Maps"]:
        for reversed_position, field in ((False, "NormalAvailable"), (True, "ReversedAvailable")):
            if entry[field]:
                scenarios.append({"Id": entry["Name"] + ("_reversed" if reversed_position else "_normal"),
                                  "MapName": entry["Name"], "StonePositionsReversed": reversed_position})
    screen_maps = {"AuthoredMap", "AuthoredMap 3", "AuthoredMap 6"}
    screening = [scenario for scenario in scenarios if scenario["MapName"] in screen_maps]
    if len(screening) != 6:
        raise ValueError("Screening requires both orientations of AuthoredMap, 3, and 6.")
    return {
        "Candidates": create_candidates(),
        "Enemies": [{"Name": name, "Roles": roles_json(roles)} for name, roles in ENEMIES],
        "Scenarios": scenarios,
        "ScreenScenarios": screening,
        "ScreenMatches": 2,
        "FinalMatches": 8,
        "FinalCandidates": 10,
        "LeagueMatches": 6,
        "LeagueCandidates": 6,
        "BaseSeed": 202610040,
        "TimeoutSeconds": 600,
        "TimeScale": 6,
        "PreserveFixedDeltaTime": True,
        "DisableDiagnostics": True,
        "Conditions": {"PartySize": 5, "RoleCoverage": "6 weapons x all 12 current personalities",
                       "TagalongOrder": "No follower in the first slot; each follows the preceding member. Role order is fixed.",
                       "SkillsAndStats": "Same character prefab for both teams; current weapon and skill assets.",
                       "Scope": "Best measured candidates under equally weighted opponents and available maps; not exhaustive."},
        "Batch": {"Executable": ".unity/CombatAutoBattleSearch/CombatAutoBattleSearch.app",
                  "ParallelProcesses": 2, "MatchesPerJob": 4, "JobWorkerCount": 1},
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-metadata", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    config = create_config(json.loads(args.build_metadata.read_text(encoding="utf-8")))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    coverage = {(role["Weapon"], role["Personality"]) for candidate in config["Candidates"] for role in candidate["Roles"]}
    if len(coverage) != 72:
        raise ValueError(f"Incomplete role coverage: {len(coverage)} / 72")
    print(f"{len(config['Candidates'])} candidates, {len(coverage)} roles, {len(config['Scenarios'])} scenarios: {args.output}")


if __name__ == "__main__":
    main()
