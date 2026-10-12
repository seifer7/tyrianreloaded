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

#pragma warning(disable : 5105)

#include "episodes.h"

#include "config.h"
#include "varz.h"
#include "file.h"
#include "tinydir.h"
#include "lvllib.h"
#include "lvlmast.h"
#include "opentyr.h"
#include "mtrand.h"

#include <errno.h>
#include <ctype.h>
#include <limits.h>
#include <stdarg.h>
#include <sys/stat.h>
#ifdef TARGET_WIN32
#include <direct.h>
#endif

/* MAIN Weapons Data */
JE_WeaponPortType *weaponPort;
JE_WeaponPortTypeOld oldWeaponPort[PORT_NUM + 1];
JE_WeaponType     *weapons;
JE_WeaponTypeOld     oldWeapons[WEAP_NUM + 1]; /* [0..weapnum] */

/* Items */
JE_PowerType   *powerSys;
JE_ShipType    *ships;
JE_OptionType  *options;
JE_ShieldType  *shields;
JE_SpecialType *special;

JE_PowerTypeOld   oldPowerSys[POWER_NUM + 1];
JE_ShipTypeOld    oldShips[SHIP_NUM + 1];
JE_OptionTypeOld  oldOptions[OPTION_NUM + 1]; /* [0..optionnum] */
JE_ShieldTypeOld  oldShields[SHIELD_NUM + 1];
JE_SpecialTypeOld oldSpecial[SPECIAL_NUM + 1];

/* Enemy data */
JE_EnemyDatTypeOld oldEnemyDat;
JE_EnemyDatType *enemyDat;

/* EPISODE variables */
JE_byte    initial_episode_num, episodeNum = 0;
JE_boolean episodeAvail[EPISODE_MAX]; /* [1..episodemax] */
char       episode_file[13], cube_file[13];

JE_longint episode1DataLoc;

static bool loading_item_data;
static const char *loading_item_path;
static size_t loading_item_line;

static void item_data_error(const char *path, size_t line, const char *format, ...)
{
	fprintf(stderr, "error: %s:%zu: ", path, line);
	va_list args;
	va_start(args, format);
	vfprintf(stderr, format, args);
	va_end(args);
	fprintf(stderr, "\nRun the Data Extractor executable first to build the required extracted data files.\n"
	                "If using mods, check the referenced definitions in your .tyrr files.\n");
	SDL_Quit();
	exit(EXIT_FAILURE);
}

/* Tells the game whether the level currently loaded is a bonus level. */
JE_boolean bonusLevel;

/* Tells if the game jumped back to Episode 1 */
JE_boolean jumpBackToEpisode1;

bool value_in_array_je_byte(int val, JE_byte* arr, size_t n) {
	for (size_t i = 0; i < n; i++) {
		if (arr[i] == val)
			return true;
	}
	return false;
}

bool add_random_shop_item(const enum ITEM_TYPE* item_type, JE_byte* itemAvail, int index, const size_t itemAvailSize) {
	int itemCount = 0;

	switch (*item_type) {
	case ItemType_Ship:
		itemCount = ships[0].count;
		break;
	case ItemType_WeaponFront:
	case ItemType_WeaponRear:
		itemCount = weaponPort[0].count;
		break;
	case ItemType_Generator:
		itemCount = powerSys[0].count;
		break;
	case ItemType_SidekickLeft:
	case ItemType_SidekickRight:
		itemCount = options[0].count;
		break;
	case ItemType_Shield:
		itemCount = shields[0].count;
		break;
	default:
		return false;
	}

	int i = 0;
	int ii = 0;
	int itemsListCount = 0;
	JE_byte *itemList;
	itemList = (JE_byte*) malloc(sizeof(JE_byte) * itemCount);
	memset(itemList, 0, sizeof(JE_byte) * itemCount);

	switch (*item_type) {
	case ItemType_Ship:
		for (; ii < itemCount; ii++) {
			if (strcmp(ships[ii].id, "0") == 0) continue;
			if (strcmp(ships[ii].id, "") == 0) break;
			if (value_in_array_je_byte(ii, itemAvail, itemAvailSize)) continue;
			int probability = ships[ii].probability;
			int randomNum = (mt_rand() % 100);
			if (!(randomNum < probability)) continue;
			itemList[i++] = ii;
			itemsListCount++;
		}
		break;
	case ItemType_WeaponFront:
	case ItemType_WeaponRear:
		for (; ii < itemCount; ii++) {
			if (strcmp(weaponPort[ii].id, "0") == 0) continue;
			if (strcmp(weaponPort[ii].id, "") == 0) break;
			if (*item_type == ItemType_WeaponRear && weaponPort[ii].position != 1) continue;
			if (*item_type == ItemType_WeaponFront && weaponPort[ii].position == 1) continue;
			if (value_in_array_je_byte(ii, itemAvail, itemAvailSize)) continue;
			int probability = weaponPort[ii].probability;
			int randomNum = (mt_rand() % 100);
			if (!(randomNum < probability)) continue;
			itemList[i++] = ii;
			itemsListCount++;
		}
		break;
	case ItemType_Generator:
		for (; ii < itemCount; ii++) {
			if (strcmp(powerSys[ii].id, "0") == 0) continue;
			if (strcmp(powerSys[ii].id, "") == 0) break;
			if (value_in_array_je_byte(ii, itemAvail, itemAvailSize)) continue;
			int probability = powerSys[ii].probability;
			int randomNum = (mt_rand() % 100);
			if (!(randomNum < probability)) continue;
			itemList[i++] = ii;
			itemsListCount++;
		}
		break;
	case ItemType_SidekickLeft:
	case ItemType_SidekickRight:
		for (; ii < itemCount; ii++) {
			if (strcmp(options[ii].id, "0") == 0) continue;
			if (strcmp(options[ii].id, "") == 0) break;
			if (*item_type == ItemType_SidekickLeft && options[ii].position == 2) continue;
			if (*item_type == ItemType_SidekickRight && options[ii].position == 1) continue;
			if (value_in_array_je_byte(ii, itemAvail, itemAvailSize)) continue;
			int probability = options[ii].probability;
			int randomNum = (mt_rand() % 100);
			if (!(randomNum < probability)) continue;
			itemList[i++] = ii;
			itemsListCount++;
		}
		break;
	case ItemType_Shield:
		for (; ii < itemCount; ii++) {
			if (strcmp(shields[ii].id, "0") == 0) continue;
			if (strcmp(shields[ii].id, "") == 0) break;
			if (value_in_array_je_byte(ii, itemAvail, itemAvailSize)) continue;
			int probability = shields[ii].probability;
			int randomNum = (mt_rand() % 100);
			if (!(randomNum < probability)) continue;
			itemList[i++] = ii;
			itemsListCount++;
		}
		break;
	default:
		return false;
	}

	if (itemsListCount == 0) return false;

	int num = (mt_rand() % itemsListCount);
	itemAvail[index] = itemList[num];
	return true;
}

int item_idx_from_jebyte(const enum ITEM_TYPE* item_type, const JE_byte* id) {
	char c[11] = "";
	sprintf(c, "%d", *id);
	return item_idx(item_type, c);
}

int item_idx_from_int(const enum ITEM_TYPE* item_type, const int* id) {
	char c[11] = "";
	sprintf(c, "%d", *id);
	return item_idx(item_type, c);
}

int item_idx(const enum ITEM_TYPE* item_type, const char* id) {
	int index = -1;
	get_item_index(item_type, id, &index);
	return (int) index;
}

bool get_item_index_from_int(const enum ITEM_TYPE* item_type, const int* id, int* output) {
	char c[11] = "";
	sprintf(c, "%d", *id);
	return get_item_index(item_type, c, output);
}

bool get_item_index(const enum ITEM_TYPE* item_type, const char* id, int* output) {
	int index = -1;
	int i = -1;
	bool end = false;
	for (;;) {
		switch (*item_type) {
		case ItemType_Shot:
			if (i == -1) i = weapons[0].count-1;
			if (strcmp(weapons[i].id, id) == 0) index = i;
			break;
		case ItemType_Ship:
			if (i == -1) i = ships[0].count-1;
			if (strcmp(ships[i].id, id) == 0) index = i;
			break;
		case ItemType_WeaponFront:
		case ItemType_WeaponRear:
			if (i == -1) i = weaponPort[0].count-1;
			if (strcmp(weaponPort[i].id, id) == 0) index = i;
			break;
		case ItemType_Generator:
			if (i == -1) i = powerSys[0].count-1;
			if (strcmp(powerSys[i].id, id) == 0) index = i;
			break;
		case ItemType_Shield:
			if (i == -1) i = shields[0].count-1;
			if (strcmp(shields[i].id, id) == 0) index = i;
			break;
		case ItemType_SidekickLeft:
		case ItemType_SidekickRight:
			if (options[0].count == 0) index = 0;
			if (i == -1) i = options[0].count-1;
			if (strcmp(options[i].id, id) == 0) index = i;
			break;
		case ItemType_Special:
			if (i == -1) i = special[0].count-1;
			if (strcmp(special[i].id, id) == 0) index = i;
			break;
		case ItemType_Enemy:
			if (i == -1) i = enemyDat[0].count-1;
			if (strcmp(enemyDat[i].id, id) == 0) index = i;
			break;
		default:
			end = true;
			break;
		}
		if (end == true || index != -1 || i == 0 || i == -1) break;
		i--;
	}
	if (index != -1) {
		*output = index;
		return true;
	}
	if (loading_item_data)
	{
		item_data_error(loading_item_path, loading_item_line,
		                "required extracted item ID '%s' (type %d) is missing", id, *item_type);
	}
	return false;
}

