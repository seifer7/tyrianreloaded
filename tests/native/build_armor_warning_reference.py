"""Trace native armor rescue spawning, sound cadence and warning colors."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void draw_armor_warning(", "void draw_armor_warning(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include "mtrand.h"
#define VGAScreen NULL
#define ITEM_ENEMY_ARMORSHIP 0
#define ItemType_Enemy 7
#define S_WARNING 17
static struct{bool is_alive;uint32_t armor;}player[2];
static struct{uint16_t enemydie;int8_t eyc;uint8_t armorleft;}enemy[100];
static bool twoPlayerMode,galagaMode,isNetworkGame;static uint8_t thisPlayerNum;
static uint16_t armorShipDelay,backMove3,tempW,tempI2;static uint8_t warningSoundDelay,warningCol,soundQueue[8];
static int8_t warningColChange;static int b,tempI,tempItemType,ITEM_INDEX_STORE[1]={42};
static unsigned int mode;
static int JE_newEnemy(int offset,int id,int unique){printf("create:%d,%d,%d\n",offset,id,unique);return mode%3==0?0:51;}
static void get_item_index_from_int(int *type,int *id,uint16_t *out){printf("drop:%d,%d\n",*type,*id);*out=(uint16_t)(*id-500);}
static void fill_rectangle_xy(void *s,int l,int t,int r,int b,uint8_t c){(void)s;printf("fill:%d,%d,%d,%d,%u\n",l,t,r,b,c);}
static void JE_outText(void *s,int x,int y,const char *text,int hue,int bright){(void)s;printf("label:%d,%d,%s,%d,%d\n",x,y,text,hue,bright);}
""" + function(source, "draw_armor_warning") + r"""
int main(void){
 for(mode=0;mode<16384;mode++){
  player[0].is_alive=mode&1;player[1].is_alive=mode&2;twoPlayerMode=mode&4;galagaMode=mode&8;
  isNetworkGame=mode&16;thisPlayerNum=(mode&32)?2:1;
  player[0].armor=(mode>>6)%8;player[1].armor=(mode>>9)%8;
  armorShipDelay=mode%5==0?0:(uint16_t)(mode%503);backMove3=(uint16_t)(mode*97);
  warningSoundDelay=(uint8_t)(mode*13);warningCol=(uint8_t)(mode*71);warningColChange=(int8_t)(mode%7-3);
  soundQueue[7]=9;enemy[50].enemydie=10;enemy[50].eyc=-4;enemy[50].armorleft=255;
  mt_srand(mode+1);printf("case:%u\n",mode);draw_armor_warning();
  printf("state:%u,%u,%u,%d,%u,%u,%d,%u\n",armorShipDelay,warningSoundDelay,warningCol,
   warningColChange,soundQueue[7],enemy[50].enemydie,enemy[50].eyc,enemy[50].armorleft);
  printf("rng:%u\n",mt_rand());
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
