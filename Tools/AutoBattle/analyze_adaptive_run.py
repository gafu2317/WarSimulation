#!/usr/bin/env python3

"""Convert an adaptive-search run into SQLite and compact analysis summaries."""

import argparse
import datetime
import json
import os
import pathlib
import sqlite3
import tempfile


SCHEMA = """
CREATE TABLE run_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
CREATE TABLE stages (
    stage TEXT PRIMARY KEY,
    source_kind TEXT NOT NULL,
    matches_per_candidate INTEGER,
    match_offset INTEGER,
    elapsed_seconds REAL,
    expected_jobs INTEGER NOT NULL DEFAULT 0,
    completed_jobs INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE jobs (
    stage TEXT NOT NULL,
    job_id TEXT NOT NULL,
    status TEXT NOT NULL,
    attempt TEXT,
    report_path TEXT,
    match_offset INTEGER,
    match_count INTEGER,
    elapsed_seconds REAL,
    PRIMARY KEY (stage, job_id)
);
CREATE TABLE scenarios (
    scenario_key TEXT PRIMARY KEY,
    map_name TEXT NOT NULL,
    stone_positions_reversed INTEGER NOT NULL
);
CREATE TABLE candidates (
    candidate_key TEXT PRIMARY KEY,
    first_stage TEXT NOT NULL,
    first_position INTEGER,
    roles_json TEXT NOT NULL
);
CREATE TABLE result_samples (
    id INTEGER PRIMARY KEY,
    stage TEXT NOT NULL,
    scenario_key TEXT NOT NULL,
    candidate_key TEXT NOT NULL,
    candidate_position INTEGER,
    match_offset INTEGER,
    match_count INTEGER NOT NULL,
    wins INTEGER NOT NULL,
    losses INTEGER NOT NULL,
    timeouts INTEGER NOT NULL,
    total_game_seconds REAL,
    total_real_seconds REAL,
    average_game_seconds REAL,
    average_real_seconds REAL,
    median_game_seconds REAL,
    median_decided_game_seconds REAL,
    game_seconds_samples_json TEXT,
    decided_game_seconds_samples_json TEXT,
    source_path TEXT NOT NULL,
    source_job TEXT
);
CREATE INDEX result_samples_lookup
    ON result_samples(stage, scenario_key, candidate_key);
CREATE TABLE model_effects (
    scenario_key TEXT NOT NULL,
    kind TEXT NOT NULL,
    label TEXT NOT NULL,
    left_label TEXT,
    right_label TEXT,
    coefficient REAL NOT NULL
);
CREATE INDEX model_effects_lookup
    ON model_effects(scenario_key, kind, coefficient);
CREATE VIEW candidate_results AS
SELECT
    stage,
    scenario_key,
    candidate_key,
    MIN(candidate_position) AS candidate_position,
    SUM(match_count) AS match_count,
    SUM(wins) AS wins,
    SUM(losses) AS losses,
    SUM(timeouts) AS timeouts,
    CAST(SUM(wins) AS REAL) / NULLIF(SUM(match_count), 0) AS win_rate,
    CAST(SUM(timeouts) AS REAL) / NULLIF(SUM(match_count), 0) AS timeout_rate,
    SUM(wins) - SUM(timeouts) AS score,
    SUM(total_game_seconds) AS total_game_seconds,
    SUM(total_real_seconds) AS total_real_seconds,
    CAST(SUM(total_game_seconds) AS REAL) / NULLIF(SUM(match_count), 0) AS average_game_seconds,
    CAST(SUM(total_real_seconds) AS REAL) / NULLIF(SUM(match_count), 0) AS average_real_seconds,
    MIN(median_game_seconds) AS median_game_seconds,
    MAX(median_game_seconds) AS max_median_game_seconds
FROM result_samples
GROUP BY stage, scenario_key, candidate_key;
CREATE VIEW scenario_results AS
SELECT
    result_samples.stage,
    result_samples.scenario_key,
    scenarios.map_name,
    scenarios.stone_positions_reversed,
    SUM(result_samples.match_count) AS match_count,
    SUM(result_samples.wins) AS wins,
    SUM(result_samples.losses) AS losses,
    SUM(result_samples.timeouts) AS timeouts,
    CAST(SUM(result_samples.wins) AS REAL)
        / NULLIF(SUM(result_samples.match_count), 0) AS win_rate,
    CAST(SUM(result_samples.timeouts) AS REAL)
        / NULLIF(SUM(result_samples.match_count), 0) AS timeout_rate,
    SUM(result_samples.total_game_seconds) AS total_game_seconds,
    SUM(result_samples.total_real_seconds) AS total_real_seconds,
    CAST(SUM(result_samples.total_game_seconds) AS REAL)
        / NULLIF(SUM(result_samples.match_count), 0) AS average_game_seconds,
    CAST(SUM(result_samples.total_real_seconds) AS REAL)
        / NULLIF(SUM(result_samples.match_count), 0) AS average_real_seconds
FROM result_samples
JOIN scenarios USING (scenario_key)
GROUP BY result_samples.stage, result_samples.scenario_key;
CREATE VIEW map_results AS
SELECT
    stage,
    map_name,
    SUM(match_count) AS match_count,
    SUM(wins) AS wins,
    SUM(losses) AS losses,
    SUM(timeouts) AS timeouts,
    CAST(SUM(wins) AS REAL) / NULLIF(SUM(match_count), 0) AS win_rate,
    CAST(SUM(timeouts) AS REAL) / NULLIF(SUM(match_count), 0) AS timeout_rate,
    CAST(SUM(total_game_seconds) AS REAL)
        / NULLIF(SUM(match_count), 0) AS average_game_seconds,
    CAST(SUM(total_real_seconds) AS REAL)
        / NULLIF(SUM(match_count), 0) AS average_real_seconds
FROM scenario_results
GROUP BY stage, map_name;
CREATE VIEW candidate_overall_results AS
SELECT
    stage,
    candidate_key,
    SUM(match_count) AS match_count,
    SUM(wins) AS wins,
    SUM(losses) AS losses,
    SUM(timeouts) AS timeouts,
    CAST(SUM(wins) AS REAL) / NULLIF(SUM(match_count), 0) AS win_rate,
    CAST(SUM(timeouts) AS REAL) / NULLIF(SUM(match_count), 0) AS timeout_rate,
    SUM(wins) - SUM(timeouts) AS score,
    CAST(SUM(total_game_seconds) AS REAL)
        / NULLIF(SUM(match_count), 0) AS average_game_seconds,
    CAST(SUM(total_real_seconds) AS REAL)
        / NULLIF(SUM(match_count), 0) AS average_real_seconds
FROM candidate_results
GROUP BY stage, candidate_key;
"""


