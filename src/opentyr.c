/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009  The OpenTyrian Development Team
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
#include "opentyr.h"

#include "config.h"
#include "destruct.h"
#include "editship.h"
#include "episodes.h"
#include "file.h"
#include "font.h"
#include "fonthand.h"
#include "helptext.h"
#include "joystick.h"
#include "jukebox.h"
#include "keyboard.h"
#include "loudness.h"
#include "mainint.h"
#include "mouse.h"
#include "mtrand.h"
#include "network.h"
#include "nortsong.h"
#include "nortvars.h"
#include "opentyrian_version.h"
#include "palette.h"
#include "params.h"
#include "picload.h"
#include "sprite.h"
#include "tyrian2.h"
#include "varz.h"
#include "vga256d.h"
#include "video.h"
#include "video_scale.h"
#include "xmas.h"

#include "SDL.h"

#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

const char *opentyrian_str = "Tyrian Reloaded";
const char *opentyrian_version = OPENTYRIAN_VERSION;

bool str_pop_int(char *str, int *val)
{
	// grab the value from str
	char *end;
	*val = strtol(str, &end, 10);
	if (end == str)
		return false;

	// shift the rest to the beginning
	memmove(str, end, strlen(end) + 1);
	return true;
}

bool str_pop_str(char *str, char *val, size_t val_size)
{
	const char *start = str + strspn(str, " \t");
	const size_t length = strcspn(start, " \t");
	if (length == 0)
		return false;

	if (length >= val_size)
	{
		fprintf(stderr, "error: item reference exceeds its maximum length\n");
		exit(EXIT_FAILURE);
	}

	memcpy(val, start, length);
	val[length] = '\0';
	const char *end = start + length;
	end += strspn(end, " \t");
	memmove(str, end, strlen(end) + 1);
	return true;
}

static size_t getShopContentsPickerItemsCount(void)
{
	return 3;
}

static const char *getShopContentsPickerItem(size_t i, char *buffer, size_t bufferSize)
{
	(void)buffer, (void)bufferSize;

	if (i == 1)
		return "Mixed";
	if (i == 2)
		return "Random";
	return "Mission";
}

static size_t getDisplayPickerItemsCount(void)
{
	return 1 + (size_t)SDL_GetNumVideoDisplays();
}

static const char *getDisplayPickerItem(size_t i, char *buffer, size_t bufferSize)
{
	if (i == 0)
		return "Window";

	snprintf(buffer, bufferSize, "Display %d", (int)i);
	return buffer;
}

static size_t getScalerPickerItemsCount(void)
{
	return (size_t)scalers_count;
}

static const char *getScalerPickerItem(size_t i, char *buffer, size_t bufferSize)
{
	(void)buffer, (void)bufferSize;

	return scalers[i].name;
}

static size_t getScalingModePickerItemsCount(void)
{
	return (size_t)ScalingMode_MAX;
}

static const char *getScalingModePickerItem(size_t i, char *buffer, size_t bufferSize)
{
	(void)buffer, (void)bufferSize;

	return scaling_mode_names[i];
}

typedef enum
{
	MENU_ITEM_NONE = 0,
	MENU_ITEM_DONE,
	MENU_ITEM_GRAPHICS,
	MENU_ITEM_SOUND,
	MENU_ITEM_JUKEBOX,
	MENU_ITEM_DESTRUCT,
	MENU_ITEM_GAMEPLAY,
	MENU_ITEM_SHOP_CONTENTS,
	MENU_ITEM_DISPLAY,
	MENU_ITEM_SCALER,
	MENU_ITEM_SCALING_MODE,
	MENU_ITEM_MUSIC_VOLUME,
	MENU_ITEM_SOUND_VOLUME,
} MenuItemId;

typedef enum
{
	MENU_NONE = 0,
	MENU_SETUP,
	MENU_GRAPHICS,
	MENU_SOUND,
	MENU_GAMEPLAY,
} MenuId;

