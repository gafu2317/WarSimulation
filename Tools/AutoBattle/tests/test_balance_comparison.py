import sys
import pathlib
import unittest
sys.path.insert(0, str(pathlib.Path(__file__).parents[1]))
import balance_comparison
from test_league_search import config, normalize, report_for, league_search

class BalanceComparisonTests(unittest.TestCase):
    def test_matrix_includes_isolated_and_combined_penalties_and_delay_controls(self):
        conditions = balance_comparison.conditions([.9, .8, .7], [15, 30, 60])
        self.assertEqual(len(conditions), 66)
        for delay in (15, 30, 60):
            controls = [x for x in conditions if x['Id'] == f'none_revive_{delay}']
            self.assertEqual(len(controls), 1)
            self.assertEqual(controls[0]['Balance']['RecklessHPMultiplier'], 1)
        with self.assertRaises(ValueError):
            balance_comparison.conditions([.8], [float('nan')])

    def test_old_player_results_are_rejected_and_balance_reaches_jobs(self):
        value = config()
        value['Balance'] = {'RecklessAGIMultiplier': .7, 'ReviveDelayAfterArrivalSeconds': 60}
        protocol = normalize(value)
        job = league_search.build_jobs(protocol, protocol['Candidates'], protocol['Enemies'], 'screen', 1, 0)[0]
        self.assertEqual(job['config']['Balance'], protocol['Balance'])
        report = report_for(job)
        with self.assertRaises(ValueError):
            league_search.validate_report(report, job)
        report['Balance'] = protocol['Balance']
        league_search.validate_report(report, job)

    def test_target_mode_counts_enemy_losses_as_target_wins_and_preserves_timeouts(self):
        import tempfile
        import json
        from unittest import mock
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            source = root / 'config.json'
            source.write_text(json.dumps(config()))
            def prepare(directory, *args):
                directory.mkdir(parents=True, exist_ok=True)
                return {}
            def stage(*args, **kwargs):
                if args[6] == 'forward':
                    return {'Ranking': [{'Wins': 3, 'MatchCount': 4, 'Timeouts': 1}]}
                return {'Ranking': [{'Losses': 1, 'MatchCount': 2, 'Timeouts': 0}] * 2}
            argv = ['probe', '--config', str(source), '--output-directory', str(root / 'output'),
                    '--target-name', 'A', '--exclude-fai', '--multipliers', '.8', '--revive-delays', '15']
            with mock.patch.object(sys, 'argv', argv), \
                 mock.patch.object(balance_comparison.league.batch_battles, 'resolve_executable', return_value=source), \
                 mock.patch.object(balance_comparison.league, 'prepare_run_directory', side_effect=prepare), \
                 mock.patch.object(balance_comparison.league, 'run_stage', side_effect=stage), \
                 mock.patch.object(balance_comparison.league, 'matchup_tables', return_value=[]):
                self.assertEqual(balance_comparison.main(), 0)
            results = json.loads((root / 'output/comparison-results.json').read_text())
            row = results['Conditions'][0]['Report']['Ranking'][0]
            self.assertEqual((row['Wins'], row['MatchCount'], row['Timeouts'], row['WinRate']), (5, 8, 1, .625))
            self.assertEqual(len(results['Conditions']), 7)