def parse_args():
    parser = argparse.ArgumentParser(
        description="Convert a stopped or completed adaptive-search run to SQLite."
    )
    parser.add_argument("--run-directory", required=True, type=pathlib.Path)
    parser.add_argument("--database", type=pathlib.Path)
    parser.add_argument("--summary-json", type=pathlib.Path)
    parser.add_argument("--summary-markdown", type=pathlib.Path)
    return parser.parse_args()


def load_json(path):
    with path.open(encoding="utf-8") as source:
        return json.load(source)


def make_scenario_key(map_name, reversed_positions):
    return f"{map_name}_{'reversed' if reversed_positions else 'normal'}"


def add_scenario(connection, scenario_key, map_name, reversed_positions):
    connection.execute(
        "INSERT OR IGNORE INTO scenarios(scenario_key, map_name, stone_positions_reversed) VALUES (?, ?, ?)",
        (scenario_key, map_name, int(bool(reversed_positions))),
    )


def add_candidate(connection, stage, candidate_position, candidate_key, roles):
    connection.execute(
        """
        INSERT OR IGNORE INTO candidates(
            candidate_key, first_stage, first_position, roles_json
        ) VALUES (?, ?, ?, ?)
        """,
        (
            candidate_key,
            stage,
            candidate_position,
            json.dumps(roles, ensure_ascii=False, separators=(",", ":")),
        ),
    )


