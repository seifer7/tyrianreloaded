"""Trace the unchanged native level HUD and shield/power updates."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void draw_level_hud(", "void draw_level_hud(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef uint32_t uint;
#define COUNTOF(value) (sizeof(value) / sizeof((value)[0]))
#define OPTION_SHAPES 0
#define S_EXPLOSION_11 11
#define S_SOUL_OF_ZINGLON 9
typedef struct { JE_byte power; } Weapon;
typedef struct { Weapon weapon[2]; } Items;
typedef struct {
    Items items;
    JE_byte *lives;
    unsigned long cash;
    uint shield,shield_max,armor;
    bool is_alive;
} Player;
static Player player[2];
static bool endLevel,galagaMode,twoPlayerMode,onePlayerAction;
static JE_word textErase,mapX3Ofs,oldMapX3Ofs,enemyOnScreen;
static JE_byte shieldT,shieldWait;
static uint power,lastPower,powerAdd;
static uint temp;
static int32_t galagaLife;
static JE_byte soundQueue[8];
static void *VGAScreen,*VGAScreenSeg;
static int optionShapes;
static void blit_sprite(void *screen,int x,int y,int sheet,int index)
{(void)screen;(void)x;(void)y;(void)sheet;(void)index;puts("message");}
static void fill_rectangle_xy(void *screen,int x1,int y1,int x2,int y2,JE_byte color)
{(void)screen;printf("fill:%d,%d,%d,%d,%u\n",x1,y1,x2,y2,color);}
static void JE_rectangle(void *screen,int x1,int y1,int x2,int y2,JE_byte color)
{(void)screen;printf("rectangle:%d,%d,%d,%d,%u\n",x1,y1,x2,y2,color);}
static void JE_drawShield(void){puts("shield");}
static void dump_state(const uint old_weapon_bar[2])
{
 printf("state:%d,%u,%u,%u,%u,%u,%u,%lu,%d,%u,%u,%u,%u,%u,%u,%u,%u\n",
  twoPlayerMode,textErase,shieldWait,player[0].shield,player[1].shield,
  *player[0].lives,*player[1].lives,player[0].cash,galagaLife,
  soundQueue[6],soundQueue[7],old_weapon_bar[0],old_weapon_bar[1],
  power,lastPower,oldMapX3Ofs,enemyOnScreen);
}
""" + function(source, "draw_level_hud") + r"""
int main(void)
{
 for(int scenario=0;scenario<4096;scenario++){
  int mode=scenario%1024;
  galagaMode=(mode&2)!=0;twoPlayerMode=(mode&4)!=0;onePlayerAction=(mode&8)!=0;
  endLevel=(mode&1)!=0;textErase=(JE_word)(scenario%4);shieldT=(JE_byte)(scenario%7);
  power=(uint)(scenario*11%1100);powerAdd=(uint)(scenario*3%50);
  lastPower=(uint)(scenario*7%100);mapX3Ofs=(JE_word)(scenario*5);
  oldMapX3Ofs=999;enemyOnScreen=(JE_word)(scenario%19+1);
  shieldWait=(JE_byte)(scenario%5==0?1:scenario%5==1?0:scenario%16);
  galagaLife=scenario%3==0?10000:scenario*13;
  soundQueue[6]=21;soundQueue[7]=22;
  for(int i=0;i<2;i++){
   player[i].is_alive=(mode&(1<<(i==0?4:5)))!=0;
   player[i].shield=(uint)((scenario*7+i*11)%28);
   player[i].shield_max=(uint)(20+scenario%13);
   player[i].armor=((mode&64)!=0)?1:0;
   player[i].cash=(unsigned long)(scenario*31+i);
   player[i].items.weapon[0].power=(JE_byte)((scenario+i)%5);
   player[i].items.weapon[1].power=(JE_byte)((scenario*3+i)%5);
   player[i].lives=&player[i].items.weapon[i==0?0:1].power;
  }
  if((mode&128)!=0)player[1].armor=0;
  player[0].items.weapon[0].power=(JE_byte)(scenario%12);
  player[0].items.weapon[1].power=(JE_byte)(scenario/3%12);
  uint old_weapon_bar[2]={(uint)(scenario%15),(uint)(scenario%12)};
  printf("case:%d,%d\n",scenario,mode);dump_state(old_weapon_bar);
  draw_level_hud(old_weapon_bar);
  dump_state(old_weapon_bar);
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
