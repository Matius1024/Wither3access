param(
    [string] $GameDir = "C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3 Wild Hunt",
    [switch] $Deploy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Get-Item -LiteralPath $PSScriptRoot).Parent.FullName
$modRoot = Join-Path $projectRoot "mods\modWither3Access"
$gameScripts = Join-Path $GameDir "content\content0\scripts"

function Copy-SourceScript {
    param(
        [string] $RelativePath
    )

    $source = Join-Path $gameScripts $RelativePath
    $target = Join-Path (Join-Path $modRoot "content\scripts") $RelativePath
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Nie znaleziono skryptu gry: $source"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    return $target
}

function Replace-Once {
    param(
        [string] $Path,
        [string] $Needle,
        [string] $Replacement
    )

    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    if ($text.IndexOf($Replacement, [System.StringComparison]::Ordinal) -ge 0) {
        return
    }
    $index = $text.IndexOf($Needle, [System.StringComparison]::Ordinal)
    if ($index -lt 0) {
        throw "Nie znaleziono kotwicy w $Path`n$Needle"
    }
    $text = $text.Remove($index, $Needle.Length).Insert($index, $Replacement)
    if ($text.Length -gt 5242880) {
        throw "Nieoczekiwanie duzy wynik po zmianie w $Path"
    }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $text, $utf8)
}

$menuBase = Copy-SourceScript "game\gui\menus\menuBase.ws"
$ingameMenu = Copy-SourceScript "game\gui\main_menu\ingameMenu.ws"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`tprivate var m_fxHideErrorWindow`t`t: CScriptedFlashFunction;`r`n`t" `
    -Replacement "`tprivate var m_fxHideErrorWindow`t`t: CScriptedFlashFunction;`r`n`tprivate var m_w3AccessMenuData`t`t: CScriptedFlashArray;`r`n`tprivate var m_w3AccessOptionsData`t: CScriptedFlashArray;`r`n`tprivate var m_w3AccessOptionsDepth`t: int;`r`n`tprivate var m_w3AccessBackHandled`t: bool;`r`n`tprivate var m_w3AccessBackHandledAt`t: int;`r`n`t"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tsuper.OnConfigUI();`r`n`t`t" `
    -Replacement "`t`tsuper.OnConfigUI();`r`n`t`tW3Access_MenuReady(isMainMenu);`r`n`t`t"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tif (ignoreInput)`r`n`t`t{" `
    -Replacement "`t`tif (!(m_w3AccessOptionsDepth > 0 && (actionType == IGMActionType_MenuHolder || actionType == IGMActionType_MenuLastHolder)))`r`n`t`t{`r`n`t`t`tW3Access_ItemActivated(actionType, menuTag);`r`n`t`t}`r`n`t`tif (ignoreInput)`r`n`t`t{"

Replace-Once `
    -Path $menuBase `
    -Needle "`t`tif (soundName == `"gui_global_highlight`")`r`n`t`t{" `
    -Replacement "`t`tif (soundName == `"gui_global_highlight`")`r`n`t`t{`r`n`t`t`tW3Access_MenuHighlight();"

Replace-Once `
    -Path $menuBase `
    -Needle "`t`tLogChannel('OnModuleSelected',GetMenuName()+`" UISavedData.selectedModule `"+UISavedData.selectedModule+`" vs moduleID `"+moduleID);`r`n`t`tUISavedData.selectedModule = moduleID;" `
    -Replacement "`t`tLogChannel('OnModuleSelected',GetMenuName()+`" UISavedData.selectedModule `"+UISavedData.selectedModule+`" vs moduleID `"+moduleID);`r`n`t`tUISavedData.selectedModule = moduleID;`r`n`t`tW3Access_MenuSelected(GetMenuName(), moduleID, moduleBindingName);"

