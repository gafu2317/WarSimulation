#!/usr/bin/env python3
"""Compare explicit parties against an enemy pool, then run a directed league."""

import argparse
import concurrent.futures
import hashlib
import importlib.util
import json
import math
import pathlib
import signal
import statistics
import sys
import time


BACKEND_PATH = pathlib.Path(__file__).with_name("batch_battles.py")
SPEC = importlib.util.spec_from_file_location("batch_battles", BACKEND_PATH)
batch_battles = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(batch_battles)
VERSION = 1
METADATA_FIELDS = ("PlayerBuildGuid", "UnityVersion", "TimeScale", "FixedDeltaTime", "PreserveFixedDeltaTime")
COUNT_FIELDS = ("MatchCount", "Wins", "Losses", "Timeouts", "TotalSkippedAiDecisionCount")
TOTAL_FIELDS = ("TotalGameSeconds", "TotalRealSeconds")
WEAPON_NAMES = {1: "剣", 2: "盾", 3: "杖", 4: "魔導書", 5: "聖書", 6: "ロザリオ"}
PERSONALITY_NAMES = {0: "標準", 1: "陽キャ", 2: "戦闘狂", 7: "狡猾", 9: "献身的", 16: "寂しがり",
                     19: "猪突猛進", 20: "門番", 21: "便乗屋", 22: "復讐鬼", 24: "高所好き", 25: "影薄い"}


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=pathlib.Path)
    parser.add_argument("--output-directory", required=True, type=pathlib.Path)
    parser.add_argument("--executable", type=pathlib.Path)
    parser.add_argument("--parallel", type=int)
    parser.add_argument("--plan-only", action="store_true")
    return parser.parse_args()


def candidate_key(roles):
    return "|".join(f"{role['Weapon']}:{role['Personality']}" for role in sorted(
        roles, key=lambda role: (role["Weapon"], role["Personality"])
    ))


def table_text(value):
    return str(value).replace("|", " / ").replace("\n", " ")


def roles_label(roles):
    return " → ".join(f"{index}. {WEAPON_NAMES.get(role['Weapon'], role['Weapon'])}（{PERSONALITY_NAMES.get(role['Personality'], role['Personality'])}）"
                      for index, role in enumerate(roles, 1))


def normalize_parties(values, label):
    if not isinstance(values, list) or not values:
        raise ValueError(f"{label} must be a non-empty list.")
    parties, keys, names = [], set(), set()
    for index, value in enumerate(values):
        roles = value.get("Roles") if isinstance(value, dict) else None
        if not isinstance(roles, list) or not 1 <= len(roles) <= 10:
            raise ValueError(f"{label}[{index}] requires 1 to 10 Roles.")
        normalized = []
        for role in roles:
            if not isinstance(role, dict):
                raise ValueError(f"Invalid role in {label}[{index}]: {role}")
            weapon, personality = role.get("Weapon"), role.get("Personality")
            if type(weapon) is not int or weapon not in range(1, 7) or type(personality) is not int or personality < 0:
                raise ValueError(f"Invalid role in {label}[{index}]: {role}")
            normalized.append({"Weapon": weapon, "Personality": personality})
        key = candidate_key(normalized)
        name = value.get("Name", f"{label}_{index + 1}")
        if not isinstance(name, str) or not name or name in names or key in keys:
            raise ValueError(f"{label} requires unique names and compositions.")
        keys.add(key)
        names.add(name)
        parties.append({"Name": name, "CandidateKey": key, "Roles": normalized})
    return parties