typedef struct
{
	MenuItemId id;
	const char *name;
	const char *description;
	size_t (*getPickerItemsCount)(void);
	const char *(*getPickerItem)(size_t i, char *buffer, size_t bufferSize);
} MenuItem;

typedef struct
{
	const char *header;
	const MenuItem items[6];
} Menu;

typedef enum
{
	SETUP_INPUT_NONE,
	SETUP_INPUT_PREVIOUS,
	SETUP_INPUT_NEXT,
	SETUP_INPUT_BACK,
	SETUP_INPUT_ACTIVATE,
	SETUP_INPUT_SET_VOLUME,
	SETUP_INPUT_ADJUST_MUSIC_VOLUME,
	SETUP_INPUT_ADJUST_SOUND_VOLUME,
} SetupInputAction;

typedef struct
{
	SetupInputAction action;
	size_t hoveredIndex;
	int value;
} SetupInput;

static const Menu menus[] = {
	[MENU_SETUP] = {
		.header = "Setup",
		.items = {
			{ MENU_ITEM_GRAPHICS, "Graphics...", "Change the graphics settings." },
			{ MENU_ITEM_SOUND, "Sound...", "Change the sound settings." },
			{ MENU_ITEM_JUKEBOX, "Jukebox", "Listen to the music of Tyrian." },
			// { MENU_ITEM_DESTRUCT, "Destruct", "Play a bonus mini-game." },
			{ MENU_ITEM_GAMEPLAY, "Gameplay...", "Change gameplay settings." },
			{ MENU_ITEM_DONE, "Done", "Return to the main menu." },
			{ -1 }
		},
	},
	[MENU_GRAPHICS] = {
		.header = "Graphics",
		.items = {
			{ MENU_ITEM_DISPLAY, "Display:", "Change the display mode.", getDisplayPickerItemsCount, getDisplayPickerItem },
			{ MENU_ITEM_SCALER, "Scaler:", "Change the pixel art scaling algorithm.", getScalerPickerItemsCount, getScalerPickerItem },
			{ MENU_ITEM_SCALING_MODE, "Scaling Mode:", "Change the scaling mode.", getScalingModePickerItemsCount, getScalingModePickerItem },
			{ MENU_ITEM_DONE, "Done", "Return to the previous menu." },
			{ -1 }
		},
	},
	[MENU_SOUND] = {
		.header = "Sound",
		.items = {
			{ MENU_ITEM_MUSIC_VOLUME, "Music Volume", "Change volume with the left/right arrow keys." },
			{ MENU_ITEM_SOUND_VOLUME, "Sound Volume", "Change volume with the left/right arrow keys." },
			{ MENU_ITEM_DONE, "Done", "Return to the previous menu." },
			{ -1 }
		},
	},
	[MENU_GAMEPLAY] = {
		.header = "Gameplay",
		.items = {
			{ MENU_ITEM_SHOP_CONTENTS, "Shop Contents:", "Change how the Upgrade shop is filled.", getShopContentsPickerItemsCount, getShopContentsPickerItem },
			{ MENU_ITEM_DONE, "Done", "Return to the previous menu." },
			{ -1 }
		},
	},
};

typedef struct
{
	MenuId parents[COUNTOF(menus)];
	size_t selectedMenuItemIndexes[COUNTOF(menus)];
	MenuId currentMenu;
	MenuItemId currentPicker;
	size_t pickerSelectedIndex;
	bool restart;
} SetupMenuState;

