import argparse
import importlib.util
import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).parents[1]))
import specialist_search as search

spec = importlib.util.spec_from_file_location("league_test_helpers", pathlib.Path(__file__).with_name("test_league_search.py"))
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)


class SpecialistSearchTests(unittest.TestCase):
    def test_both_army_directions_preserve_candidate_wins_and_sample_counts(self):
        protocol = helpers.normalize()
        candidates = protocol["Candidates"][:1]
        opponents = protocol["Enemies"]
        forward = search.league.build_jobs(protocol, candidates, opponents, "confirm", 3, 1, True)
        reverse = search.league.build_jobs(protocol, opponents, candidates, "confirm", 3, 1, True)
        results = [(job, helpers.report_for(job, winner=lambda c: True)) for job in forward]
        results += [(job, helpers.report_for(job, winner=lambda c: False)) for job in reverse]
        for job in forward:
            job["id"] = "f_" + job["id"]
        for job in reverse:
            job["id"] = "r_" + job["id"]
        report = search.aggregate_panel(protocol, candidates, opponents, results, 3, True)
        row = report["Ranking"][0]
        self.assertEqual(row["MatchCount"], 24)
        self.assertEqual(row["Wins"], 24)
        self.assertEqual(row["WinRate"], 1)
        cells = [c for c in report["Cells"] if c["CandidateKey"] == row["CandidateKey"]]
        self.assertTrue(all(c["AllyMatches"] == c["EnemyMatches"] == 3 for c in cells))

    def test_map_specialist_and_all_controls_survive_global_screening(self):
        rows = [{"Name": key, "CandidateKey": key, "Roles": []} for key in ["global", "base", "A", "B", "AB", "specialist"]]
        wins = {"global": 1, "base": 2, "A": 3, "B": 3, "AB": 8, "specialist": 9}
        report = {"Ranking": rows, "Cells": [
            {"CandidateKey": key, "Scenario": {"MapName": "map"}, "MatchCount": 10,
             "Wins": value, "Losses": 10 - value, "Timeouts": 0} for key, value in wins.items()]}
        pair = {"Name": "pair", "Base": "base", "A": "A", "B": "B", "AB": "AB"}
        selected, pairs = search.select_panel(report, {"Maps": [{"MapName": "map"}], "Pairs": [pair],
                                                    "BaselineKey": "base", "PerMapTop": 1, "ConfirmPairCount": 1})
        self.assertEqual({r["CandidateKey"] for r in selected}, set(wins))
        self.assertEqual(pairs, [{**pair, "SelectedOnMap": "map"}])
        score = search.pair_scores(search.map_ranking(report, "map"), [pair])[0]
        self.assertAlmostEqual(score["Interaction"], .4)

    def test_overlapping_candidates_and_opponents_include_self_without_double_counting(self):
        protocol = helpers.normalize()
        candidates = protocol["Enemies"]
        opponents = protocol["Enemies"]
        forward = search.league.build_jobs(protocol, candidates, opponents, "confirm", 3, 1, False)
        reverse = search.league.build_jobs(protocol, opponents, candidates, "confirm", 3, 1, False)
        for job in forward:
            job["id"] = "f_" + job["id"]
        for job in reverse:
            job["id"] = "r_" + job["id"]
        results = [(job, helpers.report_for(job)) for job in forward + reverse]
        report = search.aggregate_panel(protocol, candidates, opponents, results, 3, True)
        self.assertEqual(len(report["Cells"]), 8)
        for row in report["Ranking"]:
            self.assertEqual(row["MatchCount"], 24)
            self.assertEqual(row["Wins"], 12)
        for cell in report["Cells"]:
            self.assertEqual(cell["AllyWins"], 3)
            self.assertEqual(cell["EnemyWins"], 0)
            self.assertEqual(cell["EnemyLosses"], 3)

    def test_negative_interaction_is_not_called_positive_when_ab_wins_most(self):
        rows = [{"CandidateKey": key, "WinRate": rate, "MatchCount": 20, "Wins": int(rate * 20)}
                for key, rate in zip(["base", "A", "B", "AB"], [.1, .7, .7, .8])]
        pair = {"Name": "pair", "Base": "base", "A": "A", "B": "B", "AB": "AB"}
        self.assertAlmostEqual(search.pair_scores(rows, [pair])[0]["Interaction"], -.5)


if __name__ == "__main__":
    unittest.main()