def normalize_scenarios(values):
    scenarios, ids, conditions = [], set(), set()
    for index, scenario in enumerate(values or []):
        if not isinstance(scenario, dict):
            raise ValueError("Scenarios must contain objects.")
        name = scenario.get("MapName")
        reversed_position = scenario.get("StonePositionsReversed", False)
        scenario_id = scenario.get("Id", f"scenario_{index + 1}")
        if not isinstance(name, str) or not name or type(reversed_position) is not bool or not isinstance(scenario_id, str):
            raise ValueError("Each scenario requires MapName, a boolean StonePositionsReversed, and a string Id.")
        condition = (name, reversed_position)
        if scenario_id in ids or condition in conditions:
            raise ValueError("Scenarios must have unique Ids and map/orientation pairs.")
        ids.add(scenario_id)
        conditions.add(condition)
        scenarios.append({"Id": scenario_id, "MapName": name, "StonePositionsReversed": reversed_position})
    if not scenarios:
        raise ValueError("Scenarios must not be empty.")
    return scenarios


def normalize_config(config, args):
    candidates = normalize_parties(config.get("Candidates"), "Candidates")
    enemies = normalize_parties(config.get("Enemies"), "Enemies")
    scenarios = normalize_scenarios(config.get("Scenarios"))
    screen_scenarios = normalize_scenarios(config.get("ScreenScenarios", config.get("Scenarios")))
    conditions = {(scenario["MapName"], scenario["StonePositionsReversed"]) for scenario in scenarios}
    if any((scenario["MapName"], scenario["StonePositionsReversed"]) not in conditions for scenario in screen_scenarios):
        raise ValueError("ScreenScenarios must be a subset of Scenarios.")
    batch = config.get("Batch") or {}
    if not isinstance(batch, dict):
        raise ValueError("Batch must be an object.")
    settings = {name: config.get(name, default) for name, default in (
        ("ScreenMatches", 3), ("FinalMatches", 20), ("FinalCandidates", 10),
        ("LeagueMatches", 20), ("LeagueCandidates", 5), ("BaseSeed", 12000),
        ("TimeoutSeconds", 600), ("TimeScale", 6), ("PreserveFixedDeltaTime", False),
        ("DisableDiagnostics", True),
    )}
    settings.update({
        "ParallelProcesses": args.parallel if args.parallel is not None else batch.get("ParallelProcesses", 5),
        "MatchesPerJob": batch.get("MatchesPerJob", 5),
        "JobWorkerCount": batch.get("JobWorkerCount", 1),
        "Executable": str(args.executable or batch.get("Executable", batch_battles.DEFAULT_EXECUTABLE)),
    })
    for field in ("ScreenMatches", "FinalMatches", "FinalCandidates", "LeagueMatches", "LeagueCandidates",
                  "ParallelProcesses", "MatchesPerJob", "JobWorkerCount"):
        if type(settings[field]) is not int or settings[field] < 1:
            raise ValueError(f"{field} must be a positive integer.")
    if settings["FinalCandidates"] > len(candidates) or settings["LeagueCandidates"] > settings["FinalCandidates"]:
        raise ValueError("LeagueCandidates <= FinalCandidates <= candidate count is required.")
    if settings["LeagueCandidates"] < 2:
        raise ValueError("LeagueCandidates must be at least 2.")
    if type(settings["BaseSeed"]) is not int or settings["BaseSeed"] < 0:
        raise ValueError("BaseSeed must be a non-negative integer.")
    if settings["BaseSeed"] + settings["ScreenMatches"] + settings["FinalMatches"] + settings["LeagueMatches"] > 2147483647:
        raise ValueError("Seed range exceeds the Player integer range.")
    for field in ("TimeoutSeconds", "TimeScale"):
        if type(settings[field]) not in (int, float) or not math.isfinite(settings[field]) or settings[field] <= 0:
            raise ValueError(f"{field} must be a positive finite number.")
    for field in ("PreserveFixedDeltaTime", "DisableDiagnostics"):
        if type(settings[field]) is not bool:
            raise ValueError(f"{field} must be boolean.")
    return {"Candidates": candidates, "Enemies": enemies, "Scenarios": scenarios, "ScreenScenarios": screen_scenarios, **settings}


def player_fingerprint(executable):
    root = executable.parent.parent if executable.parent.name == "MacOS" and executable.parent.parent.name == "Contents" else executable.parent
    paths = sorted(path for path in root.rglob("*") if path.is_file()) if root.name == "Contents" else [executable]
    digest = hashlib.sha256()
    for path in paths:
        digest.update(str(path.relative_to(root)).encode("utf-8") + b"\0")
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
        digest.update(b"\0")
    return digest.hexdigest()