Replace-Once `
    -Path $menuBase `
    -Needle "`tevent  OnInputHandled(NavCode:string, KeyCode:int, ActionId:int) `r`n`t{`r`n`t`tLogChannel('GUIWARNING', `"Unecesary call of OnInputHandled NavCode `"+NavCode+`" KeyCode `"+KeyCode);`r`n`t}" `
    -Replacement "`tevent  OnInputHandled(NavCode:string, KeyCode:int, ActionId:int) `r`n`t{`r`n`t`tW3Access_InputHandled(GetMenuName(), NavCode, KeyCode, ActionId);`r`n`t`tLogChannel('GUIWARNING', `"Unecesary call of OnInputHandled NavCode `"+NavCode+`" KeyCode `"+KeyCode);`r`n`t}"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tif (groupId == NameToFlashUInt('SpecialSettingsGroupId'))`r`n`t`t{`r`n`t`t`tHandleSpecialValueChanged(optionName, optionValue);`r`n`t`t`treturn true;`r`n`t`t}" `
    -Replacement "`t`tif (groupId == NameToFlashUInt('SpecialSettingsGroupId'))`r`n`t`t{`r`n`t`t`tW3Access_OptionValueChangedByName(optionName, optionValue);`r`n`t`t`tHandleSpecialValueChanged(optionName, optionValue);`r`n`t`t`treturn true;`r`n`t`t}"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tif (optionName == 'GwentDifficulty')`r`n`t`t{`r`n`t`t`tif ( optionValue == `"0`" )" `
    -Replacement "`t`tif (optionName == 'GwentDifficulty')`r`n`t`t{`r`n`t`t`tW3Access_OptionValueChangedByName(optionName, optionValue);`r`n`t`t`tif ( optionValue == `"0`" )"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tgroupName = mInGameConfigWrapper.GetGroupName( groupId );" `
    -Replacement "`t`tgroupName = mInGameConfigWrapper.GetGroupName( groupId );`r`n`t`tW3Access_OptionValueChanged(groupId, optionName, optionValue);"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`tevent  OnOptionSelectionChanged( optionName : name, value : bool)`r`n`t{" `
    -Replacement "`tevent  OnOptionSelectionChanged( optionName : name, value : bool)`r`n`t{`r`n`t`tif(value)`r`n`t`t{`r`n`t`t`tW3Access_OptionFocused(optionName);`r`n`t`t}"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`tevent  OnShowOptionSubmenu( actionType:int, menuTag:int, id:string ) : void`r`n`t{`t`t`r`n`t`tupdateDLSSGOptionChanged();" `
    -Replacement "`tevent  OnShowOptionSubmenu( actionType:int, menuTag:int, id:string ) : void`r`n`t{`t`t`r`n`t`tm_w3AccessOptionsDepth += 1;`r`n`t`tm_w3AccessBackHandled = false;`r`n`t`tm_w3AccessBackHandledAt = 0;`r`n`t`tW3Access_MenuPush();`r`n`t`tW3Access_MenuSubItemsReady(m_w3AccessOptionsData, id, menuTag, `"Opcje`");`r`n`t`tupdateDLSSGOptionChanged();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`tpublic function HandleLoadGameFailed():void" `
    -Replacement "`tprotected function HandleW3AccessOptionBack() : void`r`n`t{`r`n`t`tm_w3AccessBackHandled = true;`r`n`t`tm_w3AccessBackHandledAt = theGame.GetLocalTimeAsMilliseconds();`r`n`t`tif (m_w3AccessOptionsDepth > 1)`r`n`t`t{`r`n`t`t`tm_w3AccessOptionsDepth -= 1;`r`n`t`t`tW3Access_MenuPop();`r`n`t`t}`r`n`t`telse if (m_w3AccessOptionsDepth == 1)`r`n`t`t{`r`n`t`t`tRestoreW3AccessRootMenu();`r`n`t`t}`r`n`t`telse`r`n`t`t{`r`n`t`t`tW3Access_MenuPop();`r`n`t`t}`r`n`t}`r`n`r`n`tprotected function RestoreW3AccessRootMenu() : void`r`n`t{`r`n`t`tm_w3AccessOptionsDepth = 0;`r`n`t`tif (m_w3AccessMenuData)`r`n`t`t{`r`n`t`t`tW3Access_RootMenuItemsReady(m_w3AccessMenuData, isMainMenu ? `"Menu glowne`" : `"Menu pauzy`");`r`n`t`t}`r`n`t`telse`r`n`t`t{`r`n`t`t`tW3Access_MenuReset();`r`n`t`t}`r`n`t}`r`n`r`n`tpublic function HandleLoadGameFailed():void"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`tevent  OnShowSaveGameMenu() : void`r`n`t{" `
    -Replacement "`tevent  OnShowSaveGameMenu() : void`r`n`t{"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`tevent  OnShowLoadGameMenu() : void`r`n`t{" `
    -Replacement "`tevent  OnShowLoadGameMenu() : void`r`n`t{"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tvar interactionModule : CR4HudModuleInteractions;`r`n`t`tvar hud : CR4ScriptedHud;`r`n`t`t`r`n`t`ttheGame.SetHDRMenuActive(false);" `
    -Replacement "`t`tvar interactionModule : CR4HudModuleInteractions;`r`n`t`tvar hud : CR4ScriptedHud;`r`n`t`t`r`n`t`tm_w3AccessOptionsDepth = 0;`r`n`t`tm_w3AccessBackHandled = false;`r`n`t`tm_w3AccessBackHandledAt = 0;`r`n`t`tW3Access_MenuReset();`r`n`t`ttheGame.SetHDRMenuActive(false);"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`tevent  OnOptionPanelNavigateBack()`r`n`t{`r`n`t`tvar graphicChangesPending:bool;`r`n`t`tvar hud : CR4ScriptedHud;`r`n`t`t`r`n`t`ttheGame.SetHDRMenuActive(false);" `
    -Replacement "`tevent  OnOptionPanelNavigateBack()`r`n`t{`r`n`t`tvar graphicChangesPending:bool;`r`n`t`tvar hud : CR4ScriptedHud;`r`n`t`t`r`n`t`tHandleW3AccessOptionBack();`r`n`t`ttheGame.SetHDRMenuActive(false);"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`ttheGame.SetHDRMenuActive(false);`r`n`t`t`r`n`t`thud = (CR4ScriptedHud)(theGame.GetHud());" `
    -Replacement "`t`ttheGame.SetHDRMenuActive(false);`r`n`t`tif (m_w3AccessBackHandled && theGame.GetLocalTimeAsMilliseconds() - m_w3AccessBackHandledAt < 500)`r`n`t`t{`r`n`t`t`tm_w3AccessBackHandled = false;`r`n`t`t}`r`n`t`telse`r`n`t`t{`r`n`t`t`tm_w3AccessBackHandled = false;`r`n`t`t`tif (m_w3AccessOptionsDepth > 0)`r`n`t`t`t{`r`n`t`t`t`tRestoreW3AccessRootMenu();`r`n`t`t`t}`r`n`t`t`telse`r`n`t`t`t{`r`n`t`t`t`tW3Access_MenuPop();`r`n`t`t`t}`r`n`t`t}`r`n`t`t`r`n`t`thud = (CR4ScriptedHud)(theGame.GetHud());"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tl_DataFlashArray = IngameMenu_FillOptionsSubMenuData(m_flashValueStorage, isMainMenu);`r`n`t`t`r`n`t`tm_initialSelectionsToIgnore = 1;" `
    -Replacement "`t`tl_DataFlashArray = IngameMenu_FillOptionsSubMenuData(m_flashValueStorage, isMainMenu);`r`n`t`tm_w3AccessOptionsData = l_DataFlashArray;`r`n`t`tm_w3AccessOptionsDepth = 1;`r`n`t`tm_w3AccessBackHandled = false;`r`n`t`tm_w3AccessBackHandledAt = 0;`r`n`t`tW3Access_MenuPush();`r`n`t`tW3Access_MenuItemsReady(m_w3AccessOptionsData, `"Opcje`");`r`n`t`t`r`n`t`tm_initialSelectionsToIgnore = 1;"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tl_DataFlashArray = m_structureCreator.PopulateMenuData();`r`n`t`t`r`n`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.entries`", l_DataFlashArray );" `
    -Replacement "`t`tl_DataFlashArray = m_structureCreator.PopulateMenuData();`r`n`t`tm_w3AccessMenuData = l_DataFlashArray;`r`n`t`tm_w3AccessOptionsDepth = 0;`r`n`t`tm_w3AccessBackHandled = false;`r`n`t`tm_w3AccessBackHandledAt = 0;`r`n`t`tW3Access_RootMenuItemsReady(l_DataFlashArray, isMainMenu ? `"Menu glowne`" : `"Menu pauzy`");`r`n`t`t`r`n`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.entries`", l_DataFlashArray );"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_MenuHolder:`r`n`t`t`t`t`r`n`t`t`t`t`r`n`t`t`t`tm_initialSelectionsToIgnore = 1;" `
    -Replacement "`t`t`tcase IGMActionType_MenuHolder:`r`n`t`t`t`t`r`n`t`t`t`t`r`n`t`t`t`tif (m_w3AccessOptionsDepth == 0)`r`n`t`t`t`t{`r`n`t`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`t`tW3Access_MenuSubItemsReady(m_w3AccessMenuData, `"`", menuTag, isMainMenu ? `"Menu glowne`" : `"Menu pauzy`");`r`n`t`t`t`t}`r`n`t`t`t`tm_initialSelectionsToIgnore = 1;"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_MenuLastHolder:`r`n`t`t`t`tm_initialSelectionsToIgnore = 1;" `
    -Replacement "`t`t`tcase IGMActionType_MenuLastHolder:`r`n`t`t`t`tif (m_w3AccessOptionsDepth == 0)`r`n`t`t`t`t{`r`n`t`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`t`tW3Access_MenuSubItemsReady(m_w3AccessMenuData, `"`", menuTag, isMainMenu ? `"Menu glowne`" : `"Menu pauzy`");`r`n`t`t`t`t}`r`n`t`t`t`tm_initialSelectionsToIgnore = 1;"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`t`tif (hasSaveDataToLoad())`r`n`t`t`t`t{`r`n`t`t`t`t`tSendLoadData();" `
    -Replacement "`t`t`t`tif (hasSaveDataToLoad())`r`n`t`t`t`t{`r`n`t`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`t`tSendLoadData();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`t`tif ( !theGame.AreSavesLocked() )`r`n`t`t`t`t{`r`n`t`t`t`t`tSendSaveData();" `
    -Replacement "`t`t`t`tif ( !theGame.AreSavesLocked() )`r`n`t`t`t`t{`r`n`t`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`t`tSendSaveData();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_ControllerHelp:`r`n`t`t`t`tcurMenuDepth += 1;`r`n`t`t`t`tSendControllerData();" `
    -Replacement "`t`t`tcase IGMActionType_ControllerHelp:`r`n`t`t`t`tcurMenuDepth += 1;`r`n`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`tSendControllerData();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_NewGamePlus:`r`n`t`t`t`tfetchNewGameConfigFromTag(menuTag);`r`n`t`t`t`tSendNewGamePlusSaves();" `
    -Replacement "`t`t`tcase IGMActionType_NewGamePlus:`r`n`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`tfetchNewGameConfigFromTag(menuTag);`r`n`t`t`t`tSendNewGamePlusSaves();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_InstalledDLC:`r`n`t`t`t`tSendInstalledDLCList();" `
    -Replacement "`t`t`tcase IGMActionType_InstalledDLC:`r`n`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`tSendInstalledDLCList();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_UIRescale:`r`n`t`t`t`tcurMenuDepth += 1;`r`n`t`t`t`tSendRescaleData();" `
    -Replacement "`t`t`tcase IGMActionType_UIRescale:`r`n`t`t`t`tcurMenuDepth += 1;`r`n`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`tSendRescaleData();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_ImportSave:`r`n`t`t`t`tlastSetTag = menuTag;`r`n`t`t`t`tfetchNewGameConfigFromTag( menuTag );`r`n`t`t`t`tSendImportSaveData( );" `
    -Replacement "`t`t`tcase IGMActionType_ImportSave:`r`n`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`tlastSetTag = menuTag;`r`n`t`t`t`tfetchNewGameConfigFromTag( menuTag );`r`n`t`t`t`tSendImportSaveData( );"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tcase IGMActionType_KeyBinds:`r`n`t`t`t`tcurMenuDepth += 1;`r`n`t`t`t`tSendKeybindData();" `
    -Replacement "`t`t`tcase IGMActionType_KeyBinds:`r`n`t`t`t`tcurMenuDepth += 1;`r`n`t`t`t`tW3Access_MenuPush();`r`n`t`t`t`tSendKeybindData();"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tIngameMenu_PopulateImportSaveData(m_flashValueStorage, dataFlashArray);`r`n`t`t`r`n`t`tm_initialSelectionsToIgnore = 1;" `
    -Replacement "`t`tIngameMenu_PopulateImportSaveData(m_flashValueStorage, dataFlashArray);`r`n`t`tW3Access_MenuItemsReady(dataFlashArray, `"Import zapisu`");`r`n`t`t`r`n`t`tm_initialSelectionsToIgnore = 1;"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t{`r`n`t`t`tisShowingLoadList = true;`r`n`t`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.loadSlots`", dataFlashArray );" `
    -Replacement "`t`t{`r`n`t`t`tisShowingLoadList = true;`r`n`t`t`tW3Access_MenuItemsReady(dataFlashArray, `"Wczytaj gre`");`r`n`t`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.loadSlots`", dataFlashArray );"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tisShowingSaveList = true;`r`n`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.saveSlots`", dataFlashArray );" `
    -Replacement "`t`tisShowingSaveList = true;`r`n`t`tW3Access_MenuItemsReady(dataFlashArray, `"Zapisz gre`");`r`n`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.saveSlots`", dataFlashArray );"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`t`tOnPlaySoundEvent( `"gui_global_panel_open`" );`r`n`t`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.newGamePlusSlots`", dataFlashArray );" `
    -Replacement "`t`t`tOnPlaySoundEvent( `"gui_global_panel_open`" );`r`n`t`t`tW3Access_MenuItemsReady(dataFlashArray, `"Nowa gra plus`");`r`n`t`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.newGamePlusSlots`", dataFlashArray );"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tm_flashValueStorage.SetFlashArray(`"ingamemenu.installedDLCs`", dataArray);" `
    -Replacement "`t`tW3Access_MenuItemsReady(dataArray, `"Zainstalowane dodatki`");`r`n`t`tm_flashValueStorage.SetFlashArray(`"ingamemenu.installedDLCs`", dataArray);"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.gamepad.mappings`", dataFlashArray );" `
    -Replacement "`t`tW3Access_MenuItemsReady(dataFlashArray, `"Pomoc kontrolera`");`r`n`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.gamepad.mappings`", dataFlashArray );"

Replace-Once `
    -Path $ingameMenu `
    -Needle "`t`tIngameMenu_GatherKeybindData(dataFlashArray, m_flashValueStorage);`r`n`t`t`r`n`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.keybindValues`", dataFlashArray );" `
    -Replacement "`t`tIngameMenu_GatherKeybindData(dataFlashArray, m_flashValueStorage);`r`n`t`t`r`n`t`tW3Access_MenuItemsReady(dataFlashArray, `"Przypisanie klawiszy`");`r`n`t`tm_flashValueStorage.SetFlashArray( `"ingamemenu.keybindValues`", dataFlashArray );"

if ($Deploy) {
    $targetModsDir = Join-Path $GameDir "mods"
    $targetModRoot = Join-Path $targetModsDir "modWither3Access"
    New-Item -ItemType Directory -Force -Path $targetModsDir | Out-Null
    Copy-Item -LiteralPath $modRoot -Destination $targetModsDir -Recurse -Force
    Write-Output "Zainstalowano mod: $targetModRoot"
}

Write-Output "Zbudowano skrypty moda: $modRoot"
