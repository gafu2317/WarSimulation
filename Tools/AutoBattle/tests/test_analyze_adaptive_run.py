import importlib.util
import json
import pathlib
import sqlite3
import tempfile
import unittest


SCRIPT_PATH = pathlib.Path(__file__).parents[1] / "analyze_adaptive_run.py"
SPEC = importlib.util.spec_from_file_location("analyze_adaptive_run", SCRIPT_PATH)
analyze_adaptive_run = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(analyze_adaptive_run)


def make_result(candidate_key, wins, losses, timeouts):
    match_count = wins + losses + timeouts
    return {
        "Index": 0,
        "CandidateKey": candidate_key,
        "Roles": [{"Weapon": 1, "Personality": 2}],
        "MatchCount": match_count,
        "Wins": wins,
        "Losses": losses,
        "Timeouts": timeouts,
        "WinRate": wins / match_count,
        "Scenarios": [
            {
                "MapName": "Map",
                "StonePositionsReversed": False,
                "MatchCount": match_count,
                "Wins": wins,
                "Losses": losses,
                "Timeouts": timeouts,
                "TotalGameSeconds": 12.0,
                "TotalRealSeconds": 2.0,
                "AverageGameSeconds": 12.0 / match_count,
                "AverageRealSeconds": 2.0 / match_count,
                "GameSecondsSamples": [12.0],
                "DecidedGameSecondsSamples": [12.0],
            }
        ],
    }


class AdaptiveRunAnalysisTests(unittest.TestCase):
    def test_build_database_flattens_initial_results_into_queryable_rows(self):
        with tempfile.TemporaryDirectory() as temporary:
            run_directory = pathlib.Path(temporary)
            (run_directory / "manifest.json").write_text(
                json.dumps({"CandidateCount": 6993}), encoding="utf-8"
            )
            payload = {
                "Stage": "initial",
                "MatchesPerCandidate": 1,
                "MatchOffset": 0,
                "Jobs": [{"Id": "job", "Status": "completed"}],
                "Scenarios": {
                    "Map_normal": {
                        "1:2|1:2|1:2|1:2|1:2": make_result(
                            "1:2|1:2|1:2|1:2|1:2", 1, 0, 0
                        )
                    }
                },
            }
            (run_directory / "initial-results.json").write_text(
                json.dumps(payload), encoding="utf-8"
            )

            database = analyze_adaptive_run.build_database(run_directory)
            connection = sqlite3.connect(database)
            result = connection.execute(
                "SELECT match_count, wins, losses, timeouts, score FROM candidate_results"
            ).fetchone()
            scenario_result = connection.execute(
                "SELECT match_count, wins, losses, timeouts FROM scenario_results"
            ).fetchone()
            map_result = connection.execute(
                "SELECT match_count, wins, losses, timeouts FROM map_results"
            ).fetchone()
            overall_result = connection.execute(
                "SELECT match_count, wins, losses, timeouts FROM candidate_overall_results"
            ).fetchone()
            connection.close()

            self.assertEqual(result, (1, 1, 0, 0, 1))
            self.assertEqual(scenario_result, (1, 1, 0, 0))
            self.assertEqual(map_result, (1, 1, 0, 0))
            self.assertEqual(overall_result, (1, 1, 0, 0))

    def test_incomplete_jobs_are_recorded_but_not_loaded_as_results(self):
        with tempfile.TemporaryDirectory() as temporary:
            run_directory = pathlib.Path(temporary)
            job_directory = run_directory / "stages" / "probe" / "jobs" / "job_001"
            job_directory.mkdir(parents=True)

            database = analyze_adaptive_run.build_database(run_directory)
            connection = sqlite3.connect(database)
            jobs = connection.execute(
                "SELECT status FROM jobs WHERE stage = 'probe'"
            ).fetchall()
            result_count = connection.execute(
                "SELECT COUNT(*) FROM result_samples WHERE stage = 'probe'"
            ).fetchone()[0]
            excluded = connection.execute(
                "SELECT value FROM run_metadata WHERE key = 'excluded_incomplete_jobs'"
            ).fetchone()[0]
            connection.close()

            self.assertEqual(jobs, [("incomplete",)])
            self.assertEqual(result_count, 0)
            self.assertEqual(json.loads(excluded), 1)


if __name__ == "__main__":
    unittest.main()