def prepare_run_directory(directory, protocol, executable):
    directory.mkdir(parents=True, exist_ok=True)
    effective = {"Version": VERSION, "Protocol": protocol, "PlayerSHA256": player_fingerprint(executable)}
    fingerprint = hashlib.sha256(json.dumps(effective, sort_keys=True, ensure_ascii=False).encode("utf-8")).hexdigest()
    path = directory / "manifest.json"
    if path.exists():
        manifest = batch_battles.load_config(path)
        if manifest.get("Fingerprint") != fingerprint:
            raise ValueError("Output directory belongs to another config or Player build. Use a new output directory.")
    else:
        manifest = {"Fingerprint": fingerprint, **effective}
        batch_battles.write_json_atomic(path, manifest)
    return manifest


def build_jobs(protocol, candidates, opponents, stage, matches, seed_offset, league=False):
    jobs = []
    for scenario_index, scenario in enumerate(protocol["Scenarios"]):
        for enemy_index, enemy in enumerate(opponents):
            allies = [candidate for candidate in candidates if not league or candidate["CandidateKey"] != enemy["CandidateKey"]]
            for offset in range(0, matches, protocol["MatchesPerJob"]):
                count = min(protocol["MatchesPerJob"], matches - offset)
                jobs.append({
                    "id": f"s{scenario_index:03d}_e{enemy_index:03d}_m{offset:05d}_{offset + count:05d}",
                    "stage": stage, "scenario": scenario, "enemy": enemy, "candidates": allies, "match_count": count,
                    "config": {
                        "MapNames": [scenario["MapName"]],
                        "Enemy": enemy["Roles"], "Candidates": [{"Roles": candidate["Roles"]} for candidate in allies],
                        "MatchesPerCandidate": count, "BaseSeed": protocol["BaseSeed"] + seed_offset,
                        "MatchOffset": offset, "TotalMatchesPerCandidate": matches,
                        "MinPartySize": 1, "MaxPartySize": 10,
                        "UseCommonSeeds": True, "UseFixedStonePosition": True, "EvaluateBothStonePositions": False,
                        "StonePositionsReversed": scenario["StonePositionsReversed"],
                        **{field: protocol[field] for field in ("TimeoutSeconds", "TimeScale", "PreserveFixedDeltaTime", "DisableDiagnostics")},
                    },
                })
    return jobs


