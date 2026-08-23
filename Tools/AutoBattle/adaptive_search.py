#!/usr/bin/env python3

"""Run a resumable, model-based auto-battle search.

The Unity Player performs only the requested matches. Candidate generation,
feature extraction, model fitting, and stage selection stay outside Unity so
the unmeasured candidate set can be ranked without starting more battles.
"""

import argparse
import concurrent.futures
import hashlib
import importlib.util
import itertools
import json
import pathlib
import random
import re
import sys
import time


SCRIPT_DIRECTORY = pathlib.Path(__file__).resolve().parent
BACKEND_PATH = SCRIPT_DIRECTORY / "batch_battles.py"
BACKEND_SPEC = importlib.util.spec_from_file_location("batch_battles", BACKEND_PATH)
if BACKEND_SPEC is None or BACKEND_SPEC.loader is None:
    raise RuntimeError(f"Unable to load batch backend: {BACKEND_PATH}")
batch_battles = importlib.util.module_from_spec(BACKEND_SPEC)
BACKEND_SPEC.loader.exec_module(batch_battles)


RUNNER_VERSION = 1
DEFAULT_OUTPUT_ROOT = pathlib.Path("Logs/AutoBattleAdaptive")
DEFAULT_EXECUTABLE = pathlib.Path(".unity/CombatAutoBattleLight/CombatAutoBattleLight.app")

WEAPON_NAMES = {
    1: "Sword",
    2: "Shield",
    3: "Wand",
    4: "Grimoire",
    5: "Bible",
    6: "Rosary",
}
PERSONALITY_NAMES = {
    1: "AttentionSeeker",
    2: "BattleJunkie",
    7: "Cunning",
    9: "Devoted",
    16: "Lonely",
    19: "Reckless",
}

ROLE_OPTIONS = tuple(
    [(1, personality) for personality in (2, 7, 19)]
    + [(2, personality) for personality in (1, 9, 16)]
    + [(3, personality) for personality in (2, 7, 19)]
    + [(4, personality) for personality in (9, 16)]
    + [(5, personality) for personality in (9, 16)]
    + [(6, personality) for personality in (9, 16)]
)
ROLE_INDEX = {role: index for index, role in enumerate(ROLE_OPTIONS)}
PAIR_OPTIONS = tuple(itertools.combinations_with_replacement(range(len(ROLE_OPTIONS)), 2))
PAIR_INDEX = {pair: 1 + len(ROLE_OPTIONS) + index for index, pair in enumerate(PAIR_OPTIONS)}
FEATURE_COUNT = 1 + len(ROLE_OPTIONS) + len(PAIR_OPTIONS)
FIXED_ENEMY = ((3, 0), (3, 0), (5, 9), (6, 9), (4, 2))


def parse_args():
    parser = argparse.ArgumentParser(
        description="Run a resumable 300 -> 100 -> 10 model-based auto-battle search."
    )
    parser.add_argument("--config", required=True, type=pathlib.Path)
    parser.add_argument("--executable", type=pathlib.Path)
    parser.add_argument("--output-directory", type=pathlib.Path)
    parser.add_argument("--parallel", type=int)
    parser.add_argument("--job-worker-count", type=int)
    parser.add_argument("--matches-per-job", type=int)
    return parser.parse_args()


def load_json(path):
    with path.open(encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"JSON root must be an object: {path}")
    return value


