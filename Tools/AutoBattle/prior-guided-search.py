#!/usr/bin/env python3

"""Run a scenario-specific search guided by a previous SQLite result."""

import argparse
import importlib.util
import json
import pathlib
import sqlite3
import sys


SCRIPT_DIRECTORY = pathlib.Path(__file__).resolve().parent
ADAPTIVE_PATH = SCRIPT_DIRECTORY / "adaptive_search.py"
SPEC = importlib.util.spec_from_file_location("adaptive_search", ADAPTIVE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Unable to load adaptive search helpers: {ADAPTIVE_PATH}")
adaptive_search = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(adaptive_search)


DEFAULT_CONFIG = SCRIPT_DIRECTORY / "prior-guided-search.json"
DEFAULT_PRIOR_DB = pathlib.Path("Logs/AutoBattleAdaptive/run_20260825_curated100/analysis.db")


def parse_args():
    parser = argparse.ArgumentParser(
        description="Run a resumable scenario-specific search guided by a previous SQLite result."
    )
    parser.add_argument("--config", type=pathlib.Path, default=DEFAULT_CONFIG)
    parser.add_argument("--prior-database", type=pathlib.Path, default=DEFAULT_PRIOR_DB)
    parser.add_argument("--executable", type=pathlib.Path)
    parser.add_argument("--output-directory", type=pathlib.Path, required=True)
    parser.add_argument("--plan-only", action="store_true")
    return parser.parse_args()


def load_json(path):
    with pathlib.Path(path).open(encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"JSON root must be an object: {path}")
    return value


def write_json_atomic(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def ranking_key(row):
    matches = row["match_count"]
    win_rate = row["wins"] / matches if matches else 0.0
    return (-row["wins"], row["timeouts"], -win_rate, row["average_game_seconds"], row["candidate_key"])


def role_distance(first, second):
    return sum(left != right for left, right in zip(first, second))


def load_prior_results(database_path):
    if not database_path.is_file():
        raise FileNotFoundError(f"Prior SQLite database not found: {database_path}")

    connection = sqlite3.connect(database_path)
    connection.row_factory = sqlite3.Row
    try:
        rows = connection.execute(
            """
            SELECT stage, scenario_key, candidate_key, match_count, wins, losses,
                   timeouts, average_game_seconds
            FROM candidate_results
            WHERE stage IN ('screening', 'final')
            """
        ).fetchall()
    finally:
        connection.close()

    results = {}
    for row in rows:
        results.setdefault(row["scenario_key"], []).append(dict(row))
    return results


def role_score(candidate, anchors):
    if not anchors:
        return 0.0
    role_counts = {}
    for rank, anchor in enumerate(anchors):
        weight = 1.0 / (rank + 1.0)
        for role in anchor:
            role_counts[role] = role_counts.get(role, 0.0) + weight
    return sum(role_counts.get(role, 0.0) for role in candidate)


def select_diverse(candidates, count, seed, initial=()):
    selected = list(dict.fromkeys(initial))
    if len(selected) >= count:
        return selected[:count]
    remaining = [candidate for candidate in candidates if candidate not in selected]
    if not remaining:
        return selected

    features = [adaptive_search.feature_counts(candidate) for candidate in remaining]
    usage = [0] * adaptive_search.FEATURE_COUNT
    for candidate in selected:
        for feature, value in adaptive_search.feature_counts(candidate):
            usage[feature] += value
    tie_order = list(range(len(remaining)))
    import random

    random.Random(seed).shuffle(tie_order)
    tie_rank = {index: rank for rank, index in enumerate(tie_order)}
    while len(selected) < count and remaining:
        best_index = max(
            range(len(remaining)),
            key=lambda index: (
                sum(1.0 / (1.0 + usage[feature]) for feature, _ in features[index]),
                sum(1.0 / (1.0 + usage[feature]) for feature, _ in features[index] if feature > len(adaptive_search.ROLE_OPTIONS)),
                -tie_rank[index],
            ),
        )
        candidate = remaining.pop(best_index)
        selected.append(candidate)
        for feature, value in adaptive_search.feature_counts(candidate):
            usage[feature] += value
    return selected


def build_candidates_for_scenario(
    scenario_key,
    prior_results,
    all_candidates,
    target_count,
    anchor_count,
    neighbor_count,
    recombination_count,
    unseen_count,
    seed,
):
    lookup = {adaptive_search.candidate_key(candidate): candidate for candidate in all_candidates}
    rows = prior_results.get(scenario_key, [])
    final_rows = sorted((row for row in rows if row["stage"] == "final"), key=ranking_key)
    screening_rows = sorted((row for row in rows if row["stage"] == "screening"), key=ranking_key)
    anchors = []
    for row in final_rows + screening_rows:
        candidate = lookup.get(row["candidate_key"])
        if candidate is not None and candidate not in anchors:
            anchors.append(candidate)
    anchors = anchors[:anchor_count]

    evaluated_keys = {row["candidate_key"] for row in rows}
    candidate_indices = {candidate: index for index, candidate in enumerate(all_candidates)}
    anchor_indices = [candidate_indices[candidate] for candidate in anchors]
    neighbor_indices = adaptive_search.neighbor_indices(
        anchor_indices,
        all_candidates,
        candidate_indices,
        set(anchor_indices),
    )
    neighbors = [all_candidates[index] for index in neighbor_indices]
    neighbor_set = set(neighbors)
    neighborhood = [candidate for candidate in neighbors if candidate not in anchors]
    recombinations = [
        candidate
        for candidate in all_candidates
        if candidate not in anchors
        and candidate not in neighbor_set
        and min((role_distance(candidate, anchor) for anchor in anchors), default=0) == 2
    ]
    recombination_set = set(recombinations)
    evaluated = [
        candidate
        for candidate in all_candidates
        if adaptive_search.candidate_key(candidate) in evaluated_keys
    ]
    unseen = [
        candidate
        for candidate in all_candidates
        if adaptive_search.candidate_key(candidate) not in evaluated_keys
        and candidate not in anchors
        and candidate not in neighbor_set
        and candidate not in recombination_set
    ]

    prior_by_key = {}
    for row in rows:
        current = prior_by_key.get(row["candidate_key"])
        if current is None or ranking_key(row) < ranking_key(current):
            prior_by_key[row["candidate_key"]] = row

    def prior_rank(candidate):
        row = prior_by_key.get(adaptive_search.candidate_key(candidate))
        return ranking_key(row) if row is not None else (0, 0, 0.0, 0.0, adaptive_search.candidate_key(candidate))

    neighborhood.sort(
        key=lambda candidate: (
            adaptive_search.candidate_key(candidate) in evaluated_keys,
            -role_score(candidate, anchors),
            prior_rank(candidate),
            adaptive_search.candidate_key(candidate),
        )
    )
    recombinations.sort(
        key=lambda candidate: (
            adaptive_search.candidate_key(candidate) in evaluated_keys,
            -role_score(candidate, anchors),
            adaptive_search.candidate_key(candidate),
        )
    )
    neighbor_candidates = select_diverse(neighborhood, neighbor_count, seed + 1)
    recombination_candidates = select_diverse(recombinations, recombination_count, seed + 2)
    unseen_candidates = select_diverse(unseen, unseen_count, seed + 3)
    selected = anchors + neighbor_candidates + recombination_candidates + unseen_candidates
    if len(selected) < target_count:
        fallback = [candidate for candidate in all_candidates if candidate not in selected]
        selected = select_diverse(fallback, target_count, seed + 4, initial=selected)
    else:
        selected = selected[:target_count]
    if len(selected) != target_count:
        raise ValueError(f"{scenario_key} produced {len(selected)} candidates; expected {target_count}.")

    sources = {}
    for candidate in selected:
        key = adaptive_search.candidate_key(candidate)
        if candidate in anchors:
            source = "prior_anchor"
        elif candidate in neighbor_set:
            source = "one_role_neighbor"
        elif candidate in recombination_set:
            source = "role_recombination"
        elif key in evaluated_keys:
            source = "previously_evaluated"
        else:
            source = "unseen_diverse"
        sources[key] = source
    return selected, {
        "PriorAnchors": [adaptive_search.candidate_key(candidate) for candidate in anchors],
        "PreviouslyEvaluatedCount": len(evaluated_keys),
        "PoolCounts": {
            "OneRoleNeighbors": len(neighborhood),
            "RoleRecombinations": len(recombinations),
            "Unseen": len(unseen),
            "PreviouslyEvaluated": len(evaluated),
        },
        "Sources": sources,
    }


def resolve_settings(config, args):
    search = config.get("PriorGuidedSearch") or {}
    batch = config.get("Batch") or {}
    if search.get("CandidateCount", 100) < 1:
        raise ValueError("CandidateCount must be positive.")
    if search.get("ScreenMatches", 3) < 1 or search.get("FinalMatches", 20) < 1:
        raise ValueError("ScreenMatches and FinalMatches must be positive.")
    parallel = search.get("ParallelProcesses", 5)
    matches_per_job = search.get("MatchesPerJob", 5)
    worker_count = search.get("JobWorkerCount", 1)
    if parallel != 5:
        raise ValueError("Prior-guided search requires ParallelProcesses=5.")
    if config.get("TimeScale", 6) != 6:
        raise ValueError("Prior-guided search requires TimeScale=6.")
    if config.get("PreserveFixedDeltaTime", False):
        raise ValueError("Prior-guided search requires PreserveFixedDeltaTime=false.")
    return {
        "party_size": 5,
        "candidate_count": int(search.get("CandidateCount", 100)),
        "anchor_count": int(search.get("AnchorCandidates", 10)),
        "neighbor_count": int(search.get("NeighborCandidates", 25)),
        "recombination_count": int(search.get("RecombinationCandidates", 25)),
        "unseen_count": int(search.get("UnseenCandidates", 40)),
        "screen_matches": int(search.get("ScreenMatches", 3)),
        "final_count": int(search.get("FinalCandidates", 10)),
        "final_matches": int(search.get("FinalMatches", 20)),
        "selection_seed": int(search.get("SelectionSeed", 260826)),
        "parallel": int(parallel),
        "matches_per_job": int(matches_per_job),
        "job_worker_count": int(worker_count),
        "time_scale": 6.0,
        "executable": args.executable or pathlib.Path(
            batch.get("Executable", ".unity/CombatAutoBattleLight/CombatAutoBattleLight.app")
        ),
    }


def base_config(config):
    ignored = {"PriorGuidedSearch", "Batch", "Scenarios", "MapNames"}
    return {key: value for key, value in config.items() if key not in ignored}


def final_selection(screening, scenarios, candidate_map, final_count):
    selected_by_scenario = {}
    for scenario in scenarios:
        scenario_id = scenario["Id"]
        ranking = sorted(screening[scenario_id].values(), key=adaptive_search.ranking_key)
        selected_by_scenario[scenario_id] = [
            next(candidate for candidate in candidate_map[scenario_id] if adaptive_search.candidate_key(candidate) == result["CandidateKey"])
            for result in ranking[:final_count]
        ]
    return selected_by_scenario


def write_final_report(run_directory, scenarios, screening, final, selected):
    scenario_reports = []
    for scenario in scenarios:
        scenario_id = scenario["Id"]
        scenario_reports.append(
            {
                "Scenario": scenario,
                "ScreeningRanking": sorted(screening[scenario_id].values(), key=adaptive_search.ranking_key),
                "FinalRanking": sorted(final[scenario_id].values(), key=adaptive_search.ranking_key),
            }
        )
    write_json_atomic(
        run_directory / "final-results.json",
        {
            "Status": "COMPLETED",
            "ScreenMatchesPerCandidate": 3,
            "FinalMatchesPerCandidate": 20,
            "Scenarios": scenario_reports,
            "FinalSelection": {
                scenario_id: [adaptive_search.candidate_key(candidate) for candidate in candidates]
                for scenario_id, candidates in selected.items()
            },
        },
    )


def main():
    args = parse_args()
    config = load_json(args.config)
    prior_results = load_prior_results(args.prior_database)
    scenarios = adaptive_search.build_scenarios(config)
    settings = resolve_settings(config, args)
    all_candidates = adaptive_search.enumerate_candidates(settings["party_size"])
    category_count = (
        settings["anchor_count"]
        + settings["neighbor_count"]
        + settings["recombination_count"]
        + settings["unseen_count"]
    )
    if category_count != settings["candidate_count"]:
        raise ValueError(
            "AnchorCandidates + NeighborCandidates + RecombinationCandidates + "
            "UnseenCandidates must equal CandidateCount."
        )
    candidate_map = {}
    generation = {
        "PriorDatabase": str(args.prior_database.resolve()),
        "CandidateCount": settings["candidate_count"],
        "Scenarios": {},
    }
    for index, scenario in enumerate(scenarios):
        scenario_id = scenario["Id"]
        candidates, metadata = build_candidates_for_scenario(
            scenario_id,
            prior_results,
            all_candidates,
            settings["candidate_count"],
            settings["anchor_count"],
            settings["neighbor_count"],
            settings["recombination_count"],
            settings["unseen_count"],
            settings["selection_seed"] + index,
        )
        candidate_map[scenario_id] = candidates
        generation["Scenarios"][scenario_id] = {
            "Scenario": scenario,
            **metadata,
            "Candidates": [adaptive_search.candidate_key(candidate) for candidate in candidates],
        }

    run_directory = args.output_directory
    run_directory.mkdir(parents=True, exist_ok=True)
    write_json_atomic(run_directory / "candidate-generation.json", generation)
    if args.plan_only:
        print(f"Candidate plan: {run_directory / 'candidate-generation.json'}")
        return 0

    run_directory = adaptive_search.prepare_run_directory(
        config,
        settings,
        scenarios,
        all_candidates,
        run_directory,
    )
    common_config = base_config(config)
    print(f"Run directory: {run_directory}")
    print(f"Scenario-specific candidates: {settings['candidate_count']} x {len(scenarios)}")

    screening = adaptive_search.run_stage(
        common_config,
        run_directory,
        scenarios,
        candidate_map,
        settings,
        "screening",
        settings["screen_matches"],
        0,
        settings["screen_matches"],
    )
    selected = final_selection(screening, scenarios, candidate_map, settings["final_count"])
    write_json_atomic(
        run_directory / "selection.json",
        {
            "Final": {
                scenario_id: [adaptive_search.candidate_key(candidate) for candidate in candidates]
                for scenario_id, candidates in selected.items()
            }
        },
    )
    final = adaptive_search.run_stage(
        common_config,
        run_directory,
        scenarios,
        selected,
        settings,
        "final",
        settings["final_matches"],
        settings["screen_matches"],
        settings["screen_matches"] + settings["final_matches"],
    )
    write_final_report(run_directory, scenarios, screening, final, selected)
    print(f"Completed: {run_directory / 'final-results.json'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