static int draw_setup_menu(MenuId currentMenu, const size_t selectedMenuItemIndexes[],
                           MenuItemId currentPicker, size_t pickerSelectedIndex, char buffer[100], size_t *menuItemsCountOut)
{
	const int xCenter = 320 / 2;
	const int yMenuHeader = 4;
	const int xMenuItem = 45;
	const int xMenuItemName = xMenuItem;
	const int xMenuItemValue = 180;
	const int wMenuItemValue = 95;
	const int yMenuItems = 37;
	const int dyMenuItems = 21;
	const int dyPickerItem = 15;
	const int dyPickerItemPadding = 2;

	memcpy(VGAScreen->pixels, VGAScreen2->pixels, (size_t)VGAScreen->pitch * VGAScreen->h);

	const Menu *menu = &menus[currentMenu];
	const MenuItem *menuItems = menu->items;
	const size_t selectedMenuItemIndex = selectedMenuItemIndexes[currentMenu];
	int yPicker = 0;

	draw_font_hv_shadow(VGAScreen, xCenter, yMenuHeader, menu->header, large_font, centered, 15, -3, false, 2);

	size_t menuItemsCount = 0;
	for (size_t i = 0; menuItems[i].id != (MenuItemId)-1; ++i)
	{
		menuItemsCount += 1;
		const MenuItem *menuItem = &menuItems[i];
		const int y = yMenuItems + dyMenuItems * i;
		const bool selected = i == selectedMenuItemIndex;
		const bool disabled = currentPicker != MENU_ITEM_NONE && !selected;

		if (selected)
			yPicker = y;

		draw_font_hv_shadow(VGAScreen, xMenuItemName, y, menuItem->name, normal_font, left_aligned, 15, -3 + (selected ? 2 : 0) + (disabled ? -4 : 0), false, 2);

		switch (menuItem->id)
		{
		case MENU_ITEM_DISPLAY:
		{
			const char *value = "Window";
			if (fullscreen_display >= 0)
			{
				snprintf(buffer, 100, "Display %d", fullscreen_display + 1);
				value = buffer;
			}
			draw_font_hv_shadow(VGAScreen, xMenuItemValue, y, value, normal_font, left_aligned, 15, -3 + (selected ? 2 : 0) + (disabled ? -4 : 0), false, 2);
			break;
		}
		case MENU_ITEM_SCALER:
			draw_font_hv_shadow(VGAScreen, xMenuItemValue, y, scalers[scaler].name, normal_font, left_aligned, 15, -3 + (selected ? 2 : 0) + (disabled ? -4 : 0), false, 2);
			break;
		case MENU_ITEM_SCALING_MODE:
			draw_font_hv_shadow(VGAScreen, xMenuItemValue, y, scaling_mode_names[scaling_mode], normal_font, left_aligned, 15, -3 + (selected ? 2 : 0) + (disabled ? -4 : 0), false, 2);
			break;
		case MENU_ITEM_SHOP_CONTENTS:
			draw_font_hv_shadow(VGAScreen, xMenuItemValue, y, getShopContentsPickerItem((size_t)shopContentSetting, buffer, 100), normal_font, left_aligned, 15, -3 + (selected ? 2 : 0) + (disabled ? -4 : 0), false, 2);
			break;
		case MENU_ITEM_MUSIC_VOLUME:
			JE_barDrawShadow(VGAScreen, xMenuItemValue, y, 1, music_disabled ? 170 : 174, (tyrMusicVolume + 4) / 8, 2, 10);
			JE_rectangle(VGAScreen, xMenuItemValue - 2, y - 2, xMenuItemValue + 96, y + 11, 242);
			break;
		case MENU_ITEM_SOUND_VOLUME:
			JE_barDrawShadow(VGAScreen, xMenuItemValue, y, 1, samples_disabled ? 170 : 174, (fxVolume + 4) / 8, 2, 10);
			JE_rectangle(VGAScreen, xMenuItemValue - 2, y - 2, xMenuItemValue + 96, y + 11, 242);
			break;
		default:
			break;
		}
	}

	JE_textShade(VGAScreen, xMenuItemName, 190, menuItems[selectedMenuItemIndex].description, 15, 4, PART_SHADE);

	if (currentPicker != MENU_ITEM_NONE)
	{
		const MenuItem *selectedMenuItem = &menuItems[selectedMenuItemIndex];
		const size_t pickerItemsCount = selectedMenuItem->getPickerItemsCount();
		const int hPicker = dyPickerItem * pickerItemsCount - dyPickerItemPadding;

		yPicker = MIN(yPicker, 200 - 10 - (hPicker + 5 + 2));

		JE_rectangle(VGAScreen, xMenuItemValue - 5, yPicker - 3, xMenuItemValue + wMenuItemValue + 5 - 1, yPicker + hPicker + 3 - 1, 248);
		JE_rectangle(VGAScreen, xMenuItemValue - 4, yPicker - 4, xMenuItemValue + wMenuItemValue + 4 - 1, yPicker + hPicker + 4 - 1, 250);
		JE_rectangle(VGAScreen, xMenuItemValue - 3, yPicker - 5, xMenuItemValue + wMenuItemValue + 3 - 1, yPicker + hPicker + 5 - 1, 248);
		fill_rectangle_wh(VGAScreen, xMenuItemValue - 2, yPicker - 2, wMenuItemValue + 2 + 2, hPicker + 2 + 2, 224);

		for (size_t i = 0; i < pickerItemsCount; ++i)
		{
			const int y = yPicker + dyPickerItem * (int)i;
			const bool selected = i == pickerSelectedIndex;
			const char *value = selectedMenuItem->getPickerItem(i, buffer, 100);
			draw_font_hv_shadow(VGAScreen, xMenuItemValue, y, value, normal_font, left_aligned, 15, -3 + (selected ? 2 : 0), false, 2);
		}
	}

	*menuItemsCountOut = menuItemsCount;
	return yPicker;
}

