"""Run unchanged selection menus with scripted input and traced presentation services."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "menus.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include.*$', '', source, flags=re.MULTILINE)
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint16_t Uint16;
typedef int SDL_Scancode;
typedef struct {void *data;} Sheet;
typedef struct {int pitch,h;uint8_t *pixels;} SDL_Surface;
enum {large_font=0,normal_font=1,centered=1,left_aligned=0,EPISODE_AVAILABLE=4,
    MOUSE_POINTER_NORMAL=0,S_CURSOR=28,S_SELECT=8,S_SPRING=16,KMOD_SHIFT=3,
    DIFFICULTY_EASY=1,DIFFICULTY_NORMAL=2,DIFFICULTY_HARD=3,DIFFICULTY_IMPOSSIBLE=4,
    DIFFICULTY_SUICIDE=6,DIFFICULTY_ZINGLON=8,SDL_BUTTON_LEFT=1,SDL_BUTTON_RIGHT=3,
    SDL_SCANCODE_UP=82,SDL_SCANCODE_DOWN=81,SDL_SCANCODE_SPACE=44,SDL_SCANCODE_RETURN=40,
    SDL_SCANCODE_KP_ENTER=88,SDL_SCANCODE_ESCAPE=41,SDL_SCANCODE_L=15,SDL_SCANCODE_O=18,
    SDL_SCANCODE_R=21,SDL_SCANCODE_D=7,SDL_SCANCODE_G=10,SDL_SCANCODE_RIGHTBRACKET=48};
#define COUNTOF(a) (sizeof(a)/sizeof((a)[0]))
static uint8_t front[64000],back[64000],keysactive[512];
static SDL_Surface surfaces[]={{320,200,front},{320,200,back}};
static SDL_Surface *VGAScreen=&surfaces[0],*VGAScreen2=&surfaces[1];
static Sheet shopSpriteSheet;
static int colors,mouseCursor,mouse_x,mouse_y,lastmouse_x,lastmouse_y,lastmouse_but,lastkey_scan;
static bool newkey,newmouse,onePlayerAction,twoPlayerMode,episodeAvail[4];
static int episodeNum,initial_episode_num,difficultyLevel;
typedef struct {int key,mod,x,y,cx,cy,button,held;} Frame;
static Frame frames[128];
static int frame_count,frame_index,modifiers;
static bool ready;
static void key(int k,int mod,int held) {frames[frame_count++]=(Frame){k,mod,-999,0,0,0,0,held};}
static void mouse(int x,int y,int cx,int cy,int button,int k) {
    frames[frame_count++]=(Frame){k,0,x,y,cx,cy,button,0};
}
static void script(int kind,int mode) {
    frame_count=frame_index=0;
    int x=kind==1?20:140,y=kind==1?50:54,dy=kind==1?30:24;
    switch(mode) {
    case 0:key(40,0,0);break;
    case 1:key(41,0,0);break;
    case 2:key(82,0,0);key(40,0,0);break;
    case 3:key(81,0,0);key(40,0,0);break;
    case 4:key(88,0,0);break;
    case 5:key(44,0,0);break;
    case 6:mouse(0,0,0,0,3,0);break;
    case 7:mouse(x,y,x,y,1,0);break;
    case 8:mouse(x,y+2*dy,x,y+2*dy,1,0);break;
    case 9:mouse(x+40,y,x+40,y,1,0);break;
    case 10:mouse(x,y+13,x,y+13,1,0);break;
    case 11:mouse(x,y+dy,0,0,1,0);break;
    case 12:mouse(x,y+2*dy,x,y+2*dy,3,40);break;
    case 13:mouse(x,y+dy,x,y+dy,1,82);break;
    case 14:key(0,0,0);key(0,0,0);key(40,0,0);break;
    case 15:key(10,1,0);for(int i=0;i<2;i++)key(81,0,0);key(40,0,0);break;
    case 16:key(48,1,0);key(10,1,0);key(48,1,0);for(int i=0;i<3;i++)key(81,0,0);key(40,0,0);break;
    case 17:case 18:case 19:
        key(10,1,0);key(48,1,0);
        if(mode==18)key(0,0,15);
        else {key(15,0,0);key(18,0,0);if(mode==19)key(5,0,0);key(21,0,0);key(7,0,0);}
        for(int i=0;i<4;i++)key(81,0,0);key(40,0,0);break;
    case 20:key(10,0,0);key(48,0,0);key(82,0,0);key(40,0,0);break;
    case 21:mouse(x,y+dy,x,y+dy,0,0);key(40,0,0);break;
    case 22:mouse(-1,-1,-1,-1,1,0);break;
    case 23:mouse(x+39,y+12,x+39,y+12,1,0);break;
    default:for(int i=0;i<mode-21;i++)key(mode%2?82:81,0,0);key(40,0,0);break;
    }
    key(41,0,0);
}
static void service_SDL_events(bool clear) {
    if(clear)newkey=newmouse=false;
    if(!ready)return;
    ready=false;
    Frame f=frames[frame_index-1];
    if(f.x!=-999){mouse_x=f.x;mouse_y=f.y;}
    if(f.button){newmouse=true;lastmouse_but=f.button;lastmouse_x=f.cx;lastmouse_y=f.cy;}
    if(f.key){newkey=true;lastkey_scan=f.key;keysactive[f.key]=1;}
}
static void SDL_Delay(int ticks) {
    if(frame_index>=frame_count){fprintf(stderr,"Menu script exhausted\n");exit(2);}
    Frame f=frames[frame_index++];ready=true;modifiers=f.mod;
    memset(keysactive,0,sizeof(keysactive));
    int lord[]={15,18,21,7};for(int i=0;i<4;i++)if(f.held&(1<<i))keysactive[lord[i]]=1;
    printf("delay:%d\n",ticks);
}
static int SDL_GetModState(void) {return modifiers;}
static void push_joysticks_as_keyboard(void) {puts("joystick");}
static void keep_alive(void) {puts("keep");}
#define NETWORK_KEEP_ALIVE() keep_alive()
static void JE_loadCompShapes(Sheet *sheet,char code) {puts("cursor-load");sheet->data=sheet;(void)code;}
static void JE_loadPic(SDL_Surface *surface,int pic,bool palette) {
    puts("picture");memset(surface->pixels,0xA8,64000);(void)pic;(void)palette;
}
static int JE_textWidth(const char *text,int font) {(void)text;(void)font;return 40;}
static void draw_font_hv_shadow(SDL_Surface *surface,int x,int y,const char *text,int font,int align,int hue,int value,bool black,int distance) {
    printf("draw:%d,%d,%d,%s,%d,%d,%d\n",surface==VGAScreen2,x,y,text,font,align,value);
    int left=x-(align?20:0);
    if(left>=0&&left<320&&y>=0&&y<200)surface->pixels[y*320+left]=(uint8_t)(hue*16+value+8);
    (void)black;(void)distance;
}
static void fade_palette(int palette,int steps,int first,int last) {puts("fade-in");(void)palette;(void)steps;(void)first;(void)last;}
static void fade_black(int steps) {printf("black:%d\n",steps);}
static void JE_playSampleNum(int sample) {printf("sound:%d\n",sample);}
static void JE_mouseStart(void) {service_SDL_events(false);puts("cursor-start");}
static void JE_showVGA(void) {
    uint32_t hash=2166136261u;for(int i=0;i<64000;i++)hash=(hash^front[i])*16777619u;
    printf("show:%u\n",hash);
}
static void JE_mouseReplace(void) {puts("cursor-restore");}
static void JE_initEpisode(int episode) {printf("episode:%d\n",episode);episodeNum=episode;}
""" + source + r"""
int main(void) {
    for(int kind=0;kind<3;kind++)for(int mode=0;mode<40;mode++) {
        printf("case:%d,%d\n",kind,mode);
        for(int i=0;i<6;i++)snprintf(episode_name[i],31,"E%d",i);
        for(int i=0;i<7;i++)snprintf(difficulty_name[i],21,"D%d",i);
        for(int i=0;i<5;i++)snprintf(gameplay_name[i],26,"G%d",i);
        memset(front,0,64000);memset(back,0,64000);memset(keysactive,0,512);
        shopSpriteSheet.data=mode%2?&shopSpriteSheet:NULL;
        newkey=newmouse=ready=false;mouse_x=mouse_y=0;modifiers=0;
        onePlayerAction=true;twoPlayerMode=true;episodeNum=7;initial_episode_num=9;difficultyLevel=10;
        for(int i=0;i<4;i++)episodeAvail[i]=(mode%4==0 || i%2==0);
        script(kind,mode);
        bool result=kind==0?gameplaySelect():kind==1?episodeSelect():difficultySelect();
        printf("result:%d,%d,%d,%d,%d,%d,%d\n",result,onePlayerAction,twoPlayerMode,episodeNum,initial_episode_num,difficultyLevel,frame_index);
    }
    return 0;
}
"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
