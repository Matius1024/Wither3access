/***********************************************************************
 * Wither3.access
 * Minimal speech event helper for The Witcher 3 script mods.
 *
 * Transport v0: write speech events to the game/script log through
 * LogChannel. The external bridge tails lines containing W3ACCESS.
 ***********************************************************************/

function W3Access_Speak(text : string)
{
	var configWrapper : CInGameConfigWrapper;
	var stamp : string;

	if(text != "")
	{
		text = W3Access_SanitizeLogText(text);
		LogChannel('W3ACCESS', "W3ACCESS|" + text);

		configWrapper = (CInGameConfigWrapper)theGame.GetInGameConfigWrapper();
		if(configWrapper)
		{
			stamp = IntToString(theGame.GetLocalTimeAsMilliseconds());
			configWrapper.WriteIniFile("Wither3AccessSpeech.ini", "last", text);
			configWrapper.WriteIniFile("Wither3AccessSpeech.ini", "stamp", stamp);
		}
	}
}

function W3Access_MenuSelected(menuName : name, moduleID : int, moduleBindingName : string)
{
	var label : string;
	var menuLabel : string;

	label = W3Access_LocalizeMenuBinding(moduleBindingName);
	menuLabel = W3Access_LocalizeName(menuName);

	W3Access_Speak(menuLabel + ". " + label + ".");
}

function W3Access_MenuReady(isMainMenu : bool)
{
	if(isMainMenu)
	{
		W3Access_Speak("Wither3.access. Menu glowne wykryte.");
	}
	else
	{
		W3Access_Speak("Wither3.access. Menu pauzy wykryte.");
	}
}

function W3Access_MenuItemsReady(items : CScriptedFlashArray, title : string)
{
	var index : int;
	var item : CScriptedFlashObject;
	var label : string;
	var currentValue : string;

	if(!items)
	{
		return;
	}

	LogChannel('W3ACCESS', "W3ACCESS_MENU|BEGIN|" + W3Access_EscapeMenuToken(title));

	for(index = 0; index < items.GetLength(); index += 1)
	{
		label = W3Access_GetMenuArrayLabel(items, index);
		if(label != "")
		{
			item = items.GetElementFlashObject(index);
			if(item)
			{
				currentValue = W3Access_GetMenuItemCurrentValue(item);
				if(currentValue != "")
				{
					label += ": " + currentValue;
				}
			}
			LogChannel('W3ACCESS', "W3ACCESS_MENU|ITEM|" + W3Access_EscapeMenuToken(label));
		}
	}

	LogChannel('W3ACCESS', "W3ACCESS_MENU|END");
}

function W3Access_GetMenuArrayLabel(items : CScriptedFlashArray, index : int) : string
{
	var item : CScriptedFlashObject;
	var label : string;

	if(!items)
	{
		return "";
	}

	item = items.GetElementFlashObject(index);
	if(item)
	{
		label = W3Access_GetMenuItemLabel(item);
		if(label != "")
		{
			return label;
		}
	}

	label = items.GetElementFlashString(index);
	if(label != "")
	{
		return label;
	}

	return "";
}

function W3Access_GetMenuItemLabel(item : CScriptedFlashObject) : string
{
	var label : string;

	if(!item)
	{
		return "";
	}

	label = item.GetMemberFlashString("label");
	if(label != "") return label;
	label = item.GetMemberFlashString("name");
	if(label != "") return label;
	label = item.GetMemberFlashString("title");
	if(label != "") return label;
	label = item.GetMemberFlashString("text");
	if(label != "") return label;
	label = item.GetMemberFlashString("slotName");
	if(label != "") return label;
	label = item.GetMemberFlashString("saveName");
	if(label != "") return label;
	label = item.GetMemberFlashString("keybindName");
	if(label != "") return label;
	label = item.GetMemberFlashString("actionName");
	if(label != "") return label;
	label = item.GetMemberFlashString("inputName");
	if(label != "") return label;
	label = item.GetMemberFlashString("id");
	if(label != "") return label;

	return "";
}