static void wait_for_setup_input(int oldFullscreenDisplay, bool *mouseMoved)
{
	do
	{
		SDL_Delay(16);

		Uint16 oldMouseX = mouse_x;
		Uint16 oldMouseY = mouse_y;

		push_joysticks_as_keyboard();
		service_SDL_events(false);

		*mouseMoved = mouse_x != oldMouseX || mouse_y != oldMouseY;
	} while (!(newkey || newmouse || *mouseMoved || fullscreen_display != oldFullscreenDisplay));
}

static SetupInput collect_setup_menu_input(const SetupMenuState *state, const MenuItem *menuItems,
                                           size_t menuItemsCount, bool mouseMoved)
{
	const int xMenuItem = 45, xMenuItemValue = 180, wMenuItemValue = 95;
	const int yMenuItems = 37, dyMenuItems = 21, hMenuItem = 13;
	const size_t selectedIndex = state->selectedMenuItemIndexes[state->currentMenu];
	SetupInput input = { SETUP_INPUT_NONE, SIZE_MAX, 0 };

	if (mouseMoved || newmouse)
	{
		if (mouse_x >= xMenuItem && mouse_x < xMenuItem + 230)
		{
			for (size_t i = 0; i < menuItemsCount; ++i)
			{
				const int y = yMenuItems + dyMenuItems * i;
				if (mouse_y >= y && mouse_y < y + hMenuItem)
				{
					input.hoveredIndex = i;
					if (newmouse && lastmouse_but == SDL_BUTTON_LEFT &&
					    lastmouse_y >= y && lastmouse_y < y + hMenuItem)
					{
						if (lastmouse_x >= 45 && lastmouse_x < 180)
							input.action = SETUP_INPUT_ACTIVATE;
						else if (lastmouse_x >= xMenuItemValue && lastmouse_x < xMenuItemValue + wMenuItemValue)
						{
							switch (menuItems[i].id)
							{
							case MENU_ITEM_SHOP_CONTENTS:
							case MENU_ITEM_DISPLAY:
							case MENU_ITEM_SCALER:
							case MENU_ITEM_SCALING_MODE:
								input.action = SETUP_INPUT_ACTIVATE;
								break;
							case MENU_ITEM_MUSIC_VOLUME:
								input.action = SETUP_INPUT_SET_VOLUME;
								input.value = (lastmouse_x - xMenuItemValue) * 255 / (wMenuItemValue - 1);
								break;
							case MENU_ITEM_SOUND_VOLUME:
								input.action = SETUP_INPUT_SET_VOLUME;
								input.value = -((lastmouse_x - xMenuItemValue) * 255 / (wMenuItemValue - 1) + 1);
								break;
							default:
								break;
							}
						}
					}
					break;
				}
			}
		}
	}

	if (newmouse)
	{
		if (lastmouse_but == SDL_BUTTON_RIGHT)
			input.action = SETUP_INPUT_BACK;
	}
	else if (newkey)
	{
		const size_t activeIndex = input.hoveredIndex == SIZE_MAX ? selectedIndex : input.hoveredIndex;
		switch (lastkey_scan)
		{
		case SDL_SCANCODE_UP: input.action = SETUP_INPUT_PREVIOUS; break;
		case SDL_SCANCODE_DOWN: input.action = SETUP_INPUT_NEXT; break;
		case SDL_SCANCODE_LEFT:
			if (menuItems[activeIndex].id == MENU_ITEM_MUSIC_VOLUME)
			{
				input.action = SETUP_INPUT_ADJUST_MUSIC_VOLUME;
				input.value = -8;
			}
			else if (menuItems[activeIndex].id == MENU_ITEM_SOUND_VOLUME)
			{
				input.action = SETUP_INPUT_ADJUST_SOUND_VOLUME;
				input.value = -8;
			}
			break;
		case SDL_SCANCODE_RIGHT:
			if (menuItems[activeIndex].id == MENU_ITEM_MUSIC_VOLUME)
			{
				input.action = SETUP_INPUT_ADJUST_MUSIC_VOLUME;
				input.value = 8;
			}
			else if (menuItems[activeIndex].id == MENU_ITEM_SOUND_VOLUME)
			{
				input.action = SETUP_INPUT_ADJUST_SOUND_VOLUME;
				input.value = 8;
			}
			break;
		case SDL_SCANCODE_SPACE:
		case SDL_SCANCODE_RETURN:
		case SDL_SCANCODE_KP_ENTER: input.action = SETUP_INPUT_ACTIVATE; break;
		case SDL_SCANCODE_ESCAPE: input.action = SETUP_INPUT_BACK; break;
		default: break;
		}
	}

	return input;
}

