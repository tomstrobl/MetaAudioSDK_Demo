using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MetaAudioSDKDemo.Tests.Editor
{
    /// <summary>
    /// Runs the demo's PlayMode tests from a menu item and writes a plain-text summary to
    /// Temp/TestResults.txt (handy for reviewing results outside the Test Runner window).
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunMenu
    {
        static readonly string ResultsPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "TestResults.txt");

        // Entering play mode reloads the domain, so the callbacks are registered again on every load.
        static TestRunMenu() => ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new ResultWriter());

        [MenuItem("Tools/MetaAudioSDK Demo/Run Scene Loop Test")]
        public static void Run()
        {
            if (File.Exists(ResultsPath)) File.Delete(ResultsPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                assemblyNames = new[] { "MetaAudioSDKDemo.Tests.PlayMode" },
            }));
        }

        class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                Append(sb, result, 0);
                Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath)!);
                File.WriteAllText(ResultsPath, sb.ToString());
                Debug.Log($"[Tests] {result.PassCount} passed, {result.FailCount} failed. Details: {ResultsPath}");
            }

            static void Append(StringBuilder sb, ITestResultAdaptor r, int depth)
            {
                sb.Append(' ', depth * 2).AppendLine($"{r.TestStatus}: {r.Name} ({r.Duration:F1}s)");
                if (!string.IsNullOrEmpty(r.Message)) sb.Append(' ', depth * 2 + 2).AppendLine(r.Message.Trim());
                if (r.TestStatus == TestStatus.Failed && !r.HasChildren && !string.IsNullOrEmpty(r.StackTrace))
                    sb.Append(' ', depth * 2 + 2).AppendLine(r.StackTrace.Trim());
                if (r.HasChildren) foreach (var c in r.Children) Append(sb, c, depth + 1);
            }
        }
    }
}