static void apply_item_field(enum ITEM_TYPE item_type, size_t item_i, const char *key, char value[255])
{
		char* tempptr;
		switch (item_type) {
			case ItemType_Ship:
				if (strcmp(key, "id") == 0) {
					memcpy(ships[item_i].id, value, sizeof(ships[item_i].id)-1);
				}
				else if (strcmp(key, "name") == 0) {
					memcpy(ships[item_i].name, value, sizeof(ships[item_i].name)-1);
				}
				else if (strcmp(key, "shipgraphic") == 0) {
					ships[item_i].shipgraphic = atoi(value);
				}
				else if (strcmp(key, "itemgraphic") == 0) {
					ships[item_i].itemgraphic = atoi(value);
				}
				else if (strcmp(key, "ani") == 0) {
					ships[item_i].ani = atoi(value);
				}
				else if (strcmp(key, "spd") == 0) {
					ships[item_i].spd = atoi(value);
				}
				else if (strcmp(key, "dmg") == 0) {
					ships[item_i].dmg = atoi(value);
				}
				else if (strcmp(key, "cost") == 0) {
					ships[item_i].cost = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphic") == 0) {
					ships[item_i].bigshipgraphic = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphx") == 0) {
					ships[item_i].bigshipgraphx = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphy") == 0) {
					ships[item_i].bigshipgraphy = atoi(value);
				}
				else if (strcmp(key, "probability") == 0) {
					ships[item_i].probability = atoi(value);
				}
			break;
			case ItemType_WeaponFront:
			case ItemType_WeaponRear:
				if (strcmp(key, "id") == 0) {
					memcpy(weaponPort[item_i].id, value, sizeof(weaponPort[item_i].id) - 1);
				}
				else if (strcmp(key, "name") == 0) {
					memcpy(weaponPort[item_i].name, value, sizeof(weaponPort[item_i].name) - 1);
				}
				else if (strcmp(key, "opnum") == 0) {
					weaponPort[item_i].opnum = atoi(value);
				}
				else if (strcmp(key, "op0") == 0) {
					char tempC[11];
					enum ITEM_TYPE shotItemType = ItemType_Shot;
					int j = 0;
					while (j < COUNTOF(weaponPort[item_i].op[0]) && str_pop_str(value, tempC, sizeof(tempC)))
						weaponPort[item_i].op[0][j++] = item_idx(&shotItemType, tempC);
				}
				else if (strcmp(key, "op1") == 0) {
					char tempC[11];
					enum ITEM_TYPE shotItemType = ItemType_Shot;
					int j = 0;
					while (j < COUNTOF(weaponPort[item_i].op[1]) && str_pop_str(value, tempC, sizeof(tempC)))
						weaponPort[item_i].op[1][j++] = item_idx(&shotItemType, tempC);
				}
				else if (strcmp(key, "cost") == 0) {
					weaponPort[item_i].cost = strtol(value, &tempptr, 10);
				}
				else if (strcmp(key, "itemgraphic") == 0) {
					weaponPort[item_i].itemgraphic = atoi(value);
				}
				else if (strcmp(key, "poweruse") == 0) {
					weaponPort[item_i].poweruse = atoi(value);
				}
				else if (strcmp(key, "position") == 0) {
					weaponPort[item_i].position = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphic") == 0) {
					weaponPort[item_i].bigshipgraphic = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphfrntx") == 0) {
					weaponPort[item_i].bigshipgraphfrntx = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphfrnty") == 0) {
					weaponPort[item_i].bigshipgraphfrnty = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphrearx") == 0) {
					weaponPort[item_i].bigshipgraphrearx = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphreary") == 0) {
					weaponPort[item_i].bigshipgraphreary = atoi(value);
				}
				else if (strcmp(key, "linkgunweapon") == 0) {
					memcpy(weaponPort[item_i].linkgunweaponId, value, sizeof(weaponPort[item_i].linkgunweaponId) - 1);
				}
				else if (strcmp(key, "chargegunweapons") == 0) {
					char tempC[11];
					int j = 0;
					while (j < COUNTOF(weaponPort[item_i].chargegunweaponIds) && str_pop_str(value, tempC, sizeof(tempC)))
					{
						strcpy(weaponPort[item_i].chargegunweaponIds[j], tempC);
						j++;
					}
				}
				else if (strcmp(key, "probability") == 0) {
					weaponPort[item_i].probability = atoi(value);
				}
				break;
			case ItemType_Generator:
				if (strcmp(key, "id") == 0) {
					memcpy(powerSys[item_i].id, value, sizeof(powerSys[item_i].id)-1);
				}
				else if (strcmp(key, "name") == 0) {
					memcpy(powerSys[item_i].name, value, sizeof(powerSys[item_i].name)-1);
				}
				else if (strcmp(key, "itemgraphic") == 0) {
					powerSys[item_i].itemgraphic = atoi(value);
				}
				else if (strcmp(key, "power") == 0) {
					powerSys[item_i].power = atoi(value);
				}
				else if (strcmp(key, "speed") == 0) {
					powerSys[item_i].speed = atoi(value);
				}
				else if (strcmp(key, "cost") == 0) {
					powerSys[item_i].cost = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphic") == 0) {
					powerSys[item_i].bigshipgraphic = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphx") == 0) {
					powerSys[item_i].bigshipgraphx = atoi(value);
				}
				else if (strcmp(key, "bigshipgraphy") == 0) {
					powerSys[item_i].bigshipgraphy = atoi(value);
				}
				else if (strcmp(key, "probability") == 0) {
					powerSys[item_i].probability = atoi(value);
				}
			break;
			case ItemType_Shield:
				if (strcmp(key, "id") == 0) {
					memcpy(shields[item_i].id, value, sizeof(shields[item_i].id)-1);
				}
				else if (strcmp(key, "name") == 0) {
					memcpy(shields[item_i].name, value, sizeof(shields[item_i].name)-1);
				}
				else if (strcmp(key, "itemgraphic") == 0) {
					shields[item_i].itemgraphic = atoi(value);
				}
				else if (strcmp(key, "tpwr") == 0) {
					shields[item_i].tpwr = atoi(value);
				}
				else if (strcmp(key, "mpwr") == 0) {
					shields[item_i].mpwr = atoi(value);
				}
				else if (strcmp(key, "cost") == 0) {
					shields[item_i].cost = atoi(value);
				}
				else if (strcmp(key, "probability") == 0) {
					shields[item_i].probability = atoi(value);
				}
				break;
			case ItemType_SidekickLeft:
			case ItemType_SidekickRight:
				if (strcmp(key, "id") == 0) {
					memcpy(options[item_i].id, value, sizeof(options[item_i].id) - 1);
				}
				else if (strcmp(key, "name") == 0) {
					memcpy(options[item_i].name, value, sizeof(options[item_i].name) - 1);
				}
				else if (strcmp(key, "itemgraphic") == 0) {
					options[item_i].itemgraphic = atoi(value);
				}
				else if (strcmp(key, "pwr") == 0) {
					options[item_i].pwr = atoi(value);
				}
				else if (strcmp(key, "cost") == 0) {
					options[item_i].cost = atoi(value);
				}
				else if (strcmp(key, "tr") == 0) {
					options[item_i].tr = atoi(value);
				}
				else if (strcmp(key, "option") == 0) {
					options[item_i].option = atoi(value);
				}
				else if (strcmp(key, "opspd") == 0) {
					options[item_i].opspd = atoi(value);
				}
				else if (strcmp(key, "ani") == 0) {
					options[item_i].ani = atoi(value);
				}
				else if (strcmp(key, "gr0") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(options[item_i].gr[0]))
						options[item_i].gr[0][j++] = temp;
				}
				else if (strcmp(key, "gr1") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(options[item_i].gr[1]))
						options[item_i].gr[1][j++] = temp;
				}
				else if (strcmp(key, "gr2") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(options[item_i].gr[2]))
						options[item_i].gr[2][j++] = temp;
				}
				else if (strcmp(key, "gr3") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(options[item_i].gr[3]))
						options[item_i].gr[3][j++] = temp;
				}
				else if (strcmp(key, "gr4") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(options[item_i].gr[4]))
						options[item_i].gr[4][j++] = temp;
				}
				else if (strcmp(key, "wport") == 0) {
					memcpy(options[item_i].wportId, value, sizeof(options[item_i].wportId) - 1);
				}
				else if (strcmp(key, "wpnum") == 0) {
					enum ITEM_TYPE tempItemType = ItemType_Shot;
					options[item_i].wpnum = item_idx(&tempItemType, value);
				}
				else if (strcmp(key, "ammo") == 0) {
					options[item_i].ammo = atoi(value);
				}
				else if (strcmp(key, "stop") == 0) {
					options[item_i].stop = atoi(value);
				}
				else if (strcmp(key, "icongr") == 0) {
					options[item_i].icongr = atoi(value);
				}
				else if (strcmp(key, "position") == 0) {
					options[item_i].position = atoi(value);
				}
				else if (strcmp(key, "probability") == 0) {
					options[item_i].probability = atoi(value);
				}
				break;
			case ItemType_Special:
				if (strcmp(key, "id") == 0) {
					memcpy(special[item_i].id, value, sizeof(special[item_i].id) - 1);
				}
				else if (strcmp(key, "name") == 0) {
					memcpy(special[item_i].name, value, sizeof(special[item_i].name) - 1);
				}
				else if (strcmp(key, "itemgraphic") == 0) {
					special[item_i].itemgraphic = atoi(value);
				}
				else if (strcmp(key, "pwr") == 0) {
					special[item_i].pwr = atoi(value);
				}
				else if (strcmp(key, "stype") == 0) {
					special[item_i].stype = atoi(value);
				}
				else if (strcmp(key, "wpn") == 0) {
					enum ITEM_TYPE tempItemType = ItemType_Shot;
					special[item_i].wpn = item_idx(&tempItemType, value);
				}
				break;
			case ItemType_Shot:
				if (strcmp(key, "id") == 0) {
					memcpy(weapons[item_i].id, value, sizeof(weapons[item_i].id) - 1);
				}
				else if (strcmp(key, "drain") == 0) {
					weapons[item_i].drain = atoi(value);
				}
				else if (strcmp(key, "shotrepeat") == 0) {
					weapons[item_i].shotrepeat = atoi(value);
				}
				else if (strcmp(key, "multi") == 0) {
					weapons[item_i].multi = atoi(value);
				}
				else if (strcmp(key, "weapani") == 0) {
					weapons[item_i].weapani = atoi(value);
				}
				else if (strcmp(key, "max") == 0) {
					weapons[item_i].max = atoi(value);
				}
				else if (strcmp(key, "tx") == 0) {
					weapons[item_i].tx = atoi(value);
				}
				else if (strcmp(key, "ty") == 0) {
					weapons[item_i].ty = atoi(value);
				}
				else if (strcmp(key, "aim") == 0) {
					weapons[item_i].aim = atoi(value);
				}
				else if (strcmp(key, "attack") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].attack))
						weapons[item_i].attack[j++] = temp;
				}
				else if (strcmp(key, "chain") == 0) {
					int j = 0;
					char tempC[11];
					while (j < COUNTOF(weapons[item_i].chainIds) && str_pop_str(value, tempC, sizeof(tempC)))
					{
						memcpy(weapons[item_i].chainIds[j], tempC, sizeof(weapons[item_i].chainIds[j]));
						j++;
					}
				}
				else if (strcmp(key, "iceticks") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].iceticks))
						weapons[item_i].iceticks[j++] = temp;
				}
				else if (strcmp(key, "infinite") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].infinite))
						weapons[item_i].infinite[j++] = temp;
				}
				else if (strcmp(key, "del") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].del))
						weapons[item_i].del[j++] = temp;
				}
				else if (strcmp(key, "sx") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].sx))
						weapons[item_i].sx[j++] = temp;
				}
				else if (strcmp(key, "sy") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].sy))
						weapons[item_i].sy[j++] = temp;
				}
				else if (strcmp(key, "bx") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].bx))
						weapons[item_i].bx[j++] = temp;
				}
				else if (strcmp(key, "by") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].by))
						weapons[item_i].by[j++] = temp;
				}
				else if (strcmp(key, "sg") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(weapons[item_i].sg))
						weapons[item_i].sg[j++] = temp;
				}
				else if (strcmp(key, "acceleration") == 0) {
					weapons[item_i].acceleration = atoi(value);
				}
				else if (strcmp(key, "accelerationx") == 0) {
					weapons[item_i].accelerationx = atoi(value);
				}
				else if (strcmp(key, "circlesize") == 0) {
					weapons[item_i].circlesize = atoi(value);
				}
				else if (strcmp(key, "sound") == 0) {
					weapons[item_i].sound = atoi(value);
				}
				else if (strcmp(key, "trail") == 0) {
					weapons[item_i].trail = atoi(value);
				}
				else if (strcmp(key, "shipblastfilter") == 0) {
					weapons[item_i].shipblastfilter = atoi(value);
				}
				break;
			case ItemType_Enemy:
				if (strcmp(key, "id") == 0) {
					memcpy(enemyDat[item_i].id, value, sizeof(enemyDat[item_i].id) - 1);
				}
				else if (strcmp(key, "ani") == 0) {
					enemyDat[item_i].ani = atoi(value);
				}
				else if (strcmp(key, "tur") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(enemyDat[item_i].tur))
						enemyDat[item_i].tur[j++] = temp;
				}
				else if (strcmp(key, "freq") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(enemyDat[item_i].freq))
						enemyDat[item_i].freq[j++] = temp;
				}
				else if (strcmp(key, "xmove") == 0) {
					enemyDat[item_i].xmove = atoi(value);
				}
				else if (strcmp(key, "ymove") == 0) {
					enemyDat[item_i].ymove = atoi(value);
				}
				else if (strcmp(key, "xaccel") == 0) {
					enemyDat[item_i].xaccel = atoi(value);
				}
				else if (strcmp(key, "yaccel") == 0) {
					enemyDat[item_i].yaccel = atoi(value);
				}
				else if (strcmp(key, "xcaccel") == 0) {
					enemyDat[item_i].xcaccel = atoi(value);
				}
				else if (strcmp(key, "ycaccel") == 0) {
					enemyDat[item_i].ycaccel = atoi(value);
				}
				else if (strcmp(key, "startx") == 0) {
					enemyDat[item_i].startx = atoi(value);
				}
				else if (strcmp(key, "starty") == 0) {
					enemyDat[item_i].starty = atoi(value);
				}
				else if (strcmp(key, "startxc") == 0) {
					enemyDat[item_i].startxc = atoi(value);
				}
				else if (strcmp(key, "startyc") == 0) {
					enemyDat[item_i].startyc = atoi(value);
				}
				else if (strcmp(key, "armor") == 0) {
					enemyDat[item_i].armor = atoi(value);
				}
				else if (strcmp(key, "esize") == 0) {
					enemyDat[item_i].esize = atoi(value);
				}
				else if (strcmp(key, "egraphic") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(enemyDat[item_i].egraphic))
						enemyDat[item_i].egraphic[j++] = temp;
				}
				else if (strcmp(key, "esize") == 0) {
					enemyDat[item_i].esize = atoi(value);
				}
				else if (strcmp(key, "explosiontype") == 0) {
					enemyDat[item_i].explosiontype = atoi(value);
				}
				else if (strcmp(key, "animate") == 0) {
					enemyDat[item_i].animate = atoi(value);
				}
				else if (strcmp(key, "shapebank") == 0) {
					enemyDat[item_i].shapebank = atoi(value);
				}
				else if (strcmp(key, "xrev") == 0) {
					enemyDat[item_i].xrev = atoi(value);
				}
				else if (strcmp(key, "yrev") == 0) {
					enemyDat[item_i].yrev = atoi(value);
				}
				else if (strcmp(key, "dgr") == 0) {
					enemyDat[item_i].dgr = atoi(value);
				}
				else if (strcmp(key, "dlevel") == 0) {
					enemyDat[item_i].dlevel = atoi(value);
				}
				else if (strcmp(key, "dani") == 0) {
					enemyDat[item_i].dani = atoi(value);
				}
				else if (strcmp(key, "elaunchfreq") == 0) {
					enemyDat[item_i].elaunchfreq = atoi(value);
				}
				else if (strcmp(key, "elaunchtype") == 0) {
					memcpy(enemyDat[item_i].elaunchtypeId, value, sizeof(enemyDat[item_i].elaunchtypeId) - 1);
				}
				else if (strcmp(key, "elaunchtypepos") == 0) {
					enemyDat[item_i].elaunchtypepos = atoi(value);
				}
				else if (strcmp(key, "elaunchspecial") == 0) {
					enemyDat[item_i].elaunchspecial = atoi(value);
				}
				else if (strcmp(key, "value") == 0) {
					enemyDat[item_i].value = atoi(value);
				}
				else if (strcmp(key, "eenemydie") == 0) {
					memcpy(enemyDat[item_i].eenemydieId, value, sizeof(enemyDat[item_i].eenemydieId) - 1);
				}
				else if (strcmp(key, "eshotwait") == 0) {
					int j = 0, temp;
					while (str_pop_int(value, &temp) && j < COUNTOF(enemyDat[item_i].eshotwait))
						enemyDat[item_i].eshotwait[j++] = temp;
				}
				else if (strcmp(key, "enemyground") == 0) {
					enemyDat[item_i].enemyground = atoi(value) == 1;
				}
				break;
			case ItemType_Count:
				break;
		}
	
}

