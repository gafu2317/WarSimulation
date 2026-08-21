#!/usr/bin/env python3

import argparse
import json
import pathlib


def parse_args():
    parser = argparse.ArgumentParser(description="Write a Markdown analysis from a parallel auto-battle summary.")
    parser.add_argument("--summary", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--top-count", type=int, default=20)
    return parser.parse_args()


def load_summary(path):
    with path.open(encoding="utf-8") as source:
        summary = json.load(source)
    if not summary.get("Ranking"):
        raise ValueError("Summary has no Ranking entries.")
    return summary


def aggregate_maps(ranking):
    maps = {}
    for candidate in ranking:
        for result in candidate["Maps"]:
            aggregate = maps.setdefault(
                result["MapName"],
                {"MapName": result["MapName"], "MatchCount": 0, "Wins": 0, "Losses": 0, "Timeouts": 0},
            )
            for field in ("MatchCount", "Wins", "Losses", "Timeouts"):
                aggregate[field] += result[field]

    for result in maps.values():
        result["WinRate"] = result["Wins"] / result["MatchCount"] if result["MatchCount"] else 0.0
    return sorted(maps.values(), key=lambda item: (-item["WinRate"], item["MapName"]))


def format_roles(candidate):
    return " / ".join(
        f"W{role['Weapon']}-P{role['Personality']}" for role in candidate["Roles"]
    )


def format_scenarios(candidate):
    scenarios = []
    for scenario in candidate["Scenarios"]:
        side = "反転" if scenario["StonePositionsReversed"] else "通常"
        scenarios.append(
            f"{scenario['MapName']} {side}: "
            f"{scenario['Wins']}/{scenario['MatchCount']}勝, "
            f"timeout {scenario['Timeouts']}"
        )
    return "、".join(scenarios)


def write_analysis(summary, output, top_count):
    ranking = summary["Ranking"]
    total_matches = sum(candidate["MatchCount"] for candidate in ranking)
    total_wins = sum(candidate["Wins"] for candidate in ranking)
    total_losses = sum(candidate["Losses"] for candidate in ranking)
    total_timeouts = sum(candidate["Timeouts"] for candidate in ranking)
    timeout_rate = total_timeouts / total_matches if total_matches else 0.0
    top = ranking[: max(1, top_count)]
    maps = aggregate_maps(ranking)
    best_map = maps[0] if maps else None
    worst_map = maps[-1] if maps else None

    lines = [
        "# 自動戦闘編成探索結果と考察",
        "",
        f"- 探索条件: {summary['WorkerCount']}並列 / Job Worker {summary['JobWorkerCount']}",
        f"- 完了試合数: {total_matches:,}",
        f"- 所要時間: {summary['ElapsedSeconds']:.1f}秒（{summary['MatchesPerMinute']:.2f}試合/分）",
        f"- 勝利: {total_wins:,} / 敗北: {total_losses:,} / timeout: {total_timeouts:,}（{timeout_rate:.1%}）",
        "",
        "## 上位編成",
        "",
        "| 順位 | CandidateKey | 勝率 | 勝敗timeout | 編成（Weapon-Personality） |",
        "|---:|---|---:|---|---|",
    ]
    for index, candidate in enumerate(top, start=1):
        lines.append(
            f"| {index} | `{candidate['CandidateKey']}` | {candidate['WinRate']:.1%} | "
            f"{candidate['Wins']}/{candidate['Losses']}/{candidate['Timeouts']} | {format_roles(candidate)} |"
        )

    lines.extend(["", "## 上位編成のマップ・配置別成績", ""])
    for index, candidate in enumerate(top, start=1):
        lines.extend([
            f"### {index}位 `{candidate['CandidateKey']}`",
            "",
            f"{format_scenarios(candidate)}",
            "",
        ])

    lines.extend([
        "## マップ傾向",
        "",
        "| 順位 | マップ | 勝率 | 勝敗timeout |",
        "|---:|---|---:|---|",
    ])
    for index, result in enumerate(maps, start=1):
        lines.append(
            f"| {index} | {result['MapName']} | {result['WinRate']:.1%} | "
            f"{result['Wins']}/{result['Losses']}/{result['Timeouts']} |"
        )

    lines.extend([
        "",
        "## 考察",
        "",
        f"- 全体ではtimeoutが{timeout_rate:.1%}を占めるため、勝率だけでなくtimeout数を併記して評価する。",
        f"- 全候補を通したマップ別では、{best_map['MapName']}が最高勝率（{best_map['WinRate']:.1%}）、"
        f"{worst_map['MapName']}が最低勝率（{worst_map['WinRate']:.1%}）。",
        "- 上位編成は`Scenarios`の通常・反転差を確認し、片側だけの強さを全マップ対応の強さと混同しない。",
        "- 1試合/条件のため、同率上位やtimeoutを含む順位は暫定値。候補を絞った後に複数シードで再評価する。",
        "",
        "## 再現",
        "",
        f"元データ: `{summary.get('Source', 'workers_5/summary.json')}`",
    ])

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    args = parse_args()
    if args.top_count < 1:
        raise ValueError("--top-count must be positive.")
    summary = load_summary(args.summary)
    summary["Source"] = str(args.summary)
    write_analysis(summary, args.output, args.top_count)
    print(args.output)


if __name__ == "__main__":
    main()