function W3Access_GetMenuItemCurrentValue(item : CScriptedFlashObject) : string
{
	var value : string;
	var subItems : CScriptedFlashArray;
	var optionIndex : int;
	var label : string;

	if(!item)
	{
		return "";
	}

	value = item.GetMemberFlashString("current");
	if(value != "")
	{
		if(value == "0")
		{
			label = item.GetMemberFlashString("offString");
			if(label != "")
			{
				return label;
			}
		}
		if(value == "1")
		{
			label = item.GetMemberFlashString("onString");
			if(label != "")
			{
				return label;
			}
		}
		subItems = item.GetMemberFlashArray("subElements");
		if(subItems)
		{
			optionIndex = StringToInt(value, -1);
			if(optionIndex >= 0 && optionIndex < subItems.GetLength())
			{
				label = W3Access_GetMenuArrayLabel(subItems, optionIndex);
				if(label != "")
				{
					return label;
				}
			}
		}
		return value;
	}
	value = item.GetMemberFlashString("value");
	if(value != "") return value;
	value = item.GetMemberFlashString("currentValue");
	if(value != "") return value;
	value = item.GetMemberFlashString("keybindValue");
	if(value != "") return value;
	value = item.GetMemberFlashString("inputValue");
	if(value != "") return value;

	return "";
}

function W3Access_RootMenuItemsReady(items : CScriptedFlashArray, title : string)
{
	W3Access_MenuReset();
	W3Access_MenuItemsReady(items, title);
}

function W3Access_MenuSubItemsReady(items : CScriptedFlashArray, itemId : string, itemTag : int, title : string)
{
	var item : CScriptedFlashObject;
	var subItems : CScriptedFlashArray;
	var label : string;

	if(!items)
	{
		return;
	}

	LogChannel('W3ACCESS', "W3ACCESS_DEBUG|SubmenuRequest|" + itemId + "|" + IntToString(itemTag));
	item = W3Access_FindMenuItem(items, itemId, itemTag);
	if(item)
	{
		label = item.GetMemberFlashString("listTitle");
		if(label == "")
		{
			label = W3Access_GetMenuItemLabel(item);
		}
		subItems = item.GetMemberFlashArray("subElements");
		if(subItems)
		{
			W3Access_MenuItemsReady(subItems, label != "" ? label : title);
			return;
		}
	}

	LogChannel('W3ACCESS', "W3ACCESS_DEBUG|SubmenuNotFound|" + itemId + "|" + IntToString(itemTag));
}

function W3Access_FindMenuItem(items : CScriptedFlashArray, itemId : string, itemTag : int) : CScriptedFlashObject
{
	var foundItem : CScriptedFlashObject;

	if(itemId != "" && itemTag != 0)
	{
		foundItem = W3Access_FindMenuItemByIdAndTag(items, itemId, itemTag);
		if(foundItem)
		{
			return foundItem;
		}
	}

	if(itemTag != 0 && itemTag != NameToFlashUInt('MenuSelector'))
	{
		foundItem = W3Access_FindMenuItemByTag(items, itemTag);
		if(foundItem)
		{
			return foundItem;
		}
	}

	if(itemId != "")
	{
		foundItem = W3Access_FindMenuItemById(items, itemId);
		if(foundItem)
		{
			return foundItem;
		}
	}

	if(itemTag != 0 && itemTag != NameToFlashUInt('MenuSelector'))
	{
		foundItem = W3Access_FindMenuItemByTag(items, itemTag);
	}

	return foundItem;
}

function W3Access_FindMenuItemByIdAndTag(items : CScriptedFlashArray, itemId : string, itemTag : int) : CScriptedFlashObject
{
	var index : int;
	var item : CScriptedFlashObject;
	var childItems : CScriptedFlashArray;
	var foundItem : CScriptedFlashObject;
	var candidateId : string;
	var candidateTag : int;

	if(!items)
	{
		return foundItem;
	}

	for(index = 0; index < items.GetLength(); index += 1)
	{
		item = items.GetElementFlashObject(index);
		if(item)
		{
			candidateId = item.GetMemberFlashString("id");
			candidateTag = item.GetMemberFlashUInt("tag");
			if(candidateId == itemId && candidateTag == itemTag)
			{
				return item;
			}

			childItems = item.GetMemberFlashArray("subElements");
			foundItem = W3Access_FindMenuItemByIdAndTag(childItems, itemId, itemTag);
			if(foundItem)
			{
				return foundItem;
			}
		}
	}

	return foundItem;
}

function W3Access_FindMenuItemById(items : CScriptedFlashArray, itemId : string) : CScriptedFlashObject
{
	var index : int;
	var item : CScriptedFlashObject;
	var childItems : CScriptedFlashArray;
	var foundItem : CScriptedFlashObject;
	var candidateId : string;

	if(!items)
	{
		return foundItem;
	}

	for(index = 0; index < items.GetLength(); index += 1)
	{
		item = items.GetElementFlashObject(index);
		if(item)
		{
			candidateId = item.GetMemberFlashString("id");
			if(candidateId == itemId)
			{
				return item;
			}

			childItems = item.GetMemberFlashArray("subElements");
			foundItem = W3Access_FindMenuItemById(childItems, itemId);
			if(foundItem)
			{
				return foundItem;
			}
		}
	}

	return foundItem;
}