def add_result_sample(
    connection,
    stage,
    scenario_key,
    candidate_position,
    result,
    source_path,
    source_job,
    match_offset,
):
    candidate_key = result["CandidateKey"]
    roles = result.get("Roles", [])
    add_candidate(connection, stage, candidate_position, candidate_key, roles)
    connection.execute(
        """
        INSERT INTO result_samples(
            stage, scenario_key, candidate_key, candidate_position,
            match_offset, match_count, wins, losses, timeouts,
            total_game_seconds, total_real_seconds, average_game_seconds,
            average_real_seconds, median_game_seconds,
            median_decided_game_seconds, game_seconds_samples_json,
            decided_game_seconds_samples_json, source_path, source_job
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            stage,
            scenario_key,
            candidate_key,
            candidate_position,
            match_offset,
            result.get("MatchCount", 0),
            result.get("Wins", 0),
            result.get("Losses", 0),
            result.get("Timeouts", 0),
            result.get("TotalGameSeconds"),
            result.get("TotalRealSeconds"),
            result.get("AverageGameSeconds"),
            result.get("AverageRealSeconds"),
            result.get("MedianGameSeconds"),
            result.get("MedianDecidedGameSeconds"),
            json.dumps(result.get("GameSecondsSamples", []), separators=(",", ":")),
            json.dumps(result.get("DecidedGameSecondsSamples", []), separators=(",", ":")),
            source_path,
            source_job,
        ),
    )


def iter_report_results(stage, payload, source_path, source_job, match_offset=0, scenario_hint=None):
    if isinstance(payload.get("Scenarios"), dict):
        for scenario_key, ranking in payload["Scenarios"].items():
            if isinstance(ranking, dict):
                ranking = ranking.values()
            for result in ranking:
                yield from iter_result_scenarios(
                    stage, result, scenario_key, source_path, source_job, match_offset
                )
        return
    for result in payload.get("Ranking", []):
        yield from iter_result_scenarios(
            stage, result, scenario_hint, source_path, source_job, match_offset
        )


def iter_result_scenarios(stage, result, scenario_hint, source_path, source_job, match_offset):
    scenarios = result.get("Scenarios") or [None]
    for scenario in scenarios:
        if scenario is None:
            scenario_key = scenario_hint
            if not scenario_key:
                continue
        else:
            scenario_key = make_scenario_key(
                scenario["MapName"], scenario.get("StonePositionsReversed", False)
            )
        sample_result = dict(result)
        if scenario is not None:
            sample_result.update(scenario)
        yield {
            "stage": stage,
            "scenario_key": scenario_key,
            "candidate_position": result.get("Index"),
            "result": sample_result,
            "source_path": source_path,
            "source_job": source_job,
            "match_offset": match_offset,
        }


def record_stage(connection, stage, source_kind, matches, match_offset, elapsed, expected, completed):
    connection.execute(
        """
        INSERT OR REPLACE INTO stages(
            stage, source_kind, matches_per_candidate, match_offset,
            elapsed_seconds, expected_jobs, completed_jobs
        ) VALUES (?, ?, ?, ?, ?, ?, ?)
        """,
        (stage, source_kind, matches, match_offset, elapsed, expected, completed),
    )


def record_root_stage(connection, run_directory, path):
    payload = load_json(path)
    stage = payload["Stage"]
    jobs = payload.get("Jobs", [])
    completed = sum(job.get("Status") == "completed" for job in jobs)
    record_stage(
        connection,
        stage,
        "aggregate",
        payload.get("MatchesPerCandidate"),
        payload.get("MatchOffset", 0),
        payload.get("ElapsedSeconds"),
        len(jobs),
        completed,
    )
    for job in jobs:
        connection.execute(
            """
            INSERT OR REPLACE INTO jobs(
                stage, job_id, status, attempt, report_path,
                match_offset, match_count, elapsed_seconds
            ) VALUES (?, ?, ?, NULL, NULL, ?, ?, NULL)
            """,
            (
                stage,
                job.get("Id", ""),
                job.get("Status", "unknown"),
                payload.get("MatchOffset", 0),
                payload.get("MatchesPerCandidate"),
            ),
        )
    source_path = str(path.relative_to(run_directory))
    for item in iter_report_results(stage, payload, source_path, None, payload.get("MatchOffset", 0)):
        add_scenario_from_key(connection, item["scenario_key"])
        add_result_sample(
            connection,
            item["stage"],
            item["scenario_key"],
            item["candidate_position"],
            item["result"],
            item["source_path"],
            item["source_job"],
            item["match_offset"],
        )


def add_scenario_from_key(connection, scenario_key):
    if connection.execute(
        "SELECT 1 FROM scenarios WHERE scenario_key = ?", (scenario_key,)
    ).fetchone():
        return
    map_name, orientation = scenario_key.rsplit("_", 1)
    add_scenario(connection, scenario_key, map_name, orientation == "reversed")


def record_complete_jobs(connection, run_directory, root_stages):
    for stage_directory in sorted((run_directory / "stages").glob("*/")):
        stage = stage_directory.name
        if stage in root_stages:
            continue
        job_directories = sorted((stage_directory / "jobs").glob("*/"))
        if not job_directories:
            continue
        complete_count = 0
        expected_count = len(job_directories)
        for job_directory in job_directories:
            marker_path = job_directory / "complete.json"
            status = "completed" if marker_path.exists() else "incomplete"
            report_path = None
            attempt = None
            match_offset = None
            match_count = None
            elapsed = None
            if marker_path.exists():
                marker = load_json(marker_path)
                report_path = job_directory / marker["Report"]
                if not report_path.exists():
                    raise FileNotFoundError(f"Complete marker points to missing report: {report_path}")
                attempt = report_path.parts[-3]
                config_path = report_path.parent.parent / "config.json"
                config = load_json(config_path) if config_path.exists() else {}
                match_offset = config.get("MatchOffset", 0)
                match_count = config.get("MatchesPerCandidate")
                elapsed = marker.get("ElapsedSeconds")
                complete_count += 1
                payload = load_json(report_path)
                source_path = str(report_path.relative_to(run_directory))
                for item in iter_report_results(
                    stage,
                    payload,
                    source_path,
                    job_directory.name,
                    match_offset,
                    (config.get("MapNames") or [None])[0],
                ):
                    add_scenario_from_key(connection, item["scenario_key"])
                    add_result_sample(
                        connection,
                        item["stage"],
                        item["scenario_key"],
                        item["candidate_position"],
                        item["result"],
                        item["source_path"],
                        item["source_job"],
                        item["match_offset"],
                    )
            connection.execute(
                """
                INSERT OR REPLACE INTO jobs(
                    stage, job_id, status, attempt, report_path,
                    match_offset, match_count, elapsed_seconds
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    stage,
                    job_directory.name,
                    status,
                    attempt,
                    str(report_path.relative_to(run_directory)) if report_path else None,
                    match_offset,
                    match_count,
                    elapsed,
                ),
            )
        record_stage(
            connection,
            stage,
            "complete_jobs",
            None,
            None,
            None,
            expected_count,
            complete_count,
        )


