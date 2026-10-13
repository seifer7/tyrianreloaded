"""Trace native initial level map offsets, parallax and ship-state setup."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void initialize_level_map_and_ship_state(", "void initialize_level_map_and_ship_state(")
    return r"""
#include <stdint.h>
#include <stdio.h>
#include <stdbool.h>
#include <stddef.h>
#define COUNTOF(array) (sizeof(array) / sizeof((array)[0]))
typedef uint16_t JE_word;
typedef unsigned int uint;
typedef struct{int x,y,x_velocity,y_velocity;unsigned int invulnerable_ticks;}player_t;
typedef struct{uint8_t *mainmap[300][14];}mega_data1_t;
typedef struct{uint8_t *mainmap[600][14];}mega_data2_t;
typedef struct{uint8_t *mainmap[600][15];}mega_data3_t;
static player_t player[2];
static JE_word mapY,mapY2,mapY3,mapX,mapX3,mapXPos,mapXOfs,oldMapXOfs,mapX2Pos,mapX2Ofs,mapX3Pos,mapX3Ofs;
static uint8_t map1YDelay,map1YDelayMax,map2YDelay,map2YDelayMax;
static uint8_t **mapYPos,**mapY2Pos,**mapY3Pos;
static mega_data1_t megaData1;
static mega_data2_t megaData2;
static mega_data3_t megaData3;
static int mapXbpPos,mapX2bpPos,mapX3bpPos;
static JE_word backPos,backPos2,backPos3,power;
static int starfield_speed;
static bool musicFade;
static void JE_getShipInfo(void){}
""" + function(source, "initialize_level_map_and_ship_state") + r"""
int main(void){
 for(unsigned int mode=0;mode<1024;mode++){
  mapX=(JE_word)(mode*97);mapX3=(JE_word)(mode*31);musicFade=mode&1;
  backPos=(JE_word)(mode*17);backPos2=(JE_word)(mode*19);backPos3=(JE_word)(mode*23);
  power=(JE_word)(mode*29);starfield_speed=(int)(mode*37);
  mapXPos=(JE_word)(mode*41);mapXOfs=(JE_word)(mode*43);oldMapXOfs=(JE_word)(mode*47);
  mapX2Pos=(JE_word)(mode*53);mapX2Ofs=(JE_word)(mode*49);mapX3Pos=(JE_word)(mode*59);mapX3Ofs=(JE_word)(mode*61);
  mapXbpPos=(int)(mode*61)-30000;mapX2bpPos=(int)(mode*67)-30000;mapX3bpPos=(int)(mode*71)-30000;
  map1YDelay=map1YDelayMax=map2YDelay=map2YDelayMax=(uint8_t)(mode+2);
  for(int i=0;i<2;i++){player[i].x_velocity=(int)(mode*73+i)-900;player[i].y_velocity=(int)(mode*79+i)-1000;player[i].invulnerable_ticks=mode*83+i;}
  printf("case:%u\n",mode);initialize_level_map_and_ship_state();
  printf("state:%u,%u,%u,%td,%td,%td,%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%d,%d,%u,%u,%u,%u,%d,%u,%u,%u,%u,%d\n",
   mapY,mapY2,mapY3,mapYPos-&megaData1.mainmap[0][0],mapY2Pos-&megaData2.mainmap[0][0],
   mapY3Pos-&megaData3.mainmap[0][0],mapX,mapX3,mapXPos,mapXOfs,oldMapXOfs,mapX2Pos,mapX2Ofs,mapX3Pos,mapX3Ofs,
   mapXbpPos,mapX2bpPos,mapX3bpPos,map1YDelay,map1YDelayMax,map2YDelay,map2YDelayMax,
   musicFade,backPos,backPos2,backPos3,power,starfield_speed);
  for(int i=0;i<2;i++)printf("player:%d,%d,%u\n",player[i].x_velocity,player[i].y_velocity,player[i].invulnerable_ticks);
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
