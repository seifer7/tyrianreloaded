"""Generate a native item-parser oracle from unchanged source regions."""

import argparse
import re
from pathlib import Path


def build_source(root):
    source = (root / "src" / "episodes.c").read_text(encoding="utf-8")
    header = (root / "src" / "episodes.h").read_text(encoding="utf-8")
    item_type = header[header.index("enum ITEM_TYPE {"):header.index("\nextern bool value_in_array")]
    error = source[source.index("static void item_data_error("):source.index("/* Tells the game whether")]
    parser = source[source.index("// Fields point into"):source.index("static int compare_item_paths")]
    typed = build_typed_source(root, source, header)
    return r"""
#include <stdlib.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include <errno.h>
#include <limits.h>
#include <stdarg.h>
#include <stdint.h>
#define COUNTOF(x) ((unsigned)(sizeof(x) / sizeof *(x)))
typedef unsigned int uint;
static const char *loading_item_path;
static size_t loading_item_line;
static void SDL_Quit(void) {}
""" + item_type + error + typed[0] + parser + typed[1] + r"""
int main(int argc, char **argv)
{
    if (argc == 3 && strcmp(argv[1], "--typed") == 0) {
        load_typed_items(argv[2]);
        dump_typed_items();
        return 0;
    }
    if (argc != 2) return 2;
    ItemLoad load = { 0 };
    load_item_bundle(argv[1], &load);
    for (int type = 0; type < ItemType_Count; type++) {
        for (size_t i = 0; i < load.types[type].count; i++) {
            ItemRecord *record = &load.types[type].records[i];
            printf("%d:%s:%zu\n", type, record->id, record->line);
            for (size_t j = 0; j < record->field_count; j++)
                printf("%zu:%s=%s\n", record->fields[j].line, record->fields[j].key, record->fields[j].value);
            free(record->fields);
        }
        free(load.types[type].records);
        free(load.types[type].slots);
    }
    for (size_t i = 0; i < load.buffer_count; i++)
        free(load.buffers[i]);
    free(load.buffers);
    return 0;
}
"""


def build_typed_source(root, source, header):
    arrays = {
        "JE_WeaponType": ("weapons", 0),
        "JE_ShipType": ("ships", 1),
        "JE_WeaponPortType": ("weaponPort", 2),
        "JE_PowerType": ("powerSys", 4),
        "JE_ShieldType": ("shields", 5),
        "JE_OptionType": ("options", 6),
        "JE_SpecialType": ("special", 8),
        "JE_EnemyDatType": ("enemyDat", 9),
    }
    declarations = """
typedef uint8_t JE_byte;
typedef int8_t JE_shortint;
typedef uint16_t JE_word;
typedef int16_t JE_integer;
typedef int32_t JE_longint;
typedef bool JE_boolean;
static bool loading_item_data;
static enum ITEM_TYPE tempItemType;
extern void mt_srand(unsigned long);
extern unsigned long mt_rand(void);
bool get_item_index(const enum ITEM_TYPE *, const char *, int *);
int item_idx(const enum ITEM_TYPE *, const char *);
"""
    dump = ["static void dump_typed_items(void) {"]
    for match in re.finditer(r"typedef struct\s*\{([^{}]+)\}\s*(JE_\w+);", header):
        body, name = match.groups()
        if name not in arrays:
            continue
        array, type_number = arrays[name]
        declarations += match.group() + f"\nstatic {name} *{array};\n"
        dump.append(f"for (size_t i = 0; i < {array}[0].count; i++) {{")
        body = re.sub(r"/\*.*?\*/", "", body, flags=re.S)
        for field_type, fields in re.findall(r"(\w+)\s+([^;]+);", body):
            for field in fields.split(","):
                field = field.strip()
                field_name = re.match(r"\w+", field).group()
                if field_name == "count":
                    continue
                dimensions = [int(n) for n in re.findall(r"\[(\d+)\]", field)]
                expression = f"{array}[i].{field_name}"
                dump.append(f'printf("{type_number}:%zu:{field_name.lower()}=", i);')
                if field_type == "char":
                    if len(dimensions) == 2:
                        dump.append(f"for (int j = 0; j < {dimensions[0]}; j++) {{")
                        dump.append('if (j) putchar(\',\');')
                        expression += "[j]"
                    dump.append(f'for (const unsigned char *p = (const unsigned char *){expression}; *p; p++) printf("%02X", *p);')
                    if len(dimensions) == 2:
                        dump.append("}")
                elif dimensions:
                    count = 1
                    for dimension in dimensions:
                        count *= dimension
                    dump.append(f'for (int j = 0; j < {count}; j++) printf("%s%d", j ? "," : "", (int)(({field_type} *){expression})[j]);')
                else:
                    dump.append(f'printf("%d", (int){expression});')
                dump.append('putchar(\'\\n\');')
        dump.append("}")
    dump.append('for (int i = 0; i < ITEM_STORE_MAX; i++) printf("known:%d=%d\\n", i, ITEM_INDEX_STORE[i]);')
    dump += [
        "mt_srand(1234);",
        "for (int type = 0; type < ItemType_Count; type++) {",
        "JE_byte available[10] = {0};",
        "for (int turn = 0; turn < 20; turn++) {",
        "enum ITEM_TYPE item_type = (enum ITEM_TYPE)type;",
        "bool result = add_random_shop_item(&item_type, available, turn % 10, 10);",
        'printf("shop:%d:%d=%d", type, turn, (int)result);',
        'for (int j = 0; j < 10; j++) printf(",%d", (int)available[j]);',
        'putchar(\'\\n\');',
        "}}}",
    ]
    varz = (root / "src" / "varz.h").read_text(encoding="utf-8")
    enum_start = varz.rfind("enum {", 0, varz.index("ITEM_GENERATOR_ADVANCEDMR12"))
    declarations += varz[enum_start:varz.index("};", enum_start) + 2]
    declarations += "\nstatic int ITEM_INDEX_STORE[ITEM_STORE_MAX];\n"
    helpers = (root / "src" / "opentyr.c").read_text(encoding="utf-8")
    declarations += helpers[helpers.index("bool str_pop_int("):helpers.index("static size_t getShopContentsPicker")]
    declarations += source[source.index("bool value_in_array_je_byte("):source.index("#ifdef DATA_EXTRACTOR")]
    load_start = source.index("void load_items(void)")
    load_end = source.index("\n}\n", load_start) + 3
    load_tail = source[source.index('\tloading_item_path = "data";', load_start):load_end]
    load = """
static void load_typed_items(const char *path) {
    loading_item_data = true;
    ItemLoad load = {0};
    ItemRecords *items = load.types;
    load_item_bundle(path, &load);
""" + load_tail
    return declarations, load + "\n" + "\n".join(dump)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
