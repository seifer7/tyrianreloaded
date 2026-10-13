"""Trace unchanged in-game help, shared presentation state and input wait ordering."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    return r"""
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
typedef int SDL_Surface;
static SDL_Surface primary,game,*VGAScreen=&game,*VGAScreenSeg=&primary;
static bool twoPlayerMode,newkey,newmouse;
static uint8_t helpBoxColor,helpBoxBrightness;
static uint16_t textErase;
static const char *miscText[5]={"","","","","Continue"};
static int mode,frame;
#define OPTION_SHAPES 5
#define TINY_FONT 2
static void JE_clearKeyboard(void){puts("clear");}
static void JE_wipeKey(void){puts("wipe");}
static void JE_barShade(SDL_Surface *s,int l,int t,int r,int b){printf("shade:%d,%d,%d,%d\n",l,t,r,b);}
static void fill_rectangle_xy(SDL_Surface *s,int l,int t,int r,int b,int c){printf("fill:%d,%d,%d,%d,%d\n",l,t,r,b,c);}
static void blit_sprite(SDL_Surface *s,int x,int y,unsigned int table,unsigned int sprite){printf("option:%d,%d,%u\n",x,y,sprite);}
static void JE_HBox(SDL_Surface *s,int x,int y,unsigned int number,unsigned int width){
    printf("box:%d,%d,%u,%u,%u,%u\n",x,y,number,width,helpBoxColor,helpBoxBrightness);helpBoxColor=12;
}
static int JE_textWidth(const char *text,int font){puts("width:Continue");return 39;}
static void JE_outText(SDL_Surface *s,int x,int y,const char *text,unsigned int hue,int b){printf("text:%d,%d,%s\n",x,y,text);}
static void service_SDL_events(bool clear){
    printf("events:%d\n",clear);
    if(clear){newkey=newmouse=false;frame++;}
    else {newkey=(mode&2)==0&&frame==mode%5+1;newmouse=(mode&2)!=0&&frame==mode%5+1;}
}
static void JE_mouseStart(void){puts("mouse");}
static void JE_showVGA(void){puts("present");}
static void JE_mouseReplace(void){puts("replace");}
static void SDL_Delay(unsigned int ms){printf("delay:%u\n",ms);}
static void push_joysticks_as_keyboard(void){puts("joysticks");}
static void keep_alive(void){puts("keep");}
#define NETWORK_KEEP_ALIVE() keep_alive()
""" + function(source, "JE_inGameHelp") + r"""
int main(void) {
    for(mode=0;mode<32;mode++) {
        frame=0;newkey=newmouse=true;twoPlayerMode=mode&1;textErase=100;VGAScreen=&game;
        printf("help:%d\n",mode);JE_inGameHelp();
        printf("state:%u,%u,%u,%d,%d,%d\n",textErase,helpBoxColor,helpBoxBrightness,newkey,newmouse,VGAScreen==&game);
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
