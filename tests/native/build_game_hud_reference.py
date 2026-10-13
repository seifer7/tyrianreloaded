"""Compare unchanged in-game HUD and cube rendering call order without game assets."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <limits.h>
typedef unsigned int uint;
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef struct {int unused;} SDL_Surface;
static SDL_Surface screen,*VGAScreen=&screen;
static int sheet9,sheet10,*spriteSheet9=&sheet9,*spriteSheet10=&sheet10;
#define OPTION_SHAPES 5
#define FULL_SHADE 1
#define TINY_FONT 2
#define COUNTOF(x) ((unsigned)(sizeof(x)/sizeof *(x)))
typedef struct {unsigned long cash;JE_byte *lives;uint superbombs;struct {JE_byte special,power;} items;} Player;
static Player player[2];
static struct {unsigned int itemgraphic;} special[3];
static char miscText[80][32],network_player_name[32],network_opponent_name[32];
static JE_byte thisPlayerNum;
static JE_word tempW;
static bool twoPlayerMode,galagaMode,onePlayerAction,isNetworkGame,youAreCheating;
static void JE_textShade(SDL_Surface *s,int x,int y,const char *text,int hue,int value,int shade) {
    printf("text:%d,%d,%s,%d,%d,1\n",x,y,text,hue,value);
}
static void JE_outText(SDL_Surface *s,int x,int y,const char *text,int hue,int value) {
    printf("text:%d,%d,%s,%d,%d,0\n",x,y,text,hue,value);
}
static int JE_textWidth(const char *text,int font) {
    int width=0;for(const unsigned char *p=(const unsigned char*)text;*p;p++)width+=*p%7+1;
    printf("width:%s,%d\n",text,width);return width;
}
static void blit_sprite2(SDL_Surface *s,int x,int y,int *sheet,int graphic) {
    printf("icon:%d,%d,%d,%d,0\n",sheet==spriteSheet9?9:10,x,y,graphic);
}
static void blit_sprite2x2(SDL_Surface *s,int x,int y,int *sheet,int graphic) {
    printf("icon:%d,%d,%d,%d,1\n",sheet==spriteSheet9?9:10,x,y,graphic);
}
static void blit_sprite_dark(SDL_Surface *s,int x,int y,uint table,uint frame,bool black) {
    printf("dark:%d,%d,%u,%d\n",x,y,frame,black);
}
static void blit_sprite_hv(SDL_Surface *s,int x,int y,uint table,uint frame,uint8_t hue,int8_t value) {
    printf("hue:%d,%d,%u,%u,%d\n",x,y,frame,hue,value);
}
""" + function(source, "JE_getName") + function(source, "JE_inGameDisplays") + function(source, "JE_drawCube") + r"""
int main(void) {
    for(int i=0;i<80;i++)sprintf(miscText[i],"text%d",i);
    special[1].itemgraphic=19;special[2].itemgraphic=327;
    for(int mode=0;mode<4096;mode++) {
        memset(player,0,sizeof player);
        twoPlayerMode=mode&1;galagaMode=mode&2;onePlayerAction=mode&4;
        isNetworkGame=mode&8;youAreCheating=mode&16;thisPlayerNum=(mode&32)?2:1;
        strcpy(network_player_name,(mode&64)?"local":"");strcpy(network_opponent_name,(mode&128)?"opponent":"");
        for(int i=0;i<2;i++) {
            player[i].lives=&player[i].items.power;*player[i].lives=(mode/16+i*3)%256;
            player[i].cash=(mode&256)?ULONG_MAX-mode-i:mode*7919ul+i;
            player[i].superbombs=(mode/32+i)%12;
        }
        player[0].items.special=mode%3;
        printf("hud:%d\n",mode);JE_inGameDisplays();
        JE_drawCube(VGAScreen,(JE_word)(mode*37),(JE_word)(mode*19),(JE_byte)mode,(JE_byte)(mode*3));
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
