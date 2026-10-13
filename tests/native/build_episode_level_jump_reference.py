"""Trace the unchanged native conditional episode-level jump handler."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void handle_episode_level_jump(", "void handle_episode_level_jump(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int8_t JE_shortint;
enum { ITEM_SHIP_STALKER21126 = 0, DIFFICULTY_HARD = 3 };
static int ITEM_INDEX_STORE[1] = {1};
static struct { struct { JE_byte ship; } items; } player[1];
static JE_byte mainLevel, playerShip, stalkerShip;
static JE_word levelTimerCountdown;
static JE_shortint initialDifficulty;
static bool jumpSection, twoPlayerMode, onePlayerAction, levelTimer;
static bool alive;
static bool all_players_alive(void) { return alive; }
""" + function(source, "handle_episode_level_jump") + r"""
int main(void)
{
    const char commands[] = {'J','2','w','t','l','H','x'};
    const char *destinations[] = {"0","1","255","256","-1","-257","  +17tail","word","  -12"};
    for (int mode = 0; mode < 2048; mode++) {
        mainLevel = (JE_byte)(mode * 7);
        jumpSection = (mode & 1) != 0;
        twoPlayerMode = (mode & 2) != 0;
        onePlayerAction = (mode & 4) != 0;
        levelTimer = (mode & 8) != 0;
        levelTimerCountdown = mode % 14 == 3 ? 0 : (uint16_t)(mode * 31);
        initialDifficulty = (JE_shortint)(mode % 8 - 2);
        playerShip = (JE_byte)(mode % 3);
        stalkerShip = (JE_byte)((mode / 3) % 3);
        player[0].items.ship = playerShip;
        ITEM_INDEX_STORE[ITEM_SHIP_STALKER21126] = stalkerShip;
        alive = (mode & 16) != 0;
        char command = commands[mode % (int)sizeof(commands)];
        const char *number = destinations[mode / (int)sizeof(commands) % 9];
        char section[64];
        snprintf(section, sizeof(section), "abc%s", number);
        printf("case:%d,%c,%s,%u,%d\n", mode, command, section, mainLevel, jumpSection);
        handle_episode_level_jump(command, section);
        printf("state:%u,%d\n", mainLevel, jumpSection);
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
