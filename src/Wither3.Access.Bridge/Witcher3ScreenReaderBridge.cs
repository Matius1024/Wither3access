using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Wither3Access
{
    internal static class Witcher3ScreenReaderBridge
    {
        private const string Prefix = "W3ACCESS";
        private const string MenuPrefix = "W3ACCESS_MENU";
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;
        private const int VkW = 0x57;
        private const int VkS = 0x53;
        private const int VkUp = 0x26;
        private const int VkDown = 0x28;
        private const int VkF9 = 0x78;
        private const int VkF10 = 0x79;
        private const int VkF12 = 0x7B;
        private static readonly List<string> WatchFiles = new List<string>();
        private static readonly List<string> StateFiles = new List<string>();
        private static readonly HashSet<string> UnavailableFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Logger logger;
        private static Speaker speaker;
        private static Mutex singleInstanceMutex;
        private static bool exitWhenGameCloses = true;
        private static bool gameWasRunning;
        private static bool hotkeysEnabled = true;
        private static bool f9WasDown;
        private static bool f10WasDown;
        private static bool f12WasDown;
        private static bool closeWasDown;
        private static bool menuUpWasDown;
        private static bool menuDownWasDown;
        private static readonly Stack<MenuState> menuStack = new Stack<MenuState>();
        private static readonly List<string> trackedMenuItems = new List<string>();
        private static readonly List<string> pendingMenuItems = new List<string>();
        private static string trackedMenuTitle;
        private static string pendingMenuTitle;
        private static int trackedMenuIndex;
        private static int pendingMenuDirection;
        private static DateTime pendingMenuDirectionAt = DateTime.MinValue;
        private static DateTime lastMenuPopAt = DateTime.MinValue;
        private static bool pendingMenuBuildActive;
        private static string lastGameMessage;
        private static string lastSpokenGameMessage;
        private static DateTime lastSpokenGameMessageAt = DateTime.MinValue;

        private static int Main(string[] args)
        {
            bool createdNew;
            singleInstanceMutex = new Mutex(true, @"Global\Wither3AccessScreenReaderBridge", out createdNew);
            if (!createdNew)
            {
                return 0;
            }

            string projectRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
            string logPath = Path.Combine(GetAccessDataDir(), "logs", "screenreader-bridge.log");
            string dllPath = Path.Combine(projectRoot, "vendor", "nvdaControllerClient64.dll");

            ConfigureDefaults(projectRoot);

            foreach (string arg in args)
            {
                if (arg.StartsWith("--watch=", StringComparison.OrdinalIgnoreCase))
                {
                    WatchFiles.Add(Environment.ExpandEnvironmentVariables(arg.Substring("--watch=".Length).Trim('"')));
                }
                else if (arg.StartsWith("--state=", StringComparison.OrdinalIgnoreCase))
                {
                    StateFiles.Add(Environment.ExpandEnvironmentVariables(arg.Substring("--state=".Length).Trim('"')));
                }
                else if (arg.StartsWith("--log=", StringComparison.OrdinalIgnoreCase))
                {
                    logPath = Environment.ExpandEnvironmentVariables(arg.Substring("--log=".Length).Trim('"'));
                }
                else if (arg.StartsWith("--dll=", StringComparison.OrdinalIgnoreCase))
                {
                    dllPath = Environment.ExpandEnvironmentVariables(arg.Substring("--dll=".Length).Trim('"'));
                }
                else if (arg.Equals("--stay-open", StringComparison.OrdinalIgnoreCase))
                {
                    exitWhenGameCloses = false;
                }
                else if (arg.Equals("--no-hotkeys", StringComparison.OrdinalIgnoreCase))
                {
                    hotkeysEnabled = false;
                }
            }

            logger = new Logger(logPath);
            try
            {
                logger.Write("Screen reader bridge starting.");
                foreach (string file in WatchFiles)
                {
                    logger.Write("Watching: " + file);
                    EnsureFile(file);
                }
                foreach (string file in StateFiles)
                {
                    logger.Write("Watching state: " + file);
                }

                speaker = new Speaker(dllPath, logger);
                RunLoop();
                return 0;
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Write("Fatal: " + ex);
                }
                return 1;
            }
            finally
            {
                if (speaker != null)
                {
                    speaker.Dispose();
                }
                if (singleInstanceMutex != null)
                {
                    singleInstanceMutex.ReleaseMutex();
                    singleInstanceMutex.Dispose();
                }
            }
        }

        private static void ConfigureDefaults(string projectRoot)
        {
            WatchFiles.Add(Path.Combine(projectRoot, "runtime", "speech.queue.log"));
            WatchFiles.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "The Witcher 3",
                "Wither3Access",
                "speech.queue.log"
            ));
            WatchFiles.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "The Witcher 3",
                "scriptlog.txt"
            ));
            WatchFiles.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "The Witcher 3",
                "scriptslog.txt"
            ));
            StateFiles.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "The Witcher 3",
                "Wither3AccessSpeech.ini"
            ));
            StateFiles.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "The Witcher 3",
                "Wither3Access",
                "speech.ini"
            ));
        }

        private static void RunLoop()
        {
            Dictionary<string, long> offsets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> stateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            DateTime lastGameCheck = DateTime.MinValue;

            foreach (string file in WatchFiles)
            {
                offsets[file] = GetFileLength(file);
            }
            foreach (string file in StateFiles)
            {
                StateEvent existing = ReadStateEvent(file);
                if (existing != null)
                {
                    stateValues[file] = existing.Fingerprint;
                }
            }

            while (true)
            {
                if (exitWhenGameCloses && (DateTime.Now - lastGameCheck).TotalSeconds >= 1)
                {
                    lastGameCheck = DateTime.Now;
                    bool gameRunning = IsGameRunning();
                    if (gameRunning)
                    {
                        gameWasRunning = true;
                    }
                    else if (gameWasRunning)
                    {
                        logger.Write("Game closed. Bridge closing.");
                        return;
                    }
                }

                HandleMenuNavigationKeys();
                foreach (string file in WatchFiles)
                {
                    ReadNewLines(file, offsets);
                }
                foreach (string file in StateFiles)
                {
                    ReadStateFile(file, stateValues);
                }
                if (hotkeysEnabled && HandleHotkeys())
                {
                    return;
                }

                Thread.Sleep(15);
            }
        }

        private static void ReadStateFile(string file, Dictionary<string, string> stateValues)
        {
            if (!File.Exists(file))
            {
                return;
            }

            StateEvent stateEvent = ReadStateEvent(file);
            if (stateEvent == null || string.IsNullOrEmpty(stateEvent.Message))
            {
                return;
            }

            string previous;
            if (stateValues.TryGetValue(file, out previous) && previous == stateEvent.Fingerprint)
            {
                return;
            }

            stateValues[file] = stateEvent.Fingerprint;
            logger.Write("Speak state: " + stateEvent.Message);
            SpeakGameMessage(stateEvent.Message);
        }

        private static void ReadNewLines(string file, Dictionary<string, long> offsets)
        {
            if (!EnsureFile(file))
            {
                return;
            }

            long offset = offsets.ContainsKey(file) ? offsets[file] : 0;
            FileInfo info = new FileInfo(file);
            if (info.Length < offset)
            {
                offset = 0;
            }
            if (info.Length == offset)
            {
                offsets[file] = offset;
                return;
            }

            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        HandleLine(line);
                    }
                    offsets[file] = stream.Position;
                }
            }
        }

        private static void HandleLine(string line)
        {
            string menuCommand = ExtractCommand(line, MenuPrefix + "|");
            if (!string.IsNullOrEmpty(menuCommand))
            {
                HandleMenuCommand(menuCommand);
                return;
            }

            string message = ExtractMessage(line);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            logger.Write("Speak: " + message);
            SpeakGameMessage(message);
        }

        private static void HandleMenuNavigationKeys()
        {
            bool upDown = IsKeyDown(VkUp) || IsKeyDown(VkW);
            bool downDown = IsKeyDown(VkDown) || IsKeyDown(VkS);

            if (upDown && !menuUpWasDown)
            {
                SetPendingMenuDirection(-1);
            }
            if (downDown && !menuDownWasDown)
            {
                SetPendingMenuDirection(1);
            }

            menuUpWasDown = upDown;
            menuDownWasDown = downDown;
        }

        private static void SetPendingMenuDirection(int direction)
        {
            if (trackedMenuItems.Count == 0)
            {
                return;
            }

            pendingMenuDirection = direction;
            pendingMenuDirectionAt = DateTime.Now;
        }

        private static void HandleMenuCommand(string command)
        {
            string[] parts = command.Split('|');
            if (parts.Length == 0)
            {
                return;
            }

            if (parts[0].Equals("SET", StringComparison.OrdinalIgnoreCase))
            {
                string title = parts.Length > 1 ? NormalizeSpeechText(UnescapeMenuToken(parts[1])) : "";
                SetTrackedMenu(title, new List<string>(), 0);
                for (int index = 2; index < parts.Length; index++)
                {
                    string label = NormalizeSpeechText(UnescapeMenuToken(parts[index]));
                    if (!string.IsNullOrEmpty(label))
                    {
                        trackedMenuItems.Add(label);
                    }
                }

                ResetPendingMenu();
                logger.Write("Menu tracking set: " + trackedMenuItems.Count + " items. Title: " + trackedMenuTitle);
                if (trackedMenuItems.Count > 0)
                {
                    SpeakMenuFocus();
                }
                return;
            }

            if (parts[0].Equals("BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                pendingMenuItems.Clear();
                pendingMenuTitle = parts.Length > 1 ? NormalizeSpeechText(UnescapeMenuToken(parts[1])) : "";
                pendingMenuBuildActive = true;
                return;
            }

            if (parts[0].Equals("ITEM", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingMenuBuildActive && parts.Length > 1)
                {
                    string label = NormalizeSpeechText(UnescapeMenuToken(parts[1]));
                    if (!string.IsNullOrEmpty(label))
                    {
                        pendingMenuItems.Add(label);
                    }
                }
                return;
            }

            if (parts[0].Equals("END", StringComparison.OrdinalIgnoreCase))
            {
                if (!pendingMenuBuildActive)
                {
                    return;
                }

                SetTrackedMenu(pendingMenuTitle, pendingMenuItems, 0);
                pendingMenuItems.Clear();
                ResetPendingMenu();
                logger.Write("Menu tracking set: " + trackedMenuItems.Count + " items. Title: " + trackedMenuTitle);
                if (trackedMenuItems.Count > 0)
                {
                    SpeakMenuFocus();
                }
                return;
            }

            if (parts[0].Equals("HIGHLIGHT", StringComparison.OrdinalIgnoreCase))
            {
                if (trackedMenuItems.Count == 0)
                {
                    return;
                }

                int direction = ResolveMenuDirection();
                if (direction != 0)
                {
                    trackedMenuIndex += direction;
                    if (trackedMenuIndex < 0)
                    {
                        trackedMenuIndex = trackedMenuItems.Count - 1;
                    }
                    else if (trackedMenuIndex >= trackedMenuItems.Count)
                    {
                        trackedMenuIndex = 0;
                    }
                    pendingMenuDirection = 0;
                    SpeakMenuFocus();
                }
                return;
            }

            if (parts[0].Equals("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length > 1)
                {
                    UpdateTrackedMenuItem(NormalizeSpeechText(UnescapeMenuToken(parts[1])));
                }
                return;
            }

            if (parts[0].Equals("CLEAR", StringComparison.OrdinalIgnoreCase))
            {
                ClearTrackedMenu();
                return;
            }

            if (parts[0].Equals("PUSH", StringComparison.OrdinalIgnoreCase))
            {
                PushTrackedMenu();
                return;
            }

            if (parts[0].Equals("POP", StringComparison.OrdinalIgnoreCase))
            {
                PopTrackedMenu();
                return;
            }

            if (parts[0].Equals("RESET", StringComparison.OrdinalIgnoreCase))
            {
                ResetTrackedMenus();
            }
        }

        private static int ResolveMenuDirection()
        {
            int liveDirection = GetLiveMenuDirection();
            if (liveDirection != 0)
            {
                return liveDirection;
            }

            if (pendingMenuDirection != 0 &&
                (DateTime.Now - pendingMenuDirectionAt).TotalMilliseconds <= 1500)
            {
                return pendingMenuDirection;
            }

            return 0;
        }

        private static int GetLiveMenuDirection()
        {
            bool upDown = IsKeyDown(VkUp) || IsKeyDown(VkW);
            bool downDown = IsKeyDown(VkDown) || IsKeyDown(VkS);

            if (upDown && !downDown)
            {
                return -1;
            }
            if (downDown && !upDown)
            {
                return 1;
            }

            return 0;
        }

        private static void SpeakMenuFocus()
        {
            if (trackedMenuItems.Count == 0 ||
                trackedMenuIndex < 0 ||
                trackedMenuIndex >= trackedMenuItems.Count)
            {
                return;
            }

            string message = trackedMenuItems[trackedMenuIndex] + ", " +
                (trackedMenuIndex + 1).ToString() + " z " +
                trackedMenuItems.Count.ToString() + ".";
            if (!string.IsNullOrEmpty(trackedMenuTitle))
            {
                message = trackedMenuTitle + ". " + message;
            }
            logger.Write("Speak menu focus: " + message);
            SpeakGameMessage(message);
        }

        private static void UpdateTrackedMenuItem(string label)
        {
            if (string.IsNullOrEmpty(label) || trackedMenuItems.Count == 0)
            {
                return;
            }

            int index = FindTrackedMenuItemIndex(label);
            if (index < 0 &&
                trackedMenuIndex >= 0 &&
                trackedMenuIndex < trackedMenuItems.Count)
            {
                index = trackedMenuIndex;
            }

            if (index < 0)
            {
                logger.Write("Menu update skipped. No matching item for: " + label);
                return;
            }

            trackedMenuItems[index] = label;
            logger.Write("Menu item updated: " + label + " at " + index.ToString());
        }

        private static int FindTrackedMenuItemIndex(string label)
        {
            string labelKey = GetMenuItemKey(label);
            if (string.IsNullOrEmpty(labelKey))
            {
                return -1;
            }

            for (int index = 0; index < trackedMenuItems.Count; index++)
            {
                if (string.Equals(GetMenuItemKey(trackedMenuItems[index]), labelKey, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string GetMenuItemKey(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return "";
            }

            int separator = label.IndexOf(':');
            string key = separator > 0 ? label.Substring(0, separator) : label;
            return NormalizeSpeechText(key).Trim().TrimEnd('.');
        }

        private static void SetTrackedMenu(string title, IEnumerable<string> items, int index)
        {
            trackedMenuItems.Clear();
            trackedMenuItems.AddRange(items);
            trackedMenuTitle = title ?? "";
            trackedMenuIndex = trackedMenuItems.Count == 0 ? 0 : Math.Max(0, Math.Min(index, trackedMenuItems.Count - 1));
            pendingMenuDirection = 0;
        }

        private static void PushTrackedMenu()
        {
            if (trackedMenuItems.Count == 0)
            {
                logger.Write("Menu push skipped. No tracked menu.");
                return;
            }

            menuStack.Push(new MenuState(trackedMenuTitle, trackedMenuItems, trackedMenuIndex));
            logger.Write("Menu pushed: " + trackedMenuTitle + " (" + trackedMenuItems.Count + " items).");
        }

        private static void PopTrackedMenu()
        {
            if ((DateTime.Now - lastMenuPopAt).TotalMilliseconds < 120)
            {
                logger.Write("Menu pop skipped as duplicate.");
                return;
            }
            lastMenuPopAt = DateTime.Now;

            if (menuStack.Count == 0)
            {
                logger.Write("Menu pop skipped. Stack empty.");
                return;
            }

            MenuState state = menuStack.Pop();
            SetTrackedMenu(state.Title, state.Items, state.Index);
            ResetPendingMenu();
            logger.Write("Menu restored: " + trackedMenuTitle + " (" + trackedMenuItems.Count + " items).");
            SpeakMenuFocus();
        }

        private static void ClearTrackedMenu()
        {
            if (trackedMenuItems.Count > 0)
            {
                logger.Write("Menu tracking cleared.");
            }
            trackedMenuItems.Clear();
            pendingMenuItems.Clear();
            trackedMenuIndex = 0;
            pendingMenuDirection = 0;
            trackedMenuTitle = "";
            ResetPendingMenu();
        }

        private static void ResetTrackedMenus()
        {
            menuStack.Clear();
            ClearTrackedMenu();
            logger.Write("Menu tracking reset.");
        }

        private static void ResetPendingMenu()
        {
            pendingMenuTitle = "";
            pendingMenuBuildActive = false;
        }

        private static bool HandleHotkeys()
        {
            bool f9Down = IsKeyDown(VkF9);
            bool f10Down = IsKeyDown(VkF10);
            bool f12Down = IsKeyDown(VkF12);
            bool closeDown = f12Down && IsKeyDown(VkControl) && IsKeyDown(VkShift);

            if (closeDown && !closeWasDown)
            {
                SpeakHotkeyMessage("Zamykam bridge Wither3.access.");
                logger.Write("Bridge closed by Ctrl+Shift+F12.");
                return true;
            }
            if (f12Down && !f12WasDown && !closeDown)
            {
                string status = IsGameRunning()
                    ? "Wither3.access aktywny. Gra wykryta. Odczyt realnego menu jest wlaczony."
                    : "Wither3.access aktywny. Czekam na uruchomienie gry.";
                if (!string.IsNullOrEmpty(lastGameMessage))
                {
                    status += " Ostatni komunikat: " + lastGameMessage;
                }
                SpeakHotkeyMessage(status);
            }
            if (f9Down && !f9WasDown)
            {
                if (!string.IsNullOrEmpty(lastGameMessage))
                {
                    SpeakHotkeyMessage(lastGameMessage);
                }
                else
                {
                    SpeakHotkeyMessage("Brak ostatniego komunikatu z gry.");
                }
            }
            if (f10Down && !f10WasDown)
            {
                SpeakHotkeyMessage("Skroty Wither3.access. F12 status. F9 powtorz ostatni komunikat z gry. F10 pomoc. Control Shift F12 zamknij bridge.");
            }

            closeWasDown = closeDown;
            f12WasDown = f12Down;
            f9WasDown = f9Down;
            f10WasDown = f10Down;
            return false;
        }

        private static void SpeakGameMessage(string message)
        {
            message = NormalizeSpeechText(message);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            if (message == lastSpokenGameMessage &&
                (DateTime.Now - lastSpokenGameMessageAt).TotalMilliseconds < 350)
            {
                logger.Write("Speak skipped duplicate: " + message);
                return;
            }

            lastSpokenGameMessage = message;
            lastSpokenGameMessageAt = DateTime.Now;
            lastGameMessage = message;
            speaker.Speak(message);
        }

        private static void SpeakHotkeyMessage(string message)
        {
            logger.Write("Speak hotkey: " + message);
            speaker.Speak(message);
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
        }

        private static string ExtractMessage(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            if (line.IndexOf(Prefix + "_DEBUG", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }
            if (line.IndexOf(MenuPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            int marker = line.LastIndexOf(Prefix + "|", StringComparison.OrdinalIgnoreCase);
            int skip = Prefix.Length + 1;
            if (marker < 0)
            {
                marker = line.LastIndexOf(Prefix + ":", StringComparison.OrdinalIgnoreCase);
            }
            if (marker < 0)
            {
                marker = line.LastIndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
                skip = Prefix.Length;
            }

            if (marker < 0)
            {
                return null;
            }

            string text = line.Substring(marker + skip).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static string ExtractCommand(string line, string markerText)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            int marker = line.LastIndexOf(markerText, StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                return null;
            }

            return line.Substring(marker + markerText.Length).Trim();
        }

        private static string UnescapeMenuToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Replace("%7C", "|").Replace("%7c", "|").Replace("%25", "%");
        }

        private static string NormalizeSpeechText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            string text = StripAngleTags(value)
                .Replace("&nbsp;", " ")
                .Replace("&amp;", " i ")
                .Replace("&lt;", " ")
                .Replace("&gt;", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");

            return RestorePolishSpeechText(CollapseWhitespace(text).Trim());
        }

        private static string RestorePolishSpeechText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value
                .Replace("Menu glowne", "Menu główne")
                .Replace("Ustawienia dzwieku", "Ustawienia dźwięku")
                .Replace("Wszystkie dzwieki", "Wszystkie dźwięki")
                .Replace("dzwieku", "dźwięku")
                .Replace("dzwieki", "dźwięki")
                .Replace("Wczytaj gre", "Wczytaj grę")
                .Replace("Wznow gre", "Wznów grę")
                .Replace("Zamknij gre", "Zamknij grę")
                .Replace("gre.", "grę.")
                .Replace("Od poczatku", "Od początku")
                .Replace("poczatku", "początku")
                .Replace("Po prostu opowiesc", "Po prostu opowieść")
                .Replace("Miecz i opowiesc", "Miecz i opowieść")
                .Replace("opowiesc", "opowieść")
                .Replace("Krew, pot i lzy", "Krew, pot i łzy")
                .Replace("Droga ku zagladzie", "Droga ku zagładzie")
                .Replace("Wybor jezyka", "Wybór języka")
                .Replace("Jezyk napisow", "Język napisów")
                .Replace("Jezyk dialogow", "Język dialogów")
                .Replace("jezyka", "języka")
                .Replace("napisow", "napisów")
                .Replace("dialogow", "dialogów")
                .Replace("Poziom trudnosci", "Poziom trudności")
                .Replace("trudnosci", "trudności")
                .Replace("Czulosc", "Czułość")
                .Replace("czulosc", "czułość")
                .Replace("Odwrocenie", "Odwrócenie")
                .Replace("odwrocenie", "odwrócenie")
                .Replace("wplywa", "wpływa")
                .Replace("wcisnieciu", "wciśnięciu")
                .Replace("drazka", "drążka")
                .Replace("Intensywnosc", "Intensywność")
                .Replace("intensywnosc", "intensywność")
                .Replace("efektow", "efektów")
                .Replace("Zamien", "Zamień")
                .Replace("wlaczone", "włączone")
                .Replace("wylaczone", "wyłączone")
                .Replace("Skroty", "Skróty")
                .Replace("wlaczony", "włączony")
                .Replace("powtorz", "powtórz");
        }

        private static string StripAngleTags(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            bool inTag = false;

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current == '<')
                {
                    inTag = true;
                    builder.Append(' ');
                    continue;
                }
                if (current == '>')
                {
                    inTag = false;
                    builder.Append(' ');
                    continue;
                }
                if (!inTag)
                {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }

        private static string CollapseWhitespace(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            bool lastWasWhitespace = false;

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (char.IsWhiteSpace(current))
                {
                    if (!lastWasWhitespace)
                    {
                        builder.Append(' ');
                    }
                    lastWasWhitespace = true;
                    continue;
                }

                builder.Append(current);
                lastWasWhitespace = false;
            }

            return builder.ToString();
        }

        private static StateEvent ReadStateEvent(string file)
        {
            if (!File.Exists(file))
            {
                return null;
            }

            string text;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    text = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                logger.Write("State read failed for " + file + ": " + ex.Message);
                return null;
            }

            return ExtractStateEvent(text);
        }

        private static StateEvent ExtractStateEvent(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string last = null;
            string stamp = null;
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                string line = lines[index].Trim();
                if (line.Length == 0 || line.StartsWith("[", StringComparison.Ordinal))
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals >= 0)
                {
                    string key = line.Substring(0, equals).Trim();
                    if (key.Equals("last", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("speech", StringComparison.OrdinalIgnoreCase))
                    {
                        last = Unquote(line.Substring(equals + 1).Trim());
                        if (!string.IsNullOrEmpty(stamp))
                        {
                            break;
                        }
                        continue;
                    }
                    if (key.Equals("stamp", StringComparison.OrdinalIgnoreCase))
                    {
                        stamp = Unquote(line.Substring(equals + 1).Trim());
                        if (!string.IsNullOrEmpty(last))
                        {
                            break;
                        }
                        continue;
                    }
                }

                string message = ExtractMessage(line);
                if (!string.IsNullOrEmpty(message))
                {
                    return new StateEvent(message, message);
                }
            }

            if (string.IsNullOrEmpty(last))
            {
                return null;
            }

            return new StateEvent(last, (stamp ?? "") + "|" + last);
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }

        private static bool IsGameRunning()
        {
            Process[] processes = null;
            try
            {
                processes = Process.GetProcessesByName("witcher3");
                return processes.Length > 0;
            }
            catch
            {
                return false;
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

        private static long GetFileLength(string file)
        {
            if (!EnsureFile(file))
            {
                return 0;
            }
            return new FileInfo(file).Length;
        }

        private static bool EnsureFile(string file)
        {
            try
            {
                string directory = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                if (!File.Exists(file))
                {
                    File.WriteAllText(file, "", Encoding.UTF8);
                }
                return true;
            }
            catch (Exception ex)
            {
                if (UnavailableFiles.Add(file) && logger != null)
                {
                    logger.Write("Watch file unavailable: " + file + " (" + ex.Message + ")");
                }
                return false;
            }
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

        private sealed class Logger
        {
            private readonly string path;
            private readonly object sync = new object();

            public Logger(string path)
            {
                this.path = path;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                WriteFile("", FileMode.Create);
            }

            public void Write(string line)
            {
                lock (sync)
                {
                    WriteFile(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + line + Environment.NewLine, FileMode.Append);
                }
            }

            private void WriteFile(string text, FileMode mode)
            {
                using (FileStream stream = new FileStream(path, mode, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(text);
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
                        if (nvdaSpeak(text) == 0)
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Write("NVDA speak failed: " + ex.Message);
                    }
                }

                if (sapiVoice != null && sapiType != null)
                {
                    try
                    {
                        sapiType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, sapiVoice, new object[] { text, 3 });
                    }
                    catch (Exception ex)
                    {
                        logger.Write("SAPI speak failed: " + ex.Message);
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
                    if (sapiType != null)
                    {
                        sapiVoice = Activator.CreateInstance(sapiType);
                    }
                }
                catch (Exception ex)
                {
                    logger.Write("SAPI load failed: " + ex.Message);
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

                if (nvdaTest != null && nvdaSpeak != null)
                {
                    nvdaReady = nvdaTest() == 0;
                }
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

        private sealed class StateEvent
        {
            public readonly string Message;
            public readonly string Fingerprint;

            public StateEvent(string message, string fingerprint)
            {
                Message = message;
                Fingerprint = fingerprint;
            }
        }

        private sealed class MenuState
        {
            public readonly string Title;
            public readonly List<string> Items;
            public readonly int Index;

            public MenuState(string title, IEnumerable<string> items, int index)
            {
                Title = title ?? "";
                Items = new List<string>(items);
                Index = index;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvdaTest();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate int NvdaSpeak([MarshalAs(UnmanagedType.LPWStr)] string text);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvdaCancel();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
