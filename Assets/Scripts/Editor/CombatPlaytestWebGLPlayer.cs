using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatPlaytestWebGLPlayer
{
    private const string BuildDirectory = ".unity/CombatPlaytestWebGL";
    private const long GitFileLimitBytes = 100L * 1024L * 1024L;

    [MenuItem("Tools/War Simulation/Web Playtest/Build WebGL")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUtility.DisplayDialog(
                "WebGLへ切り替えてください",
                "現在の Build Target では WebGL プレイテストを作成できません。\n" +
                "File > Build Profiles（または Build Settings）で WebGL に切り替えてください。",
                "閉じる");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildReport report = BuildPlayer();
        string buildDirectory = Path.GetFullPath(BuildDirectory);

        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog(
                "ビルドに失敗しました",
                $"Build結果: {report.summary.result}\nConsoleを確認してください。",
                "閉じる");
            return;
        }

        // gh-pages は .gz を展開して載せるため、Pages 判定は展開後サイズで行う。
        if (!TryFindLargestPagesFile(buildDirectory, out string largestPath, out long largestBytes, out bool fromGzip))
        {
            EditorUtility.DisplayDialog(
                "ビルド完了",
                $"出力: {buildDirectory}\nサイズ確認用のファイルが見つかりませんでした。",
                "閉じる");
            return;
        }

        string relativeLargest = largestPath.StartsWith(buildDirectory, StringComparison.OrdinalIgnoreCase)
            ? largestPath.Substring(buildDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : largestPath;
        if (fromGzip) relativeLargest += " (展開後)";
        string sizeLabel = FormatBytes(largestBytes);
        bool exceedsGitLimit = largestBytes >= GitFileLimitBytes;
        string guidance = exceedsGitLimit
            ? "Pages向け展開後サイズが 100MB 以上です。git / gh-pages には載せないでください。itch.io へアップロードしてください。"
            : "Pages向け展開後サイズは全ファイル 100MB 未満です。gh-pages（GitHub Pages）で配布できます。";

        Debug.Log(
            $"[Web Playtest] ビルド完了: {buildDirectory}\n" +
            $"最大ファイル(Pages判定): {relativeLargest} ({sizeLabel})\n{guidance}",
            null);

        EditorUtility.DisplayDialog(
            "Web Playtest",
            $"出力: {BuildDirectory}\n\n最大ファイル(Pages判定):\n{relativeLargest}\n{sizeLabel}\n\n{guidance}\n\n手順: docs/Webプレイテスト.md",
            "閉じる");
    }

    public static BuildReport BuildPlayer()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("Switch the existing Editor to WebGL before building.");
        string[] scenes = GetBuildScenePaths();
        string buildDirectory = Path.GetFullPath(BuildDirectory);
        Directory.CreateDirectory(buildDirectory);
        WebGLCompressionFormat previousCompression = PlayerSettings.WebGL.compressionFormat;
        bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildDirectory,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            if (report.summary.result == BuildResult.Succeeded) PackageForItch();
            return report;
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = previousCompression;
            PlayerSettings.WebGL.decompressionFallback = previousFallback;
        }
    }

    [MenuItem("Tools/War Simulation/Web Playtest/Package itch.io Files")]
    public static void PackageForItch()
    {
        string directory = Path.GetFullPath(BuildDirectory);
        string index = Path.Combine(directory, "index.html");
        string html = ResponsivePage(File.ReadAllText(index));
        const string append = "document.body.appendChild(script);";
        const string ready = ".then((unityInstance) => {";
        const string failed = ".catch((message) => {";
        if (!html.Contains("playtestDataParts"))
        {
            if (!html.Contains(append) || !html.Contains(ready) || !html.Contains(failed))
                throw new InvalidOperationException("The WebGL template does not contain the expected loader.");
            const string load = @"fetch('Build/PlaytestData.json', { cache: 'no-store' })
        .then(response => { if (!response.ok) throw new Error(response.status); return response.json(); })
        .then(paths => Promise.all(paths.map(async path => {
          const response = await fetch(path);
          if (!response.ok) throw new Error(response.status);
          return response.blob();
        })))
        .then(playtestDataParts => {
          config.dataUrl = URL.createObjectURL(new Blob(playtestDataParts, { type: 'application/octet-stream' }));
          config.cacheControl = url => /\.bundle/.test(url) ? 'must-revalidate' : 'no-store';
          document.body.appendChild(script);
        }).catch(error => { console.error(error); unityShowBanner('ゲームデータを読み込めませんでした。ページを再読み込みしてください。', 'error'); });";
            html = html.Replace(append, load)
                .Replace(ready, ready + "\n                URL.revokeObjectURL(config.dataUrl);")
                .Replace(failed, failed + "\n                URL.revokeObjectURL(config.dataUrl);");
        }

        string build = Path.Combine(directory, "Build");
        string[] dataFiles = Directory.GetFiles(build, "*.data.unityweb");
        if (dataFiles.Length > 0)
        {
            string data = dataFiles[0];
            foreach (string old in Directory.GetFiles(build, "*.data.unityweb.part*")) File.Delete(old);
            foreach (string old in Directory.GetFiles(build, "PlaytestData.part*.bin")) File.Delete(old);
            var parts = new List<string>();
            using (var reader = new BinaryReader(new GZipStream(File.OpenRead(data), CompressionMode.Decompress)))
            {
                byte[] bytes;
                while ((bytes = reader.ReadBytes(128 * 1024 * 1024)).Length > 0)
                {
                    string part = Path.Combine(build, "PlaytestData.part" + parts.Count + ".bin");
                    File.WriteAllBytes(part, bytes);
                    parts.Add("\"Build/" + Path.GetFileName(part) + "\"");
                }
            }
            File.WriteAllText(Path.Combine(build, "PlaytestData.json"), "[" + string.Join(",", parts) + "]");
            File.WriteAllText(index, html);
            File.Delete(data);
        }
        if (!File.Exists(Path.Combine(build, "PlaytestData.json")))
            throw new InvalidOperationException("WebGL data is missing.");
        File.WriteAllText(index, html);
        foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            if (new FileInfo(file).Length > 200L * 1024 * 1024)
                throw new InvalidOperationException("itch.io HTML file limit exceeded: " + file);
        Debug.Log("Uncompressed WebGL data packaged in 128 MiB parts for itch.io.");
    }

    public static string ResponsivePage(string html)
    {
        const string style = @"<style id=""playtest-responsive"">
html, body { margin: 0; width: 100%; height: 100%; overflow: hidden; }
#unity-container { position: fixed !important; inset: 0 !important; width: 100% !important; height: 100% !important; transform: none !important; display: flex; flex-direction: column; justify-content: center; align-items: center; }
#unity-canvas { display: block; flex: 1 1 0; min-height: 0; max-height: 56.25vw; width: 100% !important; height: 0 !important; }
#unity-footer { flex: 0 0 38px; width: 100%; }
@media (max-height: 480px), (max-width: 600px) { #unity-footer { display: none; } }
</style>";
        return html.Contains("id=\"playtest-responsive\"")
            ? System.Text.RegularExpressions.Regex.Replace(html, "<style id=\"playtest-responsive\">[\\s\\S]*?</style>", style)
            : html.Replace("</head>", style + "\n</head>");
    }

    public static string[] GetBuildScenePaths()
    {
        var paths = new List<string> { PlaytestSceneNavigation.CountryScene, PlaytestSceneNavigation.CombatScene };
        EditorBuildSettingsScene[] buildSettingsScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildSettingsScenes.Length; i++)
        {
            EditorBuildSettingsScene buildSettingsScene = buildSettingsScenes[i];
            if (!buildSettingsScene.enabled || string.IsNullOrEmpty(buildSettingsScene.path) ||
                paths.Contains(buildSettingsScene.path))
                continue;

            paths.Add(buildSettingsScene.path);
        }

        foreach (string path in paths)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Build scene does not exist: " + path);
        return paths.ToArray();
    }

    private static bool TryFindLargestPagesFile(
        string directory,
        out string largestPath,
        out long largestBytes,
        out bool fromGzip)
    {
        largestPath = null;
        largestBytes = -1;
        fromGzip = false;
        if (!Directory.Exists(directory)) return false;

        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            long length;
            bool gzip;
            if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) &&
                TryReadGzipUncompressedSize(path, out long uncompressed))
            {
                length = uncompressed;
                gzip = true;
            }
            else
            {
                length = new FileInfo(path).Length;
                gzip = false;
            }

            if (length <= largestBytes) continue;
            largestBytes = length;
            largestPath = path;
            fromGzip = gzip;
        }

        return largestPath != null;
    }

    // gzip ISIZE: last 4 bytes, uncompressed size modulo 2^32 (enough under 4GB).
    private static bool TryReadGzipUncompressedSize(string path, out long uncompressedSize)
    {
        uncompressedSize = 0;
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length < 8) return false;
            stream.Seek(-4, SeekOrigin.End);
            byte[] buffer = new byte[4];
            if (stream.Read(buffer, 0, 4) != 4) return false;
            uncompressedSize = BitConverter.ToUInt32(buffer, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        double megabytes = bytes / (1024d * 1024d);
        return $"{megabytes:0.##} MB ({bytes:N0} bytes)";
    }
}
