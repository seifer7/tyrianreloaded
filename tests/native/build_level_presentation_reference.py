"""Trace native viewport copy, inversion, spotlight and presentation timing."""

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
typedef uint8_t JE_byte;
typedef uint8_t Uint8;
typedef struct{int pitch;uint8_t *pixels;}SDL_Surface;
static uint8_t game_data[80000],primary_data[80000];
static SDL_Surface game,primary,*game_screen=&game,*VGAScreenSeg=&primary;
static struct{int x,y;}player[1];
static bool playerEndLevel,skipStarShowVGA,quitRequested,smoothScroll;
static uint8_t starShowVGASpecialCode,processorType;static uint32_t frameCountMax;
static uint32_t ticks,target;
static void wait_delay(void){if(target>ticks){printf("wait:%u\n",target-ticks);ticks=target;}}
static void setDelay(uint32_t n){target=(uint32_t)((float)ticks+n*65535*((12.0f/14318180.0f)*1000.0f));}
static void JE_showVGA(void){puts("present");}
""" + function(source, "JE_starShowVGA") + r"""
static uint32_t hash(const uint8_t *p,int n){uint32_t h=2166136261u;while(n--)h=(h^*p++)*16777619u;return h;}
int main(void){
 for(unsigned int mode=0;mode<2048;mode++){
  game.pitch=mode%3==0?320:mode%3==1?327:341;primary.pitch=mode%2==0?320:333;
  game.pixels=game_data;primary.pixels=(mode&1024)?game_data:primary_data;
  for(int i=0;i<80000;i++){game_data[i]=(uint8_t)(i*71+i/317+mode*31);primary_data[i]=(uint8_t)(i*17+mode*23);}
  player[0].x=(int)(mode%401)-50;player[0].y=(int)(mode%251)-30;
  playerEndLevel=mode&1;skipStarShowVGA=mode&2;quitRequested=mode&4;smoothScroll=mode%6!=2;
  processorType=(uint8_t)(mode%7);starShowVGASpecialCode=(uint8_t)(mode/16%4);frameCountMax=mode%6==3?4:mode%6==4?3:2;
  ticks=0;target=54;
  printf("case:%u\n",mode);JE_starShowVGA();
  printf("state:%d,%d,%u,%u,%u,%u\n",quitRequested,skipStarShowVGA,hash(primary.pixels,primary.pitch*200),hash(game_data,game.pitch*200),ticks,target-ticks);
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
