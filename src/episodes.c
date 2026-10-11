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
	return false;
}

void parse_value_pair(const char* buffer, size_t* index, char* key, char* value) {
	size_t i = *index;
	bool key_done = false;
	size_t vi = 0;
	size_t ki = 0;

	for (; ; ++i) {
		char c = buffer[i];
		if(!key_done) key[ki++] = c;
		if (key_done) value[vi++] = c;
		if (c == '=') {
			key_done = true;
			key[ki-1] = '\0';
		}
		else if (c == '\0' || c == '\r' || c == '\n') {
			value[vi-1] = '\0';
			break;
		}
	}
}

void read_item_file(FILE* file, enum ITEM_TYPE item_type) {
	size_t item_i = 0;

	switch (item_type) {
	case ItemType_Ship:
		item_i = ships[0].count++;
		break;
	case ItemType_WeaponFront:
	case ItemType_WeaponRear:
		item_i = weaponPort[0].count++;
		break;
	case ItemType_Generator:
		item_i = powerSys[0].count++;
		break;
	case ItemType_Shield:
		item_i = shields[0].count++;
		break;
	case ItemType_SidekickLeft:
	case ItemType_SidekickRight:
		item_i = options[0].count++;
		break;
	case ItemType_Special:
		item_i = special[0].count++;
		break;
	case ItemType_Shot:
		item_i = weapons[0].count++;
		break;
	case ItemType_Enemy:
		item_i = enemyDat[0].count++;
		break;
	case ItemType_Count:
		break;
	}

	size_t buffer_cap = 128;
	char* buffer = malloc(buffer_cap * sizeof(char));
	if (buffer == NULL) exit(1); // TODO: Error handling
	size_t buffer_end = 1;
	buffer[buffer_end - 1] = '\0';

	for (size_t line = 0, next_line = 0; ; line = next_line)
	{
		char first_char = '\0';
		/* find beginning of next line */
		while (next_line < buffer_end)
		{
			char c = buffer[next_line];
			if (first_char == '\0') first_char = buffer[next_line];

			if (c == '\0' && next_line == buffer_end - 1)
			{
				if (line > 0)
				{
					/* shift to front */
					memmove(&buffer[0], &buffer[line], buffer_end - line);
					buffer_end -= line;
					next_line -= line;
					line = 0;
				}
				else if (buffer_end > 1)
				{
					/* need larger capacity */
					buffer_cap *= 2;
					char* new_buffer = realloc(buffer, buffer_cap * sizeof(char));
					if (new_buffer == NULL) exit(1); // TODO: Error handling
					buffer = new_buffer;
				}

				size_t read = fread(&buffer[buffer_end - 1], sizeof(char), buffer_cap - buffer_end, file);
				if (read == 0)
					break;

				buffer_end += read;
				buffer[buffer_end - 1] = '\0';
			}
			else
			{
				++next_line;

				if (c == '\n' || c == '\r')
				{
					break;
				}
			}
		}

		/* if at end of file */
		if (next_line == line)
			break;

		size_t i = line;

		assert(i <= next_line);

		char key[30] = "";
		char value[255] = "";
		char* tempptr;

		parse_value_pair(buffer, &i, key, value);

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
	
		first_char = '\0';
	}

	free(buffer);
}

