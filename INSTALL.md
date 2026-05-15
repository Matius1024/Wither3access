# Instalacja Wither3.access 0.3.alfa z GitHuba

Ten projekt jest paczka moda dostepnosciowego do The Witcher 3: Wild Hunt na
Windows. Najprostsza instalacja polega na pobraniu ZIP-a z katalogu `dist`,
rozpakowaniu go i uruchomieniu skryptu instalacyjnego.

## Co pobrac

Z repozytorium GitHub pobierz:

```text
dist/Wither3.access-0.3.alfa.zip
```

Rozpakuj ZIP do dowolnego tymczasowego folderu, na przyklad:

```text
C:\Users\<twoj_uzytkownik>\Downloads\Wither3.access-0.3.alfa
```

Po rozpakowaniu w folderze powinny byc miedzy innymi:

```text
Witcher3AccessibleLauncher.exe
Wither3Access\
mods\modWither3Access\
tools\install-release.ps1
README.md
INSTALL.md
```

## Instalacja automatyczna

Uruchom PowerShell w rozpakowanym folderze paczki i wykonaj:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\install-release.ps1 -GameDir "C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt"
```

Jesli gra jest w innym miejscu, podaj w `-GameDir` folder gry, czyli folder,
w ktorym istnieje `bin\x64_dx12\witcher3.exe` albo `bin\x64\witcher3.exe`.

Skrypt kopiuje pliki do:

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

Przy instalacji do `Program Files` moze byc potrzebny PowerShell uruchomiony
jako administrator.

## Instalacja reczna

Z rozpakowanej paczki skopiuj:

- `Witcher3AccessibleLauncher.exe` do glownego folderu gry.
- `Wither3Access` do glownego folderu gry.
- `mods\modWither3Access` do folderu `mods` w folderze gry.

Docelowy uklad ma wygladac tak:

```text
The Witcher 3 Wild Hunt\
  Witcher3AccessibleLauncher.exe
  Wither3Access\
  mods\
    modWither3Access\
```

## Uruchamianie

Po instalacji uruchom z folderu gry:

```text
Witcher3AccessibleLauncher.exe
```

Launcher startuje bridge mowy i uruchamia gre bez RED/GOG launchera. Domyslnie
uzywa DX12. DX11 wlaczysz tak:

```powershell
.\Witcher3AccessibleLauncher.exe dx11
```

NVDA jest zalecane. Jesli NVDA nie dziala, bridge sprobuje mowic przez SAPI.
