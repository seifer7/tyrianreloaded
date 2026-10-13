"""Trace unchanged saved-game screen drawing, input, actions and complete loops."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8").replace("static ", "")
    source = source[source.index("enum\n{\n\tLOAD_SCREEN_MENU_ITEMS"):source.index("ulong JE_totalScore")]
    tagged = source.replace("size_t saved_game_count(", "int saved_game_count(")
    tagged = tagged.replace("LoadScreenInput read_load_screen_input(", "int read_load_screen_input(")
    tagged = tagged.replace("LoadScreenResult handle_load_screen_action(", "int handle_load_screen_action(")
    bodies = []
    for name in ("saved_game_count", "draw_load_screen", "wait_for_load_screen_input",
                 "read_load_screen_input", "handle_load_screen_action", "JE_loadScreen"):
        body = function(tagged, name)
        if name == "saved_game_count":
            body = body.replace("int saved_game_count(", "size_t saved_game_count(", 1)
        elif name == "read_load_screen_input":
            body = body.replace("int read_load_screen_input(", "LoadScreenInput read_load_screen_input(", 1)
        elif name == "handle_load_screen_action":
            body = body.replace("int handle_load_screen_action(", "LoadScreenResult handle_load_screen_action(", 1)
        bodies.append(body)
    declarations = source[:source.index("size_t saved_game_count")]
    inputs = source[source.index("typedef struct"):source.index("LoadScreenInput read_load_screen_input")]
    results = source[source.index("typedef enum"):source.index("LoadScreenResult handle_load_screen_action")]
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define FULL_SHADE 1
#define S_CURSOR 28
#define S_SPRING 16
#define S_SELECT 8
#define S_CLINK 23
#define MOUSE_POINTER_NORMAL 0
#define SDL_BUTTON_LEFT 1
#define SDL_BUTTON_RIGHT 3
#define SDL_SCANCODE_LEFT 80
#define SDL_SCANCODE_RIGHT 79
#define SDL_SCANCODE_UP 82
#define SDL_SCANCODE_DOWN 81
#define SDL_SCANCODE_SPACE 44
#define SDL_SCANCODE_RETURN 40
#define SDL_SCANCODE_KP_ENTER 88
#define SDL_SCANCODE_ESCAPE 41
typedef uint16_t Uint16;
typedef struct {int count;char name[20],levelName[11];uint8_t episode;} SavedGame;
typedef struct {int pitch,h;uint8_t *pixels;} SDL_Surface;
static uint8_t primary_data[64000],background_data[64000];
static SDL_Surface primary={320,200,primary_data},background={320,200,background_data},*VGAScreen=&primary,*VGAScreen2=&background;
static struct {void *data;} shopSpriteSheet;
static char miscText[68][42],miscTextB[5][42];
static int colors[256],large_font,centered=1,helpBoxColor,mouseCursor;
static int mode,phase,steps,waits,list_count,mouse_x,mouse_y,lastmouse_x,lastmouse_y,lastmouse_but,lastkey_scan;
static bool newkey,newmouse,performSave,gameLoaded;
static const int counts[]={0,1,10,11,12,21,22,23,33,-9};
static SavedGame saves[40];
static void JE_loadCompShapes(void *s,int c){(void)s;shopSpriteSheet.data=(void*)1;printf("cursor:%c\n",c);}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static void fill_rectangle_wh(void *s,int x,int y,int w,int h,int c){(void)s;printf("fill:%d,%d,%d,%d,%d\n",x,y,w,h,c);}
static void draw_font_hv_shadow(void *s,int x,int y,const char *t,int f,int a,int hue,int b,bool black,int distance){
    (void)s;(void)f;(void)a;(void)black;printf("header:%d,%d,%s,%d,%d,%d\n",x,y,t,hue,b,distance);
}
static void JE_textShade(void *s,int x,int y,const char *t,int hue,int b,int shade){
    (void)s;(void)shade;printf("shaded:%d,%d,%s,%d,%d\n",x,y,t,hue,b);
}
static void arrow(void *s,int x,int y,int i){(void)s;printf("arrow:%d,%d,%d\n",x,y,i);}
#define blit_sprite2x2(s,x,y,sheet,i) arrow(s,x,y,i)
static void JE_helpBox(void *s,int x,int y,const char *t,int w){(void)s;printf("help:%d,%d,%s,%d,%d\n",x,y,t,w,helpBoxColor);}
static void fade_palette(void *p,int n,int a,int b){(void)p;printf("fadein:%d,%d,%d,%d\n",n,a,b,mouseCursor);}
static void fade_black(int n){printf("fade:%d\n",n);}
static void JE_mouseStart(void){puts("mouse");}
static void JE_showVGA(void){puts("present");}
static void JE_mouseReplace(void){puts("replace");}
static void SDL_Delay(uint32_t n){printf("delay:%u\n",n);}
static void JE_playSampleNum(int n){printf("sample:%d\n",n);}
static void service_SDL_events(bool clear){if(clear)newkey=newmouse=false;}
static void loadGameOperation(const char *name){printf("load:%s,%d\n",name,performSave);if(mode%3!=0)gameLoaded=true;}
static SavedGame *build_save_game_list(int players){
    printf("list:%d,%d\n",players,list_count);SavedGame *out=malloc(sizeof(saves));memcpy(out,saves,sizeof(saves));return out;
}
static void push_joysticks_as_keyboard(void){
    puts("joystick");
    if(phase==0){
        if(++waits<mode%4+1)return;
        if(mode==0){newkey=true;lastkey_scan=40;}
        else if(mode==1){newmouse=true;lastmouse_but=1;}
        else if(mode==2)mouse_x++;
        else if(mode>=4)mouse_y++;
        return;
    }
    const int kind=mode%8;
    newkey=true;lastkey_scan=41;
    if(steps==0){
        const int scans[]={40,82,81,79,80,0,0,81};lastkey_scan=scans[kind];
        if(kind==5||kind==6){newkey=false;newmouse=true;lastmouse_but=kind==5?1:3;
            mouse_x=lastmouse_x=15;mouse_y=lastmouse_y=160;}
    }else if(steps==1&&kind!=5&&kind!=6)lastkey_scan=kind==7?82:40;
    else if(steps==2&&kind==7)lastkey_scan=40;
    steps++;
}
""" + declarations + inputs + results + "\n".join(bodies) + r"""
static void reset(int count){
    list_count=count;saves[0].count=count;mouse_x=mouse_y=10;lastmouse_x=lastmouse_y=0;
    newkey=newmouse=false;waits=steps=0;performSave=true;gameLoaded=(mode&2)!=0;
}
int main(void){
    for(int i=0;i<68;i++)sprintf(miscText[i],"Text%d",i);
    strcpy(miscTextB[1],"Episode");strcpy(miscTextB[2],"Last-level-12345678");
    for(int i=0;i<40;i++){sprintf(saves[i].name,"Name%d",i);sprintf(saves[i].levelName,"Level%05d",i);saves[i].episode=(uint8_t)(i*19);}
    for(mode=0;mode<960;mode++){
        reset(counts[mode%10]);size_t page=mode/10%4,selected=mode/40%12;bool restart=mode>=480;
        printf("draw:%d\n",mode);draw_load_screen(saves,page,selected,restart);
    }
    const int scans[]={80,79,82,81,44,40,88,41,0};
    const int xs[]={9,10,82,83,106,107,212,213,236,237,309,310};
    const int ys[]={29,30,37,38,160,167,173,178,179,180,500,-1};
    for(mode=0;mode<8192;mode++){
        reset(counts[mode%10]);size_t page=mode/10%4,selected=mode/40%12;
        newkey=mode&1;newmouse=mode&2;lastkey_scan=scans[mode/8%9];lastmouse_but=mode/72%3+1;
        mouse_x=xs[mode/192%12];mouse_y=ys[mode/16%12];
        lastmouse_x=mode&16?mouse_x:0;lastmouse_y=mode&32?mouse_y:0;
        printf("read:%d\n",mode);LoadScreenInput i=read_load_screen_input(saves,page,&selected,mode&4);
        printf("input:%u,%d,%d,%d,%d\n",(unsigned int)selected,i.leftAction,i.rightAction,i.action,i.done);
    }
    for(mode=0;mode<7680;mode++){
        reset(counts[mode/16%10]);size_t page=mode/160%4,selected=mode/640%12;
        LoadScreenInput i={mode&1,mode&2,mode&4,mode&8};
        printf("action:%d\n",mode);LoadScreenResult r=handle_load_screen_action(saves,&page,selected,i);
        printf("actionstate:%u,%d,%d,%d\n",(unsigned int)page,r,performSave,gameLoaded);
    }
    phase=0;
    for(mode=0;mode<8;mode++){
        reset(0);if(mode==3)mouse_x=-1;
        printf("wait:%d\n",mode);bool moved=wait_for_load_screen_input();
        printf("waitstate:%d,%d,%d,%d\n",moved,mouse_x,mouse_y,waits);
    }
    phase=1;
    for(mode=0;mode<160;mode++){
        reset(counts[mode%10]);shopSpriteSheet.data=mode&1?(void*)1:NULL;
        printf("run:%d\n",mode);bool result=JE_loadScreen();
        printf("runstate:%d,%d,%d,%d\n",result,performSave,gameLoaded,steps);
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
