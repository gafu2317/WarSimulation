#!/usr/bin/env python3

import argparse
import concurrent.futures
import copy
import datetime
import json
import pathlib
import subprocess
import time


MAP_NAMES = ["AuthoredMap"] + [f"AuthoredMap {index}" for index in range(1, 10)]


def parse_args():
    parser = argparse.ArgumentParser(description="Run one headless auto-battle Player per authored map.")
    parser.add_argument("--template", required=True, type=pathlib.Path)
    parser.add_argument(
        "--executable",
        type=pathlib.Path,
        default=pathlib.Path(
            ".unity/CombatAutoBattleLight/CombatAutoBattleLight.app"
        ),
    )
    parser.add_argument("--output-root", type=pathlib.Path, default=pathlib.Path("Logs/AutoBattleParallel"))
    parser.add_argument("--worker-count", type=int, default=5)
    parser.add_argument("--benchmark-worker-counts", nargs="+", type=int)
    parser.add_argument("--job-worker-count", type=int, default=1)
    return parser.parse_args()


def validate_args(args):
    if not args.template.is_file():
        raise FileNotFoundError(f"Config template not found: {args.template}")
    args.executable = resolve_executable(args.executable)
    counts = args.benchmark_worker_counts or [args.worker_count]
    if any(count < 1 for count in counts):
        raise ValueError("Worker counts must be positive.")
    if args.job_worker_count is not None and args.job_worker_count < 1:
        raise ValueError("--job-worker-count must be positive.")


def resolve_executable(path):
    if path.is_file():
        return path
    macos_directory = path / "Contents" / "MacOS"
    if macos_directory.is_dir():
        candidates = [candidate for candidate in macos_directory.iterdir() if candidate.is_file()]
        if len(candidates) == 1:
            return candidates[0]
    raise FileNotFoundError(f"Auto-battle Player executable not found: {path}")


def load_template(path):
    with path.open(encoding="utf-8") as source:
        config = json.load(source)
    required = {
        "EnumerateAllCandidates": True,
        "EvaluateBothStonePositions": True,
        "UseCommonSeeds": True,
        "DisableDiagnostics": True,
        "MinPartySize": 5,
        "MaxPartySize": 5,
        "TimeScale": 16,
    }
    mismatches = [f"{key}={config.get(key)!r}" for key, value in required.items() if config.get(key) != value]
    if mismatches:
        raise ValueError("Template is not a five-member paired exploration config: " + ", ".join(mismatches))
    return config


def write_worker_config(template, map_name, path):
    config = copy.deepcopy(template)
    config["MapNames"] = [map_name]
    with path.open("w", encoding="utf-8") as destination:
        json.dump(config, destination, ensure_ascii=False, indent=2)
        destination.write("\n")


def run_map(executable, template, run_directory, map_name, job_worker_count):
    safe_name = map_name.replace(" ", "_")
    worker_directory = run_directory / safe_name
    worker_directory.mkdir(parents=True)
    config_path = worker_directory / "config.json"
    player_log = worker_directory / "player.log"
    write_worker_config(template, map_name, config_path)

    command = [
        str(executable.resolve()),
        "-batchmode",
        "-nographics",
        "-logFile",
        str(player_log.resolve()),
        "-autoBattleOutputDirectory",
        str(worker_directory.resolve()),
        "-autoBattleSweepConfig",
        str(config_path.resolve()),
    ]
    if job_worker_count is not None:
        command[1:1] = ["-job-worker-count", str(job_worker_count)]

    started = time.monotonic()
    completed = subprocess.run(command, check=False)
    elapsed = time.monotonic() - started
    if completed.returncode != 0:
        raise RuntimeError(f"{map_name} failed with exit code {completed.returncode}; see {player_log}")

    reports = sorted((worker_directory / "AutoBattles").glob("sweep_*.json"))
    if len(reports) != 1:
        raise RuntimeError(f"{map_name} produced {len(reports)} reports in {worker_directory}")
    with reports[0].open(encoding="utf-8") as source:
        report = json.load(source)
    return map_name, elapsed, report


