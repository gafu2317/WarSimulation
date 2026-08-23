#!/usr/bin/env python3

import argparse
import concurrent.futures
import copy
import datetime
import hashlib
import json
import pathlib
import re
import statistics
import subprocess
import sys
import threading
import time


DEFAULT_EXECUTABLE = pathlib.Path(".unity/CombatAutoBattleLight/CombatAutoBattleLight.app")
DEFAULT_OUTPUT_ROOT = pathlib.Path("Logs/AutoBattleBatch")
REPORT_SCHEMA_VERSION = 2
ACTIVE_PROCESSES = set()
ACTIVE_PROCESSES_LOCK = threading.Lock()


def parse_args():
    parser = argparse.ArgumentParser(description="Run configurable auto-battle jobs in parallel.")
    parser.add_argument("--config", required=True, type=pathlib.Path)
    parser.add_argument("--executable", type=pathlib.Path)
    parser.add_argument("--output-directory", type=pathlib.Path)
    parser.add_argument("--parallel", type=int)
    parser.add_argument("--time-scale", type=float)
    parser.add_argument("--matches-per-job", type=int)
    parser.add_argument("--job-worker-count", type=int)
    parser.add_argument("--smoke", action="store_true")
    return parser.parse_args()


def load_config(path):
    with path.open(encoding="utf-8") as source:
        config = json.load(source)
    if not isinstance(config, dict):
        raise ValueError("Config root must be an object.")
    return config


def resolve_settings(config, args):
    batch = config.get("Batch") or {}
    if not isinstance(batch, dict):
        raise ValueError("Batch must be an object.")

    settings = {
        "parallel": args.parallel if args.parallel is not None else batch.get("ParallelProcesses", 5),
        "time_scale": args.time_scale if args.time_scale is not None else config.get("TimeScale", 6.0),
        "matches_per_job": (
            args.matches_per_job
            if args.matches_per_job is not None
            else batch.get("MatchesPerJob", config.get("MatchesPerCandidate", 1))
        ),
        "job_worker_count": (
            args.job_worker_count
            if args.job_worker_count is not None
            else batch.get("JobWorkerCount")
        ),
        "map_execution": batch.get("MapExecution", "each"),
        "executable": args.executable or pathlib.Path(batch.get("Executable", DEFAULT_EXECUTABLE)),
        "output_root": pathlib.Path(batch.get("OutputRoot", DEFAULT_OUTPUT_ROOT)),
    }
    validate_settings(config, settings)
    return settings


def validate_settings(config, settings):
    if config.get("MatchesPerCandidate", 0) < 1:
        raise ValueError("MatchesPerCandidate must be positive.")
    if settings["parallel"] < 1:
        raise ValueError("ParallelProcesses must be positive.")
    if settings["time_scale"] <= 0:
        raise ValueError("TimeScale must be greater than zero.")
    if settings["matches_per_job"] < 1:
        raise ValueError("MatchesPerJob must be positive.")
    if settings["job_worker_count"] is not None and settings["job_worker_count"] < 1:
        raise ValueError("JobWorkerCount must be positive when specified.")
    if settings["map_execution"] not in ("each", "seeded"):
        raise ValueError("MapExecution must be 'each' or 'seeded'.")
    maps = config.get("MapNames") or []
    if settings["map_execution"] == "each" and not maps:
        raise ValueError("MapExecution='each' requires at least one MapNames entry.")
    if not config.get("Candidates") and not config.get("EnumerateAllCandidates"):
        raise ValueError("Specify Candidates or set EnumerateAllCandidates=true.")


def resolve_executable(path):
    if path.is_file():
        return path.resolve()
    macos_directory = path / "Contents" / "MacOS"
    if macos_directory.is_dir():
        candidates = [candidate for candidate in macos_directory.iterdir() if candidate.is_file()]
        if len(candidates) == 1:
            return candidates[0].resolve()
    raise FileNotFoundError(f"Auto-battle Player executable not found: {path}")


