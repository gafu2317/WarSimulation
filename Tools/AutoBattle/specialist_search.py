#!/usr/bin/env python3
"""Search terrain specialists and confirm controlled personality interactions."""

import argparse
import concurrent.futures
import json
import pathlib
import signal
import sys
import time

import league_search as league


FIELDS = ("Name", "CandidateKey", "Roles")


def map_ranking(report, map_name):
    rows = []
    for candidate in report["Ranking"]:
        cells = [c for c in report["Cells"] if c["CandidateKey"] == candidate["CandidateKey"] and c["Scenario"]["MapName"] == map_name]
        if not cells:
            continue
        counts = {field: sum(c[field] for c in cells) for field in ("MatchCount", "Wins", "Losses", "Timeouts")}
        rows.append({**{field: candidate[field] for field in FIELDS}, **counts,
                     "WinRate": counts["Wins"] / counts["MatchCount"]})
    return sorted(rows, key=lambda r: (-r["WinRate"], r["Timeouts"], r["CandidateKey"]))


def pair_scores(rows, pairs):
    lookup = {r["CandidateKey"]: r for r in rows}
    result = []
    for pair in pairs:
        keys = [pair[field] for field in ("Base", "A", "B", "AB")]
        if not all(key in lookup for key in keys):
            continue
        rates = [lookup[key]["WinRate"] for key in keys]
        result.append({**pair, "Rates": rates, "Interaction": rates[3] - rates[1] - rates[2] + rates[0],
                       "Counts": [lookup[key]["MatchCount"] for key in keys],
                       "Wins": [lookup[key]["Wins"] for key in keys]})
    return sorted(result, key=lambda p: (-p["Interaction"], -p["Rates"][3], p["Name"]))


def select_panel(screen, experiment):
    maps = [m["MapName"] for m in experiment["Maps"]]
    keys = {r["CandidateKey"] for r in screen["Ranking"][:2]}
    keys.add(experiment["BaselineKey"])
    scored = []
    for map_name in maps:
        rows = map_ranking(screen, map_name)
        keys.update(r["CandidateKey"] for r in rows[:experiment["PerMapTop"]])
        scored.extend({**p, "SelectedOnMap": map_name} for p in pair_scores(rows, experiment["Pairs"]))
    selected_pairs = []
    interaction_order = sorted(scored, key=lambda p: (-(p["Rates"][3] >= max(p["Rates"][1:3]) and p["Rates"][3] >= .5),
                                                      -p["Interaction"], -p["Rates"][3], p["Name"]))
    strength_order = sorted(scored, key=lambda p: (-p["Rates"][3], -p["Interaction"], p["Name"]))
    for index in range(experiment["ConfirmPairCount"]):
        order = strength_order if index == 1 else interaction_order
        pair = next((p for p in order if p["Name"] not in {x["Name"] for x in selected_pairs}), None)
        if pair is None:
            break
        selected_pairs.append({**{k: pair[k] for k in experiment["Pairs"][0]}, "SelectedOnMap": pair["SelectedOnMap"]})
    keys.update(pair[field] for pair in selected_pairs for field in ("Base", "A", "B", "AB"))
    return [{field: r[field] for field in FIELDS} for r in screen["Ranking"] if r["CandidateKey"] in keys], selected_pairs


def aggregate_panel(protocol, candidates, opponents, results, matches, both_sides):
    forward = [(job, report) for job, report in results if job["id"].startswith("f_")]
    combined = list(forward)
    if both_sides:
        opponent_lookup = {r["CandidateKey"]: r for r in opponents}
        for job, report in results:
            if not job["id"].startswith("r_"):
                continue
            for row in report["Ranking"]:
                converted = {**league.reverse_result(row), **{field: job["enemy"][field] for field in FIELDS}}
                combined.append(({**job, "enemy": opponent_lookup[row["CandidateKey"]]}, {"Ranking": [converted]}))
    report = league.aggregate_results(protocol, candidates, opponents, combined, matches * (2 if both_sides else 1), False)
    if both_sides:
        ally = league.aggregate_results(protocol, candidates, opponents, forward, matches, False)
        lookup = {(c["CandidateKey"], c["OpponentKey"], c["Scenario"]["Id"]): c for c in ally["Cells"]}
        for cell in report["Cells"]:
            first = lookup[(cell["CandidateKey"], cell["OpponentKey"], cell["Scenario"]["Id"])]
            cell.update({"AllyMatches": matches, "EnemyMatches": matches, "AllyWins": first["Wins"],
                         "EnemyWins": cell["Wins"] - first["Wins"], "AllyTimeouts": first["Timeouts"],
                         "EnemyTimeouts": cell["Timeouts"] - first["Timeouts"], "AllyLosses": first["Losses"],
                         "EnemyLosses": cell["Losses"] - first["Losses"]})
    return report


