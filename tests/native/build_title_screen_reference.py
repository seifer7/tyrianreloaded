"""Trace unchanged title drawing, idle/input handling, actions, secrets and complete loops."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8").replace("static ", "")
    source = source[source.index("enum MenuItemIndex"):source.index("bool newGame(void)")]
    source = source.replace("TitleScreenInput read_title_screen_input(", "int read_title_screen_input(")
    source = source.replace("TitleScreenResult dispatch_title_screen_action(", "int dispatch_title_screen_action(")
    source = source.replace("TitleScreenResult handle_title_screen_secret_codes(", "int handle_title_screen_secret_codes(")
    read = function(source, "read_title_screen_input").replace("int read_title_screen_input(", "TitleScreenInput read_title_screen_input(", 1)
    dispatch = function(source, "dispatch_title_screen_action").replace("int dispatch_title_screen_action(", "TitleScreenResult dispatch_title_screen_action(", 1)
    secrets = function(source, "handle_title_screen_secret_codes").replace("int handle_title_screen_secret_codes(", "TitleScreenResult handle_title_screen_secret_codes(", 1)
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#define COUNTOF(a) ((unsigned int)(sizeof(a)/sizeof((a)[0])))
#define SONG_TITLE 29
#define PLANET_SHAPES 0
#define FONT_SHAPES 0
#define MOUSE_POINTER_NORMAL 0
#define SDL_BUTTON_LEFT 1
#define SDL_BUTTON_RIGHT 3
#define SDL_SCANCODE_UP 82
#define SDL_SCANCODE_DOWN 81
#define SDL_SCANCODE_SPACE 44
#define SDL_SCANCODE_RETURN 40
#define SDL_SCANCODE_KP_ENTER 88
#define SDL_SCANCODE_ESCAPE 41
#define S_CURSOR 28
#define S_SELECT 8
#define S_SPRING 16
#define V_DATA_CUBE 37
#define SA_ENGAGE 9
#define SA_DESTRUCT 8
typedef uint16_t Uint16;
typedef uint32_t Uint32;
typedef struct {int w,h,pitch;uint8_t *pixels;} SDL_Surface;
typedef struct {uint8_t r,g,b,a;} SDL_Color;
static uint8_t primary_pixels[64000],background_pixels[64000];
static SDL_Surface primary={320,200,320,primary_pixels},background={320,200,320,background_pixels};
static SDL_Surface *VGAScreen=&primary,*VGAScreenSeg=&primary,*VGAScreen2=&background;
static struct {void *data;} shopSpriteSheet;
static int small_font=2,normal_font=1,left_aligned=0,centered=1;
static char menuText[7][21],specialName[9][10];
static const char *opentyrian_version="Version";
static int colors[256],mouseCursor,mouse_x,mouse_y,lastmouse_x,lastmouse_y,lastmouse_but,lastkey_scan,mode,phase,steps,waits;
static bool moveTyrianLogoUp,play_demo,loadDestruct,newkey,newmouse,new_text;
static char last_text[32];
static Uint32 ticks;
static size_t SDL_strlcpy(char *out,const char *in,size_t size){
    size_t length=strlen(in);if(size!=0){size_t n=length<size-1?length:size-1;memcpy(out,in,n);out[n]=0;}return length;
}
static void JE_loadCompShapes(void *s,int c){(void)s;shopSpriteSheet.data=(void*)1;printf("cursor:%c\n",c);}
static void play_song(unsigned int n){printf("song:%u\n",n);}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static void draw_font_hv_shadow(void *s,int x,int y,const char *t,int f,int a,int h,int b,bool black,int distance){
    (void)s;(void)a;(void)h;(void)black;printf("version:%d,%d,%s,%d,%d,%d\n",x,y,t,f,b,distance);
}
static void blit_sprite(void *s,int x,int y,int sheet,int i){(void)s;(void)x;(void)sheet;printf("logo:%d,%d\n",y,i);}
static void fade_palette(void *p,int n,int first,int last){(void)p;printf("fadein:%d,%d,%d\n",n,first,last);}
static void setDelay(int n){printf("setdelay:%d\n",n);}
static void JE_showVGA(void){puts("present");}
static void service_wait_delay(void){puts("wait");}
static void JE_dString(void *s,int x,int y,const char *t,int f){(void)s;(void)x;(void)y;(void)f;printf("reloaded:%s\n",t);}
static int JE_textWidth(const char *t,int f){(void)f;printf("width:%s\n",t);return (int)strlen(t)*2;}
static void draw_font_hv(void *s,int x,int y,const char *t,int f,int a,int h,int b){
    (void)s;(void)f;(void)h;printf("label:%d,%d,%s,%d,%d\n",x,y,t,a,b);
}
static void JE_mouseStartFilter(int n){printf("mouse:%d\n",n);}
static void JE_mouseReplace(void){puts("replace");}
static Uint32 SDL_GetTicks(void){return ticks;}
static void SDL_Delay(Uint32 n){printf("delay:%u\n",n);ticks+=n;}
static void fade_black(int n){printf("fade:%d\n",n);}
static void JE_playSampleNum(int n){printf("sample:%d\n",n);}
static bool newGame(void){puts("new");return (mode&1)!=0;}
static bool JE_loadScreen(void){puts("load");return (mode&2)!=0;}
static void JE_highScoreScreen(void){puts("scores");}
static void JE_helpSystem(int n){printf("help:%d\n",n);}
static void setupMenu(void){puts("setup");}
static void JE_whoa(void){puts("whoa");}
static void set_colors(SDL_Color c,int a,int b){(void)c;printf("black:%d,%d\n",a,b);}
static void newSuperTyrianGame(void){puts("super");}
static bool newSuperArcadeGame(unsigned int i){printf("arcade:%u\n",i);return (mode&4)!=0;}
static void service_SDL_events(bool clear){if(clear)newkey=newmouse=new_text=false;}
static const char *codes[]={"ALPHA","ALP","BETA","AAB","NINEBYTE9","SHIP","ZED","DESTRUCT","ENGAGE"};
static const char *entered[]={"alpha","aalpha","al","pha","betaSHIP","ninebyte9","deSTRUCT","engage","aab","zzed",
    "ship","bogus","alp","ALPHAALP","NINEBYTE9X","BETA","DESTRUCTengage"};
static void push_joysticks_as_keyboard(void){
    puts("joystick");
    if(phase==0) {
        if(mode==0)return;
        if(++waits<mode%4+1)return;
        if(mode==1){newkey=true;lastkey_scan=40;}
        else if(mode==2){new_text=true;strcpy(last_text,"a");}
        else if(mode==3){newmouse=true;lastmouse_but=1;}
        else mouse_x++;
    }else{
        int scenario=mode%12;
        if(scenario==11)return;
        newkey=true;
        if(scenario<7){if(steps<scenario)lastkey_scan=81;else lastkey_scan=40;
            if(steps>scenario)lastkey_scan=41;}
        else if(scenario==7){newkey=false;newmouse=true;lastmouse_but=3;}
        else if(scenario==8 || scenario==9){newkey=false;new_text=true;strcpy(last_text,scenario==8?"destruct":"engage");}
        else {newkey=false;new_text=true;strcpy(last_text,"ship");if(steps>0){new_text=false;newkey=true;lastkey_scan=41;}}
        steps++;
    }
}
typedef struct {bool action,done;} TitleScreenInput;
typedef enum {TITLE_SCREEN_CONTINUE,TITLE_SCREEN_RETURN_FALSE,TITLE_SCREEN_RETURN_TRUE} TitleScreenResult;
enum MenuItemIndex {MENU_ITEM_NEW_GAME,MENU_ITEM_LOAD_GAME,MENU_ITEM_HIGH_SCORES,MENU_ITEM_INSTRUCTIONS,MENU_ITEM_SETUP,MENU_ITEM_DEMO,MENU_ITEM_QUIT};
""" + function(source, "draw_title_screen") + function(source, "wait_for_title_screen_input") + read + dispatch + secrets + function(source, "titleScreen") + r"""
static void reset(void){
    for(int i=0;i<7;i++)sprintf(menuText[i],"Menu%d",i);
    for(int i=0;i<9;i++)strcpy(specialName[i],codes[i]);
    mouse_x=10;mouse_y=10;lastmouse_x=lastmouse_y=0;ticks=0;waits=steps=0;
    newkey=newmouse=new_text=play_demo=loadDestruct=false;last_text[0]=0;moveTyrianLogoUp=false;
}
int main(void){
    for(mode=0;mode<14;mode++){
        reset();moveTyrianLogoUp=mode&1;int widths[7]={0};printf("draw:%d\n",mode);
        draw_title_screen(true,mode%7,widths);draw_title_screen(false,mode%7,widths);
        printf("drawstate:%d",moveTyrianLogoUp);for(int i=0;i<7;i++)printf(",%d",widths[i]);puts("");
    }
    for(mode=0;mode<4096;mode++){
        reset();int widths[7];for(int i=0;i<7;i++)widths[i]=10+i*3;
        size_t selected=mode%7;newkey=mode&1;newmouse=mode&2;
        const int scans[]={82,81,44,40,88,41,0};lastkey_scan=scans[mode/4%7];lastmouse_but=mode%3+1;
        mouse_x=mode%181+70;mouse_y=mode/8%121+90;lastmouse_x=mode*7%181+70;lastmouse_y=mode*13%121+90;
        printf("read:%d\n",mode);TitleScreenInput i=read_title_screen_input(&selected,widths,mode&4);
        printf("input:%u,%d,%d\n",(unsigned int)selected,i.action,i.done);
    }
    for(mode=0;mode<28;mode++){
        reset();bool restart=mode&1;printf("dispatch:%d\n",mode);
        TitleScreenResult r=dispatch_title_screen_action(mode%7,&restart);printf("result:%d,%d,%d\n",r,restart,play_demo);
    }
    for(mode=0;mode<8;mode++){
        reset();phase=0;mouse_x=mode>=5?-1:10;mouse_y=mode>=6?-1:10;ticks=mode&1?UINT32_MAX-16:0;
        printf("waitcase:%d\n",mode);bool moved=false;bool result=wait_for_title_screen_input(&moved);
        printf("waitstate:%d,%d,%d,%u\n",result,moved,play_demo,ticks);
    }
    for(mode=0;mode<8;mode++)for(unsigned int i=0;i<COUNTOF(entered);i++){
        reset();bool restart=mode&1;size_t progress[9]={0};if(mode&2)for(int j=0;j<9;j++)progress[j]=j%10;
        printf("secret:%d,%u\n",mode,i);new_text=true;strcpy(last_text,entered[i]);
        TitleScreenResult r=handle_title_screen_secret_codes(progress,&restart);
        printf("secretstate:%d,%d,%d",r,restart,loadDestruct);for(int j=0;j<9;j++)printf(",%u",(unsigned int)progress[j]);puts("");
        new_text=false;r=handle_title_screen_secret_codes(progress,&restart);printf("notext:%d\n",r);
    }
    for(mode=0;mode<144;mode++){
        reset();phase=1;moveTyrianLogoUp=mode&16;shopSpriteSheet.data=mode&1?(void*)1:NULL;
        printf("run:%d\n",mode);bool r=titleScreen();
        printf("runstate:%d,%d,%d,%d,%d,%s\n",r,play_demo,loadDestruct,moveTyrianLogoUp,steps,menuText[4]);
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
