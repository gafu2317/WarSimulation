import argparse
import copy
import importlib.util
import json
import pathlib
import tempfile
import unittest
from unittest import mock


SCRIPT_PATH = pathlib.Path(__file__).parents[1] / "league_search.py"
SPEC = importlib.util.spec_from_file_location("league_search", SCRIPT_PATH)
league_search = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(league_search)


def party(name, weapon, personality=0):
    return {"Name": name, "Roles": [{"Weapon": weapon, "Personality": personality}]}


def config():
    return {
        "Candidates": [party("A", 1), party("B", 3), party("C", 2)],
        "Enemies": [party("Shield", 2, 9), party("Bible", 5, 9)],
        "Scenarios": [{"MapName": "Map A", "StonePositionsReversed": False},
                      {"MapName": "Map A", "StonePositionsReversed": True}],
        "ScreenMatches": 2, "FinalMatches": 3, "FinalCandidates": 2,
        "LeagueMatches": 2, "LeagueCandidates": 2,
        "BaseSeed": 100, "TimeScale": 4, "PreserveFixedDeltaTime": True,
        "Batch": {"ParallelProcesses": 1, "MatchesPerJob": 1, "JobWorkerCount": 1},
    }


def normalize(value=None):
    args = argparse.Namespace(parallel=None, executable=None)
    return league_search.normalize_config(value or config(), args)


def report_for(job, winner=lambda candidate: True, build="build-a"):
    count = job["match_count"]
    report = {
        "SchemaVersion": 2, "CandidateCount": len(job["candidates"]), "CompletedCandidates": len(job["candidates"]),
        "TimeScale": job["config"]["TimeScale"], "FixedDeltaTime": 0.02,
        "PreserveFixedDeltaTime": job["config"]["PreserveFixedDeltaTime"],
        "PlayerBuildGuid": build, "UnityVersion": "6000.3", "Ranking": [],
    }
    for candidate in job["candidates"]:
        wins = count if winner(candidate) else 0
        totals = {
            "MatchCount": count, "Wins": wins, "Losses": count - wins, "Timeouts": 0,
            "TotalSkippedAiDecisionCount": 0, "TotalGameSeconds": count * 10.0, "TotalRealSeconds": count * 2.5,
            "GameSecondsSamples": [10.0] * count, "DecidedGameSecondsSamples": [10.0] * count,
        }
        report["Ranking"].append({
            "CandidateKey": candidate["CandidateKey"], "Roles": candidate["Roles"], **totals,
            "Scenarios": [{"MapName": job["scenario"]["MapName"],
                           "StonePositionsReversed": job["scenario"]["StonePositionsReversed"], **totals}],
        })
    return report


