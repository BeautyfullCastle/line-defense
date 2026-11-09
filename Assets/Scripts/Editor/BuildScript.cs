using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>WebGL 빌드를 메뉴와 배치 모드에서 실행하는 빌드 스크립트입니다.</summary>
public static class BuildScript
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string OutputDirectory = "Build/WebGL";

    /// <summary>WebGL 플레이어를 Build/WebGL에 빌드합니다. 실패하면 배치 모드에서 종료 코드 1로 끝납니다.</summary>
    [MenuItem("Build/WebGL")]
    public static void BuildWebGL()
    {
        try
        {
            ConfigurePlayerSettings();
            string root = Path.GetDirectoryName(Application.dataPath);
            string outputDirectory = Path.Combine(root, OutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputDirectory,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            // 콘텐츠 팩을 먼저 빌드해 StreamingAssets에 넣음. 플레이어 빌드 콜백 안에서 번들을 빌드하면 Unity가 멈춤
            var report = H.ContentsLoader.Editor.ContentsBuilder.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"WebGL 빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}개");
                Fail();
                return;
            }
            Debug.Log($"WebGL 빌드 완료: {outputDirectory} ({report.summary.totalSize} bytes)");
        }
        catch (Exception e)
        {
            Debug.LogError("WebGL 빌드 예외: " + e);
            Fail();
        }
    }

    private static void Fail()
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "BeautyfullCastle";
        PlayerSettings.productName = "요새 노선도";
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.runInBackground = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.template = "PROJECT:Fullscreen";
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
    }
}