def run_panel(protocol, directory, manifest, executable, candidates, stage, matches, seed_offset, both_sides=False):
    opponents = protocol["Enemies"]
    jobs = league.build_jobs(protocol, candidates, opponents, stage, matches, seed_offset, False)
    for job in jobs:
        job["id"] = "f_" + job["id"]
    if both_sides:
        reverse = league.build_jobs(protocol, opponents, candidates, stage, matches, seed_offset, False)
        for job in reverse:
            job["id"] = "r_" + job["id"]
        jobs.extend(reverse)
    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=protocol["ParallelProcesses"]) as executor:
        futures = {executor.submit(league.batch_battles.run_job, executable, directory / "stages" / stage, job,
                                   protocol["JobWorkerCount"], 1): job for job in jobs}
        try:
            for future in concurrent.futures.as_completed(futures):
                job, result = futures[future], future.result()
                if result["status"] == "failed":
                    raise RuntimeError(f"{stage}/{job['id']}: {result.get('error')}")
                marker_path = directory / "stages" / stage / "jobs" / job["id"] / "complete.json"
                try:
                    marker = league.batch_battles.load_config(marker_path)
                    attempt = (marker_path.parent / marker["Report"]).parent.parent
                    if league.batch_battles.load_config(attempt / "config.json") != job["config"]:
                        raise ValueError("Saved job configuration differs.")
                    metadata = league.validate_report(result["report"], job)
                    if metadata["PlayerBuildGuid"] != protocol["Experiment"]["ExpectedBuildGuid"]:
                        raise ValueError("Runtime build GUID differs from the frozen build metadata.")
                    if "RuntimeMetadata" in manifest and not league.same_metadata(manifest["RuntimeMetadata"], metadata):
                        raise ValueError("Runtime metadata changed within the experiment.")
                except (ValueError, KeyError, TypeError, OSError):
                    marker_path.unlink(missing_ok=True)
                    raise
                if "RuntimeMetadata" not in manifest:
                    manifest["RuntimeMetadata"] = metadata
                    league.batch_battles.write_json_atomic(directory / "manifest.json", manifest)
                results.append((job, result["report"]))
                print(f"{stage} {len(results)}/{len(jobs)} {result['status']}: {job['id']}", flush=True)
                league.batch_battles.write_json_atomic(directory / "status.json", {
                    "Status": "RUNNING", "Stage": stage, "CompletedJobs": len(results), "ExpectedJobs": len(jobs)})
        except BaseException:
            for future in futures:
                future.cancel()
            executor.shutdown(wait=False, cancel_futures=True)
            league.batch_battles.terminate_active_processes()
            raise
    report = aggregate_panel(protocol, candidates, opponents, results, matches, both_sides)
    own_keys = {c["CandidateKey"] for c in candidates}
    report["Cells"] = [c for c in report["Cells"] if c["CandidateKey"] in own_keys]
    report.update({"Complete": True, "Stage": stage, "MatchesPerDirection": matches,
                   "AlternatesTeamSides": both_sides, "SeedStart": protocol["BaseSeed"] + seed_offset,
                   "SeedEndInclusive": protocol["BaseSeed"] + seed_offset + matches - 1,
                   "Metadata": manifest["RuntimeMetadata"],
                   "ActualMatches": sum(len(job["candidates"]) * job["match_count"] for job in jobs)})
    league.batch_battles.write_json_atomic(directory / f"{stage}-results.json", report)
    return report