def validate_report(report, job):
    batch_battles.validate_report(report, job["match_count"], 1)
    expected = {candidate["CandidateKey"]: candidate for candidate in job["candidates"]}
    ranking = report.get("Ranking", [])
    if len(ranking) != len(expected) or {row.get("CandidateKey") for row in ranking} != set(expected):
        raise ValueError("Report candidate set differs from the requested candidates.")
    metadata = {field: report.get(field) for field in METADATA_FIELDS}
    if not all(isinstance(metadata[field], str) and metadata[field] for field in ("PlayerBuildGuid", "UnityVersion")):
        raise ValueError("Report is missing build or Unity metadata.")
    if type(metadata["PreserveFixedDeltaTime"]) is not bool or metadata["PreserveFixedDeltaTime"] != job["config"]["PreserveFixedDeltaTime"]:
        raise ValueError("Report has incompatible fixed-step settings.")
    for field in ("TimeScale", "FixedDeltaTime"):
        if type(metadata[field]) not in (int, float) or not math.isfinite(metadata[field]) or metadata[field] <= 0:
            raise ValueError(f"Report has invalid {field}.")
    if not math.isclose(metadata["TimeScale"], job["config"]["TimeScale"], rel_tol=1e-6):
        raise ValueError("Report TimeScale differs from the requested setting.")
    for row in ranking:
        if candidate_key(row.get("Roles", [])) != row["CandidateKey"]:
            raise ValueError("Report roles do not match their candidate key.")
        scenarios = row.get("Scenarios", [])
        if len(scenarios) != 1:
            raise ValueError("Report is missing the requested scenario.")
        scenario = scenarios[0]
        if scenario.get("MapName") != job["scenario"]["MapName"] or scenario.get("StonePositionsReversed") is not job["scenario"]["StonePositionsReversed"]:
            raise ValueError("Report scenario differs from the requested map/orientation.")
        for result in (row, scenario):
            for field in COUNT_FIELDS:
                if type(result.get(field)) is not int or result[field] < 0:
                    raise ValueError(f"Report has invalid or missing {field}.")
            if result["MatchCount"] != job["match_count"] or sum(result[field] for field in ("Wins", "Losses", "Timeouts")) != result["MatchCount"]:
                raise ValueError("Report has incomplete outcome counts.")
            for field in TOTAL_FIELDS:
                if type(result.get(field)) not in (int, float) or not math.isfinite(result[field]) or result[field] < 0:
                    raise ValueError(f"Report has invalid or missing {field}.")
            samples = result.get("GameSecondsSamples", [])
            decided = result.get("DecidedGameSecondsSamples", [])
            if len(samples) != result["MatchCount"] or len(decided) != result["Wins"] + result["Losses"]:
                raise ValueError("Report duration samples are incomplete.")
            if any(type(value) not in (int, float) or not math.isfinite(value) or value < 0 for value in samples + decided):
                raise ValueError("Report duration samples are invalid.")
            if not math.isclose(sum(samples), result["TotalGameSeconds"], rel_tol=1e-5, abs_tol=1e-4):
                raise ValueError("Report duration samples disagree with the total.")
        if any(row[field] != scenario[field] for field in COUNT_FIELDS + TOTAL_FIELDS):
            raise ValueError("Report candidate and scenario totals disagree.")
    return metadata


def same_metadata(first, second):
    return all(math.isclose(first[field], second[field], rel_tol=1e-6) if field in ("TimeScale", "FixedDeltaTime")
               else first[field] == second[field] for field in METADATA_FIELDS)


def aggregate_results(protocol, candidates, opponents, results, matches, league=False):
    cells = {}
    names = {candidate["CandidateKey"]: candidate["Name"] for candidate in candidates + opponents}

    def add_cell(job, result, own_key, opponent_key, side):
        key = (own_key, opponent_key, job["scenario"]["Id"])
        cell = cells.setdefault(key, {
            "CandidateKey": key[0], "OpponentKey": key[1], "OpponentName": names[opponent_key],
            "Scenario": job["scenario"], **{field: 0 for field in COUNT_FIELDS + TOTAL_FIELDS},
            "AllyMatches": 0, "EnemyMatches": 0, "AllyWins": 0, "EnemyWins": 0,
            "GameSecondsSamples": [], "DecidedGameSecondsSamples": [],
        })
        for field in COUNT_FIELDS + TOTAL_FIELDS:
            cell[field] += result[field]
        cell[side + "Matches"] += result["MatchCount"]
        cell[side + "Wins"] += result["Wins"]
        for field in ("GameSecondsSamples", "DecidedGameSecondsSamples"):
            cell[field].extend(result[field])

    for job, report in results:
        for row in report["Ranking"]:
            add_cell(job, row, row["CandidateKey"], job["enemy"]["CandidateKey"], "Ally")
            if league:
                reverse = {**row, "Wins": row["Losses"], "Losses": row["Wins"]}
                add_cell(job, reverse, job["enemy"]["CandidateKey"], row["CandidateKey"], "Enemy")
    ranking = []
    for candidate in candidates:
        expected = [(candidate["CandidateKey"], enemy["CandidateKey"], scenario["Id"])
                    for enemy in opponents for scenario in protocol["Scenarios"]
                    if not league or candidate["CandidateKey"] != enemy["CandidateKey"]]
        if any(key not in cells or cells[key]["MatchCount"] != matches * (2 if league else 1)
               or cells[key]["AllyMatches"] != matches or (league and cells[key]["EnemyMatches"] != matches)
               for key in expected):
            raise ValueError(f"Candidate {candidate['Name']} has incomplete enemy/scenario coverage.")
        rows = [cells[key] for key in expected]
        for cell in rows:
            cell["WinRate"] = cell["Wins"] / cell["MatchCount"]
            cell["TimeoutRate"] = cell["Timeouts"] / cell["MatchCount"]
            cell["AverageGameSeconds"] = cell["TotalGameSeconds"] / cell["MatchCount"]
        row = {**candidate, **{field: sum(cell[field] for cell in rows) for field in COUNT_FIELDS + TOTAL_FIELDS}}
        row["WinRate"] = statistics.mean(cell["WinRate"] for cell in rows)
        row["TimeoutRate"] = statistics.mean(cell["TimeoutRate"] for cell in rows)
        row["WorstScenarioWinRate"] = min(statistics.mean(cell["WinRate"] for cell in rows if cell["Scenario"]["Id"] == scenario["Id"])
                                          for scenario in protocol["Scenarios"])
        row["AverageGameSeconds"] = row["TotalGameSeconds"] / row["MatchCount"]
        row["AverageRealSeconds"] = row["TotalRealSeconds"] / row["MatchCount"]
        row["MedianGameSeconds"] = statistics.median(value for cell in rows for value in cell["GameSecondsSamples"])
        ranking.append(row)
    ranking.sort(key=lambda row: (-row["WinRate"], -row["WorstScenarioWinRate"], row["TimeoutRate"], row["CandidateKey"]))
    return {"Ranking": ranking, "Cells": list(cells.values())}