def merge_reports(results):
    merged = {}
    for _, _, report in results:
        for candidate in report["Ranking"]:
            key = candidate["CandidateKey"]
            aggregate = merged.setdefault(
                key,
                {
                    "CandidateKey": key,
                    "Roles": candidate["Roles"],
                    "MatchCount": 0,
                    "Wins": 0,
                    "Losses": 0,
                    "Timeouts": 0,
                    "WinRate": 0.0,
                    "Scenarios": [],
                    "Maps": [],
                },
            )
            aggregate["MatchCount"] += candidate["MatchCount"]
            aggregate["Wins"] += candidate["Wins"]
            aggregate["Losses"] += candidate["Losses"]
            aggregate["Timeouts"] += candidate["Timeouts"]
            aggregate["Scenarios"].extend(candidate["Scenarios"])

    for candidate in merged.values():
        matches = candidate["MatchCount"]
        candidate["WinRate"] = candidate["Wins"] / matches if matches else 0.0
        by_map = {}
        for scenario in candidate["Scenarios"]:
            bucket = by_map.setdefault(
                scenario["MapName"],
                {"MapName": scenario["MapName"], "MatchCount": 0, "Wins": 0, "Losses": 0, "Timeouts": 0},
            )
            for field in ("MatchCount", "Wins", "Losses", "Timeouts"):
                bucket[field] += scenario[field]
        for bucket in by_map.values():
            matches = bucket["MatchCount"]
            bucket["WinRate"] = bucket["Wins"] / matches if matches else 0.0
        candidate["Maps"] = sorted(by_map.values(), key=lambda item: item["MapName"])
        candidate["Scenarios"].sort(key=lambda item: (item["MapName"], item["StonePositionsReversed"]))

    return sorted(
        merged.values(),
        key=lambda item: (-item["WinRate"], -item["Wins"], item["CandidateKey"]),
    )


def run_sweep(args, template, parent_directory, worker_count):
    run_directory = parent_directory / f"workers_{worker_count}"
    run_directory.mkdir(parents=True)
    started = time.monotonic()
    with concurrent.futures.ThreadPoolExecutor(max_workers=worker_count) as executor:
        futures = [
            executor.submit(
                run_map,
                args.executable,
                template,
                run_directory,
                map_name,
                args.job_worker_count,
            )
            for map_name in MAP_NAMES
        ]
        results = [future.result() for future in concurrent.futures.as_completed(futures)]
    elapsed = time.monotonic() - started

    ranking = merge_reports(results)
    completed_matches = sum(candidate["MatchCount"] for candidate in ranking)
    summary = {
        "WorkerCount": worker_count,
        "JobWorkerCount": args.job_worker_count,
        "ElapsedSeconds": elapsed,
        "CompletedMatches": completed_matches,
        "MatchesPerMinute": completed_matches * 60.0 / elapsed if elapsed else 0.0,
        "MapElapsedSeconds": {map_name: seconds for map_name, seconds, _ in sorted(results)},
        "Ranking": ranking,
    }
    with (run_directory / "summary.json").open("w", encoding="utf-8") as destination:
        json.dump(summary, destination, ensure_ascii=False, indent=2)
        destination.write("\n")
    return summary


def main():
    args = parse_args()
    validate_args(args)
    template = load_template(args.template)
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S_%f")
    parent_directory = args.output_root / stamp
    parent_directory.mkdir(parents=True)

    counts = args.benchmark_worker_counts or [args.worker_count]
    summaries = [run_sweep(args, template, parent_directory, count) for count in counts]
    benchmark = {
        "Runs": [
            {
                "WorkerCount": summary["WorkerCount"],
                "JobWorkerCount": summary["JobWorkerCount"],
                "ElapsedSeconds": summary["ElapsedSeconds"],
                "CompletedMatches": summary["CompletedMatches"],
                "MatchesPerMinute": summary["MatchesPerMinute"],
            }
            for summary in summaries
        ]
    }
    with (parent_directory / "benchmark.json").open("w", encoding="utf-8") as destination:
        json.dump(benchmark, destination, ensure_ascii=False, indent=2)
        destination.write("\n")
    print(parent_directory)


if __name__ == "__main__":
    main()