class LeagueSearchTests(unittest.TestCase):
    def test_preserves_role_order_and_rejects_duplicate_sets(self):
        value = config()
        ordered = [{"Weapon": 3, "Personality": 7}, {"Weapon": 1, "Personality": 2}]
        value["Candidates"][0]["Roles"] = ordered
        self.assertEqual(normalize(value)["Candidates"][0]["Roles"], ordered)
        value["Candidates"][1]["Roles"] = list(reversed(ordered))
        with self.assertRaisesRegex(ValueError, "unique"):
            normalize(value)

    def test_japanese_role_labels_keep_registration_order(self):
        label = league_search.roles_label([{"Weapon": 3, "Personality": 7}, {"Weapon": 1, "Personality": 21}])
        self.assertEqual(label, "1. 杖（狡猾） → 2. 剣（便乗屋）")

    def test_screen_subset_and_plan_count(self):
        value = config()
        value["ScreenScenarios"] = value["Scenarios"][:1]
        planned = league_search.plan(normalize(value))
        self.assertEqual(planned["ScreenMatches"], 12)
        self.assertEqual(planned["FinalMatches"], 24)
        self.assertEqual(planned["LeagueMatches"], 8)
        value["ScreenScenarios"] = [{"MapName": "Unknown"}]
        with self.assertRaisesRegex(ValueError, "subset"):
            normalize(value)

    def test_jobs_use_requested_enemy_pool_and_disjoint_seed_ranges(self):
        protocol = normalize()
        screen = league_search.build_jobs(protocol, protocol["Candidates"], protocol["Enemies"], "screen", 2, 0)
        final = league_search.build_jobs(protocol, protocol["Candidates"][:2], protocol["Enemies"], "final", 3, 2)
        self.assertEqual({league_search.candidate_key(job["config"]["Enemy"]) for job in screen}, {"2:9", "5:9"})
        self.assertEqual({job["config"]["BaseSeed"] + job["config"]["MatchOffset"] for job in screen}, {100, 101})
        self.assertEqual({job["config"]["BaseSeed"] + job["config"]["MatchOffset"] for job in final}, {102, 103, 104})
        self.assertTrue(all(job["config"]["UseCommonSeeds"] for job in screen + final))

    def test_league_evaluates_each_pair_in_both_directions_and_omits_self(self):
        protocol = normalize()
        candidates = protocol["Candidates"][:2]
        jobs = league_search.build_jobs(protocol, candidates, candidates, "league", 2, 5, True)
        pairs = {(candidate["Name"], job["enemy"]["Name"]) for job in jobs for candidate in job["candidates"]}
        self.assertEqual(pairs, {("A", "B"), ("B", "A")})
        self.assertEqual(len(jobs), 8)

    def test_league_team_side_advantage_cancels_in_candidate_ranking(self):
        protocol = normalize()
        candidates = protocol["Candidates"][:2]
        for ally_wins in (True, False):
            with self.subTest(ally_wins=ally_wins):
                jobs = league_search.build_jobs(protocol, candidates, candidates, "league", 2, 5, True)
                results = [(job, report_for(job, lambda candidate: ally_wins)) for job in jobs]
                aggregated = league_search.aggregate_results(protocol, candidates, candidates, results, 2, True)
                self.assertEqual([row["WinRate"] for row in aggregated["Ranking"]], [0.5, 0.5])
                self.assertTrue(all(cell["MatchCount"] == 4 for cell in aggregated["Cells"]))
                self.assertTrue(all(cell["AllyMatches"] == cell["EnemyMatches"] == 2 for cell in aggregated["Cells"]))

    def test_rejects_missing_candidates_scenarios_metadata_and_invalid_counts(self):
        protocol = normalize()
        job = league_search.build_jobs(protocol, protocol["Candidates"], protocol["Enemies"], "screen", 2, 0)[0]
        valid = report_for(job)
        league_search.validate_report(valid, job)
        mutations = [
            lambda report: report["Ranking"].pop(),
            lambda report: report.pop("PlayerBuildGuid"),
            lambda report: report.update({"FixedDeltaTime": 0}),
            lambda report: report.update({"TimeScale": 6}),
            lambda report: report["Ranking"][0].update({"Scenarios": []}),
            lambda report: report["Ranking"][0].update({"Wins": 0}),
            lambda report: report["Ranking"][0].update({"GameSecondsSamples": []}),
            lambda report: report["Ranking"][0]["Scenarios"][0].update({"StonePositionsReversed": True}),
        ]
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                report = copy.deepcopy(valid)
                mutation(report)
                with self.assertRaises(ValueError):
                    league_search.validate_report(report, job)

    def test_timeout_is_non_victory_and_scenarios_receive_equal_weight(self):
        protocol = normalize()
        candidates = protocol["Candidates"][:2]
        jobs = league_search.build_jobs(protocol, candidates, protocol["Enemies"], "final", 2, 2)
        results = []
        for job in jobs:
            report = report_for(job, lambda candidate: candidate["Name"] == "A" and not job["scenario"]["StonePositionsReversed"])
            for row in report["Ranking"]:
                if row["CandidateKey"] == "3:0":
                    for item in (row, row["Scenarios"][0]):
                        item.update({"Losses": 0, "Timeouts": item["MatchCount"], "DecidedGameSecondsSamples": []})
            results.append((job, report))
        aggregated = league_search.aggregate_results(protocol, candidates, protocol["Enemies"], results, 2)
        self.assertEqual(aggregated["Ranking"][0]["WinRate"], 0.5)
        self.assertEqual(aggregated["Ranking"][0]["WorstScenarioWinRate"], 0.0)
        self.assertEqual(aggregated["Ranking"][1]["WinRate"], 0.0)
        self.assertEqual(aggregated["Ranking"][1]["TimeoutRate"], 1.0)
        with self.assertRaisesRegex(ValueError, "incomplete"):
            league_search.aggregate_results(protocol, candidates, protocol["Enemies"], results[:-1], 2)

    def test_metadata_comparison_rejects_mixed_builds_or_steps(self):
        first = {"PlayerBuildGuid": "a", "UnityVersion": "u", "TimeScale": 4,
                 "FixedDeltaTime": 0.02, "PreserveFixedDeltaTime": True}
        self.assertTrue(league_search.same_metadata(first, dict(first)))
        for field, value in (("PlayerBuildGuid", "b"), ("FixedDeltaTime", 0.12), ("PreserveFixedDeltaTime", False)):
            self.assertFalse(league_search.same_metadata(first, {**first, field: value}))

    def test_player_change_prevents_reusing_a_run(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            executable = root / "player"
            executable.write_bytes(b"first-build")
            league_search.prepare_run_directory(root / "run", normalize(), executable)
            executable.write_bytes(b"next-build")
            with self.assertRaisesRegex(ValueError, "another config or Player"):
                league_search.prepare_run_directory(root / "run", normalize(), executable)

    def test_rejected_job_is_invalidated_and_stage_never_claims_completion(self):
        protocol = normalize()
        protocol["Scenarios"] = protocol["Scenarios"][:1]
        protocol["ScreenScenarios"] = protocol["Scenarios"]
        opponents = protocol["Enemies"][:1]
        jobs = league_search.build_jobs(protocol, protocol["Candidates"], opponents, "screen", 1, 0)
        expected = league_search.validate_report(report_for(jobs[0]), jobs[0])
        with tempfile.TemporaryDirectory() as temporary:
            directory = pathlib.Path(temporary)
            manifest = {"RuntimeMetadata": expected}

            def run_job(executable, stage_directory, job, workers, positions):
                attempt = stage_directory / "jobs" / job["id"] / "attempt_001"
                league_search.batch_battles.write_json_atomic(attempt / "config.json", job["config"])
                league_search.batch_battles.write_json_atomic(attempt.parent / "complete.json", {"Report": "attempt_001/AutoBattles/sweep.json"})
                return {"status": "reused", "report": report_for(job, build="another-build")}

            with mock.patch.object(league_search.batch_battles, "run_job", side_effect=run_job):
                with self.assertRaisesRegex(ValueError, "metadata differ"):
                    league_search.run_stage(protocol, directory, manifest, directory / "player", protocol["Candidates"], opponents, "screen", 1, 0)
            self.assertFalse((directory / "screen-results.json").exists())
            self.assertFalse((directory / "stages" / "screen" / "jobs" / jobs[0]["id"] / "complete.json").exists())

    def test_shutdown_cancels_pending_jobs_before_terminating_players(self):
        protocol = normalize()
        events = []
        future = mock.Mock()
        future.result.side_effect = RuntimeError("Player failed")
        future.cancel.side_effect = lambda: events.append("cancel")
        executor = mock.Mock()
        executor.submit.return_value = future
        executor.__enter__ = mock.Mock(return_value=executor)
        executor.__exit__ = mock.Mock(return_value=False)
        executor.shutdown.side_effect = lambda **kwargs: events.append(("shutdown", kwargs))
        with tempfile.TemporaryDirectory() as temporary:
            directory = pathlib.Path(temporary)
            with mock.patch.object(league_search.concurrent.futures, "ThreadPoolExecutor", return_value=executor), \
                 mock.patch.object(league_search.concurrent.futures, "as_completed", return_value=[future]), \
                 mock.patch.object(league_search.batch_battles, "terminate_active_processes", side_effect=lambda: events.append("terminate")):
                with self.assertRaisesRegex(RuntimeError, "Player failed"):
                    league_search.run_stage(protocol, directory, {}, directory / "player", protocol["Candidates"], protocol["Enemies"], "screen", 1, 0)
        self.assertEqual(events, ["cancel", ("shutdown", {"wait": False, "cancel_futures": True}), "terminate"])

    def test_termination_interrupts_and_saves_an_incomplete_summary(self):
        with self.assertRaises(KeyboardInterrupt):
            league_search.interrupt_run(league_search.signal.SIGTERM, None)
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            executable = root / "player"
            executable.write_bytes(b"test-player")
            config_path = root / "config.json"
            config_path.write_text(json.dumps(config()), encoding="utf-8")
            args = argparse.Namespace(config=config_path, output_directory=root / "run", executable=executable, parallel=None, plan_only=False)
            with mock.patch.object(league_search, "parse_args", return_value=args), \
                 mock.patch.object(league_search, "run_stage", side_effect=KeyboardInterrupt), mock.patch("builtins.print"):
                self.assertEqual(league_search.main(), 1)
            status = json.loads((root / "run" / "status.json").read_text())
            ranking = json.loads((root / "run" / "ranking.json").read_text())
            self.assertEqual(status["Status"], "FAILED")
            self.assertFalse(ranking["Complete"])
            self.assertIn("未完了", (root / "run" / "summary.md").read_text())

    def test_end_to_end_selection_final_league_and_resume(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            executable = root / "player"
            executable.write_bytes(b"test-player")
            config_path = root / "config.json"
            config_path.write_text(json.dumps(config()), encoding="utf-8")
            args = argparse.Namespace(config=config_path, output_directory=root / "run", executable=executable, parallel=None, plan_only=False)
            executed = []

            def run_job(executable, directory, job, workers, positions):
                job_directory = directory / "jobs" / job["id"]
                reused = league_search.batch_battles.load_completed_job(job_directory, job["match_count"], 1)
                if reused is not None:
                    return {"status": "reused", "report": reused}
                executed.append(job)
                stage = job["stage"]
                winner = lambda candidate: (candidate["Name"] == "A" or (candidate["Name"] == "B" and job["config"]["MatchOffset"] == 0)) if stage == "screen" else candidate["Name"] == "B"
                report = report_for(job, winner)
                attempt = job_directory / "attempt_001"
                league_search.batch_battles.write_json_atomic(attempt / "config.json", job["config"])
                league_search.batch_battles.write_json_atomic(attempt / "AutoBattles" / "sweep.json", report)
                league_search.batch_battles.write_json_atomic(job_directory / "complete.json", {"Report": "attempt_001/AutoBattles/sweep.json"})
                return {"status": "completed", "report": report}

            with mock.patch.object(league_search, "parse_args", return_value=args), mock.patch.object(league_search.batch_battles, "run_job", side_effect=run_job), mock.patch("builtins.print"):
                self.assertEqual(league_search.main(), 0)
                first_count = len(executed)
                self.assertEqual(league_search.main(), 0)
                self.assertEqual(len(executed), first_count)
            ranking = json.loads((root / "run" / "ranking.json").read_text())
            self.assertTrue(ranking["Complete"])
            self.assertEqual(ranking["Screening"]["Ranking"][0]["Name"], "A")
            self.assertEqual(ranking["Final"]["Ranking"][0]["Name"], "B")
            self.assertEqual(ranking["League"]["Ranking"][0]["Name"], "B")
            self.assertEqual(ranking["Final"]["SeedStart"], 102)
            self.assertEqual(ranking["League"]["SeedStart"], 105)
            self.assertEqual(ranking["League"]["MatchesPerCell"], 4)
            summary = (root / "run" / "summary.md").read_text()
            self.assertIn("本評価", summary)
            self.assertIn("相手別勝率", summary)
            self.assertIn("Map A / 反転", summary)
            self.assertIn("杖（標準）", summary)


if __name__ == "__main__":
    unittest.main()