function W3Access_FindMenuItemByTag(items : CScriptedFlashArray, itemTag : int) : CScriptedFlashObject
{
	var index : int;
	var item : CScriptedFlashObject;
	var childItems : CScriptedFlashArray;
	var foundItem : CScriptedFlashObject;
	var candidateTag : int;

	if(!items)
	{
		return foundItem;
	}

	for(index = 0; index < items.GetLength(); index += 1)
	{
		item = items.GetElementFlashObject(index);
		if(item)
		{
			candidateTag = item.GetMemberFlashUInt("tag");
			if(candidateTag == itemTag)
			{
				return item;
			}

			childItems = item.GetMemberFlashArray("subElements");
			foundItem = W3Access_FindMenuItemByTag(childItems, itemTag);
			if(foundItem)
			{
				return foundItem;
			}
		}
	}

	return foundItem;
}

function W3Access_MenuHighlight()
{
	LogChannel('W3ACCESS', "W3ACCESS_MENU|HIGHLIGHT");
}

function W3Access_MenuClear()
{
	LogChannel('W3ACCESS', "W3ACCESS_MENU|CLEAR");
}

function W3Access_MenuPush()
{
	LogChannel('W3ACCESS', "W3ACCESS_MENU|PUSH");
}

function W3Access_MenuPop()
{
	LogChannel('W3ACCESS', "W3ACCESS_MENU|POP");
}

function W3Access_MenuReset()
{
	LogChannel('W3ACCESS', "W3ACCESS_MENU|RESET");
}

function W3Access_ItemActivated(actionType : int, menuTag : int)
{
	W3Access_Speak(W3Access_GetActionTypeLabel(actionType, menuTag));
}

function W3Access_InputHandled(menuName : name, NavCode : string, KeyCode : int, ActionId : int)
{
	LogChannel('W3ACCESS', "W3ACCESS_DEBUG|Input|" + NameToString(menuName) + "|" + NavCode + "|" + KeyCode + "|" + ActionId);
}

function W3Access_OptionFocused(optionName : name)
{
	var configWrapper : CInGameConfigWrapper;
	var groupIdx : int;
	var varIdx : int;
	var groupsNum : int;
	var varsNum : int;
	var groupName : name;
	var currentName : name;
	var value : string;

	configWrapper = (CInGameConfigWrapper)theGame.GetInGameConfigWrapper();
	if(!configWrapper)
	{
		W3Access_Speak(W3Access_LocalizeName(optionName));
		return;
	}

	groupsNum = configWrapper.GetGroupsNum();
	for(groupIdx = 0; groupIdx < groupsNum; groupIdx += 1)
	{
		groupName = configWrapper.GetGroupName(groupIdx);
		varsNum = configWrapper.GetVarsNumByGroupName(groupName);
		for(varIdx = 0; varIdx < varsNum; varIdx += 1)
		{
			currentName = configWrapper.GetVarNameByGroupName(groupName, varIdx);
			if(currentName == optionName)
			{
				value = configWrapper.GetVarValue(groupName, optionName);
				W3Access_Speak(W3Access_FormatOption(groupName, optionName, value));
				return;
			}
		}
	}

	W3Access_Speak(W3Access_LocalizeName(optionName));
}

function W3Access_OptionValueChanged(groupId : int, optionName : name, optionValue : string)
{
	var configWrapper : CInGameConfigWrapper;
	var groupName : name;

	configWrapper = (CInGameConfigWrapper)theGame.GetInGameConfigWrapper();
	if(configWrapper)
	{
		groupName = configWrapper.GetGroupName(groupId);
		W3Access_Speak(W3Access_FormatOption(groupName, optionName, optionValue));
	}
	else
	{
		W3Access_Speak(W3Access_LocalizeName(optionName) + ": " + optionValue);
	}
}