void load_items(void) {
	for (int item_type = 0; item_type < ItemType_Count; item_type++) {
		if (item_type == ItemType_WeaponRear) continue;
		if (item_type == ItemType_SidekickRight) continue;
		tinydir_dir dir;

		size_t numItems = 0;
		char itemFileExt[7] = "";
		switch (item_type) {
			case ItemType_Shot:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/shots"));
				strcpy(itemFileExt, "tyrsht");
				break;
			case ItemType_Ship:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/items/ship"));
				strcpy(itemFileExt, "tyrshp");
				break;
			case ItemType_WeaponFront:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/items/weapon"));
				strcpy(itemFileExt, "tyrwep");
				break;
			case ItemType_Generator:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/items/generator"));
				strcpy(itemFileExt, "tyrgen");
				break;
			case ItemType_Shield:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/items/shield"));
				strcpy(itemFileExt, "tyrshd");
				break;
			case ItemType_SidekickLeft:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/items/sidekick"));
				strcpy(itemFileExt, "tyrsid");
				break;
			case ItemType_Special:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/items/special"));
				strcpy(itemFileExt, "tyrspc");
				break;
			case ItemType_Enemy:
				tinydir_open_sorted(&dir, TINYDIR_STRING("./data/enemy"));
				strcpy(itemFileExt, "tyremy");
				break;
			default:
				continue;
			break;
		}

		for (size_t i = 0; i < dir.n_files; i++)
		{
			tinydir_file file;
			if (tinydir_readfile_n(&dir, &file, i) == -1)
			{
				continue; // Failed to get acces to item file
			}
			if (file.is_dir) continue;
			if (_tinydir_strcmp(itemFileExt, file.extension) != 0) continue;
			numItems++;
		}

		switch (item_type) {
		case ItemType_Shot:
			weapons = calloc(max(numItems, 1), sizeof(*weapons));
			//weapons[0].count = numItems;
			break;
		case ItemType_Ship:
			ships = calloc(max(numItems, 1), sizeof(*ships));
			//ships[0].count = numItems;
			break;
		case ItemType_WeaponFront:
			weaponPort = calloc(max(numItems, 1), sizeof(*weaponPort));
			//weaponPort[0].count = numItems;
			break;
		case ItemType_Generator:
			powerSys = calloc(max(numItems, 1), sizeof(*powerSys));
			//powerSys[0].count = numItems;
			break;
		case ItemType_Shield:
			shields = calloc(max(numItems, 1), sizeof(*shields));
			//shields[0].count = numItems;
			break;
		case ItemType_SidekickLeft:
			options = calloc(max(numItems,1), sizeof(*options));
			//options[0].count = numItems;
			break;
		case ItemType_Special:
			special = calloc(max(numItems, 1), sizeof(*special));
			//special[0].count = numItems;
			break;
		case ItemType_Enemy:
			enemyDat = calloc(max(numItems, 1), sizeof(*enemyDat));
			//enemyDat[0].count = numItems;
			break;
		default:
			continue;
			break;
		}

		for (size_t i = 0, s = 0; i < dir.n_files && s < numItems; i++) {
			tinydir_file file;
			if (tinydir_readfile_n(&dir, &file, i) == -1)
			{
				continue; // Failed to get acces to ship file
			}
			if (file.is_dir) continue;
			if (_tinydir_strcmp(itemFileExt, file.extension) != 0) continue;

			FILE* itemFile = fopen(file.path, "r");
			read_item_file(itemFile, item_type);
			fclose(itemFile);

			s++;
		}
		tinydir_close(&dir);
	}

	tempItemType = ItemType_Shot;
	for (size_t i = 0; i < weapons[0].count; i++) {
		int chainlen = COUNTOF(weapons[0].chainIds);
		for (int j = 0; j < chainlen; j++) {
			if (strcmp(weapons[i].chainIds[j], "0") == 0) continue;
			get_item_index(&tempItemType, weapons[i].chainIds[j], &weapons[i].chain[j]);
		}
	}

	tempItemType = ItemType_Shot;
	for (size_t i = 0; i < weaponPort[0].count; i++) {
		if (strcmp(weaponPort[i].linkgunweaponId, "0") != 0)
			get_item_index(&tempItemType, weaponPort[i].linkgunweaponId, &weaponPort[i].linkgunweapon);

		int chargegunweaponslen = COUNTOF(weaponPort->chargegunweapons);
		for (int j = 0; j < chargegunweaponslen; j++) {
			if (strcmp(weaponPort[i].chargegunweaponIds[j], "0") == 0) continue;
			get_item_index(&tempItemType, weaponPort[i].chargegunweaponIds[j], &weaponPort[i].chargegunweapons[j]);
		}
	}

	tempItemType = ItemType_WeaponFront;
	for (size_t i = 0; i < options[0].count; i++) {
		if (strcmp(options[i].wportId, "0") != 0) {
			get_item_index(&tempItemType, options[i].wportId, &options[i].wport);
		}
	}

	tempItemType = ItemType_Enemy;
	for (size_t i = 0; i < enemyDat[0].count; i++) {
		if (strcmp(enemyDat[i].eenemydieId, "0") != 0) {
			get_item_index(&tempItemType, enemyDat[i].eenemydieId, &enemyDat[i].eenemydie);
		}
		if (strcmp(enemyDat[i].elaunchtypeId, "0") != 0) {
			get_item_index(&tempItemType, enemyDat[i].elaunchtypeId, &enemyDat[i].elaunchtype);
		}
	}

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
}

