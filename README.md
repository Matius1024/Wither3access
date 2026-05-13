# Wither3.access 0.4.alfa

Wither3.access to prototyp bezpiecznego moda dostepnosciowego do
The Witcher 3: Wild Hunt na PC. Celem tej wersji jest uruchomienie gry
bez RED/GOG launchera oraz odczyt prawdziwego menu i opcji gry przez
NVDA albo, awaryjnie, przez SAPI.

Mod nie patchuje plikow DRM i nie zmienia binarek gry. Czesc w grze jest
zwyklym script modem w folderze `mods`, a zewnetrzny bridge tylko odbiera
tekst z gry i przekazuje go do czytnika ekranu.

## Co jest w wersji 0.4.alfa

- `Witcher3AccessibleLauncher.exe` - launcher do wklejenia bezposrednio do
  folderu gry. Pomija RED/GOG launcher i uruchamia `witcher3.exe`.
- `Wither3Access\Witcher3ScreenReaderBridge.exe` - pasywny bridge mowy.
- `Wither3Access\Witcher3MenuCompanion.exe` - tymczasowy companion MVP,
  zachowany tylko awaryjnie. Launcher nie uruchamia go domyslnie.
- `Wither3Access\config\*.json` - konfiguracja awaryjnego companiona.
- `Wither3Access\vendor\nvdaControllerClient64.dll` - lokalna biblioteka
  NVDA Controller, jesli jest dolaczona do paczki.
- `mods\modWither3Access` - script mod WitcherScript ladowany przez gre.
- `tools\install-release.ps1` - skrypt instalacyjny dla tej paczki.
- `INSTALL.md` - krotka instrukcja pobrania ZIP-a z GitHuba, rozpakowania
  i skopiowania plikow do folderu gry.

Nowe w 0.4.alfa:

- szybszy odczyt podswietlenia menu po strzalkach gora/dol;
- stabilniejsze sledzenie ruchu przy przytrzymaniu klawisza;
- pierwsze wysylanie realnej listy elementow panelu `Opcje`;
- proba wykrywania listy aktualnego podmenu opcji przez `id`/`tag`;
- aktualne wartosci suwakow po powrocie na dana opcje;
- pelne nazwy opcji, podmenu i poziomow trudnosci;
- przywracanie polskich znakow w odczycie bridge'a;
- synchronizacja odczytu z rzeczywista pozycja kursora po `Esc` z podmenu opcji;
- dodatkowe logi diagnostyczne `SubmenuRequest` i `SubmenuNotFound`.

## Wymagania

- The Witcher 3: Wild Hunt na Windows PC.
- Gra musi miec folder z `bin\x64_dx12\witcher3.exe` albo
  `bin\x64\witcher3.exe`.
- NVDA jest zalecane. Bez NVDA bridge sprobuje uzyc SAPI, czyli systemowej
  syntezy mowy Windows.
- Przy instalacji do `Program Files` moze byc potrzebne uruchomienie
  PowerShella albo kopiowania jako administrator.

Domyslna sciezka GOG uzywana w tym projekcie:

```text
C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt
```

## Instalacja automatyczna

Z GitHuba pobierz paczke:

```text
dist\Wither3.access-0.4.alfa.zip
```

Rozpakuj ja do dowolnego folderu, np.:

```text
C:\Users\<twoj_uzytkownik>\Downloads\Wither3.access-0.4.alfa
```

Z rozpakowanego folderu projektu albo paczki uruchom:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\install-release.ps1 -GameDir "C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt"
```

Skrypt tworzy foldery, jesli ich nie ma, i kopiuje pliki w te miejsca:

```text
The Witcher 3 Wild Hunt\
  Witcher3AccessibleLauncher.exe
  Wither3Access\
    Witcher3ScreenReaderBridge.exe
    Witcher3MenuCompanion.exe
    README.Wither3.access.md
    config\
      menus.en.json
      menus.pl.json
      settings.json
    vendor\
      nvdaControllerClient64.dll
  mods\
    modWither3Access\
      content\
        scripts\
          game\
            accessibility\
              w3accessSpeech.ws
            gui\
              main_menu\
                ingameMenu.ws
              menus\
                menuBase.ws