function W3Access_OptionValueChangedByName(optionName : name, optionValue : string)
{
	var configWrapper : CInGameConfigWrapper;
	var groupIdx : int;
	var varIdx : int;
	var groupsNum : int;
	var varsNum : int;
	var groupName : name;
	var currentName : name;

	configWrapper = (CInGameConfigWrapper)theGame.GetInGameConfigWrapper();
	if(!configWrapper)
	{
		W3Access_Speak(W3Access_LocalizeName(optionName) + ": " + optionValue);
		return;
	}

	groupsNum = configWrapper.GetGroupsNum();
	for(groupIdx = 0; groupIdx < groupsNum; groupIdx += 1)
	{
		groupName = configWrapper.GetGroupName(groupIdx);
		varsNum = configWrapper.GetVarsNumByGroupName(groupName);
		for(varIdx = 0; varIdx < varsNum; varIdx += 1)
		{
			currentName = configWrapper.GetVarNameByGroupName(groupName, varIdx);
			if(currentName == optionName)
			{
				W3Access_Speak(W3Access_FormatOption(groupName, optionName, optionValue));
				return;
			}
		}
	}

	W3Access_Speak(W3Access_LocalizeName(optionName) + ": " + optionValue);
}

function W3Access_FormatOption(groupName : name, optionName : name, optionValue : string) : string
{
	return W3Access_GetOptionLabel(groupName, optionName) + ": " + W3Access_GetOptionValueLabel(groupName, optionName, optionValue);
}

function W3Access_GetOptionLabel(groupName : name, optionName : name) : string
{
	var configWrapper : CInGameConfigWrapper;
	var displayName : string;
	var label : string;

	configWrapper = (CInGameConfigWrapper)theGame.GetInGameConfigWrapper();
	if(configWrapper)
	{
		displayName = configWrapper.GetVarDisplayName(groupName, optionName);
		if(displayName != "")
		{
			label = GetLocStringByKeyExt("option_" + displayName);
			if(label != "")
			{
				return label;
			}
			label = GetLocStringByKeyExt(displayName);
			if(label != "")
			{
				return label;
			}
			return displayName;
		}
	}

	return W3Access_LocalizeName(optionName);
}

function W3Access_GetOptionValueLabel(groupName : name, optionName : name, optionValue : string) : string
{
	var configWrapper : CInGameConfigWrapper;
	var displayType : string;
	var optionIndex : int;
	var optionsNum : int;
	var rawValue : string;
	var label : string;

	if(optionValue == "true")
	{
		label = GetLocStringByKeyExt("panel_mainmenu_option_value_on");
		return label != "" ? label : "wlaczone";
	}
	if(optionValue == "false")
	{
		label = GetLocStringByKeyExt("panel_mainmenu_option_value_off");
		return label != "" ? label : "wylaczone";
	}

	configWrapper = (CInGameConfigWrapper)theGame.GetInGameConfigWrapper();
	if(configWrapper)
	{
		displayType = configWrapper.GetVarDisplayType(groupName, optionName);
		if(displayType == "SLIDER")
		{
			return W3Access_FormatSliderValue(groupName, optionName, optionValue);
		}
		if(displayType == "TOGGLE" && (optionValue == "0" || optionValue == "1"))
		{
			optionIndex = StringToInt(optionValue, -1);
			optionsNum = configWrapper.GetVarOptionsNum(groupName, optionName);
			if(optionIndex >= 0 && optionIndex < optionsNum)
			{
				rawValue = configWrapper.GetVarOption(groupName, optionName, optionIndex);
				label = GetLocStringByKeyExt(rawValue);
				if(label != "")
				{
					return label;
				}
				return rawValue;
			}
		}
		if(displayType == "OPTIONS" || displayType == "STEPPER" || displayType == "TOGGLESTEPPER")
		{
			optionIndex = StringToInt(optionValue, -1);
			optionsNum = configWrapper.GetVarOptionsNum(groupName, optionName);
			if(optionIndex >= 0 && optionIndex < optionsNum)
			{
				rawValue = configWrapper.GetVarOption(groupName, optionName, optionIndex);
				label = GetLocStringByKeyExt("preset_value_" + rawValue);
				if(label != "")
				{
					return label;
				}
				label = GetLocStringByKeyExt(rawValue);
				if(label != "")
				{
					return label;
				}
				return rawValue;
			}
		}
	}

	return optionValue;
}

function W3Access_FormatSliderValue(groupName : name, optionName : name, optionValue : string) : string
{
	if(groupName == 'Audio' || StrContains(NameToString(optionName), "Volume"))
	{
		return optionValue + " procent";
	}

	return optionValue;
}

function W3Access_EscapeMenuToken(value : string) : string
{
	value = W3Access_SanitizeLogText(value);
	value = StrReplaceAll(value, "%", "%25");
	value = StrReplaceAll(value, "|", "%7C");
	return value;
}

