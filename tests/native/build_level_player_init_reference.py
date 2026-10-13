"""Trace native level player placement, palette setup and explosion-sheet loading."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void initialize_level_players_and_display(", "void initialize_level_players_and_display(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#define assert(condition) ((void)0)
typedef uint8_t JE_byte;typedef int8_t JE_shortint;
typedef unsigned int uint;
typedef struct{int x,y,old_x[20],old_y[20],last_x_shot_move,last_y_shot_move;bool is_alive;}player_t;
static player_t player[2];
static int VGAScreen;
static JE_shortint difficultyLevel,oldDifficultyLevel;
static uint8_t episodeNum;static bool twoPlayerMode;
static char levelName[32]="Sample level";
static uint8_t colors[256],gammaCorrection;
static struct{uint8_t *data;}explosionSpriteSheet;
#define COUNTOF(x) (sizeof(x)/sizeof((x)[0]))
#define EPISODE_AVAILABLE 4
#define DIFFICULTY_EASY 1
static void JE_loadPic(int s,int n,bool b){printf("picture:%d,%d,%d\n",n,b,s);}
static void JE_drawOptions(void){puts("options");}
static void JE_outText(int s,int x,int y,const char*t,int h,int b){printf("text:%d,%d,%s,%d,%d\n",x,y,t,h,b);}
static void JE_showVGA(void){puts("present");}
static void JE_gammaCorrect(void *c,uint8_t g){printf("gamma:%u\n",g);}
static void fade_palette(uint8_t *c,int n,int first,int last){printf("fade:%d,%d,%d\n",n,first,last);}
static void JE_loadCompShapes(void *s,char c){printf("shapes:%c\n",c);explosionSpriteSheet.data=(uint8_t*)1;}
""" + function(source, "initialize_level_players_and_display") + r"""
int main(void){
 for(unsigned int mode=0;mode<512;mode++){
  twoPlayerMode=(mode&1)!=0;episodeNum=(uint8_t)(mode%6);
  difficultyLevel=(int8_t)(mode%15-3);oldDifficultyLevel=(int8_t)(mode*31);
  gammaCorrection=(uint8_t)(mode%4);explosionSpriteSheet.data=(mode&2)?(uint8_t*)1:NULL;
  for(int p=0;p<2;p++){player[p].x=(int)(mode*17+p*53)-500;player[p].y=(int)(mode*29+p*71)-700;
   player[p].is_alive=(mode>>p)&1;player[p].last_x_shot_move=-777;player[p].last_y_shot_move=888;
   for(int j=0;j<20;j++){player[p].old_x[j]=-111;player[p].old_y[j]=222;}}
  printf("case:%u\n",mode);initialize_level_players_and_display();
  printf("state:%d,%d,%d,%d\n",difficultyLevel,oldDifficultyLevel,episodeNum,twoPlayerMode);
  for(int p=0;p<2;p++){printf("player:%d,%d,%d,%d,%d,%d\n",player[p].is_alive,player[p].x,player[p].y,
    player[p].last_x_shot_move,player[p].last_y_shot_move,0);
   for(int j=0;j<20;j++)printf("history:%d,%d\n",player[p].old_x[j],player[p].old_y[j]);}
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