def record_models(connection, run_directory):
    path = run_directory / "models.json"
    if not path.exists():
        return
    payload = load_json(path)
    for fallback_key, report in payload.get("Models", {}).items():
        scenario_key = report.get("Scenario", {}).get("Id", fallback_key)
        model = report.get("Model", {})
        for item in model.get("RoleEffects", []):
            connection.execute(
                "INSERT INTO model_effects VALUES (?, 'role', ?, NULL, NULL, ?)",
                (scenario_key, item["Label"], item["Coefficient"]),
            )
        for item in model.get("RolePairEffects", []):
            label = f"{item['Left']} + {item['Right']}"
            connection.execute(
                "INSERT INTO model_effects VALUES (?, 'pair', ?, ?, ?, ?)",
                (scenario_key, label, item["Left"], item["Right"], item["Coefficient"]),
            )


def build_database(run_directory, database_path=None):
    run_directory = pathlib.Path(run_directory).resolve()
    if not run_directory.is_dir():
        raise FileNotFoundError(f"Run directory not found: {run_directory}")
    database_path = pathlib.Path(database_path or run_directory / "analysis.db").resolve()
    database_path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(
        prefix=f"{database_path.stem}_", suffix=".tmp", dir=database_path.parent
    )
    os.close(fd)
    pathlib.Path(temporary_name).unlink()
    try:
        connection = sqlite3.connect(temporary_name)
        connection.executescript(SCHEMA)
        manifest_path = run_directory / "manifest.json"
        manifest = load_json(manifest_path) if manifest_path.exists() else {}
        for key, value in manifest.items():
            connection.execute(
                "INSERT INTO run_metadata(key, value) VALUES (?, ?)",
                (key, json.dumps(value, ensure_ascii=False)),
            )
        root_stages = set()
        initial_path = run_directory / "initial-results.json"
        if initial_path.exists():
            root_stages.add(load_json(initial_path)["Stage"])
            record_root_stage(connection, run_directory, initial_path)
        record_complete_jobs(connection, run_directory, root_stages)
        record_models(connection, run_directory)
        excluded_jobs = connection.execute(
            "SELECT COUNT(*) FROM jobs WHERE status = 'incomplete'"
        ).fetchone()[0]
        connection.execute(
            "INSERT OR REPLACE INTO run_metadata(key, value) VALUES ('excluded_incomplete_jobs', ?)",
            (json.dumps(excluded_jobs),),
        )
        connection.execute(
            "INSERT OR REPLACE INTO run_metadata(key, value) VALUES ('generated_at_utc', ?)",
            (json.dumps(datetime.datetime.now(datetime.timezone.utc).isoformat()),),
        )
        connection.commit()
        connection.close()
        pathlib.Path(temporary_name).replace(database_path)
    except Exception:
        pathlib.Path(temporary_name).unlink(missing_ok=True)
        raise
    return database_path