def write_summary(directory, screen, confirmations, selections, experiment, complete=False, error=None):
    lines = ["# 地形・性格の組み合わせ探索", "", "状態: " + ("完了" if complete else "実行中・未完了"), "",
             "現行の確定仕様。4並列・6倍速・物理刻み維持。予選は候補が味方側、再評価は両軍入替。",
             "予選と再評価は別シード。再評価候補は全地形で共通。地形別の候補と性格差し替えの比較編成を全地形で再確認する。",
             "魔石配置の反転は地形そのものの反転ではない。相手別・配置別・軍側別の全結果はJSONに保存。",
             "A/B/ABは固定した武器・並び・残りの性格に対する差し替え。組合せ差=AB−A−B＋基準。",
             "正の組合せ差だけでは相乗効果を確定できない。少数試合、勝率の上限・下限、地形配置への依存を含む探索の目安。", ""]
    if error:
        lines += ["失敗: " + error, ""]
    total = (screen or {}).get("ActualMatches", 0) + sum(r["ActualMatches"] for r in confirmations.values())
    lines += [f"探索の集計済み実試合数: {total}（起動確認8戦を除く）", ""]
    if screen:
        lines += ["## 全性格の予選地形別参考成績", "",
                  "その性格を含む候補編成の勝敗を合算。候補数・武器・同居する性格が異なるため、性格単独の強さとは解釈しない。", ""]
        maps = list(dict.fromkeys(c["Scenario"]["MapName"] for c in screen["Cells"]))
        lines += ["|性格|" + "|".join(maps) + "|", "|---|" + "---:|" * len(maps)]
        for personality, name in league.PERSONALITY_NAMES.items():
            keys = {r["CandidateKey"] for r in screen["Ranking"] if any(x["Personality"] == personality for x in r["Roles"])}
            rates = []
            for map_name in maps:
                cells = [c for c in screen["Cells"] if c["CandidateKey"] in keys and c["Scenario"]["MapName"] == map_name]
                count = sum(c["MatchCount"] for c in cells)
                rates.append(f"{sum(c['Wins'] for c in cells) / count:.1%}" if count else "—")
            lines.append("|" + name + "|" + "|".join(rates) + "|")
        for map_name in maps:
            lines += ["", "## " + map_name, "", next((m["Description"] for m in experiment["Maps"] if m["MapName"] == map_name), ""), ""]
            report = confirmations.get(map_name)
            rows = map_ranking(report or screen, map_name)
            lines += ["再評価（両軍入替・新規シード）" if report else "予選暫定（候補が味方側・1シード）", "",
                      "|順位|編成|登録順|勝/負/時間切れ|勝率|", "|---:|---|---|---|---:|"]
            for i, r in enumerate(rows[:5], 1):
                lines.append(f"|{i}|{r['Name']}|{league.roles_label(r['Roles'])}|{r['Wins']}/{r['Losses']}/{r['Timeouts']}|{r['WinRate']:.1%}|")
            pairs = selections.get(map_name, experiment["Pairs"])
            scores = pair_scores(rows, pairs)
            lines += ["", "|組み合わせ|基準|Aのみ|Bのみ|AB|組合せ差（ポイント）|各編成の試合数|", "|---|---:|---:|---:|---:|---:|---|"]
            for p in scores:
                lines.append("|" + p["Name"] + "|" + "|".join(f"{r:.1%}" for r in p["Rates"]) + f"|{p['Interaction'] * 100:+.1f}|" + "/".join(map(str, p["Counts"])) + "|")
    if confirmations:
        lookup = {name: {r["CandidateKey"]: r for r in map_ranking(report, name)}
                  for name, report in confirmations.items()}
        keys = set.intersection(*(set(rows) for rows in lookup.values()))
        baseline = experiment["BaselineKey"]
        special = []
        for key in keys:
            if key == baseline:
                continue
            lifts = {name: rows[key]["WinRate"] - rows[baseline]["WinRate"] for name, rows in lookup.items()}
            best = max(lifts, key=lifts.get)
            others = [value for name, value in lifts.items() if name != best]
            if others:
                special.append((lifts[best] - sum(others) / len(others), best, key, lifts))
        lines += ["", "## 地形特化の候補（再評価）", "",
                  "基準編成との差を地形ごとに計算。特化差は最良地形の差から他地形の平均差を引いた値。",
                  "固定マップ上の比較であり、同種の地形一般の効果を証明するものではない。", "",
                  "|編成|最良地形|その地形の勝率|基準からの差（ポイント）|他地形に対する特化差（ポイント）|", "|---|---|---:|---:|---:|"]
        for excess, best, key, lifts in sorted(special, reverse=True)[:8]:
            row = lookup[best][key]
            lines.append(f"|{row['Name']}|{best}|{row['WinRate']:.1%}|{lifts[best] * 100:+.1f}|{excess * 100:+.1f}|")
    league.batch_battles.write_json_atomic(directory / "analysis.json", {
        "Complete": complete, "ActualMatches": total, "Screen": screen,
        "Confirmations": confirmations, "SelectedPairs": selections, "Experiment": experiment, "Error": error})
    (directory / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=pathlib.Path, required=True)
    parser.add_argument("--output-directory", type=pathlib.Path, required=True)
    parser.add_argument("--plan-only", action="store_true")
    args = parser.parse_args()
    raw = league.batch_battles.load_config(args.config)
    experiment = raw["Experiment"]
    protocol = league.normalize_config(raw, argparse.Namespace(parallel=None, executable=None))
    candidate_keys = {c["CandidateKey"] for c in protocol["Candidates"]}
    if experiment["BaselineKey"] not in candidate_keys:
        raise ValueError("Missing common baseline candidate.")
    for pair in experiment["Pairs"]:
        if any(pair[field] not in candidate_keys for field in ("Base", "A", "B", "AB")):
            raise ValueError("Missing pair control candidate.")
    for field in ("ConfirmMatches", "PerMapTop", "ConfirmPairCount"):
        if type(experiment[field]) is not int or experiment[field] < 1:
            raise ValueError(field + " must be a positive integer.")
    protocol["Experiment"] = experiment
    maps = list(dict.fromkeys(s["MapName"] for s in protocol["Scenarios"]))
    screen_count = len(protocol["Candidates"]) * len(protocol["Enemies"]) * len(protocol["Scenarios"]) * protocol["ScreenMatches"]
    max_candidates = min(len(protocol["Candidates"]), len(maps) * experiment["PerMapTop"] + 3 + 4 * experiment["ConfirmPairCount"])
    max_confirm = max_candidates * len(protocol["Enemies"]) * len(protocol["Scenarios"]) * 2 * experiment["ConfirmMatches"]
    directory = args.output_directory
    directory.mkdir(parents=True, exist_ok=True)
    league.batch_battles.write_json_atomic(directory / "plan.json", {
        "ScreenMatches": screen_count, "MaximumConfirmMatches": max_confirm, "MaximumTotalMatches": screen_count + max_confirm,
        "SmokeMatches": 8,
        "Protocol": protocol})
    print(f"Planned: screen={screen_count}, confirmation<={max_confirm}, total<={screen_count + max_confirm}", flush=True)
    if args.plan_only:
        return 0
    executable = league.batch_battles.resolve_executable(pathlib.Path(protocol["Executable"]))
    manifest = league.prepare_run_directory(directory, protocol, executable)
    screen, confirmations, selections = None, {}, {}
    started = time.monotonic()
    try:
        smoke_maps = maps[:2]
        smoke_protocol = {**protocol, "Enemies": protocol["Enemies"][:1],
                          "Scenarios": [s for s in protocol["Scenarios"] if s["MapName"] in smoke_maps]}
        run_panel(smoke_protocol, directory, manifest, executable, protocol["Candidates"][:2], "smoke", 1,
                  protocol["ScreenMatches"] + experiment["ConfirmMatches"])
        screen = run_panel(protocol, directory, manifest, executable, protocol["Candidates"], "screen", protocol["ScreenMatches"], 0)
        write_summary(directory, screen, confirmations, selections, experiment)
        candidates, pairs = select_panel(screen, experiment)
        league.batch_battles.write_json_atomic(directory / "selection.json", {"Candidates": candidates, "Pairs": pairs})
        for index, map_name in enumerate(maps):
            selections[map_name] = pairs
            sub = {**protocol, "Scenarios": [s for s in protocol["Scenarios"] if s["MapName"] == map_name]}
            confirmations[map_name] = run_panel(sub, directory, manifest, executable, candidates, f"confirm_{index:02d}",
                                                experiment["ConfirmMatches"], protocol["ScreenMatches"], True)
            write_summary(directory, screen, confirmations, selections, experiment)
    except (Exception, KeyboardInterrupt) as error:
        message = str(error) or "Interrupted"
        write_summary(directory, screen, confirmations, selections, experiment, error=message)
        league.batch_battles.write_json_atomic(directory / "status.json", {"Status": "FAILED", "Error": message})
        print(message, file=sys.stderr, flush=True)
        return 1
    write_summary(directory, screen, confirmations, selections, experiment, True)
    league.batch_battles.write_json_atomic(directory / "status.json", {
        "Status": "COMPLETED", "ElapsedSeconds": time.monotonic() - started,
        "ActualMatches": screen["ActualMatches"] + sum(r["ActualMatches"] for r in confirmations.values()),
        "Metadata": manifest["RuntimeMetadata"]})
    return 0


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, league.interrupt_run)
    sys.exit(main())
