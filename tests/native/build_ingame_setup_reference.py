"""Verify unchanged in-game setup drawing, mouse/key actions and ordered settings."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    models = source[source.index("enum InGameSetupMenuItem"):source.index("static void draw_ingame_setup_menu")]
    source = source.replace("static void ", "void ").replace("static bool ", "bool ").replace("JE_boolean JE_inGameSetup(", "bool JE_inGameSetup(")
    volume = function((root / "src" / "nortsong.c").read_text(encoding="utf-8"), "JE_changeVolume")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint16_t Uint16;
typedef uint16_t JE_word;
typedef uint8_t JE_byte;
#define COUNTOF(x) ((unsigned)(sizeof(x)/sizeof(*(x))))
#define MIN(a,b) ((a)<(b)?(a):(b))
#define MAX(a,b) ((a)>(b)?(a):(b))
#define S_CURSOR 28
#define S_SELECT 8
#define S_SPRING 16
#define S_CLINK 23
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
#define SDL_SCANCODE_W 26
#define MOUSE_POINTER_NORMAL 0
#define TINY_FONT 2
static int normal_font=1,left_aligned=0;
typedef struct {uint8_t *pixels;int pitch,h;} SDL_Surface;
static uint8_t pix[16],backup[16];
static SDL_Surface primary={pix,4,4},secondary={backup,4,4};
static SDL_Surface *VGAScreen=&primary,*VGAScreenSeg=&primary,*VGAScreen2=&secondary;
static struct {void *data;} shopSpriteSheet;
static int mouseCursor;
static const char *inGameText[6]={"Menu0","Menu1","Menu2","Menu3","Menu4","Menu5"};
static const char *detailLevel[6]={"Detail0","Detail1","Detail2","Detail3","Detail4","Detail5"};
static const char *gameSpeedText[5]={"Speed0","Speed1","Speed2","Speed3","Speed4"};
static char help[34][20];
static char *mainMenuHelp[34];
static bool music_disabled,samples_disabled,constantPlay,isNetworkGame,haltGame,playerEndLevel;
static JE_word tyrMusicVolume,fxVolume;
static JE_byte processorType,gameSpeed;
static bool newmouse,newkey;
static int mouse_x,mouse_y,lastmouse_but,lastmouse_x,lastmouse_y,lastkey_scan;
static int mode,waitframe,menuloop;
static bool running;
static int run_scan(void){
    const int scan0[]={81,81,81,41};
    const int scan1[]={81,81,81,81,40};
    const int scan2[]={81,81,81,81,81,40};
    const int scan3[]={40,81,40,81,26,79,81,80,41};
    const int *scans=mode%4==0?scan0:mode%4==1?scan1:mode%4==2?scan2:scan3;
    return scans[menuloop-1];
}
static void JE_barShade(void *s,int a,int b,int c,int d){printf("shade:%d,%d,%d,%d\n",a,b,c,d);}
static void draw_font_hv_shadow(void *s,int x,int y,const char *t,int f,int align,int h,int b,bool black,int dist){
    printf("label:%d,%d,%s,%d\n",x,y,t,b);
}
static void JE_barDrawShadow(void *s,int x,int y,int res,int c,int n,int w,int h){printf("gauge:%d,%d,%d,%d\n",x,y,c,n);}
static void JE_outTextAdjust(void *s,int x,int y,const char *t,int h,int b,int f,bool shadow){printf("help:%s\n",t);}
static void service_SDL_events(bool clear){
    printf("events:%d\n",clear);
    if(clear){newkey=newmouse=false;waitframe=0;menuloop++;}
    else if(++waitframe==mode%4+1){
        if(running){newkey=true;lastkey_scan=run_scan();}
        else {newkey=true;lastkey_scan=40;}
    }
}
static void JE_mouseStart(void){puts("mouse");}
static void JE_showVGA(void){puts("present");}
static void JE_mouseReplace(void){puts("replace");}
static void SDL_Delay(unsigned int ms){printf("delay:%u\n",ms);}
static void push_joysticks_as_keyboard(void){puts("joystick");}
static void keep(void){puts("keep");}
#define NETWORK_KEEP_ALIVE() keep()
static void JE_playSampleNum(int s){printf("sample:%d\n",s);}
static void set_volume(int m,int e){printf("volume:%d,%d\n",m,e);}
static void JE_initProcessorType(void){puts("processor");}
static void JE_setNewGameSpeed(void){puts("speed");}
static void JE_tyrianHalt(int n){puts("halt");}
static void JE_loadCompShapes(void *s,char c){shopSpriteSheet.data=(void*)1;puts("cursor");}
""" + volume + models + "".join(function(source, name) for name in (
        "draw_ingame_setup_menu", "wait_for_ingame_setup_input", "read_ingame_setup_mouse",
        "read_ingame_setup_keyboard", "adjust_ingame_setup_setting", "apply_ingame_setup_action", "JE_inGameSetup"
    )) + r"""
int main(void){
    for(int i=0;i<34;i++){sprintf(help[i],"Help%d",i);mainMenuHelp[i]=help[i];}
    int scans[]={82,81,80,79,44,40,88,41,26,0};
    for(mode=0;mode<2048;mode++){
        processorType=mode%6+1;gameSpeed=mode%5+1;tyrMusicVolume=mode%256;fxVolume=255-mode%256;
        music_disabled=mode&1;samples_disabled=mode&2;constantPlay=mode&4;isNetworkGame=mode&8;
        haltGame=playerEndLevel=false;size_t selected=mode%6;
        bool restart=true;memset(pix,0x75,sizeof pix);
        printf("setup:%d\n",mode);draw_ingame_setup_menu(selected,&restart);draw_ingame_setup_menu(selected,&restart);
        newmouse=mode&1;newkey=mode&2;mouse_x=mode%241-10;mouse_y=(mode/8)%161-10;
        lastmouse_but=mode%3+1;lastmouse_x=(mode*7)%241-10;lastmouse_y=(mode*13)%161-10;
        lastkey_scan=scans[(mode/4)%10];InGameSetupAction action={0};
        read_ingame_setup_mouse(&selected,6,mode&4,&action);read_ingame_setup_keyboard(&selected,6,&action);
        printf("read:%zu,%d,%d,%d,%d,%d,%d,%d\n",selected,action.activate,action.decrease,action.increase,
            action.close,action.volumeChanged,action.volumeItem,action.volumeValue);
        InGameSetupAction apply={mode&1,mode&2,mode&4,mode&8,mode&16,mode%3,mode%256};
        bool done=false,result=false;apply_ingame_setup_action(selected,&apply,&done,&result);
        printf("state:%u,%u,%u,%u,%d,%d,%d,%d,%d,%d\n",processorType,gameSpeed,tyrMusicVolume,fxVolume,
            music_disabled,samples_disabled,haltGame,playerEndLevel,done,result);
        running=false;menuloop=waitframe=0;mouse_x=mode%2?-1:10;mouse_y=mode%3?-1:20;
        printf("moved:%d\n",wait_for_ingame_setup_input());
    }
    for(mode=0;mode<16;mode++){
        processorType=mode%6+1;gameSpeed=mode%5+1;tyrMusicVolume=100;fxVolume=200;music_disabled=samples_disabled=false;
        constantPlay=mode&4;isNetworkGame=mode&8;haltGame=playerEndLevel=false;
        running=true;menuloop=waitframe=0;mouse_x=mouse_y=10;shopSpriteSheet.data=mode&1?(void*)1:NULL;
        printf("run:%d\n",mode);bool result=JE_inGameSetup();printf("result:%d,%d,%d,%d,%d,%d,%u,%u\n",result,menuloop,
            haltGame,playerEndLevel,music_disabled,samples_disabled,processorType,gameSpeed);
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
