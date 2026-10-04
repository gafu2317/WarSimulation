#!/usr/bin/env python3

"""Run a command, record its outcome, and send a local macOS notification."""

import argparse
import datetime
import json
import os
import pathlib
import signal
import subprocess
import sys


NOTIFICATION_SCRIPT = """use scripting additions
on run argv
    display notification (item 2 of argv) with title (item 1 of argv)
end run"""


def write_status(path, status):
    temporary = path.with_suffix(".json.tmp")
    temporary.write_text(json.dumps(status, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def timestamp():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def notify(title, message):
    if sys.platform != "darwin":
        return {"notification_status": "unsupported"}
    try:
        result = subprocess.run(
            ["/usr/bin/osascript", "-e", NOTIFICATION_SCRIPT, title, message],
            capture_output=True,
            text=True,
            timeout=15,
        )
        if result.returncode == 0:
            return {"notification_status": "sent"}
        return {"notification_status": "failed", "notification_error": result.stderr.strip()}
    except (OSError, subprocess.TimeoutExpired) as error:
        return {"notification_status": "failed", "notification_error": str(error)}


def run(command, output_directory, title, result_file=None, keep_awake=True):
    output_directory = pathlib.Path(output_directory).resolve()
    output_directory.mkdir(parents=True, exist_ok=True)
    log_path = output_directory / "runner.log"
    status_path = output_directory / "process-status.json"
    result_path = pathlib.Path(result_file).resolve() if result_file else output_directory / "final-results.json"
    status = {
        "status": "running",
        "wrapper_pid": os.getpid(),
        "started_at": timestamp(),
        "command": command,
        "log_file": str(log_path),
        "result_file": str(result_path),
    }
    write_status(status_path, status)
    process = None
    awake = None
    with log_path.open("a", encoding="utf-8") as log:
        try:
            process = subprocess.Popen(
                command,
                stdin=subprocess.DEVNULL,
                stdout=log,
                stderr=subprocess.STDOUT,
                start_new_session=True,
            )
            status["child_pid"] = process.pid
            write_status(status_path, status)
            if keep_awake and sys.platform == "darwin":
                try:
                    awake = subprocess.Popen(
                        ["/usr/bin/caffeinate", "-i", "-w", str(process.pid)],
                        stdin=subprocess.DEVNULL,
                        stdout=log,
                        stderr=subprocess.STDOUT,
                    )
                except OSError as error:
                    log.write(f"caffeinate: {error}\n")
            return_code = process.wait()
        except KeyboardInterrupt:
            status["interrupted"] = True
            if process is not None:
                process.send_signal(signal.SIGINT)
                process.wait()
            return_code = 130
        except OSError as error:
            return_code = 127
            status["error"] = str(error)
            log.write(f"Unable to start command: {error}\n")
        finally:
            if awake is not None and awake.poll() is None:
                awake.terminate()
                awake.wait()

    status.update(
        status="success" if return_code == 0 else "failed",
        finished_at=timestamp(),
        exit_code=return_code,
    )
    write_status(status_path, status)
    outcome = "中断" if status.get("interrupted") else "成功" if return_code == 0 else f"失敗 (exit={return_code})"
    message = f"{outcome}\n結果: {result_path}"
    if return_code != 0:
        message += f"\nログ: {log_path}"
    status.update(notify(title, message))
    write_status(status_path, status)
    return return_code if return_code >= 0 else 128 - return_code


def interrupt(signum, frame):
    raise KeyboardInterrupt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-directory", required=True, type=pathlib.Path)
    parser.add_argument("--title", default="自動戦闘探索")
    parser.add_argument("--result-file", type=pathlib.Path)
    parser.add_argument("--no-keep-awake", action="store_true")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("Specify the command after --.")
    if hasattr(signal, "SIGHUP"):
        signal.signal(signal.SIGHUP, signal.SIG_IGN)
    signal.signal(signal.SIGTERM, interrupt)
    return run(command, args.output_directory, args.title, args.result_file, not args.no_keep_awake)


if __name__ == "__main__":
    sys.exit(main())
