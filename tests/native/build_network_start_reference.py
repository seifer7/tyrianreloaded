"""Trace original network-start presentation, details, cancellation and synchronization."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <setjmp.h>
#define COUNTOF(a) ((unsigned int)(sizeof(a)/sizeof((a)[0])))
#define SMALL_FONT_SHAPES 2
#define PACKET_DETAILS 17
#define PACKET_QUIT 32
#define ITEM_SHIP_SILVERSHIP 0
typedef unsigned int uint;
typedef struct {int pitch,h;uint8_t *pixels;} SDL_Surface;
static uint8_t primary_data[64000],background_data[64000];
static SDL_Surface primary={320,200,primary_data},background={320,200,background_data},*VGAScreen=&primary,*VGAScreen2=&background;
static struct {uint8_t data[8];} incoming,outgoing,*packet_in[1],*packet_out_temp=&outgoing;
static struct {uint32_t cash;struct {uint8_t ship;} items;} player[2];
static int colors[256],mode,thisPlayerNum,checks,acknowledgments,stage,ITEM_INDEX_STORE[1];
static bool twoPlayerMode,gameLoaded;
static int8_t difficultyLevel,initialDifficulty;
static uint8_t episodeNum;
static const char *networkText[4]={"","","","Peer chooses episode/difficulty."};
static jmp_buf quit;
static void SDLNet_Write16(uint16_t n,void *out){uint8_t *p=out;p[0]=(uint8_t)(n>>8);p[1]=(uint8_t)n;}
static uint16_t SDLNet_Read16(const void *in){const uint8_t *p=in;return (uint16_t)(p[0]*256u+p[1]);}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static int JE_fontCenter(const char *t,int f){(void)f;return 160-(int)strlen(t);}
static void JE_dString(void *s,int x,int y,const char *t,int f){(void)s;printf("message:%d,%d,%s,%d\n",x,y,t,f);}
static void JE_showVGA(void){puts("present");}
static void fade_palette(void *p,int n,int a,int b){(void)p;printf("fadein:%d,%d,%d\n",n,a,b);}
static void fade_black(int n){printf("fade:%d\n",n);}
static void network_connect(void){puts("connect");}
static bool episodeSelect(void){puts("episode");episodeNum=(uint8_t)(mode%4+1);return (mode&2)!=0;}
static bool difficultySelect(void){puts("difficulty");return (mode&4)!=0;}
static void network_prepare(int n){printf("prepare:%d\n",n);SDLNet_Write16((uint16_t)n,outgoing.data);}
static void network_send(int n){printf("send:%d",n);if(n==8)printf(",%u,%u",SDLNet_Read16(outgoing.data+4),SDLNet_Read16(outgoing.data+6));puts("");stage=1;}
static void network_tyrian_halt(int c,bool sync){printf("quit:%d,%d\n",c,sync);longjmp(quit,1);}
static void service_SDL_events(bool clear){(void)clear;puts("events");}
static void SDL_Delay(uint32_t n){printf("delay:%u\n",n);}
static void network_update(void){puts("update");if(packet_in[0]){packet_in[0]=NULL;stage=1;}}
static void network_check(void){
    puts("check");
    if(stage==0){
        if(++checks==3){const uint16_t d[]={0,1,9,127,128,255,65535};packet_in[0]=&incoming;
            SDLNet_Write16(PACKET_DETAILS,incoming.data);SDLNet_Write16((uint16_t)(mode%4+1),incoming.data+4);
            SDLNet_Write16(d[mode/2%7],incoming.data+6);}
    }else acknowledgments++;
}
static bool network_is_sync(void){return acknowledgments>=mode%3+1;}
static void JE_initEpisode(int n){printf("initialize:%d\n",n);episodeNum=(uint8_t)n;}
""" + function(source, "networkStartScreen") + r"""
int main(int argc,char **argv){
    if(argc!=2)return 2;ITEM_INDEX_STORE[0]=atoi(argv[1]);
    const int8_t levels[]={-128,-1,0,1,2,9,127};
    for(mode=0;mode<112;mode++){
        thisPlayerNum=mode%2+1;stage=checks=acknowledgments=0;packet_in[0]=NULL;
        difficultyLevel=levels[mode/2%7];initialDifficulty=-5;episodeNum=3;twoPlayerMode=mode&16;gameLoaded=mode&32;
        player[0].cash=123;player[1].cash=456;player[0].items.ship=7;player[1].items.ship=8;
        printf("case:%d\n",mode);bool terminated=setjmp(quit)!=0;if(!terminated)networkStartScreen();
        printf("state:%d,%u,%d,%d,%d,%d,%u,%u,%u,%u\n",terminated,episodeNum,difficultyLevel,initialDifficulty,
            twoPlayerMode,gameLoaded,player[0].cash,player[1].cash,player[0].items.ship,player[1].items.ship);
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
