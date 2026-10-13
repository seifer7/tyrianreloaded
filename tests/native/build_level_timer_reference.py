"""Trace native debug overlay, bonus flashes, Pentium timing and level countdown."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void draw_level_debug_and_timer(", "void draw_level_debug_and_timer(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <math.h>
typedef int32_t JE_longint;
typedef uint8_t JE_byte;
static uint32_t debugTime,lastDebugTime,ticks,frameCountMax;
static int32_t debugHistCount;static float debugHist;
static int tempW,tempX,tempY,lastmouse_but,mouse_x,mouse_y,enemyOnScreen,VGAScreen;
static bool debug,pentiumMode,levelTimer,smoothies[9];
static uint8_t displayTime,flash;static int8_t flashChange;
static uint16_t mapX,curLoc,levelTimerCountdown,levelTimerJumpTo;
static struct{int x,y,x_velocity,y_velocity;}player[1];
static char tempStr[256];static char *miscText[68];
static uint8_t soundQueue[8];
static uint32_t SDL_GetTicks(void){return ticks;}
static void JE_outText(int s,int x,int y,const char*t,int h,int b){printf("debug:%d,%d,%s,%d,%d\n",x,y,t,h,b);}
static void JE_outTextAndDarken(int s,int x,int y,const char*t,unsigned int h,unsigned int b,unsigned int f){printf("notice:%u\n",b);}
static void JE_textShade(int s,int x,int y,const char*t,int h,int b,int sh){printf("label:%d\n",b);}
static void JE_dString(int s,int x,int y,const char*t,unsigned int f){printf("value:%s\n",t);}
static void JE_eventJump(uint16_t target){printf("jump:%u\n",target);curLoc=target;}
#define FONT_SHAPES 0
#define SMALL_FONT_SHAPES 1
#define FULL_SHADE 1
#define S_WARNING 17
#define S_CLICK 24
""" + function(source, "draw_level_debug_and_timer") + r"""
int main(void){
 char buffer[256];miscText[59]="Bonus level";miscText[66]="Time remaining";
 for(unsigned int mode=0;mode<4096;mode++){
  debug=mode&1;pentiumMode=mode%6==5;levelTimer=mode&2;
  displayTime=(uint8_t)(mode%7);flash=(uint8_t)(mode*71);flashChange=(int8_t)(mode%9-4);
  debugHist=0;debugHistCount=0;lastDebugTime=0;
  frameCountMax=mode%6==3?4:mode%6==4?3:2;
  levelTimerCountdown=(uint16_t)(mode%4==0?0:mode%4==1?1:mode%4==2?mode%701:mode*97);
  levelTimerJumpTo=(uint16_t)(mode*31);curLoc=(uint16_t)(mode*41);mapX=(uint16_t)(mode*97);
  player[0].x=(int)(mode%401)-50;player[0].y=(int)(mode%251)-30;
  player[0].x_velocity=(int)(mode%41)-20;player[0].y_velocity=(int)(mode%43)-21;
  enemyOnScreen=(int)(mode%203);soundQueue[6]=8;soundQueue[7]=9;
  for(int i=0;i<9;i++)smoothies[i]=(mode>>i)&1;
  for(unsigned int frame=0;frame<4;frame++){
   ticks=(mode%17==0?2147483648u+mode*97:mode%11==0?0:mode*97)+frame*(mode%61);
   printf("case:%u,%u\n",mode,frame);draw_level_debug_and_timer(buffer);
   printf("state:%u,%u,%d,%u,%u,%u,%u,%u,%u,%d,%.0f\n",displayTime,flash,flashChange,frameCountMax,
     levelTimerCountdown,soundQueue[6],soundQueue[7],curLoc,lastDebugTime,debugHistCount,(double)debugHist);
  }
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
