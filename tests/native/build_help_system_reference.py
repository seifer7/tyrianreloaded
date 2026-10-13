"""Trace original help topics, all eight page layouts and keyboard/mouse navigation."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8").replace("static ", "")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#define COUNTOF(a) ((unsigned int)(sizeof(a)/sizeof((a)[0])))
#define MAX_PAGE 8
#define SONG_MAPVIEW 19
#define MOUSE_POINTER_NORMAL 0
#define MOUSE_POINTER_LEFT 3
#define MOUSE_POINTER_RIGHT 4
#define S_CURSOR 28
#define S_SELECT 8
#define S_SPRING 16
#define SDL_BUTTON_LEFT 1
#define SDL_BUTTON_RIGHT 3
#define SDL_SCANCODE_UP 82
#define SDL_SCANCODE_DOWN 81
#define SDL_SCANCODE_LEFT 80
#define SDL_SCANCODE_RIGHT 79
#define SDL_SCANCODE_SPACE 44
#define SDL_SCANCODE_RETURN 40
#define SDL_SCANCODE_KP_ENTER 88
#define SDL_SCANCODE_ESCAPE 41
typedef uint8_t Uint8;
typedef uint16_t Uint16;
typedef uint8_t JE_byte;
typedef char JE_char;
typedef struct {int pitch,h;uint8_t *pixels;} SDL_Surface;
static uint8_t primary_data[64000],background_data[64000];
static SDL_Surface primary={320,200,primary_data},background={320,200,background_data},*VGAScreen=&primary,*VGAScreen2=&background;
static struct {void *data;} shopSpriteSheet;
static const JE_byte topicStart[6]={0,1,2,3,7,255};
static char topicName[6][21],miscText[68][42];
static int large_font,normal_font=1,small_font=2,centered=1,left_aligned=0,right_aligned=2;
static int colors[256],mouseCursor,helpBoxBrightness=1,verticalHeight=7;
static int mode,steps,waits,mouse_x,mouse_y,lastmouse_x,lastmouse_y,lastmouse_but,lastkey_scan;
static bool newkey,newmouse,pager;
static void JE_loadCompShapes(void *s,int c){(void)s;shopSpriteSheet.data=(void*)1;printf("cursor:%c\n",c);}
static void play_song(int n){printf("song:%d\n",n);}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static void fill_rectangle_wh(void *s,int x,int y,int w,int h,int c){(void)s;printf("fill:%d,%d,%d,%d,%d\n",x,y,w,h,c);}
static int JE_textWidth(const char *s,int f){(void)f;printf("width:%s\n",s);return (int)strlen(s)*2;}
static void draw_font_hv_shadow(void *s,int x,int y,const char *t,int f,int a,int h,int b,bool black,int d){
    (void)s;(void)x;(void)a;(void)h;(void)black;(void)d;if(y==1)pager=true;else if(y==30)pager=false;
    printf("header:%d,%s,%d,%d\n",y,t,f,b);
}
static void draw_font_hv(void *s,int x,int y,const char *t,int f,int a,int h,int b){
    (void)s;(void)y;(void)f;(void)h;(void)b;printf("footer:%d,%s,%d\n",x,t,a);
}
static void JE_HBox(void *s,int x,int y,unsigned int n,unsigned int w){
    (void)s;printf("box:%d,%d,%u,%u,%d,%d\n",x,y,n,w,helpBoxBrightness,verticalHeight);
}
static void fade_palette(void *p,int n,int a,int b){
    (void)p;printf("fadein:%d,%d,%d,%d,%d,%d\n",n,a,b,mouseCursor,helpBoxBrightness,verticalHeight);
}
static void fade_black(int n){printf("fade:%d\n",n);}
static void JE_playSampleNum(int n){printf("sample:%d\n",n);}
static void service_SDL_events(bool clear){if(clear)newkey=newmouse=false;}
static void JE_mouseStart(void){printf("mouse:%d,%d,%d\n",mouseCursor,helpBoxBrightness,verticalHeight);}
static void JE_showVGA(void){puts("present");}
static void JE_mouseReplace(void){puts("replace");}
static void SDL_Delay(uint32_t n){printf("delay:%u\n",n);}
static void push_joysticks_as_keyboard(void){
    puts("joystick");if(++waits<mode%3+1)return;waits=0;
    newkey=newmouse=false;
    const int kind=mode%12;
    if(mode>=144){
        newkey=true;lastkey_scan=41;
        if(steps==0){
            const int edge=mode-144,xs[]={153,154,165,166},dy[]={-1,0,12,13};
            newmouse=true;lastmouse_but=1;mouse_x=xs[edge/8%4];mouse_y=60+(edge/32%5)*20+dy[edge/2%4];
            lastmouse_x=edge&1?mouse_x:0;lastmouse_y=edge&1?mouse_y:0;
        }
    }
    else if(steps>=24){newkey=true;lastkey_scan=41;}
    else if(pager){
        if(kind<5){const int scans[]={80,79,44,40,88};newkey=true;lastkey_scan=scans[kind];}
        else if(kind<9){newmouse=true;lastmouse_but=kind==8?3:1;mouse_x=kind==5?159:kind==6?160:300;}
        else if(kind==9){newkey=true;newmouse=true;lastkey_scan=41;lastmouse_but=2;}
        else if(kind==10){newkey=true;lastkey_scan=0;}
        else {newmouse=true;lastmouse_but=2;}
    }else{
        if(kind<2){newkey=true;lastkey_scan=steps==0?40:steps==1?80:41;}
        else if(kind<7){const int scans[]={82,81,44,40,88};newkey=true;lastkey_scan=scans[kind-2];}
        else if(kind==7){newmouse=true;lastmouse_but=1;mouse_x=lastmouse_x=160;mouse_y=lastmouse_y=60+(steps%5)*20;}
        else if(kind==8){newmouse=true;lastmouse_but=3;}
        else if(kind==9){newkey=true;newmouse=true;lastkey_scan=41;lastmouse_but=1;mouse_x=160;mouse_y=60;lastmouse_x=0;lastmouse_y=0;}
        else if(kind==10){newkey=true;lastkey_scan=0;}
        else {mouse_x=steps&1?-1:160;mouse_y=60+(steps%5)*20;}
    }
    steps++;
}
static bool helpSystemPage(Uint8 *topic,bool *restart);
""" + function(source, "JE_helpSystem") + function(source, "helpSystemPage") + r"""
static void reset(void){
    newkey=newmouse=false;steps=waits=0;mouse_x=mouse_y=10;lastmouse_x=lastmouse_y=0;mouseCursor=mode%5;
    helpBoxBrightness=1;verticalHeight=7;shopSpriteSheet.data=mode&1?(void*)1:NULL;
}
int main(void){
    for(int i=0;i<6;i++)sprintf(topicName[i],"Topic%d",i);
    for(int i=0;i<68;i++)sprintf(miscText[i],"Text%d",i);
    pager=true;
    for(mode=0;mode<144;mode++){
        reset();Uint8 topic=(Uint8)(mode/12%6+1);bool restart=(mode&1)!=0;
        printf("pages:%d\n",mode);bool result=helpSystemPage(&topic,&restart);
        printf("pagestate:%d,%u,%d,%d\n",result,topic,restart,steps);
    }
    pager=false;
    for(mode=0;mode<304;mode++){
        reset();pager=false;printf("run:%d\n",mode);JE_helpSystem((Uint8)(mode>=144?1:mode/12%6+1));
        printf("runstate:%d,%d,%d\n",steps,helpBoxBrightness,verticalHeight);
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
