"""Verify unchanged in-game keyboard chords, edited ships and request ordering."""

import argparse
from pathlib import Path
from build_joystick_reference import function

SCANCODES = {
    "TAB": 43, "CAPSLOCK": 57, "1": 30, "2": 31, "9": 38, "0": 39,
    "F1": 58, "F2": 59, "F3": 60, "F4": 61, "F5": 62, "F6": 63, "F7": 64,
    "F8": 65, "F9": 66, "F10": 67, "F11": 68, "F12": 69, "BACKSLASH": 49,
    "SLASH": 56, "C": 6, "BACKSPACE": 42, "P": 19, "ESCAPE": 41,
    "NUMLOCKCLEAR": 83, "SCROLLLOCK": 71, "MINUS": 45,
}


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    definitions = "\n".join(f"#define SDL_SCANCODE_{name} {value}" for name, value in SCANCODES.items())
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mtrand.h"
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int8_t JE_shortint;
typedef int SDL_Surface;
typedef unsigned int uint;
#define COUNTOF(x) ((unsigned)(sizeof(x)/sizeof(*(x))))
#define SA_NONE 0
#define MUSIC_NUM 41
#define FRONT_WEAPON 0
#define REAR_WEAPON 1
#define LEFT_SIDEKICK 0
#define RIGHT_SIDEKICK 1
#define ITEM_SHIP_NORTSHIP 0
#define ITEM_SPECIAL_ASTRALZONE 1
#define ITEM_WEAPONFRONT_NORTSHIPSUPERPULSE 2
#define ITEM_WEAPONREAR_NORTSHIPSPREADER 3
static int ITEM_INDEX_STORE[4];
static struct {struct {JE_byte ship,special,sidekick[2],generator,shield;struct {JE_byte id,power;} weapon[2];} items;
    uint armor,weapon_mode;} player[2];
static int mode,x,temp;
static JE_word tempW,shipGr,shipGr2,levelTimerCountdown;
static JE_byte keysactive[512],extraShips[154],shotMultiPos[11],superArcadeMode,levelEnd,fastPlay;
static bool isNetworkGame,extraAvail,superTyrian,twoPlayerMode,constantPlay,youAreCheating;
static bool editShip1,editShip2,helpRequest,skipStarShowVGA,nortShipRequest,debug,skipLevelRequest;
static bool levelTimer,endLevel,pause_pressed,ingamemenu_pressed,superPause,smoothies[9];
static float debugHist;
static unsigned int debugHistCount,lastDebugTime;
static JE_shortint levelFilter;
static int screen1,screen2,*VGAScreen=&screen1,*VGAScreenSeg=&screen2;
static const char *miscText[64];
static void JE_gammaCheck(void){puts("gamma");if(keysactive[68])keysactive[68]=0;}
static JE_word JE_portConfigs(void){puts("ports");return mode%4;}
static void JE_getShipInfo(void){puts("ships");player[0].armor=70+mode%40;player[1].armor=30+mode%50;shipGr=30;shipGr2=31;}
static void JE_wipeShieldArmorBars(void){puts("wipe");}
static void JE_drawArmor(void){puts("armor");}
static void JE_drawShield(void){puts("shield");}
static void JE_drawOptions(void){puts("options");}
static void JE_inGameHelp(void){puts("help");}
static void JE_drawTextWindow(const char *s){puts("text");}
static uint32_t SDL_GetTicks(void){return 123456789u+mode;}
static void JE_setNewGameSpeed(void){puts("speed");}
static void play_song(uint song){printf("song:%u\n",song);}
""" + definitions + "\n" + function(source, "JE_mainKeyboardInput") + r"""
int main(int argc,char **argv){
    if(argc!=5)return 2;for(int i=0;i<4;i++)ITEM_INDEX_STORE[i]=atoi(argv[i+1]);miscText[62]="Cheat";
    for(mode=0;mode<4096;mode++){
        memset(player,0,sizeof player);memset(keysactive,0,sizeof keysactive);memset(smoothies,0,sizeof smoothies);
        for(int i=0;i<11;i++)shotMultiPos[i]=i+1;
        for(int i=0;i<150;i++)extraShips[i]=i%3;
        for(int p=0;p<2;p++){player[p].armor=10+p*20;player[p].weapon_mode=3+p;player[p].items.ship=p;}
        isNetworkGame=mode&1;superTyrian=mode&2;twoPlayerMode=mode&4;superArcadeMode=(mode&8)?1:0;
        constantPlay=mode&16;youAreCheating=mode&32;debug=mode&64;superPause=mode&128;extraAvail=mode&128;
        editShip1=mode&256;editShip2=mode&512;fastPlay=(mode&1024)?255:mode%6;
        levelFilter=mode%21==0?-99:mode%21-4;levelTimer=false;levelTimerCountdown=123;levelEnd=0;endLevel=false;
        pause_pressed=mode&256;ingamemenu_pressed=mode&512;
        helpRequest=nortShipRequest=skipLevelRequest=skipStarShowVGA=false;shipGr=10;shipGr2=11;
        debugHist=0;debugHistCount=lastDebugTime=0;
        const int base[]={6,19,30,31,32,38,39,41,42,43,45,49,56,57,58,59,60,61,62,63,64,65,66,67,68,69,71,83};
        for(unsigned int i=0;i<COUNTOF(base);i++)keysactive[base[i]]=((mode*37+i*13)%7)==0?2:0;
        int groups[][8]={{43,57,30,33,39,0,0,0},{59,61,63,64,66,49,56,0},{59,60,63,6,0,0,0,0},
            {59,60,61,0,0,0,0,0},{59,63,64,0,0,0,0,0},{42,69,71,31,38,39,0,0},
            {42,45,83,67,0,0,0,0},{42,30,71,0,0,0,0,0}};
        for(int i=0;i<8;i++)if(groups[(mode/128)%8][i])keysactive[groups[(mode/128)%8][i]]=1;
        mt_srand(mode+1);printf("keyboard:%d\n",mode);JE_mainKeyboardInput();
        printf("flags:%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%u,%d,%g,%u,%u\n",
            editShip1,editShip2,helpRequest,skipStarShowVGA,nortShipRequest,youAreCheating,debug,skipLevelRequest,
            levelTimerCountdown,levelTimer,endLevel,pause_pressed,ingamemenu_pressed,superPause,levelEnd,levelFilter,debugHist,debugHistCount,lastDebugTime);
        printf("speed:%u,%u,%u\n",fastPlay,shipGr,shipGr2);
        for(int p=0;p<2;p++)printf("player:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",player[p].items.ship,
            player[p].items.weapon[0].id,player[p].items.weapon[1].id,player[p].items.special,player[p].items.sidekick[0],
            player[p].items.sidekick[1],player[p].items.generator,player[p].items.shield,player[p].armor,player[p].weapon_mode);
        printf("smooth");for(int i=0;i<9;i++)printf(",%d",smoothies[i]);puts("");
        printf("multi");for(int i=0;i<11;i++)printf(",%u",shotMultiPos[i]);puts("");
        printf("keys");for(int i=0;i<512;i++)printf(",%u",keysactive[i]);puts("");
        printf("rng:%u\n",(uint32_t)mt_rand());
    }return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
