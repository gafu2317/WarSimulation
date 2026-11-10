#!/usr/bin/env python3
"""Compare Reckless stat penalties and revival delays with fixed parties and seeds."""

import argparse
import copy
import pathlib
import math
import signal
import sys

import league_search as league


STAT_GROUPS = [("HP",), ("STR",), ("INT",), ("FAI",), ("AGI",),
               ("HP", "AGI"), ("STR", "INT", "FAI")]


def conditions(multipliers, delays, stat_groups=STAT_GROUPS):
    variants = [("none", {})]
    for group in stat_groups:
        for multiplier in multipliers:
            if not 0 < multiplier < 1:
                raise ValueError("Penalty multipliers must be between zero and one.")
            label = "_".join(group).lower() + "_" + format(multiplier, ".6g").replace(".", "_")
            variants.append((label, {f"Reckless{stat}Multiplier": multiplier for stat in group}))
    result = []
    for delay in delays:
        if not math.isfinite(delay) or delay < 0 and delay != -1:
            raise ValueError("Revival delays must be non-negative, or -1 for the prefab default.")
        for label, penalty in variants:
            balance = {f"Reckless{stat}Multiplier": 1.0 for stat in ("HP", "STR", "INT", "FAI", "AGI")}
            balance.update(penalty, ReviveDelayAfterArrivalSeconds=delay)
            result.append({"Id": label + "_revive_" + format(delay, ".6g").replace(".", "_"), "Balance": balance})
    if len({item["Id"] for item in result}) != len(result):
        raise ValueError("Duplicate comparison conditions.")
    return result


