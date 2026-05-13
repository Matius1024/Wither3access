# Research notes: dostępność The Witcher 3

Data: 2026-05-10

## REDkit i modowanie Wiedźmina 3

Oficjalna strona REDkit opisuje narzędzie jako możliwość tworzenia modów PC i edycji prawie całej gry. Dokumentacja CDPR mówi też, że REDkit zawiera m.in. źródła Flash dla elementów UI, np. Gwinta. To ważne, bo nasz pierwszy cel dotyczy menu i podmenu.

Źródła:

- CDPR REDkit: https://www.thewitcher.com/gb/en/redkit/modding
- REDkit WitcherScript: https://cdprojektred.atlassian.net/wiki/spaces/W3REDkit/pages/36306960/WitcherScript
- REDkit Managing mods: https://cdprojektred.atlassian.net/wiki/spaces/W3REDkit/pages/36339714/Managing%2Bmods
- REDkit Video tutorials, UI modding: https://cdprojektred.atlassian.net/wiki/spaces/W3REDkit/pages/28737537/Video%2Btutorials

Wnioski:

- WitcherScript `.ws` jest głównym językiem skryptowym gry.
- Skrypty bazowe są w `content/content0/scripts`.
- Mody ładowane są z katalogu `mods`, a folder moda musi zaczynać się od `mod`.
- REDkit obsługuje workflow workspace/publish, więc powinniśmy trzymać projekt tak, żeby dało się później opublikować paczkę moda.
- UI modding jest oficjalnie wspierany, ale trzeba sprawdzić praktycznie, które ekrany menu są w Flash/Scaleform, a które przez WitcherScript.

## Wzorce z innych modów dostępnościowych

### Hades II

Hades II ma mod `Blind Accessibility` oraz osobny `TOLk Compatibility`. Ten drugi integruje grę z Tolk i czyta tekst przy najechaniu/fokusie przycisku, a także zamienia ikony w opisach na tekst. Mod główny dodaje przebudowę wielu menu, teleportację/nawigację do drzwi, nagród i punktów, oraz odczyt opisów kart, ekwipunku i innych ekranów.

Źródła:

- Hades II Blind Accessibility: https://thunderstore.io/c/hades-ii/p/Lirin/Blind_Accessibility/
- Hades2BlindAccessibility GitHub: https://github.com/Lirin111/Hades2BlindAccessibility
- Hades II TOLk Compatibility: https://new.thunderstore.io/c/hades-ii/p/Lirin/Hades_2_TOLk_Compatibility/
- Can I Play That o modach Hades II: https://caniplaythat.com/2025/11/06/blind-accessibility-mods-released-for-hades-ii/

Wniosek dla nas: rozdzielić adapter screen readera od logiki dostępnościowej. Osobny bridge do NVDA/Tolk ułatwi testowanie i nie będzie wymagał patchowania gry.

### Stardew Valley

`Stardew Access` jest dojrzałym modem dla osób niewidomych. Czyta menu, dialogi, kafelki, skrzynie, zdrowie, pieniądze i czas. Ma tile viewer, object tracker, grid movement, skróty do informacji i powtarzania ostatniej wypowiedzi. Na Windows używa Tolk.

Źródła:

- Stardew Access GitHub: https://github.com/stardew-access/stardew-access
- Stardew Access keybindings: https://raw.githubusercontent.com/stardew-access/stardew-access/development/docs/keybindings.md
- Stardew Access commands: https://raw.githubusercontent.com/stardew-access/stardew-access/development/docs/commands.md
- Stardew Access Nexus: https://www.nexusmods.com/stardewvalley/mods/16205/

Wniosek dla nas: już na etapie menu warto dodać komendy typu `powtórz ostatni tekst`, `czytaj aktualny ekran`, `czytaj aktywną opcję`, `czytaj wartość opcji`.

### Minecraft Access

Minecraft Access używa screen readera do narracji UI oraz dźwięków orientacyjnych dla świata 3D. To dobry wzorzec na później, po etapie menu.

Źródło:

- CurseForge Minecraft Access: https://www.curseforge.com/minecraft/mc-mods/blind-accessibility

Wniosek dla nas: po menu naturalnym drugim etapem będą dźwięki orientacyjne i tracker obiektów/interakcji.

### Factorio Access

Factorio Access pokazuje ważny wzorzec launcherowy: mod wymaga własnego launchera do screen readera i obsługi menu głównego. Nie jest to obejście DRM, tylko dostępnościowa warstwa startowa i konfiguracja modów.

