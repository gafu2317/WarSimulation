import argparse
import copy
import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).parents[1]))
import create_specialist_config as specialist


def build_metadata():
    return {"Success": True, "BuildGuid": "fixture-build", "Maps": [
        {"Name": name, "NormalAvailable": True, "ReversedAvailable": True}
        for name, _, _ in specialist.MAPS
    ]}


class SpecialistConfigTests(unittest.TestCase):
    def setUp(self):
        self.config = specialist.create_config(build_metadata())
        self.parties = {specialist.league.candidate_key(candidate["Roles"]): candidate
                        for candidate in self.config["Candidates"]}

    def test_complete_weapon_personality_coverage_and_unique_candidates(self):
        roles = {(role["Weapon"], role["Personality"])
                 for candidate in self.config["Candidates"] for role in candidate["Roles"]}
        expected = {(weapon, personality) for weapon in range(1, 7)
                    for personality in specialist.coverage.PERSONALITIES}
        self.assertEqual(roles, expected)
        self.assertEqual(len(self.parties), len(self.config["Candidates"]))
        self.assertGreater(len(self.parties), 96)

    def test_pair_quartets_change_only_two_personalities_in_fixed_slots(self):
        self.assertEqual(len(self.config["Experiment"]["Pairs"]), 12)
        for pair in self.config["Experiment"]["Pairs"]:
            with self.subTest(pair=pair["Name"]):
                base = self.parties[pair["Base"]]["Roles"]
                self.assertEqual(base[pair["SlotA"]]["Personality"], 0)
                self.assertEqual(base[pair["SlotB"]]["Personality"], 0)
                self.assertNotEqual(pair["SlotA"], pair["SlotB"])
                for variant, changed in (("Base", ()), ("A", ("A",)), ("B", ("B",)), ("AB", ("A", "B"))):
                    expected = copy.deepcopy(base)
                    for letter in changed:
                        expected[pair["Slot" + letter]]["Personality"] = pair["Personality" + letter]
                    self.assertEqual(self.parties[pair[variant]]["Roles"], expected)

    def test_tagalong_has_fixed_preceding_leader(self):
        for candidate in self.config["Candidates"]:
            self.assertNotEqual(candidate["Roles"][0]["Personality"], 21)
        followers = [pair for pair in self.config["Experiment"]["Pairs"] if pair["PersonalityA"] == 21]
        self.assertEqual(len(followers), 3)
        for pair in followers:
            self.assertEqual(pair["SlotA"], pair["SlotB"] + 1)
            roles = self.parties[pair["AB"]]["Roles"]
            self.assertEqual(roles[pair["SlotA"]], {"Weapon": 3, "Personality": 21})
            self.assertEqual(roles[pair["SlotB"]]["Weapon"], 1)

    def test_five_terrain_maps_have_both_orientations_and_current_baselines(self):
        scenarios = self.config["Scenarios"]
        self.assertEqual(len(scenarios), 10)
        self.assertEqual({(scenario["MapName"], scenario["StonePositionsReversed"]) for scenario in scenarios},
                         {(name, reverse) for name, _, _ in specialist.MAPS for reverse in (False, True)})
        self.assertEqual(self.config["ScreenScenarios"], scenarios)
        self.assertEqual(self.config["Enemies"][0]["Roles"],
                         specialist.coverage.roles_json(((1, 1), (2, 9), (3, 0), (5, 9), (6, 9))))
        self.assertEqual(self.config["BaseSeed"], 202610130)
        protocol = specialist.league.normalize_config(self.config, argparse.Namespace(parallel=None, executable=None))
        self.assertEqual((protocol["ParallelProcesses"], protocol["TimeScale"], protocol["ScreenMatches"]), (4, 6, 1))
        self.assertTrue(protocol["PreserveFixedDeltaTime"])

    def test_unavailable_orientation_and_failed_build_are_rejected(self):
        build = build_metadata()
        build["Maps"][0]["ReversedAvailable"] = False
        with self.assertRaisesRegex(ValueError, "Required map orientation"):
            specialist.create_config(build)
        with self.assertRaisesRegex(ValueError, "build successfully"):
            specialist.create_config({"Success": False})


if __name__ == "__main__":
    unittest.main()