def write_summary(directory, reports, planned, complete=False, error=None):
    league.batch_battles.write_json_atomic(directory / "comparison-results.json", {
        "Complete": complete, "CompletedConditions": len(reports), "ExpectedConditions": len(planned),
        "Conditions": reports, "Error": error,
    })
    lines = ["# 猪突猛進・復帰時間の比較", "", f"状態: {'完了' if complete else '未完了'} ({len(reports)}/{len(planned)}条件)", "",
             "能力補正は両軍の猪突猛進だけに適用。復帰待ちは自陣到着後から計測し、設定値-1はプレハブ既定値を維持する。",
             "全条件で同じ編成・マップ・配置・シードを使用し、条件ごとの選抜は行わない。", ""]
    if error:
        lines.extend([f"エラー: {error}", ""])
    if reports:
        names = {row["CandidateKey"]: row["Name"] for row in reports[0]["Report"]["Ranking"]}
        lines.extend(["|条件|" + "|".join(names.values()) + "|", "|---|" + "---:|" * len(names)])
        for item in reports:
            rows = {row["CandidateKey"]: row for row in item["Report"]["Ranking"]}
            lines.append("|" + item["Id"] + "|" + "|".join(
                f"{rows[key]['WinRate']:.1%} ({rows[key]['Wins']}/{rows[key]['MatchCount']}, 時間切れ{rows[key]['Timeouts']})"
                for key in names) + "|")
    lines.extend(["", "各条件の summary.md に相手別・マップ別勝率を保存する。",
                  "少数シードの結果は予備評価。同じ条件の試合数を増やす場合は新しい出力先を使う。", ""])
    (directory / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=pathlib.Path, default=pathlib.Path(__file__).with_name("balance-comparison.json"))
    parser.add_argument("--output-directory", required=True, type=pathlib.Path)
    parser.add_argument("--executable", type=pathlib.Path)
    parser.add_argument("--parallel", type=int)
    parser.add_argument("--matches", type=int)
    parser.add_argument("--multipliers", type=float, nargs="+", default=[0.9, 0.8, 0.7])
    parser.add_argument("--revive-delays", type=float, nargs="+", default=[15, 30, 60])
    parser.add_argument("--plan-only", action="store_true")
    parser.add_argument("--target-name", help="Compare this party only, against the configured Enemies on both sides.")
    parser.add_argument("--exclude-fai", action="store_true", help="Omit FAI penalties for parties without Reckless FAI users.")
    parser.add_argument("--single-stat-only", action="store_true", help="Only penalize one stat per condition.")
    parser.add_argument("--keep-revive-delay", action="store_true", help="Keep the prefab revival delay for every condition.")
    args = parser.parse_args()
    protocol = league.normalize_config(league.batch_battles.load_config(args.config), args)
    matches = args.matches if args.matches is not None else protocol["LeagueMatches"]
    if matches < 1:
        raise ValueError("Matches must be positive.")
    target = None
    if args.target_name:
        target = next((party for party in protocol["Candidates"] if party["Name"] == args.target_name), None)
        if target is None or any(party["CandidateKey"] == target["CandidateKey"] for party in protocol["Enemies"]):
            raise ValueError("Target must exist and must not be in Enemies.")
    groups = [("HP",), ("STR",), ("INT",), ("AGI",), ("HP", "AGI"), ("STR", "INT")] if args.exclude_fai else STAT_GROUPS
    if args.single_stat_only:
        groups = [group for group in groups if len(group) == 1]
    planned = conditions(args.multipliers, [-1] if args.keep_revive_delay else args.revive_delays, groups)
    directory = args.output_directory.resolve()
    directory.mkdir(parents=True, exist_ok=True)
    count = len(protocol["Candidates"])
    league.batch_battles.write_json_atomic(directory / "plan.json", {
        "Conditions": planned, "Protocol": protocol, "Target": target, "MatchesPerDirection": matches,
        "TotalMatches": len(planned) * (2 * len(protocol["Enemies"]) if target else count * (count - 1)) * len(protocol["Scenarios"]) * matches,
    })
    if args.plan_only:
        print(f"Prepared {len(planned)} conditions: {directory / 'plan.json'}", flush=True)
        return 0
    executable = league.batch_battles.resolve_executable(pathlib.Path(protocol["Executable"]))
    league.prepare_run_directory(directory, {"Protocol": protocol, "Conditions": planned, "Matches": matches, "Target": target}, executable)
    reports = []
    write_summary(directory, reports, planned)
    league.batch_battles.write_json_atomic(directory / "status.json", {
        "Status": "RUNNING", "CompletedConditions": 0, "ExpectedConditions": len(planned),
    })
    try:
        for condition in planned:
            effective = copy.deepcopy(protocol)
            effective["Balance"] = condition["Balance"]
            effective = league.normalize_config({**effective, "Batch": {
                field: effective[field] for field in ("Executable", "ParallelProcesses", "MatchesPerJob", "JobWorkerCount")
            }}, argparse.Namespace(parallel=None, executable=None))
            output = directory / condition["Id"]
            manifest = league.prepare_run_directory(output, effective, executable)
            print(f"Condition {len(reports) + 1}/{len(planned)}: {condition['Id']}", flush=True)
            if target:
                forward = league.run_stage(effective, output, manifest, executable,
                    [target], effective["Enemies"], "forward", matches, 0)
                reverse = league.run_stage(effective, output, manifest, executable,
                    effective["Enemies"], [target], "reverse", matches, 0)
                own = forward["Ranking"][0]
                rows = reverse["Ranking"]
                wins = own["Wins"] + sum(row["Losses"] for row in rows)
                total = own["MatchCount"] + sum(row["MatchCount"] for row in rows)
                report = {"Ranking": [{**target, "Wins": wins, "MatchCount": total,
                    "WinRate": wins / total, "Timeouts": own["Timeouts"] + sum(row["Timeouts"] for row in rows)}],
                    "Forward": forward, "Reverse": reverse}
                detail = ["自軍側", *league.matchup_tables(forward), "敵軍側（表は相手の勝率）", *league.matchup_tables(reverse)]
            else:
                report = league.run_stage(effective, output, manifest, executable, effective["Candidates"],
                                          effective["Candidates"], "league", matches, 0, league=True)
                detail = league.matchup_tables(report)
            (output / "summary.md").write_text("\n".join([
                "# " + condition["Id"], "", *detail, ""
            ]), encoding="utf-8")
            reports.append({**condition, "Report": report})
            write_summary(directory, reports, planned)
            league.batch_battles.write_json_atomic(directory / "status.json", {
                "Status": "RUNNING", "CompletedConditions": len(reports), "ExpectedConditions": len(planned),
            })
    except (Exception, KeyboardInterrupt) as error:
        message = str(error) or "Interrupted"
        write_summary(directory, reports, planned, error=message)
        league.batch_battles.write_json_atomic(directory / "status.json", {"Status": "FAILED", "Error": message})
        print(message, file=sys.stderr)
        return 130 if isinstance(error, KeyboardInterrupt) else 1
    write_summary(directory, reports, planned, complete=True)
    league.batch_battles.write_json_atomic(directory / "status.json", {"Status": "COMPLETED"})
    return 0


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, league.interrupt_run)
    sys.exit(main())