def write_json_atomic(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def role_to_json(role):
    return {"Weapon": role[0], "Personality": role[1]}


def candidate_to_json(candidate):
    return {"Roles": [role_to_json(role) for role in candidate]}


def candidate_key(candidate):
    return "|".join(f"{weapon}:{personality}" for weapon, personality in candidate)


def is_legal_weapon_composition(candidate):
    weapons = [role[0] for role in candidate]
    sword_wand = sum(weapon in (1, 3) for weapon in weapons)
    return (
        sword_wand >= 2
        and weapons.count(2) <= 2
        and weapons.count(4) <= 2
        and weapons.count(5) <= 2
    )


def enumerate_candidates(party_size=5):
    if party_size < 1:
        raise ValueError("PartySize must be positive.")
    candidates = []
    for candidate in itertools.combinations_with_replacement(ROLE_OPTIONS, party_size):
        if is_legal_weapon_composition(candidate):
            candidates.append(candidate)
    return candidates


def feature_counts(candidate):
    counts = {}
    for role in candidate:
        counts[ROLE_INDEX[role] + 1] = counts.get(ROLE_INDEX[role] + 1, 0) + 1
    for left, right in itertools.combinations_with_replacement(
        (ROLE_INDEX[role] for role in candidate), 2
    ):
        pair = (left, right) if left <= right else (right, left)
        feature = PAIR_INDEX[pair]
        counts[feature] = counts.get(feature, 0) + 1
    return tuple(sorted(counts.items()))


def select_balanced_candidates(candidates, count, seed):
    if count < 1 or count > len(candidates):
        raise ValueError(f"InitialCandidates must be between 1 and {len(candidates)}.")

    features = [feature_counts(candidate) for candidate in candidates]
    tie_order = list(range(len(candidates)))
    random.Random(seed).shuffle(tie_order)
    tie_rank = {candidate_index: rank for rank, candidate_index in enumerate(tie_order)}
    feature_usage = [0] * FEATURE_COUNT
    remaining = set(range(len(candidates)))
    selected = []

    for _ in range(count):
        best_index = max(
            remaining,
            key=lambda index: (
                sum(1.0 / (1.0 + feature_usage[feature]) for feature, _ in features[index]),
                sum(1.0 / (1.0 + feature_usage[feature]) for feature, value in features[index] if feature > len(ROLE_OPTIONS)),
                -tie_rank[index],
            ),
        )
        selected.append(best_index)
        remaining.remove(best_index)
        for feature, value in features[best_index]:
            feature_usage[feature] += value
    return selected


def solve_linear_system(matrix, vector):
    size = len(vector)
    augmented = [matrix[row][:] + [vector[row]] for row in range(size)]
    for column in range(size):
        pivot = max(range(column, size), key=lambda row: abs(augmented[row][column]))
        pivot_value = augmented[pivot][column]
        if abs(pivot_value) < 1e-12:
            raise ValueError("Model feature matrix is singular.")
        augmented[column], augmented[pivot] = augmented[pivot], augmented[column]
        pivot_value = augmented[column][column]
        for entry in range(column, size + 1):
            augmented[column][entry] /= pivot_value
        for row in range(size):
            if row == column:
                continue
            factor = augmented[row][column]
            if factor == 0.0:
                continue
            for entry in range(column, size + 1):
                augmented[row][entry] -= factor * augmented[column][entry]
    return [augmented[row][size] for row in range(size)]


def fit_model(candidates, observations, ridge=1.0):
    if ridge < 0.0:
        raise ValueError("Ridge must not be negative.")
    features = [feature_counts(candidate) for candidate in candidates]
    matrix = [[0.0] * FEATURE_COUNT for _ in range(FEATURE_COUNT)]
    vector = [0.0] * FEATURE_COUNT
    for candidate_index, outcome in observations:
        row = ((0, 1),) + features[candidate_index]
        for feature, value in row:
            vector[feature] += value * outcome
            for other_feature, other_value in row:
                matrix[feature][other_feature] += value * other_value
    for feature in range(1, FEATURE_COUNT):
        matrix[feature][feature] += ridge
    coefficients = solve_linear_system(matrix, vector)
    return coefficients, features


def predict_scores(candidates, coefficients, features=None):
    features = features or [feature_counts(candidate) for candidate in candidates]
    return [
        coefficients[0] + sum(coefficients[feature] * value for feature, value in row)
        for row in features
    ]


def role_label(role):
    return f"{WEAPON_NAMES[role[0]]}+{PERSONALITY_NAMES[role[1]]}"


def model_summary(coefficients):
    return {
        "Intercept": coefficients[0],
        "RoleEffects": [
            {
                "Weapon": role[0],
                "Personality": role[1],
                "Label": role_label(role),
                "Coefficient": coefficients[index + 1],
            }
            for index, role in enumerate(ROLE_OPTIONS)
        ],
        "RolePairEffects": [
            {
                "Left": role_label(ROLE_OPTIONS[left]),
                "Right": role_label(ROLE_OPTIONS[right]),
                "Coefficient": coefficients[PAIR_INDEX[(left, right)]],
            }
            for left, right in PAIR_OPTIONS
        ],
    }


def outcome_value(candidate_result):
    matches = candidate_result.get("MatchCount", 0)
    if matches <= 0:
        return 0.0
    return (candidate_result.get("Wins", 0) - candidate_result.get("Timeouts", 0)) / matches


def empty_stats(candidate):
    return {
        "CandidateKey": candidate_key(candidate),
        "Roles": [role_to_json(role) for role in candidate],
        "MatchCount": 0,
        "Wins": 0,
        "Losses": 0,
        "Timeouts": 0,
        "TotalGameSeconds": 0.0,
        "TotalRealSeconds": 0.0,
    }


def add_stats(destination, source):
    for field in ("MatchCount", "Wins", "Losses", "Timeouts"):
        destination[field] += source.get(field, 0)
    for field in ("TotalGameSeconds", "TotalRealSeconds"):
        destination[field] += source.get(field, 0.0)


def aggregate_stage_reports(job_results, candidates_by_scenario):
    aggregated = {}
    for job_result in job_results:
        if job_result["status"] == "failed":
            continue
        scenario_id = job_result["scenario_id"]
        scenario_candidates = candidates_by_scenario[scenario_id]
        scenario_stats = aggregated.setdefault(scenario_id, {})
        for result in job_result["report"].get("Ranking", []):
            key = result["CandidateKey"]
            if key not in scenario_stats:
                candidate = scenario_candidates[ key ]
                scenario_stats[key] = empty_stats(candidate)
            add_stats(scenario_stats[key], result)
    for scenario_id, candidate_map in aggregated.items():
        expected = candidates_by_scenario[scenario_id]
        if set(candidate_map) != set(expected):
            missing = sorted(set(expected) - set(candidate_map))
            raise ValueError(f"Scenario {scenario_id} is missing candidates: {missing[:3]}")
    return aggregated


def combine_stats(first, second):
    combined = {key: dict(value) for key, value in first.items()}
    for key, value in second.items():
        if key not in combined:
            combined[key] = dict(value)
        else:
            add_stats(combined[key], value)
    return combined


def ranking_key(result, model_score=0.0):
    return (
        -result.get("Wins", 0),
        result.get("Timeouts", 0),
        -outcome_value(result),
        -model_score,
        result["CandidateKey"],
    )


def info_scores(candidate_indices, features, support):
    return {
        index: sum(1.0 / (1.0 + support[feature]) for feature, _ in features[index])
        for index in candidate_indices
    }


def neighbor_indices(seed_indices, candidates, candidate_lookup, excluded):
    neighbors = set()
    for seed_index in seed_indices:
        roles = list(candidates[seed_index])
        for position in range(len(roles)):
            for replacement in ROLE_OPTIONS:
                if replacement == roles[position]:
                    continue
                changed = tuple(sorted(roles[:position] + roles[position + 1 :] + [replacement]))
                index = candidate_lookup.get(changed)
                if index is not None and index not in excluded:
                    neighbors.add(index)
    return neighbors


def select_probe_candidates(candidates, scores, initial_indices, top_count, low_count, neighbor_count):
    features = [feature_counts(candidate) for candidate in candidates]
    support = [0] * FEATURE_COUNT
    for index in initial_indices:
        for feature, value in features[index]:
            support[feature] += value
    initial_set = set(initial_indices)
    candidate_lookup = {candidate: index for index, candidate in enumerate(candidates)}
    ranking = sorted(
        range(len(candidates)),
        key=lambda index: (-scores[index], candidate_key(candidates[index])),
    )
    chosen = []

    predicted = [index for index in ranking if index not in initial_set][:top_count]
    chosen.extend(predicted)

    available = [
        index for index in range(len(candidates)) if index not in initial_set and index not in set(chosen)
    ]
    information = info_scores(available, features, support)
    low_information = sorted(
        available,
        key=lambda index: (-information[index], -scores[index], candidate_key(candidates[index])),
    )[:low_count]
    chosen.extend(low_information)

    excluded = initial_set | set(chosen)
    neighbor_pool = neighbor_indices(predicted, candidates, candidate_lookup, excluded)
    neighbors = sorted(
        neighbor_pool,
        key=lambda index: (-scores[index], candidate_key(candidates[index])),
    )[:neighbor_count]
    chosen.extend(neighbors)

    for index in ranking:
        if len(chosen) >= top_count + low_count + neighbor_count:
            break
        if index not in initial_set and index not in set(chosen):
            chosen.append(index)
    return {
        "PredictedTop": predicted,
        "LowInformation": low_information,
        "RoleNeighbors": neighbors,
        "Probe": chosen,
    }


def build_scenarios(config):
    explicit = config.get("Scenarios")
    if explicit:
        scenarios = []
        for index, scenario in enumerate(explicit):
            if not isinstance(scenario, dict) or not scenario.get("MapName"):
                raise ValueError("Every Scenarios entry requires MapName.")
            reversed_position = bool(scenario.get("StonePositionsReversed", False))
            scenario_id = scenario.get("Id") or f"{scenario['MapName']}_{'reversed' if reversed_position else 'normal'}"
            scenarios.append(
                {
                    "Id": str(scenario_id),
                    "MapName": scenario["MapName"],
                    "StonePositionsReversed": reversed_position,
                }
            )
        return scenarios

    maps = config.get("MapNames") or []
    if not maps:
        raise ValueError("MapNames or Scenarios is required.")
    evaluate_both = config.get("EvaluateBothStonePositions", True)
    positions = (False, True) if evaluate_both else (False,)
    scenarios = []
    for map_name in maps:
        for reversed_position in positions:
            scenarios.append(
                {
                    "Id": f"{map_name}_{'reversed' if reversed_position else 'normal'}",
                    "MapName": map_name,
                    "StonePositionsReversed": reversed_position,
                }
            )
    return scenarios


def resolve_settings(config, args, candidate_count):
    adaptive = config.get("AdaptiveSearch") or config.get("Adaptive") or {}
    batch = config.get("Batch") or {}
    if not isinstance(adaptive, dict) or not isinstance(batch, dict):
        raise ValueError("AdaptiveSearch and Batch must be objects.")

    def positive(name, default):
        value = adaptive.get(name, default)
        if not isinstance(value, int) or value < 1:
            raise ValueError(f"{name} must be a positive integer.")
        return value

    initial_count = positive("InitialCandidates", 300)
    probe_count = positive("ProbeCandidates", 100)
    final_count = positive("FinalCandidates", 10)
    top_count = positive("TopPredicted", 60)
    low_count = positive("LowInformation", 20)
    neighbor_count = positive("RoleNeighbors", 20)
    if top_count + low_count + neighbor_count != probe_count:
        raise ValueError("TopPredicted + LowInformation + RoleNeighbors must equal ProbeCandidates.")
    if initial_count > candidate_count:
        raise ValueError(f"InitialCandidates must not exceed {candidate_count}.")
    if probe_count > candidate_count - initial_count:
        raise ValueError("ProbeCandidates must fit outside the initial candidate set.")
    if final_count > probe_count:
        raise ValueError("FinalCandidates must not exceed ProbeCandidates.")

    parallel = args.parallel if args.parallel is not None else adaptive.get("ParallelProcesses", batch.get("ParallelProcesses", 5))
    matches_per_job = args.matches_per_job if args.matches_per_job is not None else adaptive.get("MatchesPerJob", batch.get("MatchesPerJob", 5))
    job_worker_count = args.job_worker_count if args.job_worker_count is not None else adaptive.get("JobWorkerCount", batch.get("JobWorkerCount"))
    for name, value in (("ParallelProcesses", parallel), ("MatchesPerJob", matches_per_job)):
        if not isinstance(value, int) or value < 1:
            raise ValueError(f"{name} must be a positive integer.")
    if job_worker_count is not None and (not isinstance(job_worker_count, int) or job_worker_count < 1):
        raise ValueError("JobWorkerCount must be a positive integer when specified.")

    return {
        "party_size": positive("PartySize", 5),
        "initial_count": initial_count,
        "probe_count": probe_count,
        "final_count": final_count,
        "top_count": top_count,
        "low_count": low_count,
        "neighbor_count": neighbor_count,
        "probe_matches": positive("ProbeMatches", 3),
        "final_matches": positive("FinalMatches", 20),
        "selection_seed": adaptive.get("SelectionSeed", 12000),
        "ridge": adaptive.get("Ridge", 1.0),
        "parallel": parallel,
        "matches_per_job": matches_per_job,
        "job_worker_count": job_worker_count,
        "time_scale": config.get("TimeScale", 6.0),
        "executable": args.executable or pathlib.Path(batch.get("Executable", DEFAULT_EXECUTABLE)),
        "output_root": pathlib.Path(batch.get("OutputRoot", DEFAULT_OUTPUT_ROOT)),
    }


def logical_fingerprint(config, settings, scenarios, candidates):
    fingerprint_input = {
        "RunnerVersion": RUNNER_VERSION,
        "Config": config,
        "PartySize": settings["party_size"],
        "Scenarios": scenarios,
        "CandidateCount": len(candidates),
        "RoleOptions": ROLE_OPTIONS,
    }
    encoded = json.dumps(fingerprint_input, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def prepare_run_directory(config, settings, scenarios, candidates, requested_directory):
    run_directory = requested_directory or settings["output_root"] / time.strftime("%Y%m%d_%H%M%S")
    run_directory.mkdir(parents=True, exist_ok=True)
    fingerprint = logical_fingerprint(config, settings, scenarios, candidates)
    manifest_path = run_directory / "manifest.json"
    if manifest_path.exists():
        manifest = load_json(manifest_path)
        if manifest.get("Fingerprint") != fingerprint:
            raise ValueError(f"Output directory belongs to a different search: {run_directory}")
    else:
        write_json_atomic(
            manifest_path,
            {
                "Fingerprint": fingerprint,
                "RunnerVersion": RUNNER_VERSION,
                "ScenarioCount": len(scenarios),
                "CandidateCount": len(candidates),
            },
        )
    return run_directory


def slug(value):
    return re.sub(r"[^A-Za-z0-9_-]+", "_", value).strip("_") or "scenario"


def make_stage_config(base_config, scenario, candidates, settings, matches, match_offset, total_matches):
    ignored = {"AdaptiveSearch", "Adaptive", "Batch", "Scenarios", "MapNames"}
    stage_config = {key: value for key, value in base_config.items() if key not in ignored}
    stage_config.update(
        {
            "MapNames": [scenario["MapName"]],
            "Candidates": [candidate_to_json(candidate) for candidate in candidates],
            "Enemy": [role_to_json(role) for role in FIXED_ENEMY],
            "CandidateCount": len(candidates),
            "MatchCount": matches,
            "BaseSeed": base_config.get("BaseSeed", 12000),
            "MinPartySize": settings["party_size"],
            "MaxPartySize": settings["party_size"],
            "MatchesPerCandidate": matches,
            "MatchOffset": match_offset,
            "TotalMatchesPerCandidate": total_matches,
            "EvaluateBothStonePositions": False,
            "UseFixedStonePosition": True,
            "StonePositionsReversed": scenario["StonePositionsReversed"],
            "UseCommonSeeds": True,
            "DisableDiagnostics": base_config.get("DisableDiagnostics", True),
            "TimeScale": base_config.get("TimeScale", settings["time_scale"]),
        }
    )
    return stage_config


def build_stage_jobs(base_config, scenarios, candidates_by_scenario, settings, stage_name, matches, match_offset, total_matches):
    jobs = []
    for scenario in scenarios:
        scenario_id = scenario["Id"]
        candidates = candidates_by_scenario[scenario_id]
        for offset in range(0, matches, settings["matches_per_job"]):
            count = min(settings["matches_per_job"], matches - offset)
            actual_offset = match_offset + offset
            end = actual_offset + count
            job_id = f"{slug(scenario_id)}_{stage_name}_matches_{actual_offset}_{end}"
            jobs.append(
                {
                    "id": job_id,
                    "scenario_id": scenario_id,
                    "match_count": count,
                    "config": make_stage_config(
                        base_config,
                        scenario,
                        candidates,
                        settings,
                        count,
                        actual_offset,
                        total_matches,
                    ),
                }
            )
    return jobs


def run_jobs(executable, run_directory, jobs, settings):
    results = []
    executor = concurrent.futures.ThreadPoolExecutor(max_workers=settings["parallel"])
    futures = {
        executor.submit(
            batch_battles.run_job,
            executable,
            run_directory,
            job,
            settings["job_worker_count"],
            1,
        ): job
        for job in jobs
    }
    try:
        for future in concurrent.futures.as_completed(futures):
            job = futures[future]
            result = future.result()
            result["scenario_id"] = job["scenario_id"]
            results.append(result)
            print(f"{result['status']}: {job['id']}", flush=True)
    except KeyboardInterrupt:
        batch_battles.terminate_active_processes()
        for future in futures:
            future.cancel()
        executor.shutdown(wait=True, cancel_futures=True)
        raise
    else:
        executor.shutdown(wait=True)
    failures = [result for result in results if result["status"] == "failed"]
    if failures:
        messages = [failure.get("error", {}).get("Message", failure.get("error", {})) for failure in failures]
        raise RuntimeError(f"{len(failures)} auto-battle jobs failed: {messages[:3]}")
    return results


def run_stage(base_config, run_directory, scenarios, candidates_by_scenario, settings, stage_name, matches, match_offset, total_matches):
    stage_directory = run_directory / "stages" / stage_name
    jobs = build_stage_jobs(
        base_config,
        scenarios,
        candidates_by_scenario,
        settings,
        stage_name,
        matches,
        match_offset,
        total_matches,
    )
    executable = batch_battles.resolve_executable(settings["executable"])
    started = time.monotonic()
    job_results = run_jobs(executable, stage_directory, jobs, settings)
    elapsed = time.monotonic() - started
    aggregated = aggregate_stage_reports(
        job_results,
        {
            scenario_id: {candidate_key(candidate): candidate for candidate in candidates}
            for scenario_id, candidates in candidates_by_scenario.items()
        },
    )
    write_json_atomic(
        run_directory / f"{stage_name}-results.json",
        {
            "Stage": stage_name,
            "MatchesPerCandidate": matches,
            "MatchOffset": match_offset,
            "ElapsedSeconds": elapsed,
            "Jobs": [
                {"Id": job["id"], "ScenarioId": job["scenario_id"], "Status": result["status"]}
                for job, result in ((job, next(item for item in job_results if item.get("id") == job["id"])) for job in jobs)
            ],
            "Scenarios": aggregated,
        },
    )
    return aggregated


def stats_by_key(stage_stats, scenario_id):
    return stage_stats.get(scenario_id, {})


def make_observations(stage_stats, scenario_id, expected_count, candidate_lookup):
    observations = []
    for key, result in stats_by_key(stage_stats, scenario_id).items():
        index = candidate_lookup[key]
        observations.append((index, outcome_value(result)))
    if len(observations) != expected_count:
        raise ValueError(f"Scenario {scenario_id} does not have one initial result for every candidate.")
    return observations


def build_models_and_probe_selection(initial_stats, scenarios, candidates, settings):
    candidate_lookup = {candidate_key(candidate): index for index, candidate in enumerate(candidates)}
    all_features = [feature_counts(candidate) for candidate in candidates]
    initial_indices = select_balanced_candidates(candidates, settings["initial_count"], settings["selection_seed"])
    candidates_by_scenario = {}
    model_reports = {}
    selections = {"Initial": initial_indices, "Scenarios": {}}
    for scenario in scenarios:
        scenario_id = scenario["Id"]
        observations = make_observations(initial_stats, scenario_id, settings["initial_count"], candidate_lookup)
        coefficients, _ = fit_model(candidates, observations, settings["ridge"])
        scores = predict_scores(candidates, coefficients, all_features)
        selection = select_probe_candidates(
            candidates,
            scores,
            initial_indices,
            settings["top_count"],
            settings["low_count"],
            settings["neighbor_count"],
        )
        probe_indices = selection["Probe"]
        if len(probe_indices) != settings["probe_count"]:
            raise ValueError(f"Scenario {scenario_id} selected {len(probe_indices)} probe candidates.")
        candidates_by_scenario[scenario_id] = [candidates[index] for index in probe_indices]
        model_reports[scenario_id] = {
            "Scenario": scenario,
            "Model": model_summary(coefficients),
            "ProbePredictions": {
                candidate_key(candidates[index]): scores[index]
                for index in probe_indices
            },
            "TopPredictions": [
                {
                    "CandidateKey": candidate_key(candidates[index]),
                    "Roles": [role_to_json(role) for role in candidates[index]],
                    "PredictedScore": scores[index],
                }
                for index in sorted(range(len(candidates)), key=lambda index: (-scores[index], candidate_key(candidates[index])))[: settings["probe_count"]]
            ],
        }
        selections["Scenarios"][scenario_id] = {
            "PredictedTop": [candidate_key(candidates[index]) for index in selection["PredictedTop"]],
            "LowInformation": [candidate_key(candidates[index]) for index in selection["LowInformation"]],
            "RoleNeighbors": [candidate_key(candidates[index]) for index in selection["RoleNeighbors"]],
            "Probe": [candidate_key(candidates[index]) for index in probe_indices],
        }
    return initial_indices, candidates_by_scenario, model_reports, selections


def select_final_candidates(
    initial_stats,
    probe_stats,
    scenarios,
    initial_candidates_by_scenario,
    candidates_by_scenario,
    settings,
    model_reports,
):
    final_by_scenario = {}
    final_selection = {}
    for scenario in scenarios:
        scenario_id = scenario["Id"]
        cumulative = combine_stats(stats_by_key(initial_stats, scenario_id), stats_by_key(probe_stats, scenario_id))
        predicted_scores = model_reports[scenario_id]["ProbePredictions"]
        ranking = sorted(
            cumulative.values(),
            key=lambda result: ranking_key(result, predicted_scores.get(result["CandidateKey"], 0.0)),
        )
        selected = ranking[: settings["final_count"]]
        candidate_pool = (
            initial_candidates_by_scenario[scenario_id] + candidates_by_scenario[scenario_id]
        )
        final_by_scenario[scenario_id] = [
            next(candidate for candidate in candidate_pool if candidate_key(candidate) == result["CandidateKey"])
            for result in selected
        ]
        final_selection[scenario_id] = [result["CandidateKey"] for result in selected]
    return final_by_scenario, final_selection


def final_report(final_stats, scenarios, final_matches, run_directory):
    scenario_reports = []
    for scenario in scenarios:
        scenario_id = scenario["Id"]
        ranking = sorted(stats_by_key(final_stats, scenario_id).values(), key=ranking_key)
        scenario_reports.append(
            {
                "Scenario": scenario,
                "Ranking": ranking,
            }
        )
    report = {
        "Status": "COMPLETED",
        "ScenarioCount": len(scenarios),
        "FinalMatchesPerCandidate": final_matches,
        "Scenarios": scenario_reports,
    }
    write_json_atomic(run_directory / "final-results.json", report)
    return report


def main():
    args = parse_args()
    config = load_json(args.config)
    scenarios = build_scenarios(config)
    preliminary_party_size = int((config.get("AdaptiveSearch") or config.get("Adaptive") or {}).get("PartySize", 5))
    candidates = enumerate_candidates(preliminary_party_size)
    settings = resolve_settings(config, args, len(candidates))
    if settings["party_size"] != preliminary_party_size:
        candidates = enumerate_candidates(settings["party_size"])
        settings = resolve_settings(config, args, len(candidates))
    if settings["selection_seed"] is None:
        raise ValueError("SelectionSeed must be specified when omitted is not supported.")
    if not isinstance(settings["ridge"], (int, float)) or settings["ridge"] < 0:
        raise ValueError("Ridge must be a non-negative number.")
    run_directory = prepare_run_directory(config, settings, scenarios, candidates, args.output_directory)
    initial_candidates = [candidates[index] for index in select_balanced_candidates(candidates, settings["initial_count"], settings["selection_seed"])]
    initial_by_scenario = {scenario["Id"]: initial_candidates for scenario in scenarios}

    print(f"Run directory: {run_directory}")
    print(f"Candidates: {len(candidates)}; scenarios: {len(scenarios)}")
    initial_stats = run_stage(
        config,
        run_directory,
        scenarios,
        initial_by_scenario,
        settings,
        "initial",
        1,
        0,
        1,
    )
    write_json_atomic(
        run_directory / "selection.json",
        {
            "Initial": [candidate_key(candidate) for candidate in initial_candidates],
        },
    )

    initial_indices, probe_by_scenario, model_reports, selections = build_models_and_probe_selection(
        initial_stats,
        scenarios,
        candidates,
        settings,
    )
    write_json_atomic(
        run_directory / "models.json",
        {
            "CandidateCount": len(candidates),
            "InitialCandidates": [candidate_key(candidates[index]) for index in initial_indices],
            "Models": model_reports,
            "Selection": selections,
        },
    )
    probe_stats = run_stage(
        config,
        run_directory,
        scenarios,
        probe_by_scenario,
        settings,
        "probe",
        settings["probe_matches"],
        1,
        1 + settings["probe_matches"],
    )
    final_by_scenario, final_selection = select_final_candidates(
        initial_stats,
        probe_stats,
        scenarios,
        initial_by_scenario,
        probe_by_scenario,
        settings,
        model_reports,
    )
    write_json_atomic(
        run_directory / "selection.json",
        {
            "Initial": [candidate_key(candidate) for candidate in initial_candidates],
            "Probe": selections["Scenarios"],
            "Final": final_selection,
        },
    )
    final_stats = run_stage(
        config,
        run_directory,
        scenarios,
        final_by_scenario,
        settings,
        "final",
        settings["final_matches"],
        1 + settings["probe_matches"],
        1 + settings["probe_matches"] + settings["final_matches"],
    )
    final_report(final_stats, scenarios, settings["final_matches"], run_directory)
    print(f"Completed: {run_directory / 'final-results.json'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