static SetupInput collect_setup_picker_input(const MenuItem *item, int yPicker, bool mouseMoved)
{
	const int xValue = 180, wValue = 95, dyItem = 15, hItem = 13;
	const size_t count = item->getPickerItemsCount();
	SetupInput input = { SETUP_INPUT_NONE, SIZE_MAX, 0 };

	if (mouseMoved || newmouse)
	{
		if (mouse_x >= xValue && mouse_x < xValue + wValue)
		{
			for (size_t i = 0; i < count; ++i)
			{
				const int y = yPicker + dyItem * i;
				if (mouse_y >= y && mouse_y < y + hItem)
				{
					input.hoveredIndex = i;
					if (newmouse && lastmouse_but == SDL_BUTTON_LEFT &&
					    lastmouse_x >= xValue && lastmouse_y < xValue + 135 &&
					    lastmouse_y >= y && lastmouse_y < y + hItem)
						input.action = SETUP_INPUT_ACTIVATE;
				}
			}
		}
	}

	if (newmouse)
	{
		if (lastmouse_but == SDL_BUTTON_RIGHT)
			input.action = SETUP_INPUT_BACK;
	}
	else if (newkey)
	{
		switch (lastkey_scan)
		{
		case SDL_SCANCODE_UP: input.action = SETUP_INPUT_PREVIOUS; break;
		case SDL_SCANCODE_DOWN: input.action = SETUP_INPUT_NEXT; break;
		case SDL_SCANCODE_SPACE:
		case SDL_SCANCODE_RETURN:
		case SDL_SCANCODE_KP_ENTER: input.action = SETUP_INPUT_ACTIVATE; break;
		case SDL_SCANCODE_ESCAPE: input.action = SETUP_INPUT_BACK; break;
		default: break;
		}
	}

	return input;
}