#ifdef DATA_EXTRACTOR
static void read_legacy_item_data(FILE *f);

static void prepare_extracted_data_directories(void)
{
	const char *const directories[] = {
		"data", "data/shots", "data/enemy", "data/items"
	};
	for (uint i = 0; i < COUNTOF(directories); ++i)
	{
#ifdef TARGET_WIN32
		const int result = _mkdir(directories[i]);
#else
		const int result = mkdir(directories[i], 0700);
#endif
		if (result != 0 && errno != EEXIST)
		{
			fprintf(stderr, "error: failed to create '%s': %s\n", directories[i], strerror(errno));
			SDL_Quit();
			exit(EXIT_FAILURE);
		}
	}
}

void extract_item_data(const char *assetDirectory)
{
	prepare_extracted_data_directories();
	FILE *f = dir_fopen_die(assetDirectory, "tyrian.hdt", "rb");
	fread_s32_die(&episode1DataLoc, 1, f);
	if (fseek(f, episode1DataLoc, SEEK_SET) != 0)
	{
		fprintf(stderr, "error: cannot seek to item data in tyrian.hdt: %s\n", strerror(errno));
		fclose(f);
		exit(EXIT_FAILURE);
	}
	read_legacy_item_data(f);
}

static void close_extracted_file(FILE *f, const char *filename)
{
	char temporaryFilename[256];
	snprintf(temporaryFilename, sizeof(temporaryFilename), "%s.tmp", filename);
	bool failed = ferror(f) != 0;
	if (fclose(f) != 0)
		failed = true;
	if (failed)
	{
		fprintf(stderr, "error: failed to write extracted data to '%s'.\n", filename);
		if (remove(temporaryFilename) != 0)
			fprintf(stderr, "error: failed to remove incomplete output '%s': %s\n", temporaryFilename, strerror(errno));
		exit(EXIT_FAILURE);
	}
	if (rename(temporaryFilename, filename) != 0)
	{
		fprintf(stderr, "error: failed to finalize extracted category '%s': %s\n", filename, strerror(errno));
		exit(EXIT_FAILURE);
	}
}

static FILE *open_extracted_category(const char *filename)
{
	if (dir_file_exists(".", filename))
	{
		printf("Preserving existing category file '%s'.\n", filename);
		return NULL;
	}
	char temporaryFilename[256];
	snprintf(temporaryFilename, sizeof(temporaryFilename), "%s.tmp", filename);
	return dir_fopen_die(".", temporaryFilename, "w");
}
#endif

typedef struct
{
	char key[30];
	char value[255];
	size_t line;
} ItemField;

typedef struct
{
	char id[11];
	char path[_TINYDIR_PATH_MAX];
	size_t line;
	ItemField *fields;
	size_t field_count;
} ItemRecord;

typedef struct
{
	ItemRecord *records;
	size_t count;
} ItemRecords;

static const char *const base_item_files[] = {
	"data/shots/shots.tyrr", "data/items/ships.tyrr",
	"data/items/weapons.tyrr", "data/items/generators.tyrr",
	"data/items/shields.tyrr", "data/items/sidekicks.tyrr",
	"data/items/specials.tyrr", "data/enemy/enemies.tyrr"
};

static void *item_data_realloc(void *memory, size_t size)
{
	void *result = realloc(memory, size);
	if (result == NULL)
		item_data_error(loading_item_path, loading_item_line, "out of memory loading item definitions");
	return result;
}

static char *trim_item_text(char *text)
{
	while (isspace((unsigned char)*text))
		++text;
	size_t length = strlen(text);
	while (length > 0 && isspace((unsigned char)text[length - 1]))
		text[--length] = '\0';
	return text;
}

static bool valid_item_id(const char *id)
{
	if (strlen(id) == 0 || strlen(id) > 10)
		return false;
	for (const char *cursor = id; *cursor != '\0'; ++cursor)
		if (isspace((unsigned char)*cursor) || strchr(":[]=#;", *cursor) != NULL)
			return false;
	return true;
}

