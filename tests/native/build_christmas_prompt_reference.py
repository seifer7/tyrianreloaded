"""Trace unchanged Christmas prompt with an explicit reproducible rand service."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "xmas.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#define COUNTOF(a) ((unsigned int)(sizeof(a)/sizeof((a)[0])))
#define MOUSE_POINTER_NORMAL 0
#define SDL_BUTTON_LEFT 1
#define SDL_BUTTON_RIGHT 3
#define SDL_SCANCODE_LEFT 80
#define SDL_SCANCODE_RIGHT 79
#define SDL_SCANCODE_SPACE 44
#define SDL_SCANCODE_RETURN 40
#define SDL_SCANCODE_KP_ENTER 88
#define SDL_SCANCODE_ESCAPE 41
typedef int16_t Sint16;
typedef uint16_t Uint16;
typedef uint8_t Uint8;
typedef unsigned int uint;
static struct {void *data;} shopSpriteSheet;
static int VGAScreen,spriteSheet8,normal_font=1,centered=1,palettes[1],mouseCursor;
static int mode,steps,frames,mouse_x,mouse_y,lastmouse_x,lastmouse_y,lastmouse_but,lastkey_scan;
static bool newkey,newmouse;
static uint32_t state;
static int month;
static time_t scripted_time(time_t *out){if(out)*out=0;return 0;}
static struct tm *scripted_localtime(const time_t *now){(void)now;static struct tm t;t.tm_mon=month;return &t;}
#define time scripted_time
#define localtime scripted_localtime
static int scripted_rand(void){state=state*214013u+2531011u;return (int)((state>>16)&32767u);}
#define rand scripted_rand
static void JE_loadCompShapes(void *s,int c){(void)s;shopSpriteSheet.data=(void*)1;printf("cursor:%c\n",c);}
static void fill_rectangle_wh(int s,int x,int y,int w,int h,int c){(void)s;printf("fill:%d,%d,%d,%d,%d\n",x,y,w,h,c);frames++;}
static void blit_sprite2_blend(int s,int x,int y,int sheet,int n){(void)s;(void)sheet;printf("flake:%d,%d,%d\n",x,y,n);}
static void draw_font_hv_full_shadow(int s,int x,int y,const char *t,int f,int a,int hue,int b,bool black,int d){
    (void)s;(void)f;(void)a;printf("label:%d,%d,%s,%d,%d,%d,%d\n",x,y,t,hue,b,black,d);
}
static void fade_palette(int p,int n,int a,int b){(void)p;printf("fadein:%d,%d,%d,%d\n",n,a,b,mouseCursor);}
static void fade_black(int n){printf("fade:%d\n",n);}
static void JE_mouseStart(void){puts("mouse");}
static void JE_showVGA(void){puts("present");}
static void JE_mouseReplace(void){puts("replace");}
static void SDL_Delay(uint32_t n){printf("delay:%u\n",n);}
static void service_SDL_events(bool clear){if(clear)newkey=newmouse=false;}
static void push_joysticks_as_keyboard(void){
    puts("joystick");
    if(frames<(mode>=288?350:mode%4+1))return;
    int kind=mode%12;newkey=newmouse=false;
    if(steps>=2){newkey=true;lastkey_scan=mode&16?40:41;}
    else if(kind<2){newkey=true;lastkey_scan=kind==0?80:79;}
    else if(kind<6){const int scans[]={44,40,88,41};newkey=true;lastkey_scan=scans[kind-2];}
    else if(kind==6){newmouse=true;lastmouse_but=3;}
    else if(kind<11){
        newkey=true;newmouse=true;lastkey_scan=41;lastmouse_but=kind==10?2:1;
        const int xs[]={119,120,159,160,199,200},ys[]={119,120,132,133};
        mouse_x=xs[mode/12%6];mouse_y=ys[mode/72%4];
        lastmouse_x=kind==7?mouse_x:mode&32?500:0;lastmouse_y=kind==7?mouse_y:0;
    }else {mouse_x=steps&1?160:-1;mouse_y=120;}
    steps++;
}
""" + function(source, "xmas_time") + function(source, "xmas_prompt") + r"""
int main(void){
    for(month=0;month<12;month++)printf("month:%d,%d\n",month,xmas_time());
    for(mode=0;mode<300;mode++){
        state=(uint32_t)(mode+1);shopSpriteSheet.data=mode&1?(void*)1:NULL;mouse_x=mouse_y=10;
        lastmouse_x=lastmouse_y=0;newkey=newmouse=false;frames=steps=0;printf("case:%d\n",mode);
        bool result=xmas_prompt();printf("result:%d,%d,%d,%u\n",result,frames,steps,state);
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
