using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CountryPhonePreview
{
    public const int Width = 2532, Height = 1170, ScalePercent = 50;
    public const string Folder = "Artifacts/CountryUI/MobileNative-v1";
    public static Vector2Int Size => new(Width, Height);
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [MenuItem("WarSim/Country/Use Phone Simulator", false, 0)]
    public static void Configure()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "UnityEditor.DeviceSimulatorModule");
        var type = assembly.GetType("UnityEditor.DeviceSimulation.SimulatorWindow", true);
        var window = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType() == type);
        if (window == null)
        {
            var game = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().FullName == "UnityEditor.GameView");
            if (game != null) type.BaseType.GetMethod("SwapMainWindow", Flags).Invoke(game, new object[] { type });
            else type.GetMethod("ShowWindow", Flags).Invoke(null, null);
            window = Resources.FindObjectsOfTypeAll<EditorWindow>().First(w => w.GetType() == type);
        }

        var main = type.GetProperty("main", Flags).GetValue(window);
        var devices = (Array)main.GetType().GetProperty("devices", Flags).GetValue(main);
        int selected = -1;
        for (int i = 0; i < devices.Length; i++)
        {
            var device = devices.GetValue(i);
            var info = device.GetType().GetField("deviceInfo", Flags).GetValue(device);
            var screen = ((Array)info.GetType().GetField("screens", Flags).GetValue(info)).GetValue(0);
            if ((int)screen.GetType().GetField("width", Flags).GetValue(screen) != Height ||
                (int)screen.GetType().GetField("height", Flags).GetValue(screen) != Width) continue;
            selected = i;
            break;
        }
        if (selected < 0) throw new InvalidOperationException("The 1170 x 2532 phone simulator profile is unavailable.");
        main.GetType().GetProperty("deviceIndex", Flags).SetValue(main, selected);

        var ui = main.GetType().GetProperty("userInterface", Flags).GetValue(main);
        ((UnityEditor.UIElements.ToolbarToggle)ui.GetType().GetField("m_FitToScreenToggle", Flags).GetValue(ui)).value = false;
        ui.GetType().GetProperty("Rotation", Flags).SetValue(ui, 270);
        ui.GetType().GetProperty("Scale", Flags).SetValue(ui, ScalePercent);
        var simulation = main.GetType().GetProperty("ScreenSimulation", Flags).GetValue(main);
        simulation.GetType().GetProperty("orientation", Flags).SetValue(simulation, ScreenOrientation.AutoRotation);
        simulation.GetType().GetMethod("ApplyChanges", Flags).Invoke(simulation, null);
        window.Focus();
        window.Repaint();
    }
}