static enum ITEM_TYPE item_type_from_name(const char *name)
{
	static const char *const names[ItemType_Count] = {
		[ItemType_Shot] = "shot", [ItemType_Ship] = "ship",
		[ItemType_WeaponFront] = "weapon", [ItemType_Generator] = "generator",
		[ItemType_Shield] = "shield", [ItemType_SidekickLeft] = "sidekick",
		[ItemType_Special] = "special", [ItemType_Enemy] = "enemy"
	};
	enum ITEM_TYPE result = ItemType_Count;
	for (int type = 0; type < ItemType_Count; ++type)
		if (names[type] != NULL && strcmp(names[type], name) == 0)
		{
			result = type;
			break;
		}
	if (result == ItemType_Count)
		item_data_error(loading_item_path, loading_item_line, "unknown object type '%s'", name);
	return result;
}

static ItemRecord *replace_item_record(ItemRecords *items, const char *id, const char *path, size_t line)
{
	size_t index;
	for (index = 0; index < items->count; ++index)
		if (strcmp(items->records[index].id, id) == 0)
			break;
	if (index == items->count)
	{
		items->records = item_data_realloc(items->records, (items->count + 1) * sizeof(*items->records));
		memset(&items->records[items->count++], 0, sizeof(*items->records));
	}
	ItemRecord *record = &items->records[index];
	free(record->fields);
	memset(record, 0, sizeof(*record));
	strcpy(record->id, id);
	strcpy(record->path, path);
	record->line = line;
	return record;
}

static void validate_item_field(enum ITEM_TYPE type, const char *key, const char *value)
{
	static const char *const keys[ItemType_Count] = {
		[ItemType_Shot] = " id drain shotrepeat multi weapani max tx ty aim attack chain iceticks infinite del sx sy bx by sg acceleration accelerationx circlesize sound trail shipblastfilter ",
		[ItemType_Ship] = " id name shipgraphic itemgraphic ani spd dmg cost bigshipgraphic bigshipgraphx bigshipgraphy probability ",
		[ItemType_WeaponFront] = " id name opnum op0 op1 cost itemgraphic poweruse position bigshipgraphic bigshipgraphfrntx bigshipgraphfrnty bigshipgraphrearx bigshipgraphreary linkgunweapon chargegunweapons probability ",
		[ItemType_Generator] = " id name itemgraphic power speed cost bigshipgraphic bigshipgraphx bigshipgraphy probability ",
		[ItemType_Shield] = " id name itemgraphic tpwr mpwr cost probability ",
		[ItemType_SidekickLeft] = " id name itemgraphic pwr cost tr option opspd ani gr0 gr1 gr2 gr3 gr4 wport wpnum ammo stop icongr position probability ",
		[ItemType_Special] = " id name itemgraphic pwr stype wpn ",
		[ItemType_Enemy] = " id ani tur freq xmove ymove xaccel yaccel xcaccel ycaccel startx starty startxc startyc armor esize egraphic explosiontype animate shapebank xrev yrev dgr dlevel dani elaunchfreq elaunchtype elaunchtypepos elaunchspecial value eenemydie eshotwait enemyground "
	};
	char token[34];
	snprintf(token, sizeof(token), " %s ", key);
	if (strstr(keys[type], token) == NULL)
		item_data_error(loading_item_path, loading_item_line, "unknown field '%s'", key);
	if (strcmp(key, "name") == 0)
	{
		if (strlen(value) > 30)
			item_data_error(loading_item_path, loading_item_line, "name exceeds 30 characters");
		return;
	}
	if (*value == '\0')
		item_data_error(loading_item_path, loading_item_line, "field '%s' requires a value", key);
	const bool reference = strstr(" id op0 op1 chain linkgunweapon chargegunweapons wport wpnum wpn elaunchtype eenemydie ", token) != NULL;
	const char *cursor = value;
	while (*cursor != '\0')
	{
		while (isspace((unsigned char)*cursor))
			++cursor;
		if (*cursor == '\0')
			break;
		if (reference)
		{
			char id[11] = "";
			size_t length = 0;
			while (cursor[length] != '\0' && !isspace((unsigned char)cursor[length]))
				++length;
			if (length > 10)
				item_data_error(loading_item_path, loading_item_line, "reference ID exceeds 10 characters");
			memcpy(id, cursor, length);
			if (!valid_item_id(id))
				item_data_error(loading_item_path, loading_item_line, "invalid reference ID '%s'", id);
			cursor += length;
		}
		else
		{
			char *end;
			errno = 0;
			long number = strtol(cursor, &end, 10);
			if (end == cursor || errno == ERANGE || number < INT_MIN || number > INT_MAX ||
			    (*end != '\0' && !isspace((unsigned char)*end)))
				item_data_error(loading_item_path, loading_item_line, "invalid integer in field '%s'", key);
			cursor = end;
		}
	}
}

static void load_item_bundle(const char *path, ItemRecords items[ItemType_Count])
{
	loading_item_path = path;
	loading_item_line = 0;
	FILE *file = fopen(path, "r");
	if (file == NULL)
		item_data_error(path, 0, "cannot read extracted data file: %s", strerror(errno));
	char buffer[1024];
	ItemRecord *record = NULL;
	enum ITEM_TYPE type = ItemType_Count;
	while (fgets(buffer, sizeof(buffer), file) != NULL)
	{
		++loading_item_line;
		if (strchr(buffer, '\n') == NULL && !feof(file))
			item_data_error(path, loading_item_line, "line is too long");
		char *start = buffer;
		if (loading_item_line == 1 && strncmp(buffer, "\xEF\xBB\xBF", 3) == 0)
			start += 3;
		char *text = trim_item_text(start);
		if (*text == '\0' || *text == '#' || *text == ';')
			continue;
		if (*text == '[')
		{
			size_t length = strlen(text);
			char *colon = strchr(text, ':');
			if (colon == NULL || text[length - 1] != ']')
				item_data_error(path, loading_item_line, "expected [type:id] header");
			text[length - 1] = '\0';
			*colon = '\0';
			char *id = trim_item_text(colon + 1);
			if (!valid_item_id(id))
				item_data_error(path, loading_item_line, "invalid object ID '%s'", id);
			type = item_type_from_name(trim_item_text(text + 1));
			record = replace_item_record(&items[type], id, path, loading_item_line);
			continue;
		}
		if (record == NULL)
			item_data_error(path, loading_item_line, "field appears before an object header");
		char *equals = strchr(text, '=');
		if (equals == NULL)
			item_data_error(path, loading_item_line, "expected key=value");
		*equals = '\0';
		char *key = trim_item_text(text);
		char *value = trim_item_text(equals + 1);
		if (strlen(key) >= sizeof(record->fields[0].key) || strlen(value) >= sizeof(record->fields[0].value))
			item_data_error(path, loading_item_line, "field name or value is too long");
		validate_item_field(type, key, value);
		if (strcmp(key, "id") == 0 && strcmp(value, record->id) != 0)
			item_data_error(path, loading_item_line, "id field does not match header ID '%s'", record->id);
		record->fields = item_data_realloc(record->fields, (record->field_count + 1) * sizeof(*record->fields));
		ItemField *field = &record->fields[record->field_count++];
		memset(field, 0, sizeof(*field));
		strcpy(field->key, key);
		strcpy(field->value, value);
		field->line = loading_item_line;
	}
	if (ferror(file))
		item_data_error(path, loading_item_line, "failed to read definitions");
	fclose(file);
}

static int compare_item_paths(const void *left, const void *right)
{
	const char *const *a = left;
	const char *const *b = right;
	int comparison = SDL_strcasecmp(*a, *b);
	return comparison != 0 ? comparison : strcmp(*a, *b);
}

static void load_additional_item_bundles(ItemRecords items[ItemType_Count])
{
	const char *const directories[] = { "data/items", "data/enemy", "data/shots" };
	char **paths = NULL;
	size_t path_count = 0;
	for (uint i = 0; i < COUNTOF(directories); ++i)
	{
		tinydir_dir dir;
		loading_item_path = directories[i];
		loading_item_line = 0;
		if (tinydir_open_sorted(&dir, directories[i]) == -1)
			item_data_error(directories[i], 0, "cannot enumerate .tyrr files: %s", strerror(errno));
		for (size_t j = 0; j < dir.n_files; ++j)
		{
			tinydir_file file;
			if (tinydir_readfile_n(&dir, &file, j) == -1)
				item_data_error(directories[i], 0, "cannot read directory entry: %s", strerror(errno));
			if (file.is_dir || SDL_strcasecmp(file.extension, "tyrr") != 0)
				continue;
			char path[_TINYDIR_PATH_MAX];
			if (snprintf(path, sizeof(path), "%s/%s", directories[i], file.name) >= (int)sizeof(path))
				item_data_error(directories[i], 0, "definition path is too long");
			bool base_file = false;
			for (uint k = 0; k < COUNTOF(base_item_files); ++k)
				if (SDL_strcasecmp(path, base_item_files[k]) == 0)
					base_file = true;
			if (base_file)
				continue;
			paths = item_data_realloc(paths, (path_count + 1) * sizeof(*paths));
			paths[path_count] = item_data_realloc(NULL, strlen(path) + 1);
			strcpy(paths[path_count++], path);
		}
		tinydir_close(&dir);
	}
	if (path_count > 1)
		qsort(paths, path_count, sizeof(*paths), compare_item_paths);
	for (size_t i = 0; i < path_count; ++i)
	{
		load_item_bundle(paths[i], items);
		free(paths[i]);
	}
	free(paths);
}

