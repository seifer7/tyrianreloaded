"""Trace the unchanged native episode mode configuration routine."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void configure_episode_mode(", "void configure_episode_mode(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
typedef uint8_t JE_byte;
typedef unsigned int uint;
#define COUNTOF(value) (sizeof(value) / sizeof((value)[0]))
enum {
    ITEM_WEAPONREAR_VULCANCANNON = 0, ITEM_SHIP_STALKER21126,
    ITEM_WEAPONFRONT_ATOMICRAILGUN, ITEM_GENERATOR_ADVANCEDMR12,
    ITEM_SHIELD_ADVANCEDINTEGRITYFIELD
};
static int ITEM_INDEX_STORE[5] = {15, 211, 39, 12, 22};
static const int REAR_WEAPON = 1, FRONT_WEAPON = 0;
typedef struct {
    unsigned long cash;
    struct {
        JE_byte ship, generator, shield, special, sidekick[2];
        struct { JE_byte id, power; } weapon[2];
    } items;
} Player;
static Player player[2];
static bool galagaMode, extraGame, doNotSaveBackup, constantDie;
static bool onePlayerAction, superTyrian, twoPlayerMode;

static void dump_player(unsigned int index)
{
    const Player *p = &player[index];
    printf("%lu,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u", p->cash, p->items.ship,
        p->items.weapon[0].id, p->items.weapon[0].power,
        p->items.weapon[1].id, p->items.weapon[1].power, p->items.generator,
        p->items.shield, p->items.special, p->items.sidekick[0], p->items.sidekick[1]);
}
static void dump_state(void)
{
    printf("%d,%d,%d,%d,%d,%d,%d;", galagaMode, extraGame, superTyrian,
        onePlayerAction, twoPlayerMode, constantDie, doNotSaveBackup);
    dump_player(0); putchar(';'); dump_player(1); putchar('\n');
}
""" + function(source, "configure_episode_mode") + r"""
int main(void)
{
    const char commands[] = {'g','x','e','q'};
    for (int mode = 0; mode < 256; mode++) {
        galagaMode = (mode & 1) != 0;
        extraGame = (mode & 2) != 0;
        superTyrian = (mode & 4) != 0;
        onePlayerAction = (mode & 8) != 0;
        twoPlayerMode = (mode & 16) != 0;
        constantDie = (mode & 32) != 0;
        doNotSaveBackup = (mode & 64) != 0;
        for (int i = 0; i < 2; i++) {
            player[i].cash = (unsigned long)(mode * 101 + i);
            player[i].items.ship = (JE_byte)(mode + i);
            player[i].items.generator = (JE_byte)(mode + 2 + i);
            player[i].items.shield = (JE_byte)(mode + 3 + i);
            player[i].items.special = (JE_byte)(mode + 4 + i);
            player[i].items.weapon[0].id = (JE_byte)(mode + 5 + i);
            player[i].items.weapon[0].power = (JE_byte)(mode + 6 + i);
            player[i].items.weapon[1].id = (JE_byte)(mode + 7 + i);
            player[i].items.weapon[1].power = (JE_byte)(mode + 8 + i);
            player[i].items.sidekick[0] = (JE_byte)(mode + 9 + i);
            player[i].items.sidekick[1] = (JE_byte)(mode + 10 + i);
        }
        char command = commands[mode % (int)sizeof(commands)];
        printf("case:%d,%c,", mode, command); dump_state();
        configure_episode_mode(command);
        printf("state:"); dump_state();
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
