"""Trace unchanged score-page drawing and keyboard/mouse navigation."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = function((root / "src" / "mainint.c").read_text(encoding="utf-8"), "JE_highScoreScreen")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define COUNTOF(a) ((unsigned int)(sizeof(a)/sizeof((a)[0])))
#define MIN(a,b) ((a)<(b)?(a):(b))
#define FULL_SHADE 1
#define S_CURSOR 28
#define S_SPRING 16
#define MOUSE_POINTER_NORMAL 0
#define SDL_BUTTON_LEFT 1
#define SDL_BUTTON_RIGHT 3
#define SDL_SCANCODE_LEFT 80
#define SDL_SCANCODE_RIGHT 79
#define SDL_SCANCODE_SPACE 44
#define SDL_SCANCODE_RETURN 40
#define SDL_SCANCODE_KP_ENTER 88
#define SDL_SCANCODE_ESCAPE 41
typedef uint8_t Uint8;
typedef struct {int32_t highScore1;char highScoreName[30];uint8_t highScoreDiff;} JE_SaveFileType;
typedef struct {int pitch,h;uint8_t *pixels;} SDL_Surface;
static uint8_t primary_data[320*200],background_data[320*200];
static SDL_Surface primary={320,200,primary_data},background={320,200,background_data},*VGAScreen=&primary,*VGAScreen2=&background;
typedef struct {void *data;} SpriteSheet;
static SpriteSheet shopSpriteSheet;
static JE_SaveFileType saveFiles[22];
static char miscText[68][42],episode_name[6][31],difficultyNameB[11][21];
static bool episodeAvail[5],newkey,newmouse;
static int mode,step,waits,lastmouse_but,lastkey_scan,mouse_x,mouse_y,helpBoxColor,mouseCursor;
static int large_font=0,normal_font=1,centered=1,colors[256];
static void JE_loadCompShapes(void *s,int c){(void)s;shopSpriteSheet.data=(void*)1;printf("cursor:%c\n",c);}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static void fill_rectangle_wh(void *s,int x,int y,int w,int h,int c){(void)s;printf("fill:%d,%d,%d,%d,%d\n",x,y,w,h,c);}
static void draw_font_hv_shadow(SDL_Surface *s,int x,int y,const char *t,int f,int a,int hue,int b,bool black,int distance) {
    (void)x;(void)a;(void)hue;(void)black;(void)distance;printf("header:%d,%d,%s,%d,%d\n",s==VGAScreen2,y,t,f,b);
}
static void JE_textShade(void *s,int x,int y,const char *t,int hue,int b,int shade) {
    (void)s;(void)hue;(void)shade;printf("shaded:%d,%d,%s,%d\n",x,y,t,b);
}
static void blit_sprite2x2(void *s,int x,int y,SpriteSheet sheet,int i){(void)s;(void)y;(void)sheet;printf("arrow:%d,%d\n",x,i);}
static void JE_helpBox(void *s,int x,int y,const char *t,int w){(void)s;(void)x;(void)y;(void)w;printf("help:%s,%d\n",t,helpBoxColor);}
static void fade_palette(void *p,int steps,int first,int last){(void)p;(void)first;(void)last;printf("fadein:%d,%d\n",steps,mouseCursor);}
static void fade_black(int n){printf("fade:%d\n",n);}
static void JE_mouseStart(void){puts("mouse");}
static void JE_showVGA(void){puts("present");}
static void JE_mouseReplace(void){puts("replace");}
static void SDL_Delay(uint32_t n){printf("delay:%u\n",n);}
static void JE_playSampleNum(int n){printf("sample:%d\n",n);}
static void push_joysticks_as_keyboard(void){
    puts("joystick");
    if(++waits<mode%3+1)return;
    int kind=step==7?16+(mode/16)%4:(mode+step*3)%16;
    step++;waits=0;
    int scans[]={80,79,0,80,79,0,0,0,0,0,0,0,0,0,79,80,44,40,88,41};
    newkey=kind<3 || kind>=14;newmouse=kind>=3 && kind<16;lastkey_scan=scans[kind];
    int xs[]={0,0,0,82,83,106,107,212,213,236,237,213,83,213,83,0};
    mouse_x=kind<16?xs[kind]:0;mouse_y=kind==11?178:kind==12?500:179;
    lastmouse_but=kind==13?2:kind==14?3:1;
}
static void service_SDL_events(bool clear){if(clear)newkey=newmouse=false;}
""" + source + r"""
int main(int argc,char **argv){
    if(argc!=2)return 2;mode=atoi(argv[1]);shopSpriteSheet.data=mode&1?(void*)1:NULL;
    for(int i=0;i<68;i++)sprintf(miscText[i],"Text%d",i);
    for(int i=0;i<6;i++)sprintf(episode_name[i],"Episode%d",i);
    for(int i=0;i<11;i++)sprintf(difficultyNameB[i],"Rank%d",i);
    for(int i=0;i<5;i++)episodeAvail[i]=(mode&(1<<i))!=0;
    const int32_t scores[]={0,-1,INT32_MIN,INT32_MAX,123456789,-987654321};
    for(int i=0;i<22;i++){saveFiles[i].highScore1=scores[(mode+i)%6];saveFiles[i].highScoreDiff=(uint8_t)(mode+i);
        sprintf(saveFiles[i].highScoreName,"Name%d",i);}
    JE_highScoreScreen();printf("steps:%d\n",step);return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