static void apply_setup_picker_action(SetupMenuState *state, const MenuItem *item, const SetupInput *input)
{
	const size_t count = item->getPickerItemsCount();

	if (input->hoveredIndex != SIZE_MAX)
	{
		if (state->pickerSelectedIndex != input->hoveredIndex)
		{
			JE_playSampleNum(S_CURSOR);
			state->pickerSelectedIndex = input->hoveredIndex;
		}
	}

	switch (input->action)
	{
	case SETUP_INPUT_PREVIOUS:
		JE_playSampleNum(S_CURSOR);
		state->pickerSelectedIndex = state->pickerSelectedIndex == 0 ? count - 1 : state->pickerSelectedIndex - 1;
		break;
	case SETUP_INPUT_NEXT:
		JE_playSampleNum(S_CURSOR);
		state->pickerSelectedIndex = state->pickerSelectedIndex == count - 1 ? 0 : state->pickerSelectedIndex + 1;
		break;
	case SETUP_INPUT_BACK:
		JE_playSampleNum(S_SPRING);
		state->currentPicker = MENU_ITEM_NONE;
		break;
	case SETUP_INPUT_ACTIVATE:
		JE_playSampleNum(S_CLICK);
		if (item->id == MENU_ITEM_DISPLAY && (int)state->pickerSelectedIndex - 1 != fullscreen_display)
			reinit_fullscreen((int)state->pickerSelectedIndex - 1);
		else if (item->id == MENU_ITEM_SCALER && state->pickerSelectedIndex != scaler)
		{
			const int oldScaler = scaler;
			if (!init_scaler(state->pickerSelectedIndex) && !init_scaler(oldScaler))
				exit(EXIT_FAILURE);
		}
		else if (item->id == MENU_ITEM_SCALING_MODE)
			scaling_mode = state->pickerSelectedIndex;
		else if (item->id == MENU_ITEM_SHOP_CONTENTS)
		{
			shopContentSetting = (int)state->pickerSelectedIndex;
			JE_saveConfiguration();
		}
		state->currentPicker = MENU_ITEM_NONE;
		break;
	default:
		break;
	}
}

static void apply_setup_menu_activation(SetupMenuState *state, MenuItemId itemId)
{
	switch (itemId)
	{
	case MENU_ITEM_DONE:
		JE_playSampleNum(S_SELECT);
		state->currentMenu = state->parents[state->currentMenu];
		break;
	case MENU_ITEM_GRAPHICS:
	case MENU_ITEM_SOUND:
		JE_playSampleNum(S_SELECT);
		state->parents[itemId == MENU_ITEM_GRAPHICS ? MENU_GRAPHICS : MENU_SOUND] = state->currentMenu;
		state->currentMenu = itemId == MENU_ITEM_GRAPHICS ? MENU_GRAPHICS : MENU_SOUND;
		state->selectedMenuItemIndexes[state->currentMenu] = 0;
		break;
	case MENU_ITEM_GAMEPLAY:
		JE_playSampleNum(S_SELECT);
		state->parents[MENU_GAMEPLAY] = state->currentMenu;
		state->currentMenu = MENU_GAMEPLAY;
		state->selectedMenuItemIndexes[state->currentMenu] = 0;
		break;
	case MENU_ITEM_JUKEBOX:
		JE_playSampleNum(S_SELECT);
		fade_black(10);
		jukebox();
		state->restart = true;
		break;
	case MENU_ITEM_DESTRUCT:
		JE_playSampleNum(S_SELECT);
		fade_black(10);
		JE_destructGame();
		state->restart = true;
		break;
	case MENU_ITEM_DISPLAY:
	case MENU_ITEM_SCALER:
	case MENU_ITEM_SCALING_MODE:
		JE_playSampleNum(S_CLICK);
		state->currentPicker = itemId;
		state->pickerSelectedIndex = itemId == MENU_ITEM_DISPLAY ? (size_t)(fullscreen_display + 1)
			: itemId == MENU_ITEM_SCALER ? scaler : scaling_mode;
		break;
	case MENU_ITEM_SHOP_CONTENTS:
		JE_playSampleNum(S_CLICK);
		state->currentPicker = itemId;
		state->pickerSelectedIndex = shopContentSetting >= 0 && shopContentSetting < 3 ? (size_t)shopContentSetting : 0;
		break;
	case MENU_ITEM_MUSIC_VOLUME:
		JE_playSampleNum(S_CLICK);
		music_disabled = !music_disabled;
		if (!music_disabled)
			restart_song();
		break;
	case MENU_ITEM_SOUND_VOLUME:
		samples_disabled = !samples_disabled;
		JE_playSampleNum(S_CLICK);
		break;
	default:
		break;
	}
}

