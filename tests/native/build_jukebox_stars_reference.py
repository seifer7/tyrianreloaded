"""Trace the unchanged jukebox starfield, keyboard controls, rendering and RNG."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "starlib.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include ".*"$', '', source, flags=re.MULTILINE)
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include "mtrand.h"
typedef int16_t JE_integer;
typedef int8_t JE_shortint;
typedef uint8_t JE_byte,Uint8;
typedef uint16_t JE_word;
typedef bool JE_boolean;
typedef float JE_real;
enum {KMOD_NONE=0,KMOD_SHIFT=3,KMOD_CTRL=192,KMOD_ALT=768,KMOD_GUI=3072,
    SDL_SCANCODE_C=6,SDL_SCANCODE_P=19,SDL_SCANCODE_S=22,SDL_SCANCODE_X=27,
    SDL_SCANCODE_1=30,SDL_SCANCODE_2,SDL_SCANCODE_3,SDL_SCANCODE_4,SDL_SCANCODE_5,
    SDL_SCANCODE_6,SDL_SCANCODE_7,SDL_SCANCODE_8,SDL_SCANCODE_9,SDL_SCANCODE_0,
    SDL_SCANCODE_ESCAPE=41,SDL_SCANCODE_MINUS=45,SDL_SCANCODE_EQUALS=46,
    SDL_SCANCODE_LEFTBRACKET=47,SDL_SCANCODE_RIGHTBRACKET=48,SDL_SCANCODE_GRAVE=53,
    SDL_SCANCODE_KP_MINUS=86,SDL_SCANCODE_KP_PLUS=87,
    SDL_SCANCODE_KP_1=89,SDL_SCANCODE_KP_2,SDL_SCANCODE_KP_3,SDL_SCANCODE_KP_4,
    SDL_SCANCODE_KP_5,SDL_SCANCODE_KP_6,SDL_SCANCODE_KP_7,SDL_SCANCODE_KP_8,SDL_SCANCODE_KP_9,SDL_SCANCODE_KP_0};
static uint8_t pixels[64000];
static struct {uint8_t *pixels;} screen={pixels},*VGAScreen=&screen;
static bool newkey;
static int lastkey_scan,lastkey_mod;
static void wait_noinput(bool key,bool mouse,bool joystick) {printf("release:%d,%d,%d\n",key,mouse,joystick);}
static void wait_input(bool key,bool mouse,bool joystick) {printf("press:%d,%d,%d\n",key,mouse,joystick);}
void JE_wackyCol(void);void JE_newStar(void);void JE_resetValues(void);void JE_changeSetup(JE_byte);
""" + source + r"""
static uint32_t hash(const void *data,size_t size) {
    const unsigned char *bytes=data;uint32_t value=2166136261u;
    for(size_t i=0;i<size;i++)value=(value^bytes[i])*16777619u;return value;
}
static uint32_t floatbits(float value) {uint32_t result;memcpy(&result,&value,4);return result;}
static void dump(int frame) {
    printf("frame:%d,%d,%d,%d,%u,%u,%u,%d,%08X,%08X,%08X,%u,%u,%d,%d,%u,%08X,%08X,%u\n",
        frame,tempX,tempY,run,setup,stepCounter,nsp2,nspVar2Inc,floatbits(nsp),floatbits(nspVarInc),
        floatbits(nspVarVarInc),changeTime,doChange,starlib_speed,speedChange,pColor,
        hash(star,sizeof(star)),hash(pixels,sizeof(pixels)),mt_rand());
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;
    int scene=atoi(argv[1]);mt_srand(314159);JE_starlib_init();JE_starlib_init();
    JE_changeSetup((JE_byte)(scene%15));
    for(int frame=0;frame<40;frame++) {
        newkey=false;
        if(scene>=15) {
            const int keys[]={87,86,47,48,30,31,32,33,22,53,6,27,41,19,89,98,46,39,38,97};
            newkey=frame<20;lastkey_scan=keys[frame%20];
            lastkey_mod=scene==15?(frame>=4&&frame<=7?1:0):scene==16?192:scene==17?1:4096;
        }
        if(scene==19&&frame==0){nsp2=65534;nspVar2Inc=1;starlib_speed=-1;speedChange=-1;pColor=0;}
        if(scene==20&&frame==0){nsp2=1;nspVar2Inc=-1;nspVarInc=-0.1f;nsp=0;pColor=255;}
        JE_starlib_main();dump(frame);
    }
    for(int type=0;type<=14;type++) {
        JE_resetValues();JE_changeSetup((JE_byte)type);
        nsp2=65534;nspVar2Inc=1;
        for(int i=0;i<32;i++){JE_newStar();dump(1000+type*32+i);}
    }
    return 0;
}
"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
