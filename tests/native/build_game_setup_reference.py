"""Trace the unchanged local/network in-game setup wrapper."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = function((root / "src" / "mainint.c").read_text(encoding="utf-8"), "JE_doInGameSetup")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#define WITH_NETWORK
#define PACKET_GAME_MENU 0x32
#define PACKET_WAITING 0x21
#define PACKET_GAME_QUIT 0x30
#define SMALL_FONT_SHAPES 1
#define SDL_SCANCODE_ESCAPE 41
typedef int SDL_Surface;
static SDL_Surface primary,game,*VGAScreen=&game,*VGAScreenSeg=&primary;
static bool haltGame,isNetworkGame,yourInGameMenuRequest,reallyEndLevel,playerEndLevel,quitRequested,constantPlay;
static int mode,stage,keysactive[512];
static struct Packet {uint8_t data[4];} received,*packet_in[1];
static uint16_t SDLNet_Read16(const uint8_t *p) {return (uint16_t)((p[0]<<8)|p[1]);}
static void mouseSetRelative(bool enabled) {printf("relative:%d\n",enabled);}
static void network_prepare(int type) {printf("send:%d\n",type);}
static void network_send(int length) {(void)length;}
static void service_SDL_events(bool clear) {(void)clear;}
static void SDL_Delay(uint32_t delay) {(void)delay;}
static void network_update(void) {if(packet_in[0]!=NULL) {packet_in[0]=NULL;stage++;}}
static void network_check(void) {
    if(packet_in[0]==NULL) {
        received.data[0]=0;received.data[1]=(uint8_t)(stage==0?PACKET_GAME_MENU:((mode&4)?PACKET_GAME_QUIT:PACKET_WAITING));
        packet_in[0]=&received;
    }
}
static bool network_is_sync(void) {return true;}
static bool JE_inGameSetup(void) {
    puts("menu");
    if(mode&4) {
        if(constantPlay)puts("halt");
        if(isNetworkGame) {haltGame=true;playerEndLevel=true;}
        return true;
    }
    return false;
}
static void JE_barShade(SDL_Surface *screen,int l,int t,int r,int b) {
    if(screen!=&primary)exit(3);printf("shade:%d,%d,%d,%d\n",l,t,r,b);
}
static void JE_dString(SDL_Surface *screen,int x,int y,const char *text,int font) {
    if(screen!=&primary)exit(3);printf("draw:%d,%d,%s,%d\n",x,y,text,font);
}
static void JE_showVGA(void) {puts("present");}
""" + source + r"""
int main(int argc,char **argv) {
    if(argc!=2)return 2;mode=atoi(argv[1]);
    yourInGameMenuRequest=(mode&1)!=0;isNetworkGame=(mode&2)!=0;constantPlay=(mode&8)!=0;
    playerEndLevel=(mode&16)!=0;reallyEndLevel=(mode&32)!=0;
    haltGame=true;quitRequested=true;keysactive[41]=1;
    JE_doInGameSetup();
    printf("state:%d,%d,%d,%d,%d,%d,%d\n",haltGame,playerEndLevel,reallyEndLevel,quitRequested,
        keysactive[41],yourInGameMenuRequest,VGAScreen==&game);
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
