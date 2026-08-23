import importlib.util
import pathlib
import unittest


SCRIPT_PATH = pathlib.Path(__file__).parents[1] / "adaptive_search.py"
SPEC = importlib.util.spec_from_file_location("adaptive_search", SCRIPT_PATH)
adaptive_search = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(adaptive_search)


class AdaptiveSearchTests(unittest.TestCase):
    def test_enumerate_candidates_matches_the_five_person_party_space(self):
        candidates = adaptive_search.enumerate_candidates(5)

        self.assertEqual(len(candidates), 6993)
        self.assertEqual(len({adaptive_search.candidate_key(candidate) for candidate in candidates}), 6993)

    def test_select_balanced_candidates_returns_a_deterministic_unique_sample(self):
        candidates = adaptive_search.enumerate_candidates(5)

        first = adaptive_search.select_balanced_candidates(candidates, 300, 12000)
        second = adaptive_search.select_balanced_candidates(candidates, 300, 12000)

        self.assertEqual(first, second)
        self.assertEqual(len(first), 300)
        self.assertEqual(len(set(first)), 300)

    def test_fit_model_and_select_probe_candidates_keep_the_requested_shape(self):
        candidates = adaptive_search.enumerate_candidates(5)
        initial = adaptive_search.select_balanced_candidates(candidates, 300, 12000)
        observations = [(index, 1.0 if index % 2 else -1.0) for index in initial]

        coefficients, features = adaptive_search.fit_model(candidates, observations)
        scores = adaptive_search.predict_scores(candidates, coefficients, features)
        selection = adaptive_search.select_probe_candidates(candidates, scores, initial, 60, 20, 20)

        self.assertEqual(len(coefficients), adaptive_search.FEATURE_COUNT)
        self.assertEqual(len(scores), 6993)
        self.assertEqual(len(selection["Probe"]), 100)
        self.assertEqual(len(set(selection["Probe"])), 100)
        self.assertTrue(set(selection["Probe"]).isdisjoint(initial))

    def test_build_scenarios_expands_ten_maps_to_normal_and_reversed_cases(self):
        scenarios = adaptive_search.build_scenarios(
            {"MapNames": [f"AuthoredMap {index}" for index in range(10)]}
        )

        self.assertEqual(len(scenarios), 20)
        self.assertEqual(sum(not scenario["StonePositionsReversed"] for scenario in scenarios), 10)
        self.assertEqual(sum(scenario["StonePositionsReversed"] for scenario in scenarios), 10)

    def test_make_stage_config_forces_one_requested_stone_position(self):
        settings = {"party_size": 5, "time_scale": 6.0}
        scenario = {"MapName": "AuthoredMap 1", "StonePositionsReversed": True}
        candidate = adaptive_search.enumerate_candidates(5)[0]

        config = adaptive_search.make_stage_config(
            {"MapNames": ["ignored"], "TimeScale": 8.0},
            scenario,
            [candidate],
            settings,
            matches=3,
            match_offset=1,
            total_matches=4,
        )

        self.assertEqual(config["MapNames"], ["AuthoredMap 1"])
        self.assertTrue(config["UseFixedStonePosition"])
        self.assertTrue(config["StonePositionsReversed"])
        self.assertEqual(config["Enemy"], [adaptive_search.role_to_json(role) for role in adaptive_search.FIXED_ENEMY])
        self.assertEqual(config["MatchOffset"], 1)
        self.assertEqual(config["TotalMatchesPerCandidate"], 4)

    def test_select_final_candidates_can_promote_a_candidate_from_the_initial_stage(self):
        candidates = adaptive_search.enumerate_candidates(5)
        initial = candidates[:2]
        probe = candidates[2:4]
        scenario = {"Id": "Map_normal", "MapName": "Map", "StonePositionsReversed": False}

        initial_result = adaptive_search.empty_stats(initial[0])
        initial_result["MatchCount"] = 1
        initial_result["Wins"] = 1
        probe_result = adaptive_search.empty_stats(probe[0])
        probe_result["MatchCount"] = 4
        probe_result["Wins"] = 0
        settings = {"final_count": 1}
        model_reports = {"Map_normal": {"ProbePredictions": {probe_result["CandidateKey"]: 1.0}}}

        final, selection = adaptive_search.select_final_candidates(
            {"Map_normal": {initial_result["CandidateKey"]: initial_result}},
            {"Map_normal": {probe_result["CandidateKey"]: probe_result}},
            [scenario],
            {"Map_normal": initial},
            {"Map_normal": probe},
            settings,
            model_reports,
        )

        self.assertEqual(final["Map_normal"], [initial[0]])
        self.assertEqual(selection["Map_normal"], [initial_result["CandidateKey"]])


if __name__ == "__main__":
    unittest.main()
