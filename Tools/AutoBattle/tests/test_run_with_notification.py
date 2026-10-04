import importlib.util
import json
import os
import pathlib
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock


SCRIPT_PATH = pathlib.Path(__file__).parents[1] / "run_with_notification.py"
SPEC = importlib.util.spec_from_file_location("run_with_notification", SCRIPT_PATH)
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class RunWithNotificationTests(unittest.TestCase):
    def test_command_observes_running_status_and_outputs_are_logged(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = pathlib.Path(temporary)
            (output / "status.json").write_text('{"stage":"screening"}', encoding="utf-8")
            code = (
                "import json, pathlib, sys; "
                "status=json.loads(pathlib.Path(sys.argv[1]).read_text()); "
                "assert status['status']=='running'; "
                "print('result output'); print('error output', file=sys.stderr)"
            )
            with mock.patch.object(runner, "notify", return_value={"notification_status": "sent"}) as notify:
                exit_code = runner.run(
                    [sys.executable, "-c", code, str(output / "process-status.json")],
                    output,
                    "探索",
                    keep_awake=False,
                )

            status = json.loads((output / "process-status.json").read_text())
            log = (output / "runner.log").read_text()
            self.assertEqual(exit_code, 0)
            self.assertEqual(status["status"], "success")
            self.assertEqual(status["exit_code"], 0)
            self.assertEqual(status["wrapper_pid"], os.getpid())
            self.assertGreater(status["child_pid"], 0)
            self.assertEqual(json.loads((output / "status.json").read_text()), {"stage": "screening"})
            self.assertIn("result output", log)
            self.assertIn("error output", log)
            self.assertIn(str(output / "final-results.json"), notify.call_args.args[1])

    def test_nonzero_command_is_failed_even_if_notification_succeeds(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = pathlib.Path(temporary)
            with mock.patch.object(runner, "notify", return_value={"notification_status": "sent"}) as notify:
                exit_code = runner.run(
                    [sys.executable, "-c", "raise SystemExit(7)"], output, "探索", keep_awake=False
                )

            status = json.loads((output / "process-status.json").read_text())
            self.assertEqual(exit_code, 7)
            self.assertEqual(status["status"], "failed")
            self.assertIn("失敗 (exit=7)", notify.call_args.args[1])
            self.assertIn(str(output / "runner.log"), notify.call_args.args[1])

    def test_missing_command_records_failure_and_notifies(self):
        with tempfile.TemporaryDirectory() as temporary:
            with mock.patch.object(runner, "notify", return_value={"notification_status": "failed"}):
                exit_code = runner.run(
                    [str(pathlib.Path(temporary) / "missing-command")], temporary, "探索", keep_awake=False
                )

            status = json.loads((pathlib.Path(temporary) / "process-status.json").read_text())
            self.assertEqual(exit_code, 127)
            self.assertEqual(status["status"], "failed")
            self.assertIn("error", status)

    def test_notification_arguments_are_passed_as_data(self):
        with mock.patch.object(runner.sys, "platform", "darwin"):
            with mock.patch.object(runner.subprocess, "run", return_value=subprocess.CompletedProcess([], 0)) as run:
                result = runner.notify('title "quote"', "path\\name\nnext line")

        self.assertEqual(result, {"notification_status": "sent"})
        self.assertEqual(run.call_args.args[0][-2:], ['title "quote"', "path\\name\nnext line"])

    def test_termination_interrupts_the_command_and_records_the_outcome(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = pathlib.Path(temporary)
            wrapper_code = (
                "import importlib.util, sys; "
                "spec=importlib.util.spec_from_file_location('runner', sys.argv.pop(1)); "
                "runner=importlib.util.module_from_spec(spec); spec.loader.exec_module(runner); "
                "runner.notify=lambda title, message: {'notification_status': 'test'}; "
                "sys.exit(runner.main())"
            )
            wrapper = subprocess.Popen(
                [
                    sys.executable, "-c", wrapper_code, str(SCRIPT_PATH),
                    "--output-directory", temporary, "--no-keep-awake", "--",
                    sys.executable, "-c", "import time; print('ready', flush=True); time.sleep(30)",
                ],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            try:
                deadline = time.monotonic() + 5
                while time.monotonic() < deadline:
                    log = output / "runner.log"
                    if log.exists() and "ready" in log.read_text():
                        break
                    time.sleep(0.01)
                else:
                    self.fail("Command did not start.")
                wrapper.send_signal(signal.SIGTERM)
                stdout, stderr = wrapper.communicate(timeout=5)
                self.assertEqual(wrapper.returncode, 130, stdout + stderr)
                status = json.loads((output / "process-status.json").read_text())
                self.assertEqual(status["status"], "failed")
                self.assertEqual(status["exit_code"], 130)
                self.assertTrue(status["interrupted"])
                self.assertEqual(status["notification_status"], "test")
                with self.assertRaises(ProcessLookupError):
                    os.kill(status["child_pid"], 0)
            finally:
                if wrapper.poll() is None:
                    wrapper.kill()
                    wrapper.communicate()

    def test_cli_rejects_an_empty_command(self):
        with tempfile.TemporaryDirectory() as temporary:
            result = subprocess.run(
                [sys.executable, str(SCRIPT_PATH), "--output-directory", temporary, "--"],
                capture_output=True,
                text=True,
            )

        self.assertEqual(result.returncode, 2)
        self.assertIn("Specify the command after --", result.stderr)


if __name__ == "__main__":
    unittest.main()
