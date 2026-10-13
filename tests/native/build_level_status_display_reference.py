"""Trace the unchanged native level-status display initializer."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void initialize_level_status_display(", "void initialize_level_status_display(")
    return r"""
#include <stdint.h>
#include <stdio.h>
#include <stdbool.h>
typedef uint8_t JE_byte;
typedef int8_t JE_shortint;
typedef uint16_t JE_word;
typedef unsigned int uint;
#define COUNTOF(array) (sizeof(array) / sizeof((array)[0]))
typedef struct { JE_byte shield; } player_items_t;
typedef struct {
 player_items_t items;
 uint shield, shield_max, armor, superbombs;
} player_t;
typedef struct { JE_byte tpwr, mpwr; } shield_t;
static player_t player[2];
static shield_t shields[3]={{127,127},{127,127},{127,127}};
static JE_byte shieldWait,shieldT,flash,displayTime,levelSong;
static JE_shortint flashChange;
static JE_word cubeMax;
static char miscText20[]={4,7,9,0};
static char *miscText[21];
static void JE_drawTextWindow(const char *text){if(text!=miscText20)return;puts("text:20");}
static void JE_drawShield(void){}
static void JE_drawArmor(void){for(unsigned int i=0;i<COUNTOF(player);i++)if(player[i].armor>28)player[i].armor=28;}
static void JE_drawPortConfigButtons(void){puts("ports");}
static void play_song(unsigned int song){printf("song:%u\n",song);}
""" + function(source, "initialize_level_status_display") + r"""
int main(void){
 miscText[20]=miscText20;
 for(unsigned int mode=0;mode<256;mode++){
  cubeMax=(JE_word)(mode*31);shieldWait=(JE_byte)(mode*7);shieldT=(JE_byte)(mode*11);
  displayTime=(JE_byte)(mode*37);flash=(JE_byte)(mode*41);flashChange=(JE_shortint)(mode*43);
  levelSong=(JE_byte)mode;
  for(unsigned int i=0;i<2;i++){
   player[i].items.shield=(JE_byte)((mode*7+i*11)%3);
   player[i].shield=mode*13+i*17;player[i].shield_max=mode*19+i*23;
   player[i].armor=mode*13+i*19;player[i].superbombs=mode*23+i*5;
  }
  printf("case:%u\n",mode);initialize_level_status_display();
  printf("state:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%u\n",
   shieldWait,shieldT,player[0].shield,player[0].shield_max,player[0].armor,player[0].superbombs,
   player[1].shield,player[1].shield_max,player[1].armor,player[1].superbombs,cubeMax,flash,flashChange,displayTime);
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
