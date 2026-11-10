using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryComparisonInputReview
    {
        [MenuItem("WarSim/Country/Validate Queued Comparison Input")]
        public static void Run()
        {
            var view = UnityEngine.Object.FindAnyObjectByType<CountryComparisonView>();
            if (!EditorApplication.isPlaying || view == null || view.cities.Length != 2)
                throw new InvalidOperationException("Play the two-country comparison first.");
            const string path = "Artifacts/CountryComparison/play_validation.json";
            if (File.Exists(path)) File.Copy(path, Path.ChangeExtension(path, ".previous.json"), true);
            var keyboard = InputSystem.AddDevice<Keyboard>("Country comparison review keyboard");
            var keys = new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Tab, Key.Tab, Key.Tab };
            var expected = new[] { 0, 1, 2, 1, 0, 1 };
            var checks = new List<object>();
            var passed = true;
            var phase = -1;
            var nextFrame = Time.frameCount + 3;
            var start = EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction tick = null;
            void Finish(string error = null)
            {
                EditorApplication.update -= tick;
                InputSystem.RemoveDevice(keyboard);
                File.WriteAllText(path, JsonConvert.SerializeObject(new { status = passed && error == null ? "PASS" : "FAIL",
                    method = "Queued InputSystem events separated by three player frames", error, checks }, Formatting.Indented));
            }
            tick = () =>
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - start > 30)
                {
                    Finish("Play stopped or player frames did not advance.");
                    return;
                }
                if (Time.frameCount < nextFrame) return;
                if (phase >= 0 && phase % 2 == 0)
                {
                    var index = phase / 2;
                    var visible = new[] { view.cities[0].activeSelf, view.cities[1].activeSelf };
                    var valid = view.viewIndex == expected[index] && (expected[index] == 2 ? visible[0] && visible[1]
                        : visible[0] == (expected[index] == 0) && visible[1] == (expected[index] == 1));
                    passed &= valid;
                    checks.Add(new { key = keys[index].ToString(), viewIndex = view.viewIndex, visible, pass = valid, frame = Time.frameCount });
                }
                phase++;
                if (phase == keys.Length * 2) { Finish(); return; }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(phase % 2 == 0 ? new[] { keys[phase / 2] } : Array.Empty<Key>()));
                nextFrame = Time.frameCount + 3;
            };
            File.WriteAllText(path, "{\"status\":\"RUNNING\"}");
            EditorApplication.update += tick;
        }
    }
}