void load_items(void) {
	loading_item_data = true;
	ItemRecords items[ItemType_Count] = { 0 };
	for (uint i = 0; i < COUNTOF(base_item_files); ++i)
		load_item_bundle(base_item_files[i], items);
	load_additional_item_bundles(items);
	loading_item_path = "data";
	loading_item_line = 0;

	// The game reserves slot zero for each type's "0" object.
	for (int type = 0; type < ItemType_Count; ++type)
	{
		ItemRecords *records = &items[type];
		if (records->count == 0)
			continue;
		size_t zero;
		for (zero = 0; zero < records->count; ++zero)
			if (strcmp(records->records[zero].id, "0") == 0)
				break;
		if (zero == records->count)
			item_data_error("data", 0, "missing required object ID '0' for type %d", type);
		if (zero != 0)
		{
			ItemRecord record = records->records[zero];
			memmove(records->records + 1, records->records, zero * sizeof(*records->records));
			records->records[0] = record;
		}
	}

	// Install every ID before resolving fields, so bundles can reference later records.
#define ALLOCATE_ITEMS(type, array) \
	do { \
		size_t count = items[type].count; \
		if (count == 0) item_data_error("data", 0, "no objects for type %d", type); \
		free(array); \
		array = calloc(count, sizeof(*array)); \
		if (array == NULL) item_data_error("data", 0, "out of memory allocating item definitions"); \
		for (size_t i = 0; i < count; ++i) strcpy(array[i].id, items[type].records[i].id); \
		array[0].count = count; \
	} while (0)
	ALLOCATE_ITEMS(ItemType_Shot, weapons);
	ALLOCATE_ITEMS(ItemType_Ship, ships);
	ALLOCATE_ITEMS(ItemType_WeaponFront, weaponPort);
	ALLOCATE_ITEMS(ItemType_Generator, powerSys);
	ALLOCATE_ITEMS(ItemType_Shield, shields);
	ALLOCATE_ITEMS(ItemType_SidekickLeft, options);
	ALLOCATE_ITEMS(ItemType_Special, special);
	ALLOCATE_ITEMS(ItemType_Enemy, enemyDat);
#undef ALLOCATE_ITEMS
	for (int type = 0; type < ItemType_Count; ++type)
	{
		for (size_t i = 0; i < items[type].count; ++i)
		{
			ItemRecord *record = &items[type].records[i];
			loading_item_path = record->path;
			for (size_t j = 0; j < record->field_count; ++j)
			{
				loading_item_line = record->fields[j].line;
				apply_item_field(type, i, record->fields[j].key, record->fields[j].value);
			}
		}
	}

	tempItemType = ItemType_Shot;
	for (size_t i = 0; i < weapons[0].count; i++) {
		loading_item_path = items[ItemType_Shot].records[i].path;
		loading_item_line = items[ItemType_Shot].records[i].line;
		int chainlen = COUNTOF(weapons[0].chainIds);
		for (int j = 0; j < chainlen; j++) {
			if (weapons[i].chainIds[j][0] == '\0' || strcmp(weapons[i].chainIds[j], "0") == 0) continue;
			get_item_index(&tempItemType, weapons[i].chainIds[j], &weapons[i].chain[j]);
		}
	}

	tempItemType = ItemType_Shot;
	for (size_t i = 0; i < weaponPort[0].count; i++) {
		loading_item_path = items[ItemType_WeaponFront].records[i].path;
		loading_item_line = items[ItemType_WeaponFront].records[i].line;
		if (weaponPort[i].linkgunweaponId[0] != '\0' && strcmp(weaponPort[i].linkgunweaponId, "0") != 0)
			get_item_index(&tempItemType, weaponPort[i].linkgunweaponId, &weaponPort[i].linkgunweapon);

		int chargegunweaponslen = COUNTOF(weaponPort->chargegunweapons);
		for (int j = 0; j < chargegunweaponslen; j++) {
			if (weaponPort[i].chargegunweaponIds[j][0] == '\0' || strcmp(weaponPort[i].chargegunweaponIds[j], "0") == 0) continue;
			get_item_index(&tempItemType, weaponPort[i].chargegunweaponIds[j], &weaponPort[i].chargegunweapons[j]);
		}
	}

	tempItemType = ItemType_WeaponFront;
	for (size_t i = 0; i < options[0].count; i++) {
		loading_item_path = items[ItemType_SidekickLeft].records[i].path;
		loading_item_line = items[ItemType_SidekickLeft].records[i].line;
		if (options[i].wportId[0] != '\0' && strcmp(options[i].wportId, "0") != 0) {
			get_item_index(&tempItemType, options[i].wportId, &options[i].wport);
		}
	}

	tempItemType = ItemType_Enemy;
	for (size_t i = 0; i < enemyDat[0].count; i++) {
		loading_item_path = items[ItemType_Enemy].records[i].path;
		loading_item_line = items[ItemType_Enemy].records[i].line;
		if (enemyDat[i].eenemydieId[0] != '\0' && strcmp(enemyDat[i].eenemydieId, "0") != 0) {
			get_item_index(&tempItemType, enemyDat[i].eenemydieId, &enemyDat[i].eenemydie);
		}
		if (enemyDat[i].elaunchtypeId[0] != '\0' && strcmp(enemyDat[i].elaunchtypeId, "0") != 0) {
			get_item_index(&tempItemType, enemyDat[i].elaunchtypeId, &enemyDat[i].elaunchtype);
		}
	}

	loading_item_path = "data";
	loading_item_line = 0;
	tempItemType = ItemType_Generator;
	ITEM_INDEX_STORE[ITEM_GENERATOR_ADVANCEDMR12] = item_idx(&tempItemType, "2");
	tempItemType = ItemType_Shield;
	ITEM_INDEX_STORE[ITEM_SHIELD_ADVANCEDINTEGRITYFIELD] = item_idx(&tempItemType, "2");
	ITEM_INDEX_STORE[ITEM_SHIELD_GENCOREHIGHENERGY] = item_idx(&tempItemType, "4");
	tempItemType = ItemType_Ship;
	ITEM_INDEX_STORE[ITEM_SHIP_NORTSHIP] = item_idx(&tempItemType, "12");
	ITEM_INDEX_STORE[ITEM_SHIP_SILVERSHIP] = item_idx(&tempItemType, "11");
	ITEM_INDEX_STORE[ITEM_SHIP_STALKER] = item_idx(&tempItemType, "8");
	ITEM_INDEX_STORE[ITEM_SHIP_STALKER21126] = item_idx(&tempItemType, "13");
	ITEM_INDEX_STORE[ITEM_SHIP_SUPERCARROT] = item_idx(&tempItemType, "2");
	ITEM_INDEX_STORE[ITEM_SHIP_USPTALON] = item_idx(&tempItemType, "1");
	tempItemType = ItemType_Shot;
	ITEM_INDEX_STORE[ITEM_SHOT_147] = item_idx(&tempItemType, "147");
	ITEM_INDEX_STORE[ITEM_SHOT_148] = item_idx(&tempItemType, "148");
	tempItemType = ItemType_SidekickRight;
	ITEM_INDEX_STORE[ITEM_SIDEKICK_COMPANIONSHIPQUICKSILVER] = item_idx(&tempItemType, "24");
	tempItemType = ItemType_Special;
	ITEM_INDEX_STORE[ITEM_SPECIAL_ASTRALZONE] = item_idx(&tempItemType, "13");
	tempItemType = ItemType_WeaponFront;
	ITEM_INDEX_STORE[ITEM_WEAPONFRONT_ATOMICRAILGUN] = item_idx(&tempItemType, "39");
	ITEM_INDEX_STORE[ITEM_WEAPONFRONT_BANANABLAST] = item_idx(&tempItemType, "23");
	ITEM_INDEX_STORE[ITEM_WEAPONFRONT_HOTDOG] = item_idx(&tempItemType, "25");
	ITEM_INDEX_STORE[ITEM_WEAPONFRONT_NORTSHIPSUPERPULSE] = item_idx(&tempItemType, "36");
	ITEM_INDEX_STORE[ITEM_WEAPONFRONT_PULSECANNON] = item_idx(&tempItemType, "1");
	ITEM_INDEX_STORE[ITEM_WEAPONFRONT_MISCELLANEOUS] = item_idx(&tempItemType, "16");
	tempItemType = ItemType_WeaponRear;
	ITEM_INDEX_STORE[ITEM_WEAPONREAR_BANANABLAST] = item_idx(&tempItemType, "24");
	ITEM_INDEX_STORE[ITEM_WEAPONREAR_HOTDOG] = item_idx(&tempItemType, "26");
	ITEM_INDEX_STORE[ITEM_WEAPONREAR_NORTSHIPSPREADER] = item_idx(&tempItemType, "37");
	ITEM_INDEX_STORE[ITEM_WEAPONREAR_VULCANCANNON] = item_idx(&tempItemType, "15");
	tempItemType = ItemType_Enemy;
	ITEM_INDEX_STORE[ITEM_ENEMY_ARMORSHIP] = item_idx(&tempItemType, "560");
	ITEM_INDEX_STORE[ITEM_ENEMY_FRONTPOWERUP] = item_idx(&tempItemType, "533");
	ITEM_INDEX_STORE[ITEM_ENEMY_REARPOWERUP] = item_idx(&tempItemType, "534");
	for (int type = 0; type < ItemType_Count; ++type)
	{
		for (size_t i = 0; i < items[type].count; ++i)
			free(items[type].records[i].fields);
		free(items[type].records);
	}
	loading_item_path = NULL;
	loading_item_data = false;
}