def query_rows(connection, sql, parameters=()):
    connection.row_factory = sqlite3.Row
    return [dict(row) for row in connection.execute(sql, parameters)]


def make_summary(database_path):
    connection = sqlite3.connect(database_path)
    connection.row_factory = sqlite3.Row
    stages = query_rows(
        connection,
        """
        SELECT stage, source_kind, matches_per_candidate, match_offset,
               elapsed_seconds, expected_jobs, completed_jobs,
               expected_jobs - completed_jobs AS incomplete_jobs
        FROM stages ORDER BY stage
        """,
    )
    scenario_outcomes = query_rows(
        connection,
        """
        SELECT scenario_key, SUM(match_count) AS matches, SUM(wins) AS wins,
               SUM(losses) AS losses, SUM(timeouts) AS timeouts,
               CAST(SUM(wins) AS REAL) / NULLIF(SUM(match_count), 0) AS win_rate,
               CAST(SUM(timeouts) AS REAL) / NULLIF(SUM(match_count), 0) AS timeout_rate
        FROM result_samples WHERE stage = 'initial'
        GROUP BY scenario_key ORDER BY timeout_rate DESC, scenario_key
        """,
    )
    top_candidates = {}
    for stage in (row["stage"] for row in stages):
        top_candidates[stage] = {}
        scenario_keys = query_rows(
            connection,
            "SELECT scenario_key FROM scenarios ORDER BY scenario_key",
        )
        for scenario in scenario_keys:
            rows = query_rows(
                connection,
                """
                SELECT candidate_key, match_count, wins, losses, timeouts,
                       win_rate, timeout_rate, score
                FROM candidate_results
                WHERE stage = ? AND scenario_key = ?
                ORDER BY wins DESC, timeouts ASC, win_rate DESC, candidate_key
                LIMIT 5
                """,
                (stage, scenario["scenario_key"]),
            )
            if rows:
                top_candidates[stage][scenario["scenario_key"]] = rows
    model_effects = {}
    for scenario in query_rows(
        connection, "SELECT DISTINCT scenario_key FROM model_effects ORDER BY scenario_key"
    ):
        key = scenario["scenario_key"]
        model_effects[key] = {
            "positive_roles": query_rows(
                connection,
                """
                SELECT label, coefficient FROM model_effects
                WHERE scenario_key = ? AND kind = 'role'
                ORDER BY coefficient DESC LIMIT 5
                """,
                (key,),
            ),
            "negative_roles": query_rows(
                connection,
                """
                SELECT label, coefficient FROM model_effects
                WHERE scenario_key = ? AND kind = 'role'
                ORDER BY coefficient ASC LIMIT 5
                """,
                (key,),
            ),
            "positive_pairs": query_rows(
                connection,
                """
                SELECT label, coefficient FROM model_effects
                WHERE scenario_key = ? AND kind = 'pair'
                ORDER BY coefficient DESC LIMIT 5
                """,
                (key,),
            ),
        }
    excluded = connection.execute(
        "SELECT value FROM run_metadata WHERE key = 'excluded_incomplete_jobs'"
    ).fetchone()
    connection.close()
    return {
        "database": str(pathlib.Path(database_path).resolve()),
        "stages": stages,
        "excluded_incomplete_jobs": json.loads(excluded[0]) if excluded else 0,
        "initial_scenario_outcomes": scenario_outcomes,
        "top_candidates": top_candidates,
        "model_effects": model_effects,
    }


