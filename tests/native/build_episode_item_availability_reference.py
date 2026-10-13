"""Trace the unchanged native episode shop availability reader."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace(
        "static void read_episode_item_availability(", "void read_episode_item_availability("
    )
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t JE_byte;
enum ITEM_TYPE {
    ItemType_Ship, ItemType_WeaponFront, ItemType_WeaponRear, ItemType_Generator,
    ItemType_Count, ItemType_SidekickLeft, ItemType_SidekickRight, ItemType_Shield
};
#define COUNTOF(value) (sizeof(value) / sizeof((value)[0]))
static JE_byte itemAvail[9][10], itemAvailMax[9];
static int shopContentSetting, tempI, screenCalls;
static void JE_itemScreen(void) { screenCalls++; }
static void read_encrypted_pascal_string(char *out, size_t size, FILE *stream)
{
    static const unsigned char key[] = {204,129,63,255,71,19,25,62,1,99};
    unsigned char length, text[255];
    if (fread(&length, 1, 1, stream) != 1 || fread(text, 1, length, stream) != length)
        abort();
    for (size_t i = length; i-- > 0;) {
        text[i] ^= key[i % sizeof(key)];
        if (i != 0) text[i] ^= text[i-1];
    }
    if (size == 0 || length >= size) abort();
    memcpy(out, text, length);
    out[length] = 0;
}
static bool str_pop_int(char *text, int *value)
{
    char *end;
    *value = (int)strtol(text, &end, 10);
    if (end == text) return false;
    memmove(text, end, strlen(end) + 1);
    return true;
}
static void get_item_index_from_int(const enum ITEM_TYPE *type, const int *id, int *index)
{
    static const int ships[] = {12,11,8,13,2,1};
    static const int weapons[] = {39,23,25,36,1,16,24,26,37,15};
    static const int generators[] = {2};
    static const int sidekicks[] = {24};
    static const int shields[] = {2,4};
    const int *ids = NULL;
    size_t count = 0;
    switch (*type) {
        case ItemType_Ship: ids = ships; count = COUNTOF(ships); break;
        case ItemType_WeaponFront:
        case ItemType_WeaponRear: ids = weapons; count = COUNTOF(weapons); break;
        case ItemType_Generator: ids = generators; count = COUNTOF(generators); break;
        case ItemType_SidekickLeft:
        case ItemType_SidekickRight: ids = sidekicks; count = COUNTOF(sidekicks); break;
        case ItemType_Shield: ids = shields; count = COUNTOF(shields); break;
        default: return;
    }
    for (size_t i = 0; i < count; i++)
        if (ids[i] == *id) { *index = (int)i + 2; return; }
}
static bool add_random_shop_item(const enum ITEM_TYPE *type, JE_byte available[],
    unsigned int index, unsigned int count)
{
    (void)type; (void)available; (void)index; (void)count;
    return false;
}
""" + function(source, "read_episode_item_availability") + r"""
static const char *ids[9] = {
    "12 11 8 13 2 1", "39 23 25 36 1 16", "24 26 37 15", "2",
    "", "24", "24", "", "2 4"
};
static void encrypt_write(FILE *stream, const unsigned char *text, size_t length)
{
    static const unsigned char key[] = {204,129,63,255,71,19,25,62,1,99};
    unsigned char encrypted[255], n = (unsigned char)length, previous = 0;
    fwrite(&n, 1, 1, stream);
    for (size_t i = 0; i < length; i++) {
        encrypted[i] = text[i] ^ key[i % sizeof(key)] ^ previous;
        previous = encrypted[i];
    }
    fwrite(encrypted, 1, length, stream);
}
static void dump_state(long position)
{
    printf("state:%ld,%d,", position, screenCalls);
    for (int row = 0; row < 9; row++) {
        if (row) putchar(';');
        printf("%u:", itemAvailMax[row]);
        for (int column = 0; column < 10; column++) {
            if (column) putchar(',');
            printf("%u", itemAvail[row][column]);
        }
    }
    putchar('\n');
}
int main(void)
{
    for (int mode = 0; mode < 15; mode++) {
        shopContentSetting = mode - 1;
        FILE *stream = tmpfile();
        for (int row = 0; row < 9; row++) {
            char line[128];
            snprintf(line, sizeof(line), "abcdefgh%s", ids[row]);
            size_t length = strlen(line);
            if ((mode & 1) && ids[row][0]) {
                size_t used = strlen(ids[row]);
                char duplicate[64];
                snprintf(duplicate, sizeof(duplicate), "%s", ids[row]);
                int spaces = 0;
                for (char *p = duplicate; *p; p++)
                    if (*p == ' ' && ++spaces == 5) { *p = 0; break; }
                snprintf(line + 8 + used, sizeof(line) - 8 - used, " %s", duplicate);
                length = strlen(line);
            }
            encrypt_write(stream, (const unsigned char *)line, length);
        }
        rewind(stream);
        screenCalls = 0;
        printf("case:%d,%d\n", mode, shopContentSetting);
        read_episode_item_availability(stream, (char[256]){0});
        dump_state(ftell(stream));
        fclose(stream);
    }
    return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