def run_stage(protocol, directory, manifest, executable, candidates, opponents, stage, matches, seed_offset, league=False):
    if stage == "screen":
        protocol = {**protocol, "Scenarios": protocol["ScreenScenarios"]}
    jobs = build_jobs(protocol, candidates, opponents, stage, matches, seed_offset, league)
    results = []
    stage_directory = directory / "stages" / stage
    with concurrent.futures.ThreadPoolExecutor(max_workers=protocol["ParallelProcesses"]) as executor:
        futures = {executor.submit(batch_battles.run_job, executable, stage_directory, job, protocol["JobWorkerCount"], 1): job for job in jobs}
        try:
            for future in concurrent.futures.as_completed(futures):
                job, result = futures[future], future.result()
                if result["status"] == "failed":
                    raise RuntimeError(f"Job {job['id']} failed: {result.get('error')}")
                try:
                    job_directory = stage_directory / "jobs" / job["id"]
                    marker = batch_battles.load_config(job_directory / "complete.json")
                    attempt = (job_directory / marker["Report"]).parent.parent
                    if batch_battles.load_config(attempt / "config.json") != job["config"]:
                        raise ValueError("Saved job config differs from the requested enemy, candidates, scenario or seed range.")
                    metadata = validate_report(result["report"], job)
                    if "RuntimeMetadata" in manifest and not same_metadata(manifest["RuntimeMetadata"], metadata):
                        raise ValueError("Player buildGUID/UnityVersion/TimeScale/fixedDelta metadata differ within this run.")
                except (ValueError, KeyError, TypeError, OSError) as error:
                    marker = stage_directory / "jobs" / job["id"] / "complete.json"
                    marker.unlink(missing_ok=True)
                    raise ValueError(f"Rejected {stage}/{job['id']}: {error}") from error
                if "RuntimeMetadata" not in manifest:
                    manifest["RuntimeMetadata"] = metadata
                    batch_battles.write_json_atomic(directory / "manifest.json", manifest)
                results.append((job, result["report"]))
                print(f"{stage} {len(results)}/{len(jobs)} {result['status']}: {job['id']}", flush=True)
                batch_battles.write_json_atomic(directory / "status.json", {
                    "Status": "RUNNING", "Stage": stage, "CompletedJobs": len(results), "ExpectedJobs": len(jobs),
                })
        except BaseException:
            for future in futures:
                future.cancel()
            executor.shutdown(wait=False, cancel_futures=True)
            batch_battles.terminate_active_processes()
            raise
    report = aggregate_results(protocol, candidates, opponents, results, matches, league)
    report.update({
        "Complete": True, "Stage": stage, "MatchesPerCell": matches * (2 if league else 1),
        "MatchesPerDirection": matches, "AlternatesTeamSides": league,
        "SeedStart": protocol["BaseSeed"] + seed_offset,
        "SeedEndInclusive": protocol["BaseSeed"] + seed_offset + matches - 1,
        "Metadata": manifest["RuntimeMetadata"],
    })
    batch_battles.write_json_atomic(directory / f"{stage}-results.json", report)
    return report