static void apply_setup_menu_action(SetupMenuState *state, const MenuItem *items, size_t count,
                                    const SetupInput *input)
{
	size_t *const selectedIndex = &state->selectedMenuItemIndexes[state->currentMenu];

	if (input->hoveredIndex != SIZE_MAX && *selectedIndex != input->hoveredIndex)
	{
		JE_playSampleNum(S_CURSOR);
		*selectedIndex = input->hoveredIndex;
	}

	switch (input->action)
	{
	case SETUP_INPUT_PREVIOUS:
		JE_playSampleNum(S_CURSOR);
		*selectedIndex = *selectedIndex == 0 ? count - 1 : *selectedIndex - 1;
		break;
	case SETUP_INPUT_NEXT:
		JE_playSampleNum(S_CURSOR);
		*selectedIndex = *selectedIndex == count - 1 ? 0 : *selectedIndex + 1;
		break;
	case SETUP_INPUT_BACK:
		JE_playSampleNum(S_SPRING);
		state->currentMenu = state->parents[state->currentMenu];
		break;
	case SETUP_INPUT_ACTIVATE:
		apply_setup_menu_activation(state, items[*selectedIndex].id);
		break;
	case SETUP_INPUT_SET_VOLUME:
		if (input->value >= 0)
		{
			JE_playSampleNum(S_CURSOR);
			tyrMusicVolume = MIN(MAX(0, input->value), 255);
		}
		else
		{
			fxVolume = MIN(MAX(0, -input->value - 1), 255);
			JE_playSampleNum(S_CURSOR);
		}
		set_volume(tyrMusicVolume, fxVolume);
		break;
	case SETUP_INPUT_ADJUST_MUSIC_VOLUME:
		JE_playSampleNum(S_CURSOR);
		JE_changeVolume(&tyrMusicVolume, input->value, &fxVolume, 0);
		break;
	case SETUP_INPUT_ADJUST_SOUND_VOLUME:
		JE_changeVolume(&tyrMusicVolume, 0, &fxVolume, input->value);
		JE_playSampleNum(S_CURSOR);
		break;
	default:
		break;
	}

	if (state->currentMenu == MENU_NONE)
		fade_black(10);
}

void setupMenu(void)
{

	char buffer[100];

	if (shopSpriteSheet.data == NULL)
		JE_loadCompShapes(&shopSpriteSheet, '1');  // need mouse pointer sprites

	SetupMenuState state = {
		.currentMenu = MENU_SETUP,
		.currentPicker = MENU_ITEM_NONE,
		.restart = true,
	};

	for (; ; )
	{
		if (state.restart)
		{
			JE_loadPic(VGAScreen2, 2, false);
			fill_rectangle_wh(VGAScreen2, 0, 192, 320, 8, 0);
		}

		const Menu *menu = &menus[state.currentMenu];
		const MenuItem *const menuItems = menu->items;
		size_t menuItemsCount;
		int yPicker = draw_setup_menu(state.currentMenu, state.selectedMenuItemIndexes, state.currentPicker,
		                               state.pickerSelectedIndex, buffer, &menuItemsCount);

		if (state.restart)
		{
			mouseCursor = MOUSE_POINTER_NORMAL;

			fade_palette(colors, 10, 0, 255);

			state.restart = false;
		}

		service_SDL_events(true);

		JE_mouseStart();
		JE_showVGA();
		JE_mouseReplace();

		bool mouseMoved = false;
		wait_for_setup_input(fullscreen_display, &mouseMoved);

		if (state.currentPicker == MENU_ITEM_NONE)
		{
			SetupInput input = collect_setup_menu_input(&state, menuItems, menuItemsCount, mouseMoved);
			apply_setup_menu_action(&state, menuItems, menuItemsCount, &input);
			if (state.currentMenu == MENU_NONE)
				return;
		}
		else
		{
			const MenuItem *selectedMenuItem = &menuItems[state.selectedMenuItemIndexes[state.currentMenu]];
			SetupInput input = collect_setup_picker_input(selectedMenuItem, yPicker, mouseMoved);
			apply_setup_picker_action(&state, selectedMenuItem, &input);
		}
	}
}

