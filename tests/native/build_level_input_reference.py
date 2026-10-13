"""Trace native level game-over, demo interruption, keyboard, pause and setup."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static bool handle_level_input(", "bool handle_level_input(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#define VGAScreen NULL
#define FONT_SHAPES 0
#define SONG_GAMEOVER 10
static struct {uint32_t exploding_ticks;} player[2];
static bool constantPlay,constantDie,allPlayersGone,galagaMode,musicFade,play_demo;
static bool normalBonusLevelCurrent,bonusLevelCurrent,reallyEndLevel,firstGameOver;
static bool newkey,newmouse,button[3],isNetworkGame,stopped_demo,skipStarShowVGA;
static bool pause_pressed,windowHasFocus,pauseRequest,ingamemenu_pressed,inGameMenuRequest,yourInGameMenuRequest;
static const char *miscText[22]={"","","","","","","","","","","","","","","","","","","","","","Game over"};
static unsigned int mode,services;static uint16_t tyrMusicVolume,fxVolume;
static void JE_dString(void *s,int x,int y,const char *t,int f){(void)s;printf("draw:%d,%d,%s,%d\n",x,y,t,f);}
static void play_song(int n){printf("song:%d\n",n);}
static void set_volume(uint16_t m,uint16_t e){printf("volume:%u,%u\n",m,e);}
static void push_joysticks_as_keyboard(void){puts("joysticks");}
static void service_SDL_events(bool clear){
 if(clear)newkey=newmouse=false;
 if(services++==0){
  if((mode&1024)!=0)newkey=true;
  if((mode&2048)!=0)newmouse=true;
 }
}
static void JE_mainKeyboardInput(void){puts("keyboard");skipStarShowVGA=(mode&4096)!=0;
 pause_pressed|=(mode&8192)!=0;ingamemenu_pressed|=(mode&16384)!=0;}
static void JE_pauseGame(void){puts("pause");}
static void JE_doInGameSetup(void){puts("setup");yourInGameMenuRequest=false;}
""" + function(source, "handle_level_input") + r"""
int main(void){
 for(mode=0;mode<32768;mode++){
  constantPlay=mode&1;constantDie=mode&2;allPlayersGone=mode&4;galagaMode=mode&8;
  play_demo=mode&16;normalBonusLevelCurrent=mode&32;bonusLevelCurrent=mode&64;
  firstGameOver=mode&128;isNetworkGame=mode&256;windowHasFocus=!(mode&512);
  player[0].exploding_ticks=mode%5==0?17:0;player[1].exploding_ticks=mode%7==0?19:0;
  musicFade=mode%11==0;reallyEndLevel=mode%13==0;stopped_demo=mode%17==0;
  newkey=mode%19==0;newmouse=mode%23==0;button[0]=mode%29==0;button[1]=mode%31==0;button[2]=mode%37==0;
  skipStarShowVGA=mode%41==0;pause_pressed=mode%43==0;ingamemenu_pressed=mode%47==0;
  pauseRequest=mode%53==0;inGameMenuRequest=mode%59==0;yourInGameMenuRequest=mode%61==0;
  services=0;tyrMusicVolume=(uint16_t)(mode*71);fxVolume=(uint16_t)(mode*97);
  printf("case:%u\n",mode);bool result=handle_level_input();
  printf("state:%d,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u\n",result,
   player[0].exploding_ticks,player[1].exploding_ticks,musicFade,reallyEndLevel,firstGameOver,stopped_demo,
   newkey,newmouse,skipStarShowVGA,pause_pressed,ingamemenu_pressed,pauseRequest,inGameMenuRequest,services);
  printf("your:%d\n",yourInGameMenuRequest);
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