def write_summary(directory, reports, complete, error=None):
    payload = {"Complete": complete, "Screening": reports.get("screen"), "Final": reports.get("final"), "League": reports.get("league")}
    if error:
        payload["Error"] = error
    batch_battles.write_json_atomic(directory / "ranking.json", payload)
    lines = ["# 強編成探索", "", f"状態: {'完了' if complete else '実行中・未完了'}", "",
             "各敵編成・各マップ/魔石配置を同じ試合数で評価し、条件を等重みで平均した勝率。タイムアウトは勝利に含めず別集計。",
             "順位は平均勝率、最も苦手なシナリオの勝率、タイムアウト率の順。候補キーは同点時の表示順にのみ使用。", ""]
    if error:
        lines.extend([f"失敗: {error}", ""])
    for stage, title in (("screen", "予選: 敵プール"), ("final", "本評価: 敵プール・新規シード"), ("league", "相互リーグ: 味方/敵を入れ替えた両方向")):
        report = reports.get(stage)
        lines.extend([f"## {title}", ""])
        if not report:
            lines.extend(["未完了。順位はまだ確定していない。", ""])
            continue
        count_label = f"{report['MatchesPerDirection']} 試合 × 味方/敵の両側 = {report['MatchesPerCell']} 試合" if stage == "league" else f"{report['MatchesPerCell']} 試合"
        lines.extend([f"各条件 {count_label}、シード {report['SeedStart']}–{report['SeedEndInclusive']}。", "",
                      "|順位|編成名|編成内容（登録順）|勝率|勝/負/時間切れ|最悪シナリオ勝率|AI判断スキップ|", "|---:|---|---|---:|---|---:|---:|"])
        for index, row in enumerate(report["Ranking"], 1):
            lines.append(f"|{index}|{table_text(row['Name'])}|{roles_label(row['Roles'])}|{row['WinRate']:.1%}|{row['Wins']}/{row['Losses']}/{row['Timeouts']}|{row['WorstScenarioWinRate']:.1%}|{row['TotalSkippedAiDecisionCount']}|")
        lines.append("")
        if stage in ("final", "league"):
            lines.extend(matchup_tables(report))
    lines.extend(["本評価とリーグは別の相手集合に対する結果。勝率を足し合わせていない。",
                  "AI判断スキップは参加した試合の全軍合計。候補単体のスキップ数ではない。",
                  "少数試合の順位は暫定。現在の候補集合・敵プール・マップ・Playerビルド内での評価。", ""])
    (directory / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def matchup_tables(report):
    top = report["Ranking"][:6]
    cells = report["Cells"]
    header = "|相手|" + "|".join(table_text(row["Name"]) for row in top) + "|"
    separator = "|---|" + "---:|" * len(top)

    def rates(selected):
        if not selected:
            return "—"
        wins = sum(cell["Wins"] for cell in selected)
        matches = sum(cell["MatchCount"] for cell in selected)
        timeouts = sum(cell["Timeouts"] for cell in selected)
        return f"{statistics.mean(cell['WinRate'] for cell in selected):.1%} ({wins}/{matches}, 時間切れ{timeouts})"

    lines = ["### 上位6編成の相手別勝率（全シナリオ平均）", "", header, separator]
    opponents = dict((cell["OpponentKey"], cell["OpponentName"]) for cell in cells)
    for opponent_key, name in opponents.items():
        values = [rates([cell for cell in cells if cell["CandidateKey"] == row["CandidateKey"] and cell["OpponentKey"] == opponent_key]) for row in top]
        lines.append("|" + table_text(name) + "|" + "|".join(values) + "|")
    lines.extend(["", "### 上位6編成のマップ・配置別勝率（全相手平均）", "", header.replace("|相手|", "|マップ・魔石配置|", 1), separator])
    scenarios = dict((cell["Scenario"]["Id"], cell["Scenario"]) for cell in cells)
    for scenario_id, scenario in scenarios.items():
        values = [rates([cell for cell in cells if cell["CandidateKey"] == row["CandidateKey"] and cell["Scenario"]["Id"] == scenario_id]) for row in top]
        orientation = "反転" if scenario["StonePositionsReversed"] else "通常"
        lines.append("|" + table_text(scenario["MapName"]) + " / " + orientation + "|" + "|".join(values) + "|")
    lines.append("")
    return lines


def plan(protocol):
    scenarios, enemies = len(protocol["Scenarios"]), len(protocol["Enemies"])
    league = protocol["LeagueCandidates"]
    return {
        "ScreenMatches": len(protocol["Candidates"]) * enemies * len(protocol["ScreenScenarios"]) * protocol["ScreenMatches"],
        "FinalMatches": protocol["FinalCandidates"] * enemies * scenarios * protocol["FinalMatches"],
        "LeagueMatches": league * (league - 1) * scenarios * protocol["LeagueMatches"],
        "Protocol": protocol,
    }


def main():
    args = parse_args()
    protocol = normalize_config(batch_battles.load_config(args.config), args)
    directory = args.output_directory
    directory.mkdir(parents=True, exist_ok=True)
    planned = plan(protocol)
    batch_battles.write_json_atomic(directory / "plan.json", planned)
    print(f"Planned matches: screen={planned['ScreenMatches']} final={planned['FinalMatches']} league={planned['LeagueMatches']}", flush=True)
    if args.plan_only:
        return 0
    executable = batch_battles.resolve_executable(pathlib.Path(protocol["Executable"]))
    manifest = prepare_run_directory(directory, protocol, executable)
    reports = {}
    started = time.monotonic()
    try:
        reports["screen"] = run_stage(protocol, directory, manifest, executable, protocol["Candidates"], protocol["Enemies"], "screen", protocol["ScreenMatches"], 0)
        write_summary(directory, reports, False)
        finalists = [{field: row[field] for field in ("Name", "CandidateKey", "Roles")} for row in reports["screen"]["Ranking"][:protocol["FinalCandidates"]]]
        reports["final"] = run_stage(protocol, directory, manifest, executable, finalists, protocol["Enemies"], "final", protocol["FinalMatches"], protocol["ScreenMatches"])
        write_summary(directory, reports, False)
        league = [{field: row[field] for field in ("Name", "CandidateKey", "Roles")} for row in reports["final"]["Ranking"][:protocol["LeagueCandidates"]]]
        reports["league"] = run_stage(protocol, directory, manifest, executable, league, league, "league", protocol["LeagueMatches"], protocol["ScreenMatches"] + protocol["FinalMatches"], True)
    except (Exception, KeyboardInterrupt) as error:
        message = str(error) or "Interrupted"
        write_summary(directory, reports, False, message)
        batch_battles.write_json_atomic(directory / "status.json", {"Status": "FAILED", "Error": message, "ElapsedSeconds": time.monotonic() - started})
        print(message, file=sys.stderr, flush=True)
        return 1
    write_summary(directory, reports, True)
    batch_battles.write_json_atomic(directory / "status.json", {"Status": "COMPLETED", "ElapsedSeconds": time.monotonic() - started, "Metadata": manifest["RuntimeMetadata"]})
    print(f"Completed: {directory / 'summary.md'}", flush=True)
    return 0


def interrupt_run(signum, frame):
    raise KeyboardInterrupt


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, interrupt_run)
    sys.exit(main())
