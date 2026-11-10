using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CountrySelectionVerification
{
    [MenuItem("WarSim/Country/Selection Verification/Run EditMode Tests")]
    public static void Run()
    {
        var results = new List<string>();
        foreach (string name in new[] { "CountryBrightnessTests", "CountryButtonFeedbackTests", "CountryReportTitleTests", "CountryRosterSelectionTests", "CountryTrainingSelectionTests", "CountryDefenseTests", "CountryExpeditionTests", "CountryRosterTests" })
        {
            var suite = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).FirstOrDefault(type => type != null);
            if (suite == null) throw new InvalidOperationException("Test suite not loaded: " + name);
            foreach (var method in suite.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attributes = method.GetCustomAttributes(false);
                var cases = attributes.Where(attribute => attribute.GetType().Name == "TestCaseAttribute").ToArray();
                if (cases.Length == 0 && !attributes.Any(attribute => attribute.GetType().Name == "TestAttribute")) continue;
                var arguments = cases.Length == 0 ? new[] { Array.Empty<object>() } : cases.Select(attribute => (object[])attribute.GetType().GetProperty("Arguments").GetValue(attribute)).ToArray();
                foreach (var args in arguments)
                {
                    var instance = Activator.CreateInstance(suite);
                    try { suite.GetMethod("SetUp").Invoke(instance, null); method.Invoke(instance, args); results.Add($"PASS {name}.{method.Name}({string.Join(",", args)})"); }
                    catch (Exception exception) { results.Add($"FAIL {name}.{method.Name}({string.Join(",", args)}): {exception.InnerException ?? exception}"); }
                    finally { suite.GetMethod("TearDown").Invoke(instance, null); }
                }
            }
        }
        string path = "/private/tmp/country-selection-tests.txt";
        System.IO.File.WriteAllLines(path, results);
        int failed = results.Count(result => result.StartsWith("FAIL"));
        Debug.Log($"Country selection tests: {results.Count - failed}/{results.Count} passed; {path}");
        if (failed > 0) throw new InvalidOperationException($"{failed} selection tests failed; see {path}");
    }
}
