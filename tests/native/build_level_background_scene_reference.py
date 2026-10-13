"""Trace the unchanged native level-background composition routine."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void draw_level_backgrounds(", "void draw_level_backgrounds(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stddef.h>
#define S_WEAPON_7 7
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef uint16_t Uint16;
typedef uint8_t Uint8;
typedef unsigned int uint;
static bool forceEvents,starActive,wild,background2notTransparent,superWild,enemiesActive,background2;
static bool skyEnemyOverAll,background3x1,topEnemyOver;
static bool smoothies[9];
static JE_byte smoothie_data[9],background2over,background3over,map1YDelay,map1YDelayMax;
static JE_byte stopBackgroundNum,astralDuration,processorType,soundQueue[8];
static JE_word backMove,backPos,mapY,curLoc,enemyOnScreen,neat,tempBackMove;
static JE_word mapXOfs,mapX2Ofs,oldMapX3Ofs,mapX3Ofs,tempMapXOfs,backMove3;
static JE_word levelEnemyFrequency,levelEnemyMax,levelEnemy[40],tempW;
static int starfield_speed,screen1,screen0,b;
static int *VGAScreen=&screen1,*game_screen=&screen0;
static uint8_t map_storage[64];
static uint8_t *mapYPos=map_storage+32;
static int scenario,rng_calls,enemy_calls;
static uint mt_rand(void){rng_calls++;return (uint)(scenario*71+rng_calls*13);}
static void draw_background_1(int *screen){printf("bg1:%d\n",*screen);}
static void draw_background_2(int *screen){printf("bg2:%d\n",*screen);}
static void draw_background_2_blend(int *screen){printf("bg2blend:%d\n",*screen);}
static void draw_background_3(int *screen){printf("bg3:%d\n",*screen);}
static void JE_clr256(int *screen){printf("clear:%d\n",*screen);}
static void update_and_draw_starfield(int *screen,int speed){printf("stars:%d,%d\n",*screen,speed);}
static void iced_blur_filter(int *destination,int *screen){printf("iced:%d,%d\n",*destination,*screen);}
static void lava_filter(int *destination,int *screen){printf("lava:%d,%d\n",*destination,*screen);}
static void water_filter(int *destination,int *screen){printf("water:%d,%d\n",*destination,*screen);}
static void blur_filter(int *destination,int *screen){printf("blur:%d,%d\n",*destination,*screen);}
static void JE_drawEnemy(int priority)
{
 printf("enemy:%d,%d,%u,%u,%u\n",priority,*VGAScreen,tempMapXOfs,tempBackMove,enemyOnScreen);
 enemy_calls++;
 if(priority==50||priority==25)enemyOnScreen++;
}
static int JE_newEnemyOld(int x,int type,int y){(void)x;(void)y;printf("spawn:%d\n",type);return 17+type;}
static void JE_darkenBackground(JE_word amount){printf("darken:%d,%u\n",*VGAScreen,amount);}
static void dump(int last)
{
 printf("state:%u,%u,%u,%u,%u,%d,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%u,%u\n",
  curLoc,backMove,backPos,mapY,(unsigned)(mapYPos-map_storage),map1YDelay,astralDuration,
  *VGAScreen,background2,enemyOnScreen,last,tempMapXOfs,tempBackMove,stopBackgroundNum,
  neat,soundQueue[3],b,rng_calls,enemy_calls,background2over,background3over,
  mapXOfs,mapX2Ofs,oldMapX3Ofs,mapX3Ofs,background3x1,backMove3);
}
""" + function(source, "draw_level_backgrounds") + r"""
int main(void)
{
 for(scenario=0;scenario<4096;scenario++){
  int mode=scenario%2048;
  forceEvents=(mode&1)!=0;starActive=(mode&2)!=0;wild=(mode&4)!=0;
  background2notTransparent=(mode&8)!=0;superWild=(mode&16)!=0;
  enemiesActive=(mode&32)!=0;skyEnemyOverAll=(mode&64)!=0;
  background3x1=(mode&128)!=0;topEnemyOver=(mode&256)!=0;
  smoothies[0]=(mode&512)!=0;smoothies[1]=(mode&1024)!=0;
  smoothies[2]=(scenario&1)!=0;smoothies[3]=(scenario&2)!=0;smoothies[4]=(scenario&4)!=0;
  smoothie_data[0]=(JE_byte)(scenario%3);background2over=(JE_byte)(scenario%4);
  background3over=(JE_byte)(scenario/3%3);map1YDelay=(JE_byte)(scenario%5);
  map1YDelayMax=(JE_byte)(scenario%4);backMove=(JE_word)(scenario%4);
  backPos=(JE_word)(scenario%33);mapY=(JE_word)(scenario%17);
  curLoc=(JE_word)(scenario*3);astralDuration=(JE_byte)(scenario%2?0:3);
  processorType=(JE_byte)(scenario%6);starfield_speed=scenario%9-4;
  enemyOnScreen=(JE_word)(scenario%7);stopBackgroundNum=(JE_byte)(scenario%4);
  neat=(JE_word)(scenario*11);mapXOfs=(JE_word)(scenario*2);
  mapX2Ofs=(JE_word)(scenario*3);oldMapX3Ofs=(JE_word)(scenario*5);
  mapX3Ofs=(JE_word)(scenario*7);backMove3=(JE_word)(scenario%8);
  tempMapXOfs=(JE_word)(scenario%19);tempBackMove=(JE_word)(scenario%13);
  levelEnemyFrequency=(JE_word)(scenario%101);levelEnemyMax=(JE_word)(scenario%40+1);
  for(int i=0;i<40;i++)levelEnemy[i]=(JE_word)((i+scenario)%5);
  soundQueue[3]=(JE_byte)(scenario%16);background2=false;b=0;
  VGAScreen=&screen1;game_screen=&screen0;*VGAScreen=1;*game_screen=0;
  mapYPos=map_storage+32;rng_calls=0;enemy_calls=0;
  int last=(scenario*13)%11;
  printf("case:%d\n",scenario);dump(last);
  draw_level_backgrounds(&last);
  dump(last);
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
