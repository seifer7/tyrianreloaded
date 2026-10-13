"""Verify unchanged player ship initialization and edited-ship graphics."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "varz.c").read_text(encoding="utf-8")
    graphic = function(source.replace("JE_word JE_SGr(", "int JE_SGr("), "JE_SGr").replace("int JE_SGr(", "JE_word JE_SGr(", 1)
    return r"""
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
typedef uint16_t JE_word;
typedef uint8_t JE_byte;
typedef bool JE_boolean;
typedef unsigned int uint;
typedef struct {int id;} Sprite2_array;
#define COUNTOF(x) ((unsigned)(sizeof(x)/sizeof(*(x))))
static Sprite2_array spriteSheet9={0},extraSheet={1},*extraShapes=&extraSheet,*shipGrPtr,*shipGr2ptr;
static JE_word shipGr,shipGr2;
static JE_byte powerAdd,extraShips[154];
static struct {JE_byte power;} powerSys[3];
static struct {JE_word shipgraphic;JE_byte dmg,ani;} ships[3];
static struct {struct {JE_byte ship,generator;} items;uint armor,initial_armor,shot_hit_area_x,shot_hit_area_y;} player[2];
""" + graphic + function(source, "JE_getShipInfo") + r"""
int main(void){
    for(int mode=0;mode<1024;mode++){
        memset(player,0,sizeof player);memset(extraShips,0,sizeof extraShips);
        for(int i=0;i<3;i++){ships[i].shipgraphic=100+i*10;ships[i].dmg=mode+i;ships[i].ani=(mode+i)%3;powerSys[i].power=mode+i*17;}
        for(int i=0;i<10;i++){extraShips[i*15]=(mode+i)%15+1;extraShips[i*15+7]=mode+i*13;}
        for(int p=0;p<2;p++){player[p].items.ship=(mode&(1<<p))?91+(mode/4+p)%10:(mode/4+p)%3;player[p].items.generator=(mode+p)%3;}
        JE_getShipInfo();
        printf("info:%d,%u,%u,%u,%d,%d\n",mode,powerAdd,shipGr,shipGr2,shipGrPtr->id,shipGr2ptr->id);
        for(int p=0;p<2;p++)printf("player:%u,%u,%u,%u\n",player[p].armor,player[p].initial_armor,player[p].shot_hit_area_x,player[p].shot_hit_area_y);
        for(int s=1;s<=10;s++){Sprite2_array *ptr=(s%2)?&spriteSheet9:extraShapes;
            JE_word graphic=JE_SGr(s,&ptr);printf("extra:%d,%u,%d\n",s,graphic,ptr->id);}
    }return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