def build_jobs(config, settings):
    total_matches = config["MatchesPerCandidate"]
    matches_per_job = settings["matches_per_job"]
    maps = config.get("MapNames") or []
    map_groups = [[map_name] for map_name in maps] if settings["map_execution"] == "each" else [maps]
    jobs = []
    for map_names in map_groups:
        for match_offset in range(0, total_matches, matches_per_job):
            match_count = min(matches_per_job, total_matches - match_offset)
            job_config = copy.deepcopy(config)
            job_config.pop("Batch", None)
            job_config["MapNames"] = map_names
            job_config["TimeScale"] = settings["time_scale"]
            job_config["MatchesPerCandidate"] = match_count
            job_config["MatchOffset"] = match_offset
            job_config["TotalMatchesPerCandidate"] = total_matches
            map_label = "seeded" if settings["map_execution"] == "seeded" else map_names[0]
            jobs.append(
                {
                    "id": make_job_id(len(jobs), map_label, match_offset, match_count),
                    "config": job_config,
                    "match_count": match_count,
                }
            )
    return jobs


def make_job_id(index, map_label, match_offset, match_count):
    slug = re.sub(r"[^A-Za-z0-9_-]+", "_", map_label).strip("_") or "map"
    end = match_offset + match_count
    return f"job_{index:05d}_{slug}_matches_{match_offset}_{end}"


