using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Wither3.Access.Launcher
{
    internal static class Program
    {
        private const string DefaultGameDir = @"C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt";
        private const string RuntimeFolderName = "Wither3Access";

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string renderer = ParseRenderer(args);
                string gameDir = ResolveGameDir(args);
                string gameExe = GetGameExe(gameDir, renderer);
                if (!File.Exists(gameExe))
                {
                    ShowError(
                        "Nie znaleziono pliku gry.",
                        "Sprawdzona sciezka:\n" + gameExe + "\n\n" +
                        "Uruchom launcher z folderu gry albo dodaj argument --game-dir=\"C:\\sciezka\\do\\The Witcher 3 Wild Hunt\".");
                    return 2;
                }

                string projectRoot = ResolveProjectRoot();
                if (!HasArg(args, "--skip-mod-check") && !IsModInstalled(gameDir))
                {
                    ShowError(
                        "Nie znaleziono moda Wither3.access.",
                        "Sprawdzona sciezka:\n" + Path.Combine(gameDir, "mods", "modWither3Access") + "\n\n" +
                        "Skopiuj folder mods z paczki do folderu gry albo uruchom narzedzie instalacyjne.");
                    return 3;
                }

                if (!HasArg(args, "--no-bridge"))
                {
                    StartScreenReaderBridge(projectRoot, args);
                }
                if (HasArg(args, "--with-companion"))
                {
                    StartCompanion(projectRoot, args);
                }
                StartOptionalBridge(args);

                string workingDirectory = Path.GetDirectoryName(gameExe);
                if (string.IsNullOrEmpty(workingDirectory))
                {
                    workingDirectory = gameDir;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = gameExe;
                startInfo.Arguments = GetGameArguments(args);
                startInfo.WorkingDirectory = workingDirectory;
                startInfo.UseShellExecute = true;

                Process.Start(startInfo);
                return 0;
            }
            catch (Exception ex)
            {
                ShowError("Nie udalo sie uruchomic Wiedzmina 3.", ex.Message);
                return 1;
            }
        }

        private static string ParseRenderer(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.Equals("dx11", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--dx11", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/dx11", StringComparison.OrdinalIgnoreCase))
                {
                    return "dx11";
                }
            }

            return "dx12";
        }

        private static string GetGameExe(string gameDir, string renderer)
        {
            string relativeExe = renderer == "dx11"
                ? Path.Combine("bin", "x64", "witcher3.exe")
                : Path.Combine("bin", "x64_dx12", "witcher3.exe");

            return Path.Combine(gameDir, relativeExe);
        }

        private static string GetGameArguments(string[] args)
        {
            string arguments = "";
            if (!HasArg(args, "--no-debugscripts"))
            {
                arguments = "-net -debugscripts";
            }

            string extraArgs = GetArgValue(args, "--game-args=");
            if (!string.IsNullOrEmpty(extraArgs))
            {
                if (arguments.Length > 0)
                {
                    arguments += " ";
                }
                arguments += extraArgs.Trim('"');
            }

            return arguments;
        }

        private static string ResolveGameDir(string[] args)
        {
            string explicitGameDir = GetArgValue(args, "--game-dir=");
            if (!string.IsNullOrEmpty(explicitGameDir))
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(explicitGameDir.Trim('"')));
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );

            if (LooksLikeGameDir(baseDirectory))
            {
                return baseDirectory;
            }

            string parent = Directory.GetParent(baseDirectory) != null
                ? Directory.GetParent(baseDirectory).FullName
                : baseDirectory;

            if (LooksLikeGameDir(parent))
            {
                return parent;
            }

            return DefaultGameDir;
        }

        private static string ResolveProjectRoot()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );

            string gameRuntime = Path.Combine(baseDirectory, RuntimeFolderName);
            if (File.Exists(Path.Combine(gameRuntime, "config", "settings.json")))
            {
                return gameRuntime;
            }

            if (File.Exists(Path.Combine(baseDirectory, "config", "settings.json")))
            {
                return baseDirectory;
            }

            string parent = Directory.GetParent(baseDirectory) != null
                ? Directory.GetParent(baseDirectory).FullName
                : baseDirectory;

            if (File.Exists(Path.Combine(parent, "config", "settings.json")))
            {
                return parent;
            }

            return baseDirectory;
        }

        private static bool LooksLikeGameDir(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            return File.Exists(Path.Combine(directory, "bin", "x64_dx12", "witcher3.exe")) ||
                File.Exists(Path.Combine(directory, "bin", "x64", "witcher3.exe"));
        }

        private static bool IsModInstalled(string gameDir)
        {
            return File.Exists(Path.Combine(
                gameDir,
                "mods",
                "modWither3Access",
                "content",
                "scripts",
                "game",
                "accessibility",
                "w3accessSpeech.ws"
            ));
        }

        private static void StartCompanion(string projectRoot, string[] args)
        {
            string companionPath = Path.Combine(projectRoot, "Witcher3MenuCompanion.exe");
            if (!File.Exists(companionPath))
            {
                ShowError(
                    "Nie znaleziono companiona.",
                    "Sprawdzona sciezka:\n" + companionPath + "\n\n" +
                    "Gra zostanie uruchomiona bez odczytu menu.");
                return;
            }

            string logDir = Path.Combine(GetAccessDataDir(), "logs");
            string arguments = "--exit-if-game-never-starts --game-start-timeout=120 --log=\"" + Path.Combine(logDir, "companion.log") + "\"";
            string dllPath = Path.Combine(projectRoot, "vendor", "nvdaControllerClient64.dll");
            if (File.Exists(dllPath))
            {
                arguments += " --dll=\"" + dllPath + "\"";
            }

            foreach (string arg in args)
            {
                if (arg.Equals("--speak-when-unfocused", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--no-mouse", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--wait-for-ready", StringComparison.OrdinalIgnoreCase))
                {
                    arguments += " " + arg;
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = companionPath;
            startInfo.Arguments = arguments;
            startInfo.WorkingDirectory = projectRoot;
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;

            Process.Start(startInfo);
        }

        private static void StartScreenReaderBridge(string projectRoot, string[] args)
        {
            string bridgePath = Path.Combine(projectRoot, "Witcher3ScreenReaderBridge.exe");
            if (!File.Exists(bridgePath))
            {
                return;
            }

            string accessDataDir = GetAccessDataDir();
            string arguments =
                "--log=\"" + Path.Combine(accessDataDir, "logs", "screenreader-bridge.log") + "\" " +
                "--watch=\"" + Path.Combine(accessDataDir, "speech.queue.log") + "\"";
            string dllPath = Path.Combine(projectRoot, "vendor", "nvdaControllerClient64.dll");
            if (File.Exists(dllPath))
            {
                arguments += " --dll=\"" + dllPath + "\"";
            }

            for (int index = 0; index < args.Length; index++)
            {
                if (args[index].StartsWith("--watch=", StringComparison.OrdinalIgnoreCase) ||
                    args[index].StartsWith("--state=", StringComparison.OrdinalIgnoreCase) ||
                    args[index].Equals("--stay-open", StringComparison.OrdinalIgnoreCase))
                {
                    arguments += " " + args[index];
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = bridgePath;
            startInfo.Arguments = arguments;
            startInfo.WorkingDirectory = projectRoot;
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;

            Process.Start(startInfo);
        }

        private static void StartOptionalBridge(string[] args)
        {
            string bridgePath = GetBridgePath(args);
            if (bridgePath == null)
            {
                return;
            }

            if (!File.Exists(bridgePath))
            {
                ShowError("Nie znaleziono bridge.", "Sprawdzona sciezka:\n" + bridgePath);
                return;
            }

            string workingDirectory = Path.GetDirectoryName(bridgePath);
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = bridgePath;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.UseShellExecute = true;

            Process.Start(startInfo);
        }

        private static string GetBridgePath(string[] args)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (args[index].Equals("--bridge", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[index + 1]);
                }
            }

            string defaultBridge = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wither3.Access.Bridge.exe");
            return File.Exists(defaultBridge) ? defaultBridge : null;
        }

        private static string GetAccessDataDir()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "The Witcher 3",
                "Wither3Access"
            );
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "logs"));
            return directory;
        }

        private static string GetArgValue(string[] args, string prefix)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(prefix.Length);
                }
            }
            return null;
        }

        private static bool HasArg(string[] args, string expected)
        {
            foreach (string arg in args)
            {
                if (arg.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