```

## Instalacja reczna

1. Otworz folder gry, np.:

```text
C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt
```

2. Jesli nie istnieje folder `mods`, utworz go:

```text
The Witcher 3 Wild Hunt\mods
```

3. Skopiuj folder:

```text
mods\modWither3Access
```

do:

```text
The Witcher 3 Wild Hunt\mods\modWither3Access
```

4. Skopiuj folder:

```text
Wither3Access
```

do:

```text
The Witcher 3 Wild Hunt\Wither3Access
```

5. Skopiuj plik:

```text
Witcher3AccessibleLauncher.exe
```

bezposrednio do folderu gry:

```text
The Witcher 3 Wild Hunt\Witcher3AccessibleLauncher.exe
```

## Uruchamianie

Uruchom z folderu gry:

```text
Witcher3AccessibleLauncher.exe
```

Domyslnie startuje wersja DX12:

```text
bin\x64_dx12\witcher3.exe
```

Wersje DX11 wlaczysz argumentem:

```powershell
.\Witcher3AccessibleLauncher.exe dx11
```

Jesli launcher nie lezy w folderze gry, mozesz wskazac sciezke recznie:

```powershell
.\Witcher3AccessibleLauncher.exe --game-dir="C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt"
```

Launcher uruchamia:

- `Wither3Access\Witcher3ScreenReaderBridge.exe`
- bezposrednio `witcher3.exe -net -debugscripts`, z pominieciem RED/GOG launchera

Parametry `-net -debugscripts` sa potrzebne, bo WitcherScript zapisuje komunikaty
`LogChannel('W3ACCESS', ...)` do logu skryptow:

```text
%USERPROFILE%\Documents\The Witcher 3\scriptlog.txt
```

Bridge nasluchuje tego pliku i czyta linie `W3ACCESS`. Jesli z jakiegos powodu
trzeba uruchomic gre bez logowania skryptow, mozna dodac `--no-debugscripts`,
ale wtedy odczyt realnego menu przez ten kanal nie bedzie dzialal.

Companion mozna uruchomic recznie tylko do testow porownawczych:

```powershell
.\Witcher3AccessibleLauncher.exe --with-companion
```

Samego script moda nie uruchamia sie osobno. Gra laduje go automatycznie z:

```text
The Witcher 3 Wild Hunt\mods\modWither3Access
```

## Jak dziala odczyt menu

Script mod hookuje prawdziwe eventy UI gry:

- `CR4IngameMenu.OnConfigUI` - wykrycie gotowego menu po intro, bez wciskania `F12`.
- `CR4IngameMenu.OnItemActivated` - prawdziwe wejscie w pozycje menu.
- `CR4IngameMenu.PopulateMenuData` - realna lista pozycji menu wysylana do bridge'a.
- `CR4MenuBase.OnPlaySoundEvent("gui_global_highlight")` - realna zmiana podswietlenia pozycji.
- `CR4MenuBase.OnModuleSelected` - prawdziwa zmiana zaznaczonego elementu menu.
- `CR4IngameMenu.OnOptionSelectionChanged` - prawdziwy fokus opcji w ustawieniach.
- `CR4IngameMenu.OnOptionValueChanged` - prawdziwa zmiana wartosci opcji, np. suwaka glosnosci.

Po wejsciu gry do menu bridge powinien automatycznie powiedziec:

```text
Wither3.access. Menu glowne wykryte.
```

Po nacisnieciu strzalki w gore albo w dol bridge czeka na sygnal podswietlenia
z gry i czyta aktualna pozycje, np. `Opcje, 4 z 8`.

Gra wysyla tekst przez `W3Access_Speak(...)`. Bridge odbiera komunikaty i mowi
przez NVDA Controller, a jesli NVDA nie dziala, przez SAPI. Bridge nie klika,
nie naciska klawiszy i nie steruje menu.

Bridge ma pasywne skroty mowy, przeniesione z companiona bez sterowania gra:

- `F12` - status Wither3.access i ostatni komunikat z gry.
- `F9` - powtorz ostatni komunikat z gry.
- `F10` - pomoc skrotow.
- `Ctrl+Shift+F12` - zamknij bridge.

Logi sa zapisywane w:

```text
%USERPROFILE%\Documents\The Witcher 3\Wither3Access\logs
```

## Budowanie z kodu

Przebudowanie plikow `.exe`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-accessibility.ps1
```

Odnowienie script moda z lokalnych skryptow gry:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-script-mod.ps1
```

Odnowienie i instalacja script moda do folderu gry:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-script-mod.ps1 -Deploy
```

## Ograniczenia 0.4.alfa

- To jest pierwsza realna warstwa UI, a nie kompletna dostepnosc calej gry.
- Menu glowne i pierwsza warstwa opcji korzystaja z prawdziwych eventow gry,
  ale glebsze podmenu opcji moga jeszcze wymagac mapowania konkretnych `id`.
- Inne ekrany, ekwipunek, mapa, dialogi i walka beda wymagaly kolejnych hookow.
- Jesli inny mod nadpisuje `menuBase.ws` albo `ingameMenu.ws`, moze byc potrzebny
  Script Merger.
- Po skopiowaniu moda gra musi zostac uruchomiona ponownie, aby wczytac skrypty.

## Paczka wydania

Paczka wydania dla tej wersji jest trzymana w repozytorium jako:

```text
dist\Wither3.access-0.4.alfa.zip
```

Zawiera gotowy uklad do rozpakowania:

```text
Witcher3AccessibleLauncher.exe
Wither3Access\
mods\modWither3Access\
tools\install-release.ps1
README.md
INSTALL.md
VERSION
```

Paczke mozna odnowic z aktualnych plikow repo poleceniem:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release-package.ps1
```

Po rozpakowaniu szczegolowe kroki sa w `INSTALL.md`.