def write_summary(summary, json_path, markdown_path):
    json_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    lines = [
        "# Adaptive Search Analysis",
        "",
        f"- SQLite: `{summary['database']}`",
        f"- 途中ジョブとして除外: {summary['excluded_incomplete_jobs']}件",
        "",
        "## Stage",
        "",
        "| stage | source | completed / expected jobs |",
        "|---|---|---:|",
    ]
    for stage in summary["stages"]:
        lines.append(
            f"| {stage['stage']} | {stage['source_kind']} | "
            f"{stage['completed_jobs']} / {stage['expected_jobs']} |"
        )
    lines.extend([
        "",
        "## Initial scenario outcomes",
        "",
        "| scenario | matches | wins | losses | timeouts | timeout rate |",
        "|---|---:|---:|---:|---:|---:|",
    ])
    for row in summary["initial_scenario_outcomes"]:
        lines.append(
            f"| {row['scenario_key']} | {row['matches']} | {row['wins']} | "
            f"{row['losses']} | {row['timeouts']} | {row['timeout_rate']:.1%} |"
        )
    lines.extend(["", "## Model effects (top positive roles)", ""])
    for scenario, effects in summary["model_effects"].items():
        labels = ", ".join(
            f"{row['label']} ({row['coefficient']:+.3f})"
            for row in effects["positive_roles"]
        )
        lines.append(f"- `{scenario}`: {labels}")
    markdown_path.parent.mkdir(parents=True, exist_ok=True)
    markdown_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    args = parse_args()
    run_directory = args.run_directory.resolve()
    database_path = build_database(run_directory, args.database)
    summary = make_summary(database_path)
    write_summary(
        summary,
        (args.summary_json or run_directory / "ai-summary.json").resolve(),
        (args.summary_markdown or run_directory / "ai-summary.md").resolve(),
    )
    print(f"SQLite: {database_path}")
    print(f"JSON: {args.summary_json or run_directory / 'ai-summary.json'}")
    print(f"Markdown: {args.summary_markdown or run_directory / 'ai-summary.md'}")


if __name__ == "__main__":
    main()
