"""Trace the unchanged native next-level preparation routine."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static bool prepare_next_level(", "bool prepare_next_level(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
typedef uint8_t JE_byte;
typedef uint16_t Uint16;
#define SA_NONE 0
typedef struct { int unused; } Sprite2_array;
typedef struct { bool is_alive; struct { JE_byte super_arcade_mode; } items; } Player;
static Sprite2_array enemySpriteSheets[4];
static Player player[2];
static bool galagaMode,twoPlayerMode,smoothScroll,play_demo,record_demo;
static bool normalBonusLevelCurrent,bonusLevelCurrent,playerEndLevel;
static bool doNotSaveBackup,superTyrian,onePlayerAction,allPlayersDead;
static bool demoFileOpen;
static unsigned int fastPlay;
static int difficultyLevel,oldDifficultyLevel,mainLevel,nextLevel;
static FILE *demo_file;
static int trace_fclose(FILE *file){(void)file;puts("close-demo");return 0;}
#define fclose trace_fclose
static bool all_players_dead(void)
{return !player[0].is_alive && (!twoPlayerMode || !player[1].is_alive);}
static void mouseSetRelative(bool enabled){printf("relative:%d\n",enabled);}
static void JE_clearKeyboard(void){puts("clear-keyboard");}
static void free_sprite2s(Sprite2_array *sheet)
{printf("free:%d\n",(int)(sheet-enemySpriteSheets));}
static void setDelaySpeed(Uint16 speed){printf("speed:%u\n",speed);}
static void stop_song(void){puts("stop-song");}
static void fade_black(int steps){printf("fade-black:%d\n",steps);}
static void wait_noinput(bool clearEvents,bool allowKeyboard,bool allowMouse)
{printf("wait-no-input:%d,%d,%d\n",clearEvents,allowKeyboard,allowMouse);}
static void JE_endLevelAni(void){puts("end-animation");}
static void fade_song(void){puts("fade-song");}
static void JE_loadGame(int slot){printf("load-game:%d\n",slot);}
static void dump_state(void)
{
    printf("state:%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d\n",
        twoPlayerMode,difficultyLevel,mainLevel,smoothScroll,superTyrian,
        onePlayerAction,        player[0].items.super_arcade_mode,
        doNotSaveBackup,demo_file!=NULL,play_demo,record_demo);
}
""" + function(source, "prepare_next_level") + r"""
int main(void)
{
    for(int scenario=0;scenario<49152;scenario++){
        int mode=scenario/6;
        fastPlay=(unsigned int)(scenario%6);
        galagaMode=(mode&1)!=0;twoPlayerMode=(mode&2)!=0;
        superTyrian=(mode&4)!=0;onePlayerAction=(mode&8)!=0;
        player[0].is_alive=(mode&16)!=0;player[1].is_alive=(mode&32)!=0;
        play_demo=(mode&64)!=0;record_demo=(mode&128)!=0;
        normalBonusLevelCurrent=(mode&256)!=0;bonusLevelCurrent=(mode&512)!=0;
        playerEndLevel=(mode&1024)!=0;doNotSaveBackup=(mode&2048)!=0;
        demoFileOpen=(mode&4096)!=0;
        demo_file=demoFileOpen?(FILE *)1:NULL;
        smoothScroll=true;
        difficultyLevel=mode%8-2;oldDifficultyLevel=(mode+3)%8-2;
        mainLevel=mode%19;nextLevel=mode%23+1;
        player[0].items.super_arcade_mode=(JE_byte)(mode%9);
        printf("case:%d,%d,%u\n",scenario,mode,fastPlay);
        dump_state();
        bool result=prepare_next_level();
        printf("result:%d\n",result);
        dump_state();
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