#ifdef DATA_EXTRACTOR
static void read_legacy_item_data(FILE *f)
{
	JE_word itemNum[7]; /* [1..7] */
	fread_u16_die(itemNum, 7, f);

	FILE *outF = open_extracted_category("data/shots/shots.tyrr");
	for (int i = 0; i < WEAP_NUM + 1; ++i)
	{
		fread_u16_die(&oldWeapons[i].drain,           1, f);
		fread_u8_die( &oldWeapons[i].shotrepeat,      1, f);
		fread_u8_die( &oldWeapons[i].multi,           1, f);
		fread_u16_die(&oldWeapons[i].weapani,         1, f);
		fread_u8_die( &oldWeapons[i].max,             1, f);
		fread_u8_die( &oldWeapons[i].tx,              1, f);
		fread_u8_die( &oldWeapons[i].ty,              1, f);
		fread_u8_die( &oldWeapons[i].aim,             1, f);
		fread_u8_die(  oldWeapons[i].attack,          8, f);
		fread_u8_die(  oldWeapons[i].del,             8, f);
		fread_s8_die(  oldWeapons[i].sx,              8, f);
		fread_s8_die(  oldWeapons[i].sy,              8, f);
		fread_s8_die(  oldWeapons[i].bx,              8, f);
		fread_s8_die(  oldWeapons[i].by,              8, f);
		fread_u16_die( oldWeapons[i].sg,              8, f);
		fread_s8_die( &oldWeapons[i].acceleration,    1, f);
		fread_s8_die( &oldWeapons[i].accelerationx,   1, f);
		fread_u8_die( &oldWeapons[i].circlesize,      1, f);
		fread_u8_die( &oldWeapons[i].sound,           1, f);
		fread_u8_die( &oldWeapons[i].trail,           1, f);
		fread_u8_die( &oldWeapons[i].shipblastfilter, 1, f);

		#ifdef DATA_EXTRACTOR
		if (outF != NULL) {
			fprintf(outF, "[shot:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "drain=%d\n", oldWeapons[i].drain);
			fprintf(outF, "shotrepeat=%d\n", oldWeapons[i].shotrepeat);
			fprintf(outF, "multi=%d\n", oldWeapons[i].multi);
			fprintf(outF, "weapani=%d\n", oldWeapons[i].weapani);
			fprintf(outF, "max=%d\n", oldWeapons[i].max);
			fprintf(outF, "tx=%d\n", oldWeapons[i].tx);
			fprintf(outF, "ty=%d\n", oldWeapons[i].ty);
			fprintf(outF, "aim=%d\n", oldWeapons[i].aim);
			fputs("attack=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].attack); ii++) {
				int val = oldWeapons[i].attack[ii];
				int chainval = 0;
				if (val > 99 && val < 250) {
					chainval = val - 100;
					val = 1;
				}
				else if (val == 99) {
					val = 0;
					oldWeapons[i].iceticks[ii] = 40;
				}
				else if (val >= 250) {
					val = val - 250;
					oldWeapons[i].infinite[ii] = 1;
				}
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", val);
				oldWeapons[i].chain[ii] = chainval;
			}
			fputs("\n", outF);
			fputs("chain=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].chain); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].chain[ii]);
			}
			fputs("\n", outF);
			fputs("iceticks=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].iceticks); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].iceticks[ii]);
			}
			fputs("\n", outF);
			fputs("infinite=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].infinite); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].infinite[ii]);
			}
			fputs("\n", outF);
			fputs("del=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].del); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].del[ii]);
			}
			fputs("\n", outF);
			fputs("sx=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].sx); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].sx[ii]);
			}
			fputs("\n", outF);
			fputs("sy=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].sy); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].sy[ii]);
			}
			fputs("\n", outF);
			fputs("bx=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].bx); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].bx[ii]);
			}
			fputs("\n", outF);
			fputs("by=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].by); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].by[ii]);
			}
			fputs("\n", outF);
			fputs("sg=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeapons[i].sg); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeapons[i].sg[ii]);
			}
			fputs("\n", outF);
			fprintf(outF, "acceleration=%d\n", oldWeapons[i].acceleration);
			fprintf(outF, "accelerationx=%d\n", oldWeapons[i].accelerationx);
			fprintf(outF, "circlesize=%d\n", oldWeapons[i].circlesize);
			fprintf(outF, "sound=%d\n", oldWeapons[i].sound);
			fprintf(outF, "trail=%d\n", oldWeapons[i].trail);
			fprintf(outF, "shipblastfilter=%d\n", oldWeapons[i].shipblastfilter);
			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/shots/shots.tyrr");
	
	outF = open_extracted_category("data/items/weapons.tyrr");
	for (int i = 0; i < PORT_NUM + 1; ++i)
	{
		Uint8 nameLen;
		fread_u8_die( &nameLen,                   1, f);
		fread_die(    &oldWeaponPort[i].name,    1, 30, f);
		oldWeaponPort[i].name[MIN(nameLen, 30)] = '\0';
		fread_u8_die( &oldWeaponPort[i].opnum,       1, f);
		fread_u16_die( oldWeaponPort[i].op[0],      11, f);
		fread_u16_die( oldWeaponPort[i].op[1],      11, f);
		fread_u16_die(&oldWeaponPort[i].cost,        1, f);
		fread_u16_die(&oldWeaponPort[i].itemgraphic, 1, f);
		fread_u16_die(&oldWeaponPort[i].poweruse,    1, f);

		#ifdef DATA_EXTRACTOR
		// Sprite number of bigship graphic used in game menu.
		// Originally was hardcoded into the game menu ship graphic drawing function
		int bigshipgraphic[43] = {
			-1,
			0, 1, 2, 3, 4, // 5
			5, 6, 7, 8, 9, // 10
			10, 11, 21, 5, 13, // 15
			-1, 14, 15, 0, 14, // 20
			9, 8, 2, 15, 0, // 25
			13, 0, 8, 8, 11, // 30
			1, 0, 0, 0, 0, // 35
			0, 0, 0, 0, 0, // 40
			2, 1 // 42
		};
		// X and Y offsets of bigship graphic used in game menu.
		// Originally was hardcoded into the game menu ship graphic drawing function
		int bigshipgraphfrontxy[43][2] = {
			{ 0, 0 }, // 0
			{ 61, 48 }, { 52, 41 }, { 54, 36 }, { 61, 53 }, { 66, 41 }, // 5
			{ 51, 35 }, { 53, 39 }, { 66, 53 }, { 0, 0 }, { 0, 0 }, // 10
			{ 0, 0 }, { 0, 0 }, { 51, 35 }, { 61, 53 }, { 0, 0 }, // 15
			{ 0, 0 }, { 59, 38 }, { 0, 0 }, { 61, 48 }, { 59, 38 }, // 20
			{ 0, 0 }, { 0, 0 }, { 54, 36 }, { 0, 0 }, { 61, 48 }, // 25
			{ 0, 0 }, { 61, 48 }, { 0, 0 }, { 0, 0 }, { 0, 0 }, // 30
			{ 52, 41 }, { 59, 38 }, { 59, 38 }, { 59, 38 }, { 59, 38 }, // 35
			{ 59, 38 }, { 59, 38 }, { 59, 38 }, { 59, 38 }, { 59, 38 }, // 40
			{ 54, 36 }, { 52, 41 } // 42
		};
		// X and Y offsets of bigship graphic used in game menu.
		// Originally was hardcoded into the game menu ship graphic drawing function
		int bigshipgraphrearxy[43][2] = {
			{ 0, 0 }, // 0
			{ 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 }, // 5
			{ 0, 0 }, { 0, 0 }, { 0, 0 }, { 41, 92 }, { 27, 92 }, // 10
			{ 49, 113 }, { 43, 102 }, { 0, 0 }, { 51, 97 }, { 39, 96 }, // 15
			{ 0, 0 }, { 0, 0 }, { 41, 76 }, { 0, 0 }, { 0, 0 }, // 20
			{ 27, 92 }, { 41, 92 }, { 0, 0 }, { 41, 76 }, { 0, 0 }, // 25
			{ 39, 96 }, { 0, 0 }, { 41, 92 }, { 41, 92 }, { 43, 102 }, // 30
			{ 41, 92 }, { 41, 92 }, { 41, 92 }, { 41, 92 }, { 41, 92 }, // 35
			{ 41, 92 }, { 41, 92 }, { 41, 92 }, { 41, 92 }, { 41, 92 }, // 40
			{ 0, 0 }, { 0, 0 }  // 42
		};
		// Calculated by number of times item appeared in shop in episodes 1-4, divided by number of levels
		int probabilities[43] = {
			0,
			30, 56, 28, 4, 7, // 5
			1, 23, 14, 2, 54, // 10
			58, 37, 26, 11, 33, // 15
			0, 30, 19, 47, 33, // 20
			7, 16, 1, 1, 1, // 25
			1, 9, 5, 12, 14, // 30
			1, 1, 1, 1, 1, // 35
			1, 1, 1, 1, 7, // 40
			2, 2 // 42
		};
		// Position of weapon: Front (0) or rear (1)
		// Determined by where item appeared in shop in episodes 1-4 or where it was assigned to ship in game code for special game types / powerups.
		int positions[43] = {
			0,
			0, 0, 0, 0, 0, // 5
			0, 0, 0, 0, 1, // 10
			1, 1, 0, 1, 1, // 15
			0, 0, 1, 0, 0, // 20
			1, 1, 0, 1, 0, // 25
			1, 0, 1, 1, 1, // 30
			0, 0, 0, 0, 0, // 35
			0, 1, 0, 0, 0, // 40
			0, 0 // 42
		};
		// The ID of the weapon to use when fired as a two-player linked gun. These values were originally hardcoded in varz.c
		JE_word linkGunWeapons[38] /* [1..38] */ =
		{
			0,0,0,0,0,0,0,0,444,445,446,447,0,448,449,0,0,0,0,0,450,451,0,506,0,564,
			  445,446,447,448,449,445,446,447,448,449,450,451
		};
		// The ID of the weapon to use when fired as a charged shot on player 2. These values were originally hardcoded in varz.c
		const JE_word chargeGunWeapons[38] /* [1..38] */ =
		{
			0,0,0,0,0,0,0,0,476,458,464,482,0,488,470,0,0,0,0,0,494,500,0,528,0,558,
			  458,458,458,458,458,458,458,458,458,458,458,458
		};

		if (outF != NULL) {
			fprintf(outF, "[weapon:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldWeaponPort[i].name);
			fprintf(outF, "opnum=%d\n", oldWeaponPort[i].opnum);
			fputs("op0=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeaponPort[i].op[0]); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeaponPort[i].op[0][ii]);
			}
			fputs("\n", outF);
			fputs("op1=", outF);
			for (int ii = 0; ii < COUNTOF(oldWeaponPort[i].op[1]); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldWeaponPort[i].op[1][ii]);
			}
			fputs("\n", outF);
			fprintf(outF, "cost=%d\n", oldWeaponPort[i].cost);
			fprintf(outF, "itemgraphic=%d\n", oldWeaponPort[i].itemgraphic);
			fprintf(outF, "poweruse=%d\n", oldWeaponPort[i].poweruse);
			fprintf(outF, "position=%d\n", positions[i]);
			fprintf(outF, "bigshipgraphic=%d\n", bigshipgraphic[i]);
			fprintf(outF, "bigshipgraphfrntx=%d\n", bigshipgraphfrontxy[i][0]);
			fprintf(outF, "bigshipgraphfrnty=%d\n", bigshipgraphfrontxy[i][1]);
			fprintf(outF, "bigshipgraphrearx=%d\n", bigshipgraphrearxy[i][0]);
			fprintf(outF, "bigshipgraphreary=%d\n", bigshipgraphrearxy[i][1]);
			fprintf(outF, "linkgunweapon=%d\n", (i > 0 && i <= COUNTOF(linkGunWeapons) ? linkGunWeapons[i-1] : 0));
			fputs("chargegunweapons=", outF);
			for (int ii = 0; ii <= COUNTOF(weaponPort[0].chargegunweapons); ii++) {
				if (ii > 0) fputs(" ", outF);
				if (i > 0 && i <= COUNTOF(chargeGunWeapons) && chargeGunWeapons[i-1] > 0) {
					fprintf(outF, "%d", chargeGunWeapons[i-1] + ii);
				}
				else {
					fputs("0", outF);
				}
			}
			fputs("\n", outF);
			fprintf(outF, "probability=%d\n", probabilities[i]);
			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/items/weapons.tyrr");

	outF = open_extracted_category("data/items/specials.tyrr");
	for (int i = 0; i < SPECIAL_NUM + 1; ++i)
	{
		Uint8 nameLen;
		fread_u8_die( &nameLen,                1, f);
		fread_die(    &oldSpecial[i].name,    1, 30, f);
		oldSpecial[i].name[MIN(nameLen, 30)] = '\0';
		fread_u16_die(&oldSpecial[i].itemgraphic, 1, f);
		fread_u8_die( &oldSpecial[i].pwr,         1, f);
		fread_u8_die( &oldSpecial[i].stype,       1, f);
		fread_u16_die(&oldSpecial[i].wpn,         1, f);

		#ifdef DATA_EXTRACTOR
		if (outF != NULL) {
			fprintf(outF, "[special:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldSpecial[i].name);
			fprintf(outF, "itemgraphic=%d\n", oldSpecial[i].itemgraphic);
			fprintf(outF, "pwr=%d\n", oldSpecial[i].pwr);
			fprintf(outF, "stype=%d\n", oldSpecial[i].stype);
			fprintf(outF, "wpn=%d\n", oldSpecial[i].wpn);
			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/items/specials.tyrr");

	outF = open_extracted_category("data/items/generators.tyrr");
	for (int i = 0; i < POWER_NUM + 1; ++i)
	{
		Uint8 nameLen;
		fread_u8_die( &nameLen,                 1, f);
		fread_die(    &oldPowerSys[i].name,    1, 30, f);
		oldPowerSys[i].name[MIN(nameLen, 30)] = '\0';
		fread_u16_die(&oldPowerSys[i].itemgraphic, 1, f);
		fread_u8_die( &oldPowerSys[i].power,       1, f);
		fread_s8_die( &oldPowerSys[i].speed,       1, f);
		fread_u16_die(&oldPowerSys[i].cost,        1, f);

		#ifdef DATA_EXTRACTOR
		// Sprite number of bigship graphic used in game menu.
		// Originally was hardcoded into the game menu ship graphic drawing function
		int bigshipgraphic[7] = {
			0,
			16, 16, 17, 18, 19, // 5
			20 // 6
		};
		// X and Y offsets of bigship graphic used in game menu.
		// Originally was hardcoded into the game menu ship graphic drawing function
		int bigshipgraphxy[7][2] = {
			{ 0, 0 }, // 0
			{ 62, 84 }, { 62, 84 }, { 64, 85 }, { 67, 86 }, { 66, 84 }, // 5
			{ 63, 97 } // 6
		};
		// Calculated by number of times item appeared in shop in episodes 1-4, divided by number of levels
		int probabilities[7] = {
			0,
			61, 84, 86, 49, 25, // 5
			5, // 7
		};
		if (outF != NULL) {
			fprintf(outF, "[generator:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldPowerSys[i].name);
			fprintf(outF, "itemgraphic=%d\n", oldPowerSys[i].itemgraphic);
			fprintf(outF, "power=%d\n", oldPowerSys[i].power);
			fprintf(outF, "speed=%d\n", oldPowerSys[i].speed);
			fprintf(outF, "cost=%d\n", oldPowerSys[i].cost);
			fprintf(outF, "bigshipgraphic=%d\n", bigshipgraphic[i]);
			fprintf(outF, "bigshipgraphx=%d\n", bigshipgraphxy[i][0]);
			fprintf(outF, "bigshipgraphy=%d\n", bigshipgraphxy[i][1]);
			fprintf(outF, "probability=%d\n", probabilities[i]);
			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/items/generators.tyrr");

	outF = open_extracted_category("data/items/ships.tyrr");
	for (int i = 0; i < SHIP_NUM + 1; ++i)
	{
		Uint8 nameLen;
		fread_u8_die( &nameLen,                 1, f);
		fread_die(    &oldShips[i].name,       1, 30, f);
		oldShips[i].name[MIN(nameLen, 30)] = '\0';
		fread_u16_die(&oldShips[i].shipgraphic,    1, f);
		fread_u16_die(&oldShips[i].itemgraphic,    1, f);
		fread_u8_die( &oldShips[i].ani,            1, f);
		fread_s8_die( &oldShips[i].spd,            1, f);
		fread_u8_die( &oldShips[i].dmg,            1, f);
		fread_u16_die(&oldShips[i].cost,           1, f);
		fread_u8_die( &oldShips[i].bigshipgraphic, 1, f);

		#ifdef DATA_EXTRACTOR
		// X and Y offsets of bigship graphic used in game menu.
		// Originally was hardcoded into the game menu ship graphic drawing function
		int bigshipgraphxy[14][2] = {
			{ 0, 0 },
			{ 35, 33 },{ 35, 33 },{ 31, 36 },{ 31, 36 },{ 31, 35 },
			{ 31, 35 },{ 31, 35 },{ 31, 35 },{ 35, 33 },{ 31, 36 },
			{ 35, 33 },{ 31, 35 },{ 31, 35 },
		};
		// Calculated by number of times item appeared in shop in episodes 1-4, divided by number of levels
		int probabilities[14] = {
			0,
			67, 5, 46, 47, 32,
			16, 7, 1, 40, 1,
			1, 1, 2
		};

		if (outF != NULL) {
			fprintf(outF, "[ship:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldShips[i].name);
			fprintf(outF, "shipgraphic=%d\n", oldShips[i].shipgraphic);
			fprintf(outF, "itemgraphic=%d\n", oldShips[i].itemgraphic);
			fprintf(outF, "ani=%d\n", oldShips[i].ani);
			fprintf(outF, "spd=%d\n", oldShips[i].spd);
			fprintf(outF, "dmg=%d\n", oldShips[i].dmg);
			fprintf(outF, "cost=%d\n", oldShips[i].cost);
			fprintf(outF, "bigshipgraphic=%d\n", oldShips[i].bigshipgraphic);
			fprintf(outF, "bigshipgraphx=%d\n", bigshipgraphxy[i][0]);
			fprintf(outF, "bigshipgraphy=%d\n", bigshipgraphxy[i][1]);
			fprintf(outF, "probability=%d\n", probabilities[i]);
			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/items/ships.tyrr");

	outF = open_extracted_category("data/items/sidekicks.tyrr");
	for (int i = 0; i < OPTION_NUM + 1; ++i)
	{
		Uint8 nameLen;
		fread_u8_die(  &nameLen,                1, f);
		fread_die(     &oldOptions[i].name,    1, 30, f);
		oldOptions[i].name[MIN(nameLen, 30)] = '\0';
		fread_u8_die(  &oldOptions[i].pwr,         1, f);
		fread_u16_die( &oldOptions[i].itemgraphic, 1, f);
		fread_u16_die( &oldOptions[i].cost,        1, f);
		fread_u8_die(  &oldOptions[i].tr,          1, f);
		fread_u8_die(  &oldOptions[i].option,      1, f);
		fread_s8_die(  &oldOptions[i].opspd,       1, f);
		fread_u8_die(  &oldOptions[i].ani,         1, f);
		fread_u16_die(  oldOptions[i].gr,         20, f);
		fread_u8_die(  &oldOptions[i].wport,       1, f);
		fread_u16_die( &oldOptions[i].wpnum,       1, f);
		fread_u8_die(  &oldOptions[i].ammo,        1, f);
		fread_bool_die(&oldOptions[i].stop,           f);
		fread_u8_die(  &oldOptions[i].icongr,      1, f);

		#ifdef DATA_EXTRACTOR
		// 0 Both sides, 1 left only, 2 right only.
		int positions[31] = {
			0,
			0, 0, 0, 0, 0,
			0, 0, 0, 0, 0,
			0, 0, 0, 0, 0,
			0, 0, 0, 0, 2,
			0, 2, 0, 2, 2,
			2, 2, 2, 0, 0
		};
		// Calculated by number of times item appeared in shop in episodes 1-4, divided by number of levels
		int probabilities[31] = {
			0,
			23, 40, 18, 9, 2,
			21, 21, 30, 19, 21,
			4, 4, 11, 12, 4,
			17, 9, 5, 13, 7,
			10, 2, 9, 1, 4,
			2, 4, 2, 6, 2
		};
		if (outF != NULL) {
			fprintf(outF, "[sidekick:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldOptions[i].name);
			fprintf(outF, "itemgraphic=%d\n", oldOptions[i].itemgraphic);
			fprintf(outF, "pwr=%d\n", oldOptions[i].pwr);
			fprintf(outF, "cost=%d\n", oldOptions[i].cost);
			fprintf(outF, "tr=%d\n", oldOptions[i].tr);
			fprintf(outF, "option=%d\n", oldOptions[i].option);
			fprintf(outF, "opspd=%d\n", oldOptions[i].opspd);
			fprintf(outF, "ani=%d\n", oldOptions[i].ani);
			fputs("gr0=", outF);
			for (int ii = 0; ii < COUNTOF(oldOptions[i].gr); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldOptions[i].gr[ii]);
			}
			fputs("\n", outF);
			fputs("gr1=", outF);
			for (int ii = 0; ii < COUNTOF(oldOptions[i].gr); ii++) {
				if (ii > 0) fputs(" ", outF);
				if (oldOptions[i].pwr > 0 && oldOptions[i].ani > ii) {
					fprintf(outF, "%d", oldOptions[i].gr[ii] + 1);
				}
				else {
					fprintf(outF, "%d", 0);
				}
			}
			fputs("\n", outF);
			fputs("gr2=", outF);
			for (int ii = 0; ii < COUNTOF(oldOptions[i].gr); ii++) {
				if (ii > 0) fputs(" ", outF);
				if (oldOptions[i].pwr > 1 && oldOptions[i].ani > ii) {
					fprintf(outF, "%d", oldOptions[i].gr[ii] + 2);
				}
				else {
					fprintf(outF, "%d", 0);
				}
			}
			fputs("\n", outF);
			fputs("gr3=", outF);
			for (int ii = 0; ii < COUNTOF(oldOptions[i].gr); ii++) {
				if (ii > 0) fputs(" ", outF);
				if (oldOptions[i].pwr > 2 && oldOptions[i].ani > ii) {
					fprintf(outF, "%d", oldOptions[i].gr[ii] + 3);
				}
				else {
					fprintf(outF, "%d", 0);
				}
			}
			fputs("\n", outF);
			fputs("gr4=", outF);
			for (int ii = 0; ii < COUNTOF(oldOptions[i].gr); ii++) {
				if (ii > 0) fputs(" ", outF);
				if (oldOptions[i].pwr > 3 && oldOptions[i].ani > ii) {
					fprintf(outF, "%d", oldOptions[i].gr[ii] + 4);
				}
				else {
					fprintf(outF, "%d", 0);
				}
			}
			fputs("\n", outF);
			fprintf(outF, "wport=%d\n", oldOptions[i].wport);
			fprintf(outF, "wpnum=%d\n", oldOptions[i].wpnum);
			fprintf(outF, "ammo=%d\n", oldOptions[i].ammo);
			fprintf(outF, "stop=%d\n", oldOptions[i].stop);
			fprintf(outF, "icongr=%d\n", oldOptions[i].icongr);
			fprintf(outF, "position=%d\n", positions[i]);
			fprintf(outF, "probability=%d\n", probabilities[i]);
			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/items/sidekicks.tyrr");

	outF = open_extracted_category("data/items/shields.tyrr");
	for (int i = 0; i < SHIELD_NUM + 1; ++i)
	{
		Uint8 nameLen;
		fread_u8_die( &nameLen,                1, f);
		fread_die(    &oldShields[i].name,    1, 30, f);
		oldShields[i].name[MIN(nameLen, 30)] = '\0';
		fread_u8_die( &oldShields[i].tpwr,        1, f);
		fread_u8_die( &oldShields[i].mpwr,        1, f);
		fread_u16_die(&oldShields[i].itemgraphic, 1, f);
		fread_u16_die(&oldShields[i].cost,        1, f);

		#ifdef DATA_EXTRACTOR
		// Calculated by number of times item appeared in shop in episodes 1-4, divided by number of levels
		int probabilities[31] = {
			0,
			16, 40, 44, 68, 61,
			35, 14, 26, 5, 1,
		};
		if (outF != NULL) {
			fprintf(outF, "[shield:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldShields[i].name);
			fprintf(outF, "tpwr=%d\n", oldShields[i].tpwr);
			fprintf(outF, "mpwr=%d\n", oldShields[i].mpwr);
			fprintf(outF, "itemgraphic=%d\n", oldShields[i].itemgraphic);
			fprintf(outF, "cost=%d\n", oldShields[i].cost);
			fprintf(outF, "probability=%d\n", probabilities[i]);
			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/items/shields.tyrr");

	outF = open_extracted_category("data/enemy/enemies.tyrr");
	for (int i = 0; i < ENEMY_NUM + 1; ++i)
	{
		fread_u8_die( &oldEnemyDat[i].ani,           1, f);
		fread_u8_die(  oldEnemyDat[i].tur,           3, f);
		fread_u8_die(  oldEnemyDat[i].freq,          3, f);
		fread_s8_die( &oldEnemyDat[i].xmove,         1, f);
		fread_s8_die( &oldEnemyDat[i].ymove,         1, f);
		fread_s8_die( &oldEnemyDat[i].xaccel,        1, f);
		fread_s8_die( &oldEnemyDat[i].yaccel,        1, f);
		fread_s8_die( &oldEnemyDat[i].xcaccel,       1, f);
		fread_s8_die( &oldEnemyDat[i].ycaccel,       1, f);
		fread_s16_die(&oldEnemyDat[i].startx,        1, f);
		fread_s16_die(&oldEnemyDat[i].starty,        1, f);
		fread_s8_die( &oldEnemyDat[i].startxc,       1, f);
		fread_s8_die( &oldEnemyDat[i].startyc,       1, f);
		fread_u8_die( &oldEnemyDat[i].armor,         1, f);
		fread_u8_die( &oldEnemyDat[i].esize,         1, f);
		fread_u16_die( oldEnemyDat[i].egraphic,     20, f);
		fread_u8_die( &oldEnemyDat[i].explosiontype, 1, f);
		fread_u8_die( &oldEnemyDat[i].animate,       1, f);
		fread_u8_die( &oldEnemyDat[i].shapebank,     1, f);
		fread_s8_die( &oldEnemyDat[i].xrev,          1, f);
		fread_s8_die( &oldEnemyDat[i].yrev,          1, f);
		fread_u16_die(&oldEnemyDat[i].dgr,           1, f);
		fread_s8_die( &oldEnemyDat[i].dlevel,        1, f);
		fread_s8_die( &oldEnemyDat[i].dani,          1, f);
		fread_u8_die( &oldEnemyDat[i].elaunchfreq,   1, f);
		fread_u16_die(&oldEnemyDat[i].elaunchtype,   1, f);
		fread_s16_die(&oldEnemyDat[i].value,         1, f);
		fread_u16_die(&oldEnemyDat[i].eenemydie,     1, f);

		#ifdef DATA_EXTRACTOR
		if (outF != NULL) {
			fprintf(outF, "[enemy:%d]\n", i);
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "ani=%d\n", oldEnemyDat[i].ani);
			fputs("tur=", outF);
			for (int ii = 0; ii < COUNTOF(oldEnemyDat[i].tur); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldEnemyDat[i].tur[ii]);
			}
			fputs("\n", outF);
			fputs("freq=", outF);
			for (int ii = 0; ii < COUNTOF(oldEnemyDat[i].freq); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldEnemyDat[i].freq[ii]);
			}
			fputs("\n", outF);
			fprintf(outF, "xmove=%d\n", oldEnemyDat[i].xmove);
			fprintf(outF, "ymove=%d\n", oldEnemyDat[i].ymove);
			fprintf(outF, "xaccel=%d\n", oldEnemyDat[i].xaccel);
			fprintf(outF, "yaccel=%d\n", oldEnemyDat[i].yaccel);
			fprintf(outF, "xcaccel=%d\n", oldEnemyDat[i].xcaccel);
			fprintf(outF, "ycaccel=%d\n", oldEnemyDat[i].ycaccel);
			fprintf(outF, "startx=%d\n", oldEnemyDat[i].startx);
			fprintf(outF, "starty=%d\n", oldEnemyDat[i].starty);
			fprintf(outF, "startxc=%d\n", oldEnemyDat[i].startxc);
			fprintf(outF, "startyc=%d\n", oldEnemyDat[i].startyc);
			fprintf(outF, "armor=%d\n", oldEnemyDat[i].armor);
			fprintf(outF, "esize=%d\n", oldEnemyDat[i].esize);
			fputs("egraphic=", outF);
			for (int ii = 0; ii < COUNTOF(oldEnemyDat[i].egraphic); ii++) {
				if (ii > 0) fputs(" ", outF);
				fprintf(outF, "%d", oldEnemyDat[i].egraphic[ii]);
			}
			fputs("\n", outF);
			fprintf(outF, "explosiontype=%d\n", (oldEnemyDat[i].explosiontype >> 1));
			fprintf(outF, "animate=%d\n", oldEnemyDat[i].animate);
			fprintf(outF, "shapebank=%d\n", oldEnemyDat[i].shapebank);
			fprintf(outF, "xrev=%d\n", oldEnemyDat[i].xrev);
			fprintf(outF, "yrev=%d\n", oldEnemyDat[i].yrev);
			fprintf(outF, "dgr=%d\n", oldEnemyDat[i].dgr);
			fprintf(outF, "dlevel=%d\n", oldEnemyDat[i].dlevel);
			fprintf(outF, "dani=%d\n", oldEnemyDat[i].dani);
			fprintf(outF, "elaunchfreq=%d\n", oldEnemyDat[i].elaunchfreq);
			tempW = oldEnemyDat[i].elaunchtype % 1000;
			temp = oldEnemyDat[i].elaunchtype / 1000;
			fprintf(outF, "elaunchtype=%d\n", tempW);
			fprintf(outF, "elaunchtypepos=%d\n", tempW);
			fprintf(outF, "elaunchspecial=%d\n", temp);
			fprintf(outF, "value=%d\n", oldEnemyDat[i].value);
			fprintf(outF, "eenemydie=%d\n", oldEnemyDat[i].eenemydie);

			fputs("eshotwait=", outF);
			for (int ii = 0; ii < COUNTOF(oldEnemyDat[i].tur); ii++) {
				if (ii > 0) fputs(" ", outF);

				if (oldEnemyDat[i].tur[ii] == 252)
					temp = 1;
				else if (oldEnemyDat[i].tur[ii] > 0)
					temp = 20;
				else
					temp = 255;

				fprintf(outF, "%d", temp);
			}
			fputs("\n", outF);

			fprintf(outF, "enemyground=%d\n", (oldEnemyDat[i].explosiontype & 1) != 0);

			fputs("\n", outF);
		}
		#endif
	}
	if (outF != NULL)
		close_extracted_file(outF, "data/enemy/enemies.tyrr");
	
	fclose(f);
}
#endif

void JE_initEpisode(JE_byte newEpisode)
{
	if (newEpisode == episodeNum)
		return;
	
	episodeNum = newEpisode;
	
	snprintf(levelFile,    sizeof(levelFile),    "tyrian%d.lvl",  episodeNum);
	snprintf(cube_file,    sizeof(cube_file),    "cubetxt%d.dat", episodeNum);
	snprintf(episode_file, sizeof(episode_file), "levels%d.dat",  episodeNum);
	
	JE_analyzeLevel();
}

void JE_scanForEpisodes(void)
{
	for (int i = 0; i < EPISODE_MAX; ++i)
	{
		char ep_file[20];
		snprintf(ep_file, sizeof(ep_file), "tyrian%d.lvl", i + 1);
		episodeAvail[i] = dir_file_exists(data_dir(), ep_file);
	}
}

unsigned int JE_findNextEpisode(void)
{
	unsigned int newEpisode = episodeNum;
	
	jumpBackToEpisode1 = false;
	
	while (true)
	{
		newEpisode++;
		
		if (newEpisode > EPISODE_MAX)
		{
			newEpisode = 1;
			jumpBackToEpisode1 = true;
			gameHasRepeated = true;
		}
		
		if (episodeAvail[newEpisode-1] || newEpisode == episodeNum)
		{
			break;
		}
	}
	
	return newEpisode;
}
