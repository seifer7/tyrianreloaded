"""Trace unchanged glow text frame/brightness/input/network ordering."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "fonthand.c").read_text(encoding="utf-8")
    return r"""
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
typedef int16_t JE_integer;
typedef uint8_t JE_byte;
typedef int SDL_Surface;
static SDL_Surface surface;
static unsigned int frameCountMax;
static JE_byte textGlowFont,textGlowBrightness;
static bool warningRed;
static int mode,buttons;
static void JE_outTextAdjust(SDL_Surface *s,int x,int y,const char *text,unsigned int hue,int value,unsigned int font,bool shadow) {
    printf("draw:%d,%d,%s,%u,%d,%u\n",x,y,text,hue,value,font);
}
static void setDelay(int frames){printf("delay:%d\n",frames);}
static bool JE_anyButton(void){buttons++;bool result=buttons==(mode%25);printf("button:%d\n",result);return result;}
static void keep_alive(void){puts("keep");}
#define NETWORK_KEEP_ALIVE() keep_alive()
static void JE_showVGA(void){puts("present");}
static void wait_delay(void){puts("wait");}
""" + function(source, "JE_outTextGlow") + r"""
int main(void) {
    for(mode=0;mode<1024;mode++) {
        frameCountMax=(mode&1)?4:0;textGlowFont=mode%3;textGlowBrightness=mode%17;warningRed=mode&2;buttons=0;
        printf("glow:%d\n",mode);
        JE_outTextGlow(&surface,50,75,(mode&4)?"~Hello":"");
        printf("state:%u,%u,%d\n",frameCountMax,textGlowBrightness,buttons);
        JE_outTextGlow(&surface,70,95,"Second");
        printf("state:%u,%u,%d\n",frameCountMax,textGlowBrightness,buttons);
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
