# MVP plan: dostępne menu The Witcher 3

## Zakres pierwszego prototypu

Cel MVP: po uruchomieniu gry gracz słyszy aktualny ekran menu, aktywną pozycję i zmianę fokusu podczas poruszania się klawiaturą. Interakcje mają działać bez myszy.

Ekrany MVP:

- menu główne,
- ustawienia,
- ustawienia sterowania / klawiszy,
- wczytywanie/zapisywanie gry,
- pauza w grze,
- podstawowe dialogi potwierdzenia.

## Proponowana architektura

### 1. Warstwa gry

Nazwa robocza moda: `modWither3Access`.

Zadania:

- wykrywać aktywny ekran menu,
- wykrywać aktywną kontrolkę lub pozycję listy,
- budować tekst do wypowiedzenia: nazwa, typ, stan, wartość, podpowiedź,
- wysyłać tekst do bridge.

Kolejność eksperymentów:

1. Sprawdzić w REDkit pliki UI/menu i klasy WitcherScript odpowiedzialne za menu.
2. Dodać komunikat debug/log przy wejściu do menu i przy zmianie opcji.
3. Potwierdzić, czy zewnętrzny proces może bezpiecznie odczytywać te komunikaty.
4. Dopiero później wybrać docelowy transport.

### 2. Bridge screen readera

Nazwa robocza: `Wither3.Access.Bridge`.

Zadania:

- nasłuchiwać komunikatów z moda,
- kolejkować mowę i usuwać duplikaty,
- obsłużyć przerwanie mowy po zmianie fokusu,
- mówić przez Tolk/NVDA, z fallbackiem do SAPI,
- mieć skrót do powtórzenia ostatniej wypowiedzi.

Możliwe transporty, od najbezpieczniejszego do najbardziej ambitnego:

- tail logu/scriptlog jako prototyp,
- lokalny plik kolejki w profilu użytkownika,
- named pipe / localhost socket, jeśli REDengine pozwoli,
- DLL/ASI/injection tylko jako ostateczność i po analizie zgodności z zasadami oraz ryzykiem, nie w MVP.

### 3. Helper startowy

Zadania:

- uruchomić bridge,
- uruchomić grę z pominięciem REDlaunchera,
- pozwolić wybrać DX11/DX12,
- nie modyfikować plików DRM ani binarek.

Najprostsze opcje:

- dokumentacja `--launcher-skip` w launcherze platformy,
- skrypt PowerShell uruchamiający `bin/x64/witcher3.exe` albo `bin/x64_dx12/witcher3.exe` po wskazaniu katalogu gry.

## Pierwsze zadania techniczne

1. Zainstalować/otworzyć REDkit i utworzyć projekt `Wither3.access` albo zsynchronizować obecny folder z projektem REDkit.
2. Odnaleźć pliki menu głównego i klasę odpowiadającą za zmianę fokusu.
3. Zrobić mikro-mod, który loguje `SCREEN: main_menu` i `FOCUS: New Game` / `Options` / `Load Game`.
4. Napisać minimalny bridge, który czyta log i mówi przez SAPI/Tolk.
5. Przetestować z NVDA:
   - uruchomienie bez myszy,
   - strzałki/Enter/Escape,
   - powtarzanie ostatniej wypowiedzi,
   - brak spamowania mową przy szybkim przewijaniu.

## Zasady projektowe

- Najpierw menu i czytanie stanu, potem świat 3D.
- Nie usuwamy wyzwań gry, jeśli da się przekazać informację dźwiękiem/tekstem.
- Skróty dostępnościowe powinny być konfigurowalne.
- Tekst do mowy musi być zwięzły, np. `Opcje, przycisk, 3 z 6`, a dłuższy opis tylko na żądanie.
- Każdy ekran powinien mieć komendę `czytaj cały ekran` i `czytaj aktywną pozycję`.

## Definition of done dla MVP

- Gracz uruchamia grę bez klikania w REDlauncher.
- NVDA/SAPI czyta menu główne.
- Strzałki zmieniają fokus i bridge czyta nową pozycję.
- Enter aktywuje pozycję.
- Escape wraca o ekran wyżej.
- Ustawienia podstawowe są czytane z wartością, np. `Język napisów: polski`.
- Projekt nie wymaga patchowania exe ani obchodzenia DRM.
