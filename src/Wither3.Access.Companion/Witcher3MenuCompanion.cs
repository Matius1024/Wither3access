using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Wither3Access
{
    internal static class Witcher3MenuCompanion
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_MENU = 0x12;
        private const int VK_F6 = 0x75;
        private const int VK_F7 = 0x76;
        private const int VK_F8 = 0x77;
        private const int VK_F9 = 0x78;
        private const int VK_F10 = 0x79;
        private const int VK_F12 = 0x7B;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint MAPVK_VK_TO_VSC = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const double CalibrationStep = 0.005;
        private const double CalibrationLargeStep = 0.02;

        private static LowLevelKeyboardProc hookProc = HookCallback;
        private static IntPtr hookId = IntPtr.Zero;
        private static MenuModel model;
        private static Speaker speaker;
        private static Logger logger;
        private static HashSet<string> targetProcesses;
        private static List<string> targetTitles;
        private static string projectRootPath;
        private static string settingsFilePath;
        private static string modelConfigPath;
        private static bool autoLanguage;
        private static Dictionary<string, string> languageConfigs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static List<string> languageSources = new List<string>();
        private static DateTime lastAutoLanguageCheck = DateTime.MinValue;
        private static bool speakWhenUnfocused;
        private static bool mouseMode = false;
        private static bool waitForReady;
        private static bool exitWhenGameCloses = true;
        private static bool exitIfGameNeverStarts;
        private static int gameStartTimeoutSeconds = 120;
        private static bool menuReady = true;
        private static DateTime ignorePolledInputUntil = DateTime.MinValue;
        private static readonly int[] WatchedKeys = new int[]
        {
            VK_LEFT,
            VK_UP,
            VK_RIGHT,
            VK_DOWN,
            VK_RETURN,
            VK_ESCAPE,
            VK_F6,
            VK_F7,
            VK_F8,
            VK_F9,
            VK_F10,
            VK_F12
        };

        [STAThread]
        private static int Main(string[] args)
        {
            string projectRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
            projectRootPath = projectRoot;
            string configPath = Path.Combine(projectRoot, "config", "menus.pl.json");
            string settingsPath = Path.Combine(projectRoot, "config", "settings.json");
            settingsFilePath = settingsPath;
            string logPath = Path.Combine(projectRoot, "logs", "companion.log");
            string dllPath = Path.Combine(
                projectRoot,
                "vendor",
                IntPtr.Size == 4 ? "nvdaControllerClient32.dll" : "nvdaControllerClient64.dll"
            );

            ApplySettings(settingsPath, projectRoot, ref configPath);

            foreach (string arg in args)
            {
                if (arg.Equals("--speak-when-unfocused", StringComparison.OrdinalIgnoreCase))
                {
                    speakWhenUnfocused = true;
                }
                else if (arg.Equals("--no-mouse", StringComparison.OrdinalIgnoreCase))
                {
                    mouseMode = false;
                }
                else if (arg.Equals("--wait-for-ready", StringComparison.OrdinalIgnoreCase))
                {
                    waitForReady = true;
                }
                else if (arg.Equals("--stay-open", StringComparison.OrdinalIgnoreCase))
                {
                    exitWhenGameCloses = false;
                }
                else if (arg.Equals("--exit-if-game-never-starts", StringComparison.OrdinalIgnoreCase))
                {
                    exitIfGameNeverStarts = true;
                }
                else if (arg.StartsWith("--game-start-timeout=", StringComparison.OrdinalIgnoreCase))
                {
                    int parsedTimeout;
                    if (int.TryParse(arg.Substring("--game-start-timeout=".Length), out parsedTimeout))
                    {
                        gameStartTimeoutSeconds = Math.Max(0, parsedTimeout);
                    }
                }
                else if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    configPath = ResolvePath(projectRoot, arg.Substring("--config=".Length).Trim('"'));
                }
                else if (arg.StartsWith("--log=", StringComparison.OrdinalIgnoreCase))
                {
                    logPath = arg.Substring("--log=".Length).Trim('"');
                }
                else if (arg.StartsWith("--dll=", StringComparison.OrdinalIgnoreCase))
                {
                    dllPath = arg.Substring("--dll=".Length).Trim('"');
                }
            }
            modelConfigPath = configPath;
            menuReady = !waitForReady;

            logger = new Logger(logPath);
            try
            {
                logger.Write("Companion starting.");
                logger.Write("Project root: " + projectRoot);
                logger.Write("Config: " + configPath);
                logger.Write("Mouse mode: " + mouseMode);
                logger.Write("Wait for ready: " + waitForReady);
                logger.Write("Exit when game closes: " + exitWhenGameCloses);
                logger.Write("Exit if game never starts: " + exitIfGameNeverStarts);

                LoadModelAndTargets(configPath);

                speaker = new Speaker(dllPath, logger);
                if (waitForReady)
                {
                    speaker.Speak(model.Message("ready_wait", "Companion started. After the intro, when the main menu is visible, press F12."));
                }
                else
                {
                    speaker.Speak(model.FormatMessage("companion_ready_format", "The Witcher 3 access companion started. {0}", model.DescribeCurrent(true)));
                    if (mouseMode)
                    {
                        MoveMouseToCurrentItem();
                    }
                }

                logger.Write("Polling input loop started.");
                PollInputLoop();
                return 0;
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Write("Fatal: " + ex);
                }
                try
                {
                    if (speaker != null)
                    {
                        speaker.Speak("Witcher 3 access companion error. Details are in logs companion log.");
                    }
                }
                catch
                {
                }
                return 1;
            }
            finally
            {
                if (hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookId);
                }
                if (speaker != null)
                {
                    speaker.Dispose();
                }
            }
        }

        private static void ApplySettings(string settingsPath, string projectRoot, ref string configPath)
        {
            languageConfigs.Clear();
            languageConfigs["en"] = ResolvePath(projectRoot, "config/menus.en.json");
            languageConfigs["pl"] = ResolvePath(projectRoot, "config/menus.pl.json");
            languageSources.Clear();
            languageSources.Add(ResolvePath(projectRoot, "config/game_language.override.txt"));
            languageSources.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "The Witcher 3", "user.settings"));
            languageSources.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "The Witcher 3", "dx12user.settings"));

            if (!File.Exists(settingsPath))
            {
                autoLanguage = true;
                configPath = ResolveAutoLanguageConfig(projectRoot);
                return;
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> settings = serializer.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(settingsPath, Encoding.UTF8)
            );

            object value;
            if (settings.TryGetValue("menu_config", out value) && value != null)
            {
                string requestedConfig = Convert.ToString(value);
                autoLanguage = requestedConfig.Equals("auto", StringComparison.OrdinalIgnoreCase);
                configPath = autoLanguage
                    ? ResolveAutoLanguageConfig(projectRoot)
                    : ResolvePath(projectRoot, requestedConfig);
            }
            if (settings.TryGetValue("language_configs", out value) && value != null)
            {
                foreach (KeyValuePair<string, object> entry in Obj(value))
                {
                    languageConfigs[entry.Key] = ResolvePath(projectRoot, Convert.ToString(entry.Value));
                }
                if (autoLanguage)
                {
                    configPath = ResolveAutoLanguageConfig(projectRoot);
                }
            }
            if (settings.TryGetValue("language_sources", out value) && value != null)
            {
                languageSources.Clear();
                foreach (object item in Arr(value))
                {
                    languageSources.Add(ResolvePath(projectRoot, Convert.ToString(item)));
                }
                if (autoLanguage)
                {
                    configPath = ResolveAutoLanguageConfig(projectRoot);
                }
            }
            if (settings.TryGetValue("speak_when_unfocused", out value) && value != null)
            {
                speakWhenUnfocused = Convert.ToBoolean(value);
            }
            if (settings.TryGetValue("wait_for_ready", out value) && value != null)
            {
                waitForReady = Convert.ToBoolean(value);
            }
            if (settings.TryGetValue("mouse_mode", out value) && value != null)
            {
                mouseMode = Convert.ToBoolean(value);
            }
            if (settings.TryGetValue("exit_when_game_closes", out value) && value != null)
            {
                exitWhenGameCloses = Convert.ToBoolean(value);
            }
            if (settings.TryGetValue("exit_if_game_never_starts", out value) && value != null)
            {
                exitIfGameNeverStarts = Convert.ToBoolean(value);
            }
            if (settings.TryGetValue("game_start_timeout_seconds", out value) && value != null)
            {
                gameStartTimeoutSeconds = Math.Max(0, Convert.ToInt32(value));
            }
        }

        private static string ResolvePath(string projectRoot, string value)
        {
            value = Environment.ExpandEnvironmentVariables(value);
            if (Path.IsPathRooted(value))
            {
                return value;
            }
            return Path.GetFullPath(Path.Combine(projectRoot, value));
        }

        private static void LoadModelAndTargets(string configPath)
        {
            model = MenuModel.Load(configPath);
            targetProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in model.TargetProcesses)
            {
                targetProcesses.Add(name);
            }
            targetTitles = new List<string>(model.TargetTitles);
        }

        private static string ResolveAutoLanguageConfig(string projectRoot)
        {
            string language = DetectGameLanguage();
            string config;
            if (!languageConfigs.TryGetValue(language, out config))
            {
                config = languageConfigs.ContainsKey("en")
                    ? languageConfigs["en"]
                    : ResolvePath(projectRoot, "config/menus.en.json");
            }
            return config;
        }

        private static string DetectGameLanguage()
        {
            foreach (string source in languageSources)
            {
                try
                {
                    if (!File.Exists(source))
                    {
                        continue;
                    }
                    string text = File.ReadAllText(source, Encoding.UTF8);
                    string language = DetectLanguageFromText(text);
                    if (!string.IsNullOrEmpty(language))
                    {
                        return language;
                    }
                }
                catch
                {
                }
            }
            return "en";
        }

        private static string DetectLanguageFromText(string text)
        {
            string lower = text.ToLowerInvariant();
            string trimmed = lower.Trim();
            if (trimmed == "pl" || trimmed == "pl-pl" || trimmed == "polish" || trimmed == "polski")
            {
                return "pl";
            }
            if (trimmed == "en" || trimmed == "en-us" || trimmed == "english")
            {
                return "en";
            }
            if (
                lower.Contains("finalpolish")
                || lower.Contains("\"pl")
                || lower.Contains("pl-pl")
                || lower.Contains("polish")
                || lower.Contains("polski")
                || lower.Contains("language=pl")
            )
            {
                return "pl";
            }
            if (
                lower.Contains("finalenglish")
                || lower.Contains("\"en")
                || lower.Contains("en-us")
                || lower.Contains("english")
                || lower.Contains("language=en")
            )
            {
                return "en";
            }
            return "";
        }

        private static void RefreshAutoLanguageIfNeeded(bool force, bool speakChange)
        {
            if (!autoLanguage)
            {
                return;
            }
            if (!force && (DateTime.Now - lastAutoLanguageCheck).TotalSeconds < 1)
            {
                return;
            }
            lastAutoLanguageCheck = DateTime.Now;

            string newConfig = ResolveAutoLanguageConfig(projectRootPath);
            if (!newConfig.Equals(modelConfigPath, StringComparison.OrdinalIgnoreCase))
            {
                modelConfigPath = newConfig;
                LoadModelAndTargets(modelConfigPath);
                logger.Write("Auto language config switched to: " + modelConfigPath);
                if (speakChange && speaker != null)
                {
                    speaker.Speak(model.FormatMessage("config_reloaded_format", "Configuration reloaded. {0}", model.Reset()));
                }
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                return SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    proc,
                    GetModuleHandle(currentModule.ModuleName),
                    0
                );
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam.ToInt32() == WM_KEYDOWN || wParam.ToInt32() == WM_SYSKEYDOWN))
                {
                    KBDLLHOOKSTRUCT data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(
                        lParam,
                        typeof(KBDLLHOOKSTRUCT)
                    );
                    int vkCode = (int)data.vkCode;
                    if (IsTargetFocused())
                    {
                        bool consume = HandleKey(vkCode);
                        if (consume)
                        {
                            return new IntPtr(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Write("Hook error: " + ex);
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private static bool IsTargetFocused()
        {
            if (speakWhenUnfocused)
            {
                return true;
            }

            ActiveWindow active = ActiveWindow.Get();
            if (!string.IsNullOrEmpty(active.ProcessName) && targetProcesses.Contains(active.ProcessName))
            {
                return true;
            }

            string title = active.Title ?? "";
            foreach (string marker in targetTitles)
            {
                if (title.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HandleKey(int vkCode)
        {
            string text = null;
            bool moved = false;
            bool consume = false;
            int moveDelayMs = 0;

            if (!menuReady)
            {
                if (vkCode == VK_F12)
                {
                    menuReady = true;
                    text = model.Reset();
                    moved = true;
                    consume = true;
                }
                else
                {
                    return false;
                }
            }

            if (IsCalibrationShortcut(vkCode))
            {
                double step = IsKeyDown(VK_MENU) ? CalibrationLargeStep : CalibrationStep;
                double dx = 0;
                double dy = 0;
                if (vkCode == VK_LEFT)
                {
                    dx = -step;
                }
                else if (vkCode == VK_RIGHT)
                {
                    dx = step;
                }
                else if (vkCode == VK_UP)
                {
                    dy = -step;
                }
                else if (vkCode == VK_DOWN)
                {
                    dy = step;
                }

                text = model.NudgeCurrentPoint(dx, dy);
                moved = true;
                consume = true;
            }

            if (text == null && vkCode == VK_UP)
            {
                text = model.Move(-1);
                moved = true;
                consume = mouseMode;
            }
            else if (vkCode == VK_DOWN)
            {
                text = model.Move(1);
                moved = true;
                consume = mouseMode;
            }
            else if (vkCode == VK_RETURN)
            {
                bool keyboardActivate = model.CurrentItemUsesKeyboardActivation();
                if (mouseMode)
                {
                    if (model.CurrentItemUsesClickScan())
                    {
                        ScanClickCurrentItem();
                    }
                    else if (keyboardActivate)
                    {
                        MoveMouseToCurrentItem();
                    }
                    else
                    {
                        ClickPoint activationPoint = model.GetCurrentPoint();
                        ClickAtPoint(activationPoint);
                    }
                    consume = true;
                    moveDelayMs = 350;
                }
                if (keyboardActivate)
                {
                    SendConfiguredKeys(model.CurrentItemPreActivationKeys());
                    SendKeyTap(VK_RETURN);
                }
                text = model.Activate();
                moved = true;
            }
            else if (vkCode == VK_ESCAPE)
            {
                text = model.Back();
                moved = true;
            }
            else if (vkCode == VK_LEFT)
            {
                text = model.ChangeValue(model.Message("left", "left"));
                if (model.CurrentItemUsesLeftRight())
                {
                    if (mouseMode)
                    {
                        if (model.CurrentItemUsesDragValue())
                        {
                            DragCurrentValue(false);
                        }
                        else
                        {
                            MoveMouseToCurrentItem();
                        }
                    }
                    if (mouseMode)
                    {
                        SendKeyTap(VK_LEFT);
                    }
                    consume = true;
                }
            }
            else if (vkCode == VK_RIGHT)
            {
                text = model.ChangeValue(model.Message("right", "right"));
                if (model.CurrentItemUsesLeftRight())
                {
                    if (mouseMode)
                    {
                        if (model.CurrentItemUsesDragValue())
                        {
                            DragCurrentValue(true);
                        }
                        else
                        {
                            MoveMouseToCurrentItem();
                        }
                    }
                    if (mouseMode)
                    {
                        SendKeyTap(VK_RIGHT);
                    }
                    consume = true;
                }
            }
            else if (vkCode == VK_F7)
            {
                text = DescribeMousePosition();
                consume = true;
            }
            else if (vkCode == VK_F6)
            {
                mouseMode = !mouseMode;
                text = mouseMode
                    ? model.Message("mouse_mode_on", "Mouse mode on.")
                    : model.Message("mouse_mode_off", "Mouse mode off.");
            }
            else if (vkCode == VK_F8)
            {
                try
                {
                    ApplySettings(settingsFilePath, projectRootPath, ref modelConfigPath);
                    RefreshAutoLanguageIfNeeded(true, false);
                    LoadModelAndTargets(modelConfigPath);
                    text = model.FormatMessage("config_reloaded_format", "Configuration reloaded. {0}", model.Reset());
                    moved = true;
                }
                catch (Exception ex)
                {
                    text = model.FormatMessage("config_reload_failed_format", "Could not reload configuration. {0}", ex.Message);
                }
                consume = true;
            }
            else if (vkCode == VK_F9)
            {
                text = model.DescribeCurrent(true);
                consume = true;
            }
            else if (vkCode == VK_F10)
            {
                text = model.DescribeMenu();
                consume = true;
            }
            else if (vkCode == VK_F12)
            {
                text = model.Reset();
                moved = true;
                consume = true;
            }

            if (text != null)
            {
                logger.Write("Key " + vkCode + ": " + text);
                speaker.Speak(text);
                if (moved && mouseMode)
                {
                    if (moveDelayMs > 0)
                    {
                        Thread.Sleep(moveDelayMs);
                    }
                    MoveMouseToCurrentItem();
                }
            }

            return consume;
        }

        private static void PollInputLoop()
        {
            Dictionary<int, bool> wasDown = new Dictionary<int, bool>();
            foreach (int key in WatchedKeys)
            {
                wasDown[key] = false;
            }

            bool exitShortcutWasDown = false;
            bool gameWasRunning = false;
            DateTime lastGameProcessCheck = DateTime.MinValue;
            DateTime companionStarted = DateTime.Now;

            while (true)
            {
                bool exitShortcutDown = IsExitShortcutDown();
                if (exitShortcutDown && !exitShortcutWasDown)
                {
                    SpeakAndLogExit("companion_exit", "Companion closing.");
                    return;
                }
                exitShortcutWasDown = exitShortcutDown;

                if (exitWhenGameCloses && (DateTime.Now - lastGameProcessCheck).TotalSeconds >= 1)
                {
                    lastGameProcessCheck = DateTime.Now;
                    bool gameRunning = IsGameProcessRunning();
                    if (gameRunning)
                    {
                        gameWasRunning = true;
                    }
                    else if (gameWasRunning)
                    {
                        SpeakAndLogExit("game_closed", "Game closed. Companion closing.");
                        return;
                    }
                    else if (
                        exitIfGameNeverStarts &&
                        gameStartTimeoutSeconds > 0 &&
                        (DateTime.Now - companionStarted).TotalSeconds >= gameStartTimeoutSeconds
                    )
                    {
                        SpeakAndLogExit("game_not_started", "Game did not start. Companion closing.");
                        return;
                    }
                }

                RefreshAutoLanguageIfNeeded(false, true);
                if (DateTime.Now < ignorePolledInputUntil)
                {
                    RefreshWatchedKeyStates(wasDown);
                    Thread.Sleep(25);
                    continue;
                }

                foreach (int key in WatchedKeys)
                {
                    bool isDown = IsKeyDown(key);
                    if (isDown && !wasDown[key] && IsTargetFocused())
                    {
                        HandleKey(key);
                    }
                    wasDown[key] = isDown;
                }
                Thread.Sleep(25);
            }
        }

        private static void RefreshWatchedKeyStates(Dictionary<int, bool> wasDown)
        {
            foreach (int key in WatchedKeys)
            {
                wasDown[key] = IsKeyDown(key);
            }
        }

        private static bool IsExitShortcutDown()
        {
            return IsKeyDown(VK_CONTROL) && IsKeyDown(VK_SHIFT) && IsKeyDown(VK_F12);
        }

        private static bool IsCalibrationShortcut(int vkCode)
        {
            if (!IsKeyDown(VK_CONTROL) || !IsKeyDown(VK_SHIFT))
            {
                return false;
            }
            return vkCode == VK_LEFT || vkCode == VK_RIGHT || vkCode == VK_UP || vkCode == VK_DOWN;
        }

        private static bool IsKeyDown(int key)
        {
            return (GetAsyncKeyState(key) & 0x8000) != 0;
        }

        private static bool IsGameProcessRunning()
        {
            if (targetProcesses == null)
            {
                return false;
            }

            foreach (string configuredName in targetProcesses)
            {
                string processName = Path.GetFileNameWithoutExtension(configuredName);
                if (string.IsNullOrEmpty(processName))
                {
                    continue;
                }

                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Write("Game process check failed for " + processName + ": " + ex.Message);
                }
                finally
                {
                    if (processes != null)
                    {
                        foreach (Process process in processes)
                        {
                            process.Dispose();
                        }
                    }
                }
            }

            return false;
        }

        private static void SpeakAndLogExit(string messageKey, string fallback)
        {
            string text = model != null ? model.Message(messageKey, fallback) : fallback;
            logger.Write(text);
            if (speaker != null)
            {
                speaker.Speak(text);
                Thread.Sleep(250);
            }
        }

        private static void MoveMouseToCurrentItem()
        {
            if (!mouseMode)
            {
                return;
            }
            ClickPoint point = model.GetCurrentPoint();
            if (!point.HasValue)
            {
                return;
            }
            MoveMouseToPoint(point);
        }

        private static void ClickCurrentItem()
        {
            ClickAtPoint(model.GetCurrentPoint());
        }

        private static void ClickAtPoint(ClickPoint point)
        {
            if (!point.HasValue)
            {
                logger.Write("Mouse click skipped. No point configured.");
                return;
            }
            MoveMouseToPoint(point);
            SendMouseButton(false);
            SendMouseButton(true);
            logger.Write("Mouse click.");
        }

        private static void ScanClickCurrentItem()
        {
            foreach (ClickPoint point in model.GetCurrentScanPoints())
            {
                ClickAtPoint(point);
                Thread.Sleep(120);
            }
        }

        private static void DragCurrentValue(bool right)
        {
            ClickPoint from = model.GetValuePoint(!right);
            ClickPoint to = model.GetValuePoint(right);
            if (!from.HasValue || !to.HasValue)
            {
                return;
            }

            MoveMouseToPoint(from);
            SendMouseButton(false);
            Thread.Sleep(80);
            MoveMouseToPoint(to);
            Thread.Sleep(80);
            SendMouseButton(true);
            logger.Write("Mouse drag value " + (right ? "right." : "left."));
        }

        private static void MoveMouseToPoint(ClickPoint point)
        {
            if (!point.HasValue)
            {
                return;
            }
            int x = (int)(GetSystemMetrics(0) * point.X);
            int y = (int)(GetSystemMetrics(1) * point.Y);
            SetCursorPos(x, y);
            SendMouseMove(point);
            logger.Write("Mouse moved to " + x + "," + y);
        }

        private static void SendMouseMove(ClickPoint point)
        {
            INPUT input = new INPUT();
            input.type = INPUT_MOUSE;
            input.u.mi.dx = (int)Math.Round(ClampToUnit(point.X) * 65535.0);
            input.u.mi.dy = (int)Math.Round(ClampToUnit(point.Y) * 65535.0);
            input.u.mi.mouseData = 0;
            input.u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
            input.u.mi.time = 0;
            input.u.mi.dwExtraInfo = UIntPtr.Zero;
            SendInputChecked(input, "mouse move");
        }

        private static void SendMouseButton(bool keyUp)
        {
            INPUT input = new INPUT();
            input.type = INPUT_MOUSE;
            input.u.mi.dx = 0;
            input.u.mi.dy = 0;
            input.u.mi.mouseData = 0;
            input.u.mi.dwFlags = keyUp ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN;
            input.u.mi.time = 0;
            input.u.mi.dwExtraInfo = UIntPtr.Zero;
            SendInputChecked(input, keyUp ? "mouse left up" : "mouse left down");
        }

        private static void SendConfiguredKeys(IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                int vkCode;
                if (TryMapKeyName(key, out vkCode))
                {
                    SendKeyTap(vkCode);
                    Thread.Sleep(60);
                }
            }
        }

        private static void SendKeyTap(int vkCode)
        {
            ignorePolledInputUntil = DateTime.Now.AddMilliseconds(250);
            SendScanCode(vkCode, false);
            Thread.Sleep(35);
            SendScanCode(vkCode, true);
            logger.Write("Synthetic scan-code key tap: " + vkCode);
        }

        private static void SendScanCode(int vkCode, bool keyUp)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.u.ki.wVk = 0;
            input.u.ki.wScan = (ushort)MapVirtualKey((uint)vkCode, MAPVK_VK_TO_VSC);
            input.u.ki.dwFlags = KEYEVENTF_SCANCODE | (keyUp ? KEYEVENTF_KEYUP : 0);
            if (IsExtendedKey(vkCode))
            {
                input.u.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
            }
            input.u.ki.time = 0;
            input.u.ki.dwExtraInfo = UIntPtr.Zero;

            SendInputChecked(input, "key " + vkCode);
        }

        private static void SendInputChecked(INPUT input, string description)
        {
            uint sent = SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
            if (sent != 1)
            {
                logger.Write("SendInput failed for " + description + ". Win32: " + Marshal.GetLastWin32Error());
            }
        }

        private static double ClampToUnit(double value)
        {
            if (value < 0)
            {
                return 0;
            }
            if (value > 1)
            {
                return 1;
            }
            return value;
        }

        private static bool IsExtendedKey(int vkCode)
        {
            return vkCode == VK_LEFT
                || vkCode == VK_RIGHT
                || vkCode == VK_UP
                || vkCode == VK_DOWN;
        }

        private static bool TryMapKeyName(string key, out int vkCode)
        {
            string normalized = (key ?? "").Trim().ToLowerInvariant();
            if (normalized == "left")
            {
                vkCode = VK_LEFT;
                return true;
            }
            if (normalized == "right")
            {
                vkCode = VK_RIGHT;
                return true;
            }
            if (normalized == "up")
            {
                vkCode = VK_UP;
                return true;
            }
            if (normalized == "down")
            {
                vkCode = VK_DOWN;
                return true;
            }
            if (normalized == "enter" || normalized == "return")
            {
                vkCode = VK_RETURN;
                return true;
            }
            if (normalized == "esc" || normalized == "escape")
            {
                vkCode = VK_ESCAPE;
                return true;
            }

            vkCode = 0;
            return false;
        }

        private static string DescribeMousePosition()
        {
            POINT point;
            if (!GetCursorPos(out point))
            {
                return model.Message("mouse_position_failed", "Could not read mouse position.");
            }

            int width = GetSystemMetrics(0);
            int height = GetSystemMetrics(1);
            double x = width > 0 ? point.X / (double)width : 0;
            double y = height > 0 ? point.Y / (double)height : 0;
            return model.FormatMessage(
                "mouse_position_format",
                "Mouse position: x {0:0.000}, y {1:0.000}.",
                x,
                y
            );
        }

        private sealed class Logger
        {
            private readonly string path;
            private readonly object sync = new object();

            public Logger(string path)
            {
                this.path = path;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "", Encoding.UTF8);
            }

            public void Write(string line)
            {
                lock (sync)
                {
                    File.AppendAllText(
                        path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + line + Environment.NewLine,
                        Encoding.UTF8
                    );
                }
            }
        }

        private sealed class Speaker : IDisposable
        {
            private readonly Logger logger;
            private object sapiVoice;
            private Type sapiType;
            private NvdaTest nvdaTest;
            private NvdaSpeak nvdaSpeak;
            private NvdaCancel nvdaCancel;
            private bool nvdaReady;

            public Speaker(string dllPath, Logger logger)
            {
                this.logger = logger;
                TryLoadNvda(dllPath);
                TryLoadSapi();
            }

            public void Speak(string text)
            {
                if (nvdaReady && nvdaSpeak != null)
                {
                    try
                    {
                        if (nvdaCancel != null)
                        {
                            nvdaCancel();
                        }
                        int speakResult = nvdaSpeak(text);
                        logger.Write("NVDA speak result: " + speakResult);
                        if (speakResult == 0)
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Write("NVDA speak failed: " + ex.Message);
                    }
                }

                logger.Write("SAPI fallback speak: " + text);
                if (sapiVoice != null && sapiType != null)
                {
                    try
                    {
                        // 1 = async, 2 = purge queued speech.
                        sapiType.InvokeMember(
                            "Speak",
                            BindingFlags.InvokeMethod,
                            null,
                            sapiVoice,
                            new object[] { text, 3 }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.Write("SAPI COM speak failed: " + ex.Message);
                        TargetInvocationException targetException = ex as TargetInvocationException;
                        if (targetException != null && targetException.InnerException != null)
                        {
                            logger.Write("SAPI COM inner: " + targetException.InnerException.Message);
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (sapiVoice != null && Marshal.IsComObject(sapiVoice))
                {
                    Marshal.ReleaseComObject(sapiVoice);
                }
            }

            private void TryLoadSapi()
            {
                try
                {
                    sapiType = Type.GetTypeFromProgID("SAPI.SpVoice");
                    if (sapiType == null)
                    {
                        logger.Write("SAPI.SpVoice ProgID not found.");
                        return;
                    }
                    sapiVoice = Activator.CreateInstance(sapiType);
                    logger.Write("SAPI.SpVoice loaded.");
                }
                catch (Exception ex)
                {
                    logger.Write("SAPI.SpVoice load failed: " + ex.Message);
                }
            }

            private void TryLoadNvda(string dllPath)
            {
                if (!File.Exists(dllPath))
                {
                    logger.Write("NVDA DLL missing: " + dllPath);
                    return;
                }

                IntPtr library = LoadLibrary(dllPath);
                if (library == IntPtr.Zero)
                {
                    logger.Write("NVDA DLL load failed. Win32: " + Marshal.GetLastWin32Error());
                    return;
                }

                nvdaTest = GetDelegate<NvdaTest>(library, "nvdaController_testIfRunning");
                nvdaSpeak = GetDelegate<NvdaSpeak>(library, "nvdaController_speakText");
                nvdaCancel = GetDelegate<NvdaCancel>(library, "nvdaController_cancelSpeech");

                if (nvdaTest == null || nvdaSpeak == null)
                {
                    logger.Write("NVDA DLL exports missing.");
                    return;
                }

                int result = nvdaTest();
                logger.Write("NVDA testIfRunning result: " + result);
                nvdaReady = result == 0;
            }

            private static T GetDelegate<T>(IntPtr library, string name) where T : class
            {
                IntPtr proc = GetProcAddress(library, name);
                if (proc == IntPtr.Zero)
                {
                    return null;
                }
                return Marshal.GetDelegateForFunctionPointer(proc, typeof(T)) as T;
            }
        }

        private sealed class MenuModel
        {
            private readonly Dictionary<string, object> root;
            private readonly Dictionary<string, object> menus;
            private readonly Dictionary<string, object> messages;
            private readonly Dictionary<string, int> indices = new Dictionary<string, int>();
            private readonly Dictionary<string, ClickPoint> calibratedPoints = new Dictionary<string, ClickPoint>();
            private readonly List<string> stack = new List<string>();
            private readonly string startMenu;

            private MenuModel(Dictionary<string, object> root)
            {
                this.root = root;
                menus = Obj(root["menus"]);
                messages = root.ContainsKey("messages")
                    ? Obj(root["messages"])
                    : new Dictionary<string, object>();
                startMenu = Str(root, "start_menu", "main");
                stack.Add(startMenu);
            }

            public IEnumerable<string> TargetProcesses
            {
                get { return StringArray(root, "target_processes"); }
            }

            public IEnumerable<string> TargetTitles
            {
                get { return StringArray(root, "target_titles"); }
            }

            public static MenuModel Load(string path)
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> parsed = serializer.Deserialize<Dictionary<string, object>>(
                    File.ReadAllText(path, Encoding.UTF8)
                );
                return new MenuModel(parsed);
            }

            public string Message(string key, string fallback)
            {
                return Str(messages, key, fallback);
            }

            public string FormatMessage(string key, string fallback, params object[] args)
            {
                string format = Message(key, fallback);
                try
                {
                    return string.Format(format, args);
                }
                catch
                {
                    return fallback;
                }
            }

            public string DescribeCurrent(bool prefix)
            {
                Dictionary<string, object> item = CurrentItem();
                object[] items = Items();
                string label = Str(item, "label", Message("no_label", "Unlabeled item"));
                string position = FormatMessage("position_format", "{0} of {1}", Index + 1, items.Length);
                if (prefix)
                {
                    return Title + ". " + label + ", " + position + ".";
                }
                return label + ", " + position + ".";
            }

            public string DescribeMenu()
            {
                List<string> labels = new List<string>();
                foreach (object item in Items())
                {
                    labels.Add(Str(Obj(item), "label", Message("no_label", "Unlabeled item")));
                }
                return FormatMessage("list_format", "{0}. Items: {1}.", Title, string.Join(", ", labels.ToArray()));
            }

            public string Move(int delta)
            {
                object[] items = Items();
                if (items.Length == 0)
                {
                    Index = 0;
                }
                else
                {
                    Index = (Index + delta + items.Length) % items.Length;
                }
                return DescribeCurrent(false);
            }

            public string Activate()
            {
                Dictionary<string, object> item = CurrentItem();
                string label = Str(item, "label", Message("no_label", "Unlabeled item"));
                if (Bool(item, "back", false))
                {
                    return Back();
                }

                string next = Str(item, "enter", "");
                if (!string.IsNullOrEmpty(next))
                {
                    if (menus.ContainsKey(next))
                    {
                        stack.Add(next);
                        if (!indices.ContainsKey(next))
                        {
                            indices[next] = 0;
                        }
                        return DescribeCurrent(true);
                    }
                    return FormatMessage("missing_submenu_format", "{0}. Missing submenu model {1}.", label, next);
                }

                return FormatMessage("activate_format", "Activating: {0}.", label);
            }

            public string Back()
            {
                if (stack.Count > 1)
                {
                    stack.RemoveAt(stack.Count - 1);
                    return DescribeCurrent(true);
                }
                return Message("main_menu", "Main menu.");
            }

            public string Reset()
            {
                stack.Clear();
                stack.Add(startMenu);
                indices.Clear();
                return DescribeCurrent(true);
            }

            public string ChangeValue(string direction)
            {
                Dictionary<string, object> item = CurrentItem();
                string label = Str(item, "label", Message("no_label", "Unlabeled item"));
                return FormatMessage("value_change_format", "{0}. Change value: {1}.", label, direction);
            }

            public bool CurrentItemUsesLeftRight()
            {
                return Bool(CurrentItem(), "left_right", false);
            }

            public bool CurrentItemUsesDragValue()
            {
                return Bool(CurrentItem(), "drag_value", false);
            }

            public bool CurrentItemUsesKeyboardActivation()
            {
                return Bool(CurrentItem(), "keyboard_activate", false);
            }

            public bool CurrentItemUsesClickScan()
            {
                Dictionary<string, object> item = CurrentItem();
                return item.ContainsKey("click_scan");
            }

            public IEnumerable<string> CurrentItemPreActivationKeys()
            {
                return StringArray(CurrentItem(), "keyboard_pre_keys");
            }

            public ClickPoint GetCurrentPoint()
            {
                ClickPoint calibrated;
                if (calibratedPoints.TryGetValue(CurrentPointKey, out calibrated))
                {
                    return calibrated;
                }

                Dictionary<string, object> item = CurrentItem();
                if (item.ContainsKey("x") && item.ContainsKey("y"))
                {
                    return new ClickPoint(ToDouble(item["x"]), ToDouble(item["y"]), true);
                }

                Dictionary<string, object> pointer = CurrentMenu().ContainsKey("pointer")
                    ? Obj(CurrentMenu()["pointer"])
                    : null;
                if (pointer == null)
                {
                    return new ClickPoint(0, 0, false);
                }

                double x = Num(pointer, "x", 0.5);
                double firstY = Num(pointer, "first_y", 0.42);
                double stepY = Num(pointer, "step_y", 0.06);
                return new ClickPoint(x, firstY + (Index * stepY), true);
            }

            public IEnumerable<ClickPoint> GetCurrentScanPoints()
            {
                Dictionary<string, object> item = CurrentItem();
                if (!item.ContainsKey("click_scan"))
                {
                    yield return GetCurrentPoint();
                    yield break;
                }

                Dictionary<string, object> scan = Obj(item["click_scan"]);
                ClickPoint rowPoint = GetCurrentPoint();
                double y = Num(scan, "y", rowPoint.HasValue ? rowPoint.Y : 0.5);
                double xStart = Num(scan, "x_start", 0.35);
                double xEnd = Num(scan, "x_end", 0.85);
                int steps = (int)Num(scan, "steps", 5);
                if (steps < 1)
                {
                    steps = 1;
                }

                for (int i = 0; i < steps; i++)
                {
                    double fraction = steps == 1 ? 0.5 : i / (double)(steps - 1);
                    double x = xStart + ((xEnd - xStart) * fraction);
                    yield return new ClickPoint(Clamp(x), Clamp(y), true);
                }
            }

            public string NudgeCurrentPoint(double dx, double dy)
            {
                ClickPoint point = GetCurrentPoint();
                if (!point.HasValue)
                {
                    point = new ClickPoint(0.5, 0.5, true);
                }

                ClickPoint nudged = new ClickPoint(
                    Clamp(point.X + dx),
                    Clamp(point.Y + dy),
                    true
                );
                calibratedPoints[CurrentPointKey] = nudged;
                return FormatMessage(
                    "calibration_point_format",
                    "Click point: {0}. x {1:0.000}, y {2:0.000}.",
                    Str(CurrentItem(), "label", Message("no_label", "Unlabeled item")),
                    nudged.X,
                    nudged.Y
                );
            }

            public ClickPoint GetValuePoint(bool right)
            {
                Dictionary<string, object> item = CurrentItem();
                Dictionary<string, object> menu = CurrentMenu();
                Dictionary<string, object> pointer = menu.ContainsKey("pointer")
                    ? Obj(menu["pointer"])
                    : null;

                string prefix = right ? "right" : "left";
                string xKey = prefix + "_x";
                string yKey = prefix + "_y";
                ClickPoint rowPoint = GetCurrentPoint();

                if (item.ContainsKey(xKey))
                {
                    double x = ToDouble(item[xKey]);
                    double y = item.ContainsKey(yKey)
                        ? ToDouble(item[yKey])
                        : rowPoint.Y;
                    return new ClickPoint(x, y, true);
                }

                if (pointer != null && pointer.ContainsKey(xKey))
                {
                    double x = ToDouble(pointer[xKey]);
                    double y = pointer.ContainsKey(yKey)
                        ? ToDouble(pointer[yKey])
                        : rowPoint.Y;
                    return new ClickPoint(x, y, true);
                }

                return new ClickPoint(0, 0, false);
            }

            private string MenuId
            {
                get { return stack[stack.Count - 1]; }
            }

            private string CurrentPointKey
            {
                get { return MenuId + ":" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            }

            private string Title
            {
                get { return Str(CurrentMenu(), "title", MenuId); }
            }

            private int Index
            {
                get
                {
                    int index;
                    return indices.TryGetValue(MenuId, out index) ? index : 0;
                }
                set
                {
                    indices[MenuId] = value;
                }
            }

            private Dictionary<string, object> CurrentMenu()
            {
                return Obj(menus[MenuId]);
            }

            private object[] Items()
            {
                return Arr(CurrentMenu()["items"]);
            }

            private Dictionary<string, object> CurrentItem()
            {
                object[] items = Items();
                if (items.Length == 0)
                {
                    return new Dictionary<string, object>();
                }
                return Obj(items[Index]);
            }

            private static double Clamp(double value)
            {
                if (value < 0)
                {
                    return 0;
                }
                if (value > 1)
                {
                    return 1;
                }
                return value;
            }
        }

        private struct ClickPoint
        {
            public readonly double X;
            public readonly double Y;
            public readonly bool HasValue;

            public ClickPoint(double x, double y, bool hasValue)
            {
                X = x;
                Y = y;
                HasValue = hasValue;
            }
        }

        private struct ActiveWindow
        {
            public string Title;
            public string ProcessName;

            public static ActiveWindow Get()
            {
                IntPtr hwnd = GetForegroundWindow();
                StringBuilder title = new StringBuilder(512);
                GetWindowText(hwnd, title, title.Capacity);

                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                string processName = "";
                if (pid != 0)
                {
                    IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                    if (handle != IntPtr.Zero)
                    {
                        try
                        {
                            StringBuilder path = new StringBuilder(32768);
                            int size = path.Capacity;
                            if (QueryFullProcessImageName(handle, 0, path, ref size))
                            {
                                processName = Path.GetFileName(path.ToString());
                            }
                        }
                        finally
                        {
                            CloseHandle(handle);
                        }
                    }
                }
                return new ActiveWindow { Title = title.ToString(), ProcessName = processName };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private static Dictionary<string, object> Obj(object value)
        {
            return (Dictionary<string, object>)value;
        }

        private static object[] Arr(object value)
        {
            object[] array = value as object[];
            if (array != null)
            {
                return array;
            }

            ArrayList list = value as ArrayList;
            if (list != null)
            {
                return list.ToArray();
            }

            return new object[0];
        }

        private static string Str(Dictionary<string, object> obj, string key, string fallback)
        {
            object value;
            return obj.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : fallback;
        }

        private static bool Bool(Dictionary<string, object> obj, string key, bool fallback)
        {
            object value;
            return obj.TryGetValue(key, out value) && value != null ? Convert.ToBoolean(value) : fallback;
        }

        private static double Num(Dictionary<string, object> obj, string key, double fallback)
        {
            object value;
            return obj.TryGetValue(key, out value) && value != null ? ToDouble(value) : fallback;
        }

        private static double ToDouble(object value)
        {
            return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static IEnumerable<string> StringArray(Dictionary<string, object> obj, string key)
        {
            object value;
            if (!obj.TryGetValue(key, out value))
            {
                yield break;
            }
            foreach (object item in Arr(value))
            {
                yield return Convert.ToString(item);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate int NvdaTest();
        private delegate int NvdaCancel();
        private delegate int NvdaSpeak([MarshalAs(UnmanagedType.LPWStr)] string text);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
