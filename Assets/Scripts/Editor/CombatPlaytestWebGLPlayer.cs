using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatPlaytestWebGLPlayer
{
    private const string BuildDirectory = ".unity/CombatPlaytestWebGL";
    private const long GitFileLimitBytes = 100L * 1024L * 1024L;

    [MenuItem("Tools/War Simulation/Combat Playtest/Build WebGL")]
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

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog(
                "シーンがありません",
                "ビルドする戦闘シーン（例: GafuTest）を開いてください。",
                "閉じる");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string buildDirectory = Path.Combine(projectRoot, BuildDirectory);
        if (Directory.Exists(buildDirectory))
            Directory.Delete(buildDirectory, recursive: true);
        Directory.CreateDirectory(buildDirectory);

        WebGLCompressionFormat previousCompression = PlayerSettings.WebGL.compressionFormat;
        bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        // GitHub Pages は Content-Encoding を付けないため、フォールバック必須。
        PlayerSettings.WebGL.decompressionFallback = true;

        BuildReport report;
        try
        {
            // Development だと wasm が大きく、Gzip も付かないことが多い。
            // 100MB/ファイル制限（git・LFSなし）向けに Release + Gzip で出す。
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene.path },
                locationPathName = buildDirectory,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = previousCompression;
            PlayerSettings.WebGL.decompressionFallback = previousFallback;
        }

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
            $"[Combat Playtest WebGL] ビルド完了: {buildDirectory}\n" +
            $"最大ファイル(Pages判定): {relativeLargest} ({sizeLabel})\n{guidance}",
            null);

        EditorUtility.DisplayDialog(
            "Combat Playtest WebGL",
            $"出力: {BuildDirectory}\n\n最大ファイル(Pages判定):\n{relativeLargest}\n{sizeLabel}\n\n{guidance}\n\n手順: docs/Webプレイテスト.md",
            "閉じる");
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
