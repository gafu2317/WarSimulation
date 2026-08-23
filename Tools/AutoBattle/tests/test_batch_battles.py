import importlib.util
import json
import pathlib
import tempfile
import unittest


SCRIPT_PATH = pathlib.Path(__file__).parents[1] / "batch_battles.py"
SPEC = importlib.util.spec_from_file_location("batch_battles", SCRIPT_PATH)
batch_battles = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(batch_battles)


class BatchBattlesTests(unittest.TestCase):
    def test_build_jobs_expands_maps_and_match_ranges(self):
        config = {
            "MapNames": ["Map A", "Map B"],
            "Candidates": [{"Roles": [{"Weapon": 1, "Personality": 2}]}],
            "MatchesPerCandidate": 5,
            "TimeScale": 2,
        }
        settings = {
            "matches_per_job": 2,
            "map_execution": "each",
            "time_scale": 8,
        }

        jobs = batch_battles.build_jobs(config, settings)

        self.assertEqual(len(jobs), 6)
        self.assertEqual(jobs[0]["config"]["MapNames"], ["Map A"])
        self.assertEqual(jobs[0]["config"]["MatchesPerCandidate"], 2)
        self.assertEqual(jobs[2]["config"]["MatchesPerCandidate"], 1)
        self.assertEqual(jobs[3]["config"]["MapNames"], ["Map B"])
        self.assertEqual(jobs[3]["config"]["MatchOffset"], 0)
        self.assertEqual(jobs[0]["config"]["TotalMatchesPerCandidate"], 5)
        self.assertEqual(jobs[0]["config"]["TimeScale"], 8)

    def test_build_jobs_keeps_maps_together_for_seeded_selection(self):
        config = {
            "MapNames": ["Map A", "Map B"],
            "Candidates": [{"Roles": [{"Weapon": 1, "Personality": 2}]}],
            "MatchesPerCandidate": 3,
        }
        settings = {
            "matches_per_job": 2,
            "map_execution": "seeded",
            "time_scale": 4,
        }

        jobs = batch_battles.build_jobs(config, settings)

        self.assertEqual(len(jobs), 2)
        self.assertEqual(jobs[0]["config"]["MapNames"], ["Map A", "Map B"])

    def test_merge_reports_aggregates_candidates_scenarios_and_durations(self):
        reports = [
            self._report(1, 0, 0, 10.0, 1.0),
            self._report(0, 1, 1, 30.0, 3.0),
        ]

        ranking = batch_battles.merge_reports(reports)

        result = ranking[0]
        self.assertEqual(result["MatchCount"], 3)
        self.assertEqual(result["Wins"], 1)
        self.assertEqual(result["Losses"], 1)
        self.assertEqual(result["Timeouts"], 1)
        self.assertAlmostEqual(result["WinRate"], 1 / 3)
        self.assertAlmostEqual(result["AverageGameSeconds"], 40 / 3)
        self.assertEqual(result["MedianGameSeconds"], 15.0)
        self.assertEqual(result["MinGameSeconds"], 10.0)
        self.assertEqual(result["MaxGameSeconds"], 15.0)
        self.assertEqual(result["MedianDecidedGameSeconds"], 12.5)
        self.assertEqual(len(result["Scenarios"]), 1)
        self.assertEqual(result["Scenarios"][0]["MatchCount"], 3)

    def test_load_completed_job_reuses_only_a_complete_valid_report(self):
        with tempfile.TemporaryDirectory() as temporary:
            job_directory = pathlib.Path(temporary)
            report_path = job_directory / "attempt_001" / "AutoBattles" / "sweep.json"
            report_path.parent.mkdir(parents=True)
            report = self._report(1, 0, 0, 10.0, 1.0)
            report.update({"SchemaVersion": 2, "CandidateCount": 1, "CompletedCandidates": 1})
            report_path.write_text(json.dumps(report), encoding="utf-8")
            (job_directory / "complete.json").write_text(
                json.dumps({"Report": "attempt_001/AutoBattles/sweep.json"}),
                encoding="utf-8",
            )

            loaded = batch_battles.load_completed_job(job_directory, 1, 1)

            self.assertEqual(loaded["CompletedCandidates"], 1)

    def test_collect_metadata_reports_mixed_build_conditions(self):
        reports = [
            {"FixedDeltaTime": 0.02, "PreserveFixedDeltaTime": True, "PlayerBuildGuid": "a", "UnityVersion": "u"},
            {"FixedDeltaTime": 0.16, "PreserveFixedDeltaTime": False, "PlayerBuildGuid": "a", "UnityVersion": "u"},
        ]

        metadata = batch_battles.collect_metadata(reports)

        self.assertFalse(metadata["MetadataConsistent"])
        self.assertEqual(len(metadata["MetadataVariants"]), 2)
        json.dumps(metadata)

    def test_validate_report_rejects_a_stale_player_schema(self):
        report = self._report(1, 0, 0, 10.0, 1.0)
        report.update({"CandidateCount": 1, "CompletedCandidates": 1})

        with self.assertRaisesRegex(ValueError, "Rebuild the Player"):
            batch_battles.validate_report(report, 1, 1)

    @staticmethod
    def _report(wins, losses, timeouts, game_seconds, real_seconds):
        matches = wins + losses + timeouts
        candidate = {
            "CandidateKey": "1:2",
            "Roles": [{"Weapon": 1, "Personality": 2}],
            "MatchCount": matches,
            "Wins": wins,
            "Losses": losses,
            "Timeouts": timeouts,
            "TotalGameSeconds": game_seconds,
            "TotalRealSeconds": real_seconds,
            "TotalSkippedAiDecisionCount": matches,
            "GameSecondsSamples": [game_seconds / matches] * matches,
            "DecidedGameSecondsSamples": [game_seconds / matches] * (wins + losses),
            "Scenarios": [
                {
                    "MapName": "Map A",
                    "StonePositionsReversed": False,
                    "MatchCount": matches,
                    "Wins": wins,
                    "Losses": losses,
                    "Timeouts": timeouts,
                    "TotalGameSeconds": game_seconds,
                    "TotalRealSeconds": real_seconds,
                    "TotalSkippedAiDecisionCount": matches,
                    "GameSecondsSamples": [game_seconds / matches] * matches,
                    "DecidedGameSecondsSamples": [game_seconds / matches] * (wins + losses),
                }
            ],
        }
        return {"Ranking": [candidate]}


if __name__ == "__main__":
    unittest.main()