Źródła:

- Factorio Access mod portal: https://mods.factorio.com/mod/FactorioAccess
- Factorio Access GitHub: https://github.com/Factorio-Access/FactorioAccess

Wniosek dla nas: własny mały launcher/helper ma sens, jeśli REDlauncher albo ekran startowy są niedostępne. Powinien tylko uruchamiać legalnie zainstalowaną grę i bridge mowy.

### Tolk

Tolk jest biblioteką abstrakcji czytników ekranu dla Windows. Obsługuje m.in. NVDA, JAWS i SAPI oraz ma bindingi dla C/C++, .NET, Pythona i innych języków.

Źródło:

- Tolk GitHub: https://github.com/dkager/tolk

Wniosek dla nas: dla Windows najprostszy bridge można napisać w C#/.NET albo Pythonie i wołać Tolk/NVDA Controller. Fallback do SAPI pozwoli testować bez NVDA.

## Mody do The Witcher 3, które mogą pomóc

### Friendly HUD

Friendly HUD dla Next Gen dodaje konfigurację modułów HUD, znaczniki 3D dla questów/NPC i ulepszenia menu. To nie jest mod dla screen readera, ale pokazuje, że istniejące modyfikacje UI/HUD i skryptów są realne.

Źródło:

- Friendly HUD Next Gen: https://www.nexusmods.com/witcher3/mods/7290

### All Quest Objectives On Map

Pokazuje wszystkie aktywne cele zadań na mapie i pozwala przełączać śledzony cel bez wchodzenia do menu zadań. Później może posłużyć jako wzorzec do dostępnego trackera celów.

Źródło:

- All Quest Objectives On Map: https://www.nexusmods.com/witcher3/mods/943

### Bigger Subtitles / Big Fonts / Colored Subtitles

Te mody są bardziej dla słabowidzących niż niewidomych, ale potwierdzają, że tekst, subtitle UI i style można modyfikować. Dla nas ważniejsza jest metoda niż gotowy efekt.

Źródła:

- Bigger Subtitles: https://www.nexusmods.com/witcher3/mods/5716
- Colored Subtitles Next Gen: https://www.nexusmods.com/witcher3/mods/7523

### Hotkeys To Toggle NPC Chatter Voices Music Subtitles

Pokazuje podejście z dodatkowymi hotkeyami i konfiguracją w opcjach/key bindings.

Źródło:

- Hotkeys To Toggle NPC Chatter Voices Music Subtitles: https://www.nexusmods.com/witcher3/mods/3262

## Pominięcie REDlaunchera

Najczęściej opisywana metoda to parametr `--launcher-skip` w opcjach uruchamiania Steam/GOG/Epic. Druga ścieżka to bezpośrednie uruchamianie:

- DX11: `bin/x64/witcher3.exe`
- DX12: `bin/x64_dx12/witcher3.exe`

Źródła:

- PCGaming/Steam-community style guide: https://gameplay.tips/guides/the-witcher-3-wild-hunt-how-to-skip-launcher-the-witcher-3-next-gen.html
- Steam discussion, bezpośrednie exe DX11/DX12: https://steamcommunity.com/app/292030/discussions/1/597388908982993906/
- Arqade o tym, że skip launchera jest per-gra, nie uniwersalny: https://gaming.stackexchange.com/questions/417726/is-the-steam-launch-argument-skip-launcher-guaranteed-to-work-on-every-game

Wniosek: nie trzeba i nie należy modyfikować DRM ani binarek. Wystarczy dokumentować `--launcher-skip` oraz przygotować helper startowy, który uruchomi prawidłowy exe po wskazaniu katalogu gry.

## Najważniejsze ryzyka

- REDengine UI może nie mieć bezpośredniego dostępu do DLL/Tolk, więc bridge prawdopodobnie musi być zewnętrzny.
- Trzeba znaleźć stabilne zdarzenia fokusu w menu. Jeśli ich nie ma, trzeba je dodać w skryptach/UI.
- Czytanie przez log/tail jest dobrym prototypem, ale może mieć opóźnienia i szum.
- Różnice między Classic 1.32 i Next Gen 4.04 mogą wymagać osobnych buildów.
- Konflikty skryptów z popularnymi modami trzeba obsługiwać przez Script Merger albo ograniczyć MVP do czystej instalacji.
