#!/usr/bin/env python3
"""Bounded runtime probe using paired battles; not a balance validation suite."""
import argparse
import concurrent.futures
import copy
import json
import os
import pathlib
import signal
import subprocess
import sys
import time

import league_search as league

PROFILES = [(1, 6), (2, 6), (4, 6), (4, 12), (4, 24), (4, 6)]


def probe(args):
    config = league.batch_battles.load_config(args.config)
    protocol = league.normalize_config(config, argparse.Namespace(parallel=None, executable=None))
    protocol['TimeScale'] = args.speed
    protocol['TimeoutSeconds'] = 120
    executable = league.batch_battles.resolve_executable(pathlib.Path(protocol['Executable']))
    cases = [(1, 0, False), (0, 1, False), (2, 0, True), (0, 2, True)]
    jobs = []
    for index, (ally, enemy, reversed_position) in enumerate(cases):
        trial = copy.deepcopy(protocol)
        trial['Scenarios'] = [{**protocol['Scenarios'][0], 'StonePositionsReversed': reversed_position}]
        job = league.build_jobs(trial, [protocol['Candidates'][ally]], [protocol['Candidates'][enemy]], 'probe', 1, 0)[0]
        job['id'] = f'case_{index}'
        jobs.append(job)
    started = time.monotonic()
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.parallel) as pool:
        results = list(pool.map(lambda job: league.batch_battles.run_job(executable, args.output, job, 1, 1), jobs))
    elapsed = time.monotonic() - started
    samples = []
    for job, result in zip(jobs, results):
        if result['status'] == 'failed':
            raise RuntimeError(result)
        league.validate_report(result['report'], job)
        row = result['report']['Ranking'][0]
        samples.append({key: row[key] for key in ('Wins', 'Losses', 'Timeouts', 'TotalGameSeconds',
                       'TotalRealSeconds', 'TotalSkippedAiDecisionCount')})
    result = {'Parallel': args.parallel, 'Speed': args.speed, 'WallSeconds': elapsed,
              'MatchesPerMinute': len(samples) * 60 / elapsed, 'Samples': samples}
    league.batch_battles.write_json_atomic(args.output / 'result.json', result)


def summarize(output, results, interrupted):
    baseline = results[0]['Samples'] if results else []
    lines = ['# 自動戦闘の短時間性能調査', '',
             '同一の4対戦・シード・Playerを使用。物理刻み維持、worker数1、試合上限120ゲーム秒。',
             '短い猪突猛進対戦を対象とする予備測定。長期戦や他マップの精度上限は保証しない。', '',
             '|並列|倍速|実時間 秒|試合/分|基準との勝敗差|決着時間の平均絶対差 秒|AI判断省略 合計|',
             '|---:|---:|---:|---:|---:|---:|---:|']
    for result in results:
        samples = result['Samples']
        changed = sum((x['Wins'], x['Losses'], x['Timeouts']) != (y['Wins'], y['Losses'], y['Timeouts'])
                      for x, y in zip(samples, baseline))
        difference = sum(abs(x['TotalGameSeconds'] - y['TotalGameSeconds']) for x, y in zip(samples, baseline)) / len(samples)
        skipped = sum(x['TotalSkippedAiDecisionCount'] for x in samples)
        lines.append(f"|{result['Parallel']}|{result['Speed']}|{result['WallSeconds']:.2f}|{result['MatchesPerMinute']:.2f}|{changed}/4|{difference:.3f}|{skipped}|")
    lines.extend(['', '基準は最初の1並列・6倍。1倍との一致は未検証。同設定の再測定も含め、揺らぎを確認する。',
                  f'時間上限で終了: {interrupted}', ''])
    (output / 'summary.md').write_text('\n'.join(lines), encoding='utf-8')
    league.batch_battles.write_json_atomic(output / 'results.json', {'Profiles': results, 'BudgetExceeded': interrupted})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    parser.add_argument('--budget-seconds', type=float, default=180)
    parser.add_argument('--parallel', type=int)
    parser.add_argument('--speed', type=float)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    if args.parallel:
        probe(args)
        return
    deadline = time.monotonic() + args.budget_seconds
    results = []
    interrupted = False
    for index, (parallel, speed) in enumerate(PROFILES):
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            interrupted = True
            break
        output = args.output / f'{index}_p{parallel}_s{speed}'
        command = [sys.executable, str(pathlib.Path(__file__).resolve()), '--config', str(args.config.resolve()),
                   '--output', str(output.resolve()), '--parallel', str(parallel), '--speed', str(speed)]
        print(f'Probe {index + 1}/{len(PROFILES)}: parallel={parallel}, speed={speed}', flush=True)
        with (args.output / f'{index}.log').open('w') as log:
            process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                code = process.wait(timeout=remaining)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGTERM)
                process.wait()
                interrupted = True
                break
        if code:
            summarize(args.output, results, interrupted)
            raise RuntimeError(f'Probe failed: see {index}.log')
        results.append(league.batch_battles.load_config(output / 'result.json'))
        summarize(args.output, results, interrupted)
    summarize(args.output, results, interrupted)


if __name__ == '__main__':
    main()