#ifndef DATA_EXTRACTOR
int main(int argc, char *argv[])
{
	mt_srand(time(NULL));

	printf("\nWelcome to... >> %s %s <<\n\n", opentyrian_str, opentyrian_version);

	printf("Copyright (C) 2022 The OpenTyrian Development Team\n\n");

	printf("This program comes with ABSOLUTELY NO WARRANTY.\n");
	printf("This is free software, and you are welcome to redistribute it\n");
	printf("under certain conditions.  See the file COPYING for details.\n\n");

	if (SDL_Init(0))
	{
		printf("Failed to initialize SDL: %s\n", SDL_GetError());
		return -1;
	}

	JE_loadConfiguration();

	xmas = xmas_time();  // arg handler may override

	JE_paramCheck(argc, argv);

	JE_scanForEpisodes();

	load_items();

	init_video();
	init_keyboard();
	init_joysticks();
	printf("assuming mouse detected\n"); // SDL can't tell us if there isn't one

	if (xmas && (!dir_file_exists(data_dir(), "tyrianc.shp") || !dir_file_exists(data_dir(), "voicesc.snd")))
	{
		xmas = false;

		fprintf(stderr, "warning: Christmas is missing.\n");
	}

	JE_loadPals();
	JE_loadMainShapeTables(xmas ? "tyrianc.shp" : "tyrian.shp");

	if (xmas && !xmas_prompt())
	{
		xmas = false;

		free_main_shape_tables();
		JE_loadMainShapeTables("tyrian.shp");
	}

	/* Default Options */
	youAreCheating = false;
	smoothScroll = true;
	loadDestruct = false;

	if (!audio_disabled)
	{
		printf("initializing SDL audio...\n");

		init_audio();

		load_music();

		loadSndFile(xmas);
	}
	else
	{
		printf("audio disabled\n");
	}

	if (record_demo)
		printf("demo recording enabled (input limited to keyboard)\n");

	JE_loadExtraShapes();  /*Editship*/

	JE_loadHelpText();
	/*debuginfo("Help text complete");*/

	if (isNetworkGame)
	{
#ifdef WITH_NETWORK
		if (network_init())
		{
			network_tyrian_halt(3, false);
		}
#else
		fprintf(stderr, "OpenTyrian was compiled without networking support.");
		JE_tyrianHalt(5);
#endif
	}

#ifdef NDEBUG
	if (!isNetworkGame)
		intro_logos();
#endif

	for (; ; )
	{
		JE_initPlayerData();
		JE_sortHighScores();

		play_demo = false;
		stopped_demo = false;

		gameLoaded = false;
		jumpSection = false;

#ifdef WITH_NETWORK
		if (isNetworkGame)
		{
			networkStartScreen();
		}
		else
#endif
		{
			if (!titleScreen())
			{
				// Player quit from title screen.
				break;
			}
		}

		if (loadDestruct)
		{
			JE_destructGame();

			loadDestruct = false;
		}
		else
		{
			JE_main();

			if (trentWin)
			{
				// Player beat SuperTyrian.
				break;
			}
		}
	}

	JE_tyrianHalt(0);

	return 0;
}
#endif