function W3Access_SanitizeLogText(value : string) : string
{
	value = StrReplaceAll(value, "ą", "a");
	value = StrReplaceAll(value, "ć", "c");
	value = StrReplaceAll(value, "ę", "e");
	value = StrReplaceAll(value, "ł", "l");
	value = StrReplaceAll(value, "ń", "n");
	value = StrReplaceAll(value, "ó", "o");
	value = StrReplaceAll(value, "ś", "s");
	value = StrReplaceAll(value, "ź", "z");
	value = StrReplaceAll(value, "ż", "z");
	value = StrReplaceAll(value, "Ą", "A");
	value = StrReplaceAll(value, "Ć", "C");
	value = StrReplaceAll(value, "Ę", "E");
	value = StrReplaceAll(value, "Ł", "L");
	value = StrReplaceAll(value, "Ń", "N");
	value = StrReplaceAll(value, "Ó", "O");
	value = StrReplaceAll(value, "Ś", "S");
	value = StrReplaceAll(value, "Ź", "Z");
	value = StrReplaceAll(value, "Ż", "Z");
	value = StrReplaceAll(value, "&nbsp;", " ");
	value = StrReplaceAll(value, "&amp;", " i ");
	return value;
}

function W3Access_GetActionTypeLabel(actionType : int, menuTag : int) : string
{
	switch(actionType)
	{
		case 1:
			return "Wznow gre.";
		case 2:
			return "Otwieram podmenu.";
		case 3:
			return "Otwieram ostatnie podmenu.";
		case 4:
			return "Wczytaj gre.";
		case 5:
			return "Zapisz gre.";
		case 6:
			return "Wyjscie.";
		case 7:
			return "Ustawienie profilu.";
		case 8:
			return "Przelacznik.";
		case 9:
			return "Lista.";
		case 10:
			return "Suwak.";
		case 11:
			return "Kontynuuj ostatni zapis.";
		case 12:
			return "Samouczki.";
		case 13:
			return "Napisy koncowe.";
		case 14:
			return "Pomoc.";
		case 15:
			return "Sterowanie.";
		case 16:
			return "Pomoc kontrolera.";
		case 17:
			return "Nowa gra.";
		case 18:
			return "Zamknij gre.";
		case 19:
			return "Skalowanie interfejsu.";
		case 20:
			return "Gamma.";
		case 22:
			return "Gwint.";
		case 23:
			return "Import zapisu.";
		case 24:
			return "Przypisanie klawiszy.";
		case 25:
			return "Wstecz.";
		case 26:
			return "Nowa gra plus.";
		case 27:
			return "Zainstalowane dodatki.";
		case 28:
			return "Przycisk.";
		case 29:
			return "Przelacznik renderowania.";
		case 30:
			return "GOG.";
		case 31:
			return "Zgoda na telemetrie.";
		case 32:
			return "Lista.";
		case 33:
			return "Krokowa zmiana wartosci.";
		case 34:
			return "Przelacznik krokowy.";
		case 37:
			return "Kup dodatek Serca z kamienia.";
		case 38:
			return "Kup dodatek Krew i wino.";
		case 100:
			return "Opcje.";
		default:
			return "Aktywowano pozycje menu. Typ " + IntToString(actionType) + ". Tag " + IntToString(menuTag) + ".";
	}
}

function W3Access_LocalizeName(value : name) : string
{
	var result : string;
	result = GetLocStringByKeyExt(NameToString(value));
	if(result != "")
	{
		return result;
	}
	return NameToString(value);
}

function W3Access_LocalizeMenuBinding(value : string) : string
{
	var result : string;

	result = GetLocStringByKeyExt(value);
	if(result != "")
	{
		return result;
	}

	if(value == "Continue") return GetLocStringByKeyExt("panel_continue");
	if(value == "NewGame") return GetLocStringByKeyExt("panel_newgame");
	if(value == "LoadGame") return GetLocStringByKeyExt("panel_mainmenu_loadgame");
	if(value == "Options") return GetLocStringByKeyExt("panel_mainmenu_options");
	if(value == "Resume") return GetLocStringByKeyExt("panel_resume");
	if(value == "SaveGame") return GetLocStringByKeyExt("panel_mainmenu_savegame");
	if(value == "CloseGame") return GetLocStringByKeyExt("menu_main_quit");
	if(value == "Tutorials") return GetLocStringByKeyExt("panel_mainmenu_tutorials");
	if(value == "Gwent") return GetLocStringByKeyExt("panel_mainmenu_gwent");
	if(value == "CloudSaves") return GetLocStringByKeyExt("ui_gog_my_rewards");

	return value;
}