void JE_loadItemDat(void)
{
	FILE *f = NULL;
	
	if (episodeNum <= 3)
	{
		f = dir_fopen_die(data_dir(), "tyrian.hdt", "rb");
		fread_s32_die(&episode1DataLoc, 1, f);
		fseek(f, episode1DataLoc, SEEK_SET);
	}
	else
	{
		// episode 4 stores item data in the level file
		f = dir_fopen_die(data_dir(), levelFile, "rb");
		fseek(f, lvlPos[lvlNum-1], SEEK_SET);
	}

	JE_word itemNum[7]; /* [1..7] */
	fread_u16_die(itemNum, 7, f);

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

		#ifdef DO_DECYRPT
		char filename[60];
		snprintf(filename, sizeof(filename), "data\\shots\\%d.tyrsht", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
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
			fclose(outF);
		}
		#endif
	}
	
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

		#ifdef DO_DECYRPT
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

		char filename[60];
		snprintf(filename, sizeof(filename), "data\\items\\weapon\\%d.tyrwep", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
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
			fclose(outF);
		}
		#endif
	}

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

		#ifdef DO_DECYRPT
		char filename[60];
		snprintf(filename, sizeof(filename), "data\\items\\special\\%d.tyrspc", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldSpecial[i].name);
			fprintf(outF, "itemgraphic=%d\n", oldSpecial[i].itemgraphic);
			fprintf(outF, "pwr=%d\n", oldSpecial[i].pwr);
			fprintf(outF, "stype=%d\n", oldSpecial[i].stype);
			fprintf(outF, "wpn=%d\n", oldSpecial[i].wpn);
			fclose(outF);
		}
		#endif
	}

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

		#ifdef DO_DECYRPT
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
		char filename[60];
		snprintf(filename, sizeof(filename), "data\\items\\generator\\%d.tyrgen", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
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
			fclose(outF);
		}
		#endif
	}

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

		#ifdef DO_DECYRPT
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

		char filename[60];
		snprintf(filename, sizeof(filename), "data\\items\\ship\\%d.tyrshp", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
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
			fclose(outF);
		}
		#endif
	}

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

		#ifdef DO_DECYRPT
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
		char filename[60];
		snprintf(filename, sizeof(filename), "data\\items\\sidekick\\%d.tyrsid", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
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
			fclose(outF);
		}
		#endif
	}

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

		#ifdef DO_DECYRPT
		// Calculated by number of times item appeared in shop in episodes 1-4, divided by number of levels
		int probabilities[31] = {
			0,
			16, 40, 44, 68, 61,
			35, 14, 26, 5, 1,
		};
		char filename[60];
		snprintf(filename, sizeof(filename), "data\\items\\shield\\%d.tyrshd", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
			fprintf(outF, "id=%d\n", i);
			fprintf(outF, "name=%s\n", oldShields[i].name);
			fprintf(outF, "tpwr=%d\n", oldShields[i].tpwr);
			fprintf(outF, "mpwr=%d\n", oldShields[i].mpwr);
			fprintf(outF, "itemgraphic=%d\n", oldShields[i].itemgraphic);
			fprintf(outF, "cost=%d\n", oldShields[i].cost);
			fprintf(outF, "probability=%d\n", probabilities[i]);
			fclose(outF);
		}
		#endif
	}
	
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

		#ifdef DO_DECYRPT
		char filename[60];
		snprintf(filename, sizeof(filename), "data\\enemy\\%d.tyremy", i);
		if (!dir_file_exists(get_user_directory(), filename)) {
			FILE* outF = dir_fopen(get_user_directory(), filename, "w");
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

			fclose(outF);
		}
		#endif
	}
	
	fclose(f);
}

void JE_initEpisode(JE_byte newEpisode)
{
	if (newEpisode == episodeNum)
		return;
	
	episodeNum = newEpisode;
	
	snprintf(levelFile,    sizeof(levelFile),    "tyrian%d.lvl",  episodeNum);
	snprintf(cube_file,    sizeof(cube_file),    "cubetxt%d.dat", episodeNum);
	snprintf(episode_file, sizeof(episode_file), "levels%d.dat",  episodeNum);
	
	JE_analyzeLevel();
	JE_loadItemDat();
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