def config_fingerprint(config, settings, jobs):
    effective = {
        "Config": {key: value for key, value in config.items() if key != "Batch"},
        "Settings": {
            "TimeScale": settings["time_scale"],
            "MatchesPerJob": settings["matches_per_job"],
            "MapExecution": settings["map_execution"],
        },
        "Jobs": [{"Id": job["id"], "Config": job["config"]} for job in jobs],
    }
    encoded = json.dumps(effective, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest(), effective


def prepare_run_directory(config, settings, jobs, requested_directory):
    if requested_directory is None:
        stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        run_directory = settings["output_root"] / stamp
    else:
        run_directory = requested_directory
    run_directory.mkdir(parents=True, exist_ok=True)

    fingerprint, effective = config_fingerprint(config, settings, jobs)
    manifest_path = run_directory / "batch-config.json"
    if manifest_path.exists():
        existing = load_config(manifest_path)
        if existing.get("Fingerprint") != fingerprint:
            raise ValueError(f"Output directory belongs to a different config: {run_directory}")
    else:
        write_json_atomic(
            manifest_path,
            {"Fingerprint": fingerprint, "Effective": effective},
        )
    return run_directory


def write_json_atomic(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def load_completed_job(job_directory, expected_match_count, position_count):
    marker_path = job_directory / "complete.json"
    if not marker_path.is_file():
        return None
    try:
        marker = load_config(marker_path)
        report_path = job_directory / marker["Report"]
        report = load_config(report_path)
        validate_report(report, expected_match_count, position_count)
        return report
    except (FileNotFoundError, KeyError, TypeError, ValueError, json.JSONDecodeError):
        return None


def validate_report(report, expected_match_count, position_count):
    if report.get("SchemaVersion") != REPORT_SCHEMA_VERSION:
        raise ValueError(
            f"Player report schema is {report.get('SchemaVersion')}; expected {REPORT_SCHEMA_VERSION}. Rebuild the Player."
        )
    if report.get("CandidateCount") != report.get("CompletedCandidates"):
        raise ValueError("Report has incomplete candidates.")
    expected = expected_match_count * position_count
    ranking = report.get("Ranking") or []
    if len(ranking) != report.get("CandidateCount"):
        raise ValueError("Report ranking count does not match CandidateCount.")
    for candidate in ranking:
        if candidate.get("MatchCount") != expected:
            raise ValueError(
                f"Candidate {candidate.get('CandidateKey')} has {candidate.get('MatchCount')} matches; expected {expected}."
            )


def next_attempt_directory(job_directory):
    attempts = sorted(job_directory.glob("attempt_*"))
    return job_directory / f"attempt_{len(attempts) + 1:03d}"


def run_job(executable, run_directory, job, job_worker_count, position_count):
    job_directory = run_directory / "jobs" / job["id"]
    job_directory.mkdir(parents=True, exist_ok=True)
    completed = load_completed_job(job_directory, job["match_count"], position_count)
    if completed is not None:
        return {"id": job["id"], "status": "reused", "report": completed, "elapsed": 0.0}

    attempt_directory = next_attempt_directory(job_directory)
    attempt_directory.mkdir()
    config_path = attempt_directory / "config.json"
    player_log = attempt_directory / "player.log"
    write_json_atomic(config_path, job["config"])

    command = [
        str(executable),
        "-batchmode",
        "-nographics",
        "-logFile",
        str(player_log.resolve()),
        "-autoBattleOutputDirectory",
        str(attempt_directory.resolve()),
        "-autoBattleSweepConfig",
        str(config_path.resolve()),
    ]
    if job_worker_count is not None:
        command[1:1] = ["-job-worker-count", str(job_worker_count)]

    started = time.monotonic()
    process = subprocess.Popen(command)
    with ACTIVE_PROCESSES_LOCK:
        ACTIVE_PROCESSES.add(process)
    try:
        return_code = process.wait()
    finally:
        with ACTIVE_PROCESSES_LOCK:
            ACTIVE_PROCESSES.discard(process)
    elapsed = time.monotonic() - started
    if return_code != 0:
        failure = {
            "ExitCode": return_code,
            "ElapsedSeconds": elapsed,
            "PlayerLog": str(player_log),
        }
        write_json_atomic(attempt_directory / "failure.json", failure)
        return {"id": job["id"], "status": "failed", "error": failure, "elapsed": elapsed}

    reports = sorted((attempt_directory / "AutoBattles").glob("sweep_*.json"))
    if len(reports) != 1:
        error = {"Message": f"Player produced {len(reports)} reports.", "PlayerLog": str(player_log)}
        write_json_atomic(attempt_directory / "failure.json", error)
        return {"id": job["id"], "status": "failed", "error": error, "elapsed": elapsed}

    try:
        report = load_config(reports[0])
        validate_report(report, job["match_count"], position_count)
    except (KeyError, TypeError, ValueError, json.JSONDecodeError) as error:
        failure = {"Message": str(error), "PlayerLog": str(player_log)}
        write_json_atomic(attempt_directory / "failure.json", failure)
        return {"id": job["id"], "status": "failed", "error": failure, "elapsed": elapsed}

    marker = {
        "Report": str(reports[0].relative_to(job_directory)),
        "ElapsedSeconds": elapsed,
    }
    write_json_atomic(job_directory / "complete.json", marker)
    return {"id": job["id"], "status": "completed", "report": report, "elapsed": elapsed}


def add_totals(destination, source):
    for field in ("MatchCount", "Wins", "Losses", "Timeouts"):
        destination[field] += source.get(field, 0)
    destination["TotalGameSeconds"] += source.get("TotalGameSeconds", 0.0)
    destination["TotalRealSeconds"] += source.get("TotalRealSeconds", 0.0)
    destination["TotalSkippedAiDecisionCount"] += source.get("TotalSkippedAiDecisionCount", 0)
    destination["GameSecondsSamples"].extend(source.get("GameSecondsSamples", []))
    destination["DecidedGameSecondsSamples"].extend(source.get("DecidedGameSecondsSamples", []))


def finish_totals(result):
    matches = result["MatchCount"]
    result["WinRate"] = result["Wins"] / matches if matches else 0.0
    result["AverageGameSeconds"] = result["TotalGameSeconds"] / matches if matches else 0.0
    result["AverageRealSeconds"] = result["TotalRealSeconds"] / matches if matches else 0.0
    game_seconds = result["GameSecondsSamples"]
    decided_game_seconds = result["DecidedGameSecondsSamples"]
    result["MedianGameSeconds"] = statistics.median(game_seconds) if game_seconds else 0.0
    result["MinGameSeconds"] = min(game_seconds) if game_seconds else 0.0
    result["MaxGameSeconds"] = max(game_seconds) if game_seconds else 0.0
    result["MedianDecidedGameSeconds"] = statistics.median(decided_game_seconds) if decided_game_seconds else 0.0
    result.pop("GameSecondsSamples", None)
    result.pop("DecidedGameSecondsSamples", None)


def empty_totals():
    return {
        "MatchCount": 0,
        "Wins": 0,
        "Losses": 0,
        "Timeouts": 0,
        "WinRate": 0.0,
        "TotalGameSeconds": 0.0,
        "TotalRealSeconds": 0.0,
        "AverageGameSeconds": 0.0,
        "AverageRealSeconds": 0.0,
        "MedianGameSeconds": 0.0,
        "MinGameSeconds": 0.0,
        "MaxGameSeconds": 0.0,
        "MedianDecidedGameSeconds": 0.0,
        "TotalSkippedAiDecisionCount": 0,
        "GameSecondsSamples": [],
        "DecidedGameSecondsSamples": [],
    }


def merge_reports(reports):
    merged = {}
    for report in reports:
        for candidate in report["Ranking"]:
            key = candidate["CandidateKey"]
            aggregate = merged.setdefault(
                key,
                {
                    "CandidateKey": key,
                    "Roles": candidate["Roles"],
                    **empty_totals(),
                    "Scenarios": {},
                },
            )
            add_totals(aggregate, candidate)
            for scenario in candidate.get("Scenarios", []):
                scenario_key = (scenario["MapName"], scenario["StonePositionsReversed"])
                scenario_total = aggregate["Scenarios"].setdefault(
                    scenario_key,
                    {
                        "MapName": scenario["MapName"],
                        "StonePositionsReversed": scenario["StonePositionsReversed"],
                        **empty_totals(),
                    },
                )
                add_totals(scenario_total, scenario)

    ranking = []
    for candidate in merged.values():
        finish_totals(candidate)
        scenarios = list(candidate["Scenarios"].values())
        for scenario in scenarios:
            finish_totals(scenario)
        candidate["Scenarios"] = sorted(
            scenarios,
            key=lambda item: (item["MapName"], item["StonePositionsReversed"]),
        )
        ranking.append(candidate)
    return sorted(ranking, key=lambda item: (-item["WinRate"], -item["Wins"], item["CandidateKey"]))


def run_all(executable, run_directory, jobs, settings, position_count):
    results = []
    executor = concurrent.futures.ThreadPoolExecutor(max_workers=settings["parallel"])
    futures = [
        executor.submit(
            run_job,
            executable,
            run_directory,
            job,
            settings["job_worker_count"],
            position_count,
        )
        for job in jobs
    ]
    try:
        for future in concurrent.futures.as_completed(futures):
            result = future.result()
            results.append(result)
            print(f"{result['status']}: {result['id']}", flush=True)
    except KeyboardInterrupt:
        terminate_active_processes()
        for future in futures:
            future.cancel()
        executor.shutdown(wait=True, cancel_futures=True)
        raise
    else:
        executor.shutdown(wait=True)
    return results


def terminate_active_processes():
    with ACTIVE_PROCESSES_LOCK:
        processes = list(ACTIVE_PROCESSES)
    for process in processes:
        if process.poll() is None:
            process.terminate()


def write_summary(run_directory, results, settings, elapsed):
    successful = [result for result in results if result["status"] != "failed"]
    executed = [result for result in results if result["status"] == "completed"]
    reused = [result for result in results if result["status"] == "reused"]
    failures = [result for result in results if result["status"] == "failed"]
    ranking = merge_reports([result["report"] for result in successful])
    executed_ranking = merge_reports([result["report"] for result in executed])
    completed_matches = sum(candidate["MatchCount"] for candidate in ranking)
    executed_matches = sum(candidate["MatchCount"] for candidate in executed_ranking)
    metadata = collect_metadata([result["report"] for result in successful])
    summary = {
        "Complete": not failures,
        "ParallelProcesses": settings["parallel"],
        "JobWorkerCount": settings["job_worker_count"],
        "TimeScale": settings["time_scale"],
        **metadata,
        "ElapsedSeconds": elapsed,
        "CompletedJobs": len(successful),
        "ExecutedJobs": len(executed),
        "ReusedJobs": len(reused),
        "FailedJobs": [{"JobId": result["id"], **result["error"]} for result in failures],
        "CompletedMatches": completed_matches,
        "ExecutedMatches": executed_matches,
        "MatchesPerMinute": executed_matches * 60.0 / elapsed if elapsed else 0.0,
        "Ranking": ranking,
    }
    write_json_atomic(run_directory / "summary.json", summary)
    return summary


def collect_metadata(reports):
    fields = ("FixedDeltaTime", "PreserveFixedDeltaTime", "PlayerBuildGuid", "UnityVersion")
    variants = []
    for report in reports:
        variant = {field: report.get(field) for field in fields}
        if variant not in variants:
            variants.append(variant)
    metadata = dict(variants[0]) if variants else {field: None for field in fields}
    metadata["MetadataConsistent"] = len(variants) <= 1
    metadata["MetadataVariants"] = variants
    return metadata


def main():
    args = parse_args()
    config = load_config(args.config)
    if args.smoke:
        config["MatchesPerCandidate"] = 1
    settings = resolve_settings(config, args)
    executable = resolve_executable(settings["executable"])
    jobs = build_jobs(config, settings)
    run_directory = prepare_run_directory(config, settings, jobs, args.output_directory)
    position_count = 2 if config.get("EvaluateBothStonePositions") else 1

    started = time.monotonic()
    results = run_all(executable, run_directory, jobs, settings, position_count)
    elapsed = time.monotonic() - started
    summary = write_summary(run_directory, results, settings, elapsed)
    print(run_directory)
    return 0 if summary["Complete"] else 1


if __name__ == "__main__":
    sys.exit(main())
