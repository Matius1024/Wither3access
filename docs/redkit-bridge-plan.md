# REDkit to screen-reader bridge plan

## Target architecture

1. A REDkit/WitcherScript mod observes the real game UI.
2. The mod emits speech events with the `W3ACCESS` marker.
3. `Witcher3ScreenReaderBridge.exe` watches those events and speaks them through NVDA or SAPI.
4. The bridge never presses keys, clicks the mouse, or changes game focus.

## First hook point found locally

Installed scripts contain:

```text
content/content0/scripts/game/gui/menus/menuBase.ws
```

Useful events:

- `CR4MenuBase.OnModuleSelected(moduleID, moduleBindingName)`
- `CR4MenuBase.OnInputHandled(NavCode, KeyCode, ActionId)`

`OnModuleSelected` is the first likely hook for real menu focus changes. It already receives the selected module id and a binding/name string from the actual UI.

## Transport v0

The script helper in:

```text
mods/modWither3Access/content/scripts/game/accessibility/w3accessSpeech.ws
```

uses:

```witcherscript
LogChannel('W3ACCESS', "W3ACCESS|" + text);
```

The bridge tails files and speaks lines containing `W3ACCESS`.

Default watched files:

- `runtime/speech.queue.log` inside the project folder.
- `%USERPROFILE%\Documents\The Witcher 3\Wither3Access\speech.queue.log`.

When we identify the exact Witcher 3 script log path for this installation, add it as:

```powershell
.\Witcher3ScreenReaderBridge.exe --watch="C:\path\to\script.log"
```

## Current limitation

The first patch can say that a real menu module changed, but may initially expose internal ids instead of localized labels. Next we should hook menu-specific data creation functions, especially `ingameMenu.ws` and `ingamemenu/igmOptions.ws`, where labels and current option values are pushed into Flash objects.
