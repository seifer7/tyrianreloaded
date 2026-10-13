"""Compare unchanged setup menu drawing, pickers, volume actions and complete loops."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "opentyr.c").read_text(encoding="utf-8")
    source = source[source.index("static size_t getShopContents"):source.index("#ifndef DATA_EXTRACTOR")]
    volume = function((root / "src" / "nortsong.c").read_text(encoding="utf-8"), "JE_changeVolume")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define COUNTOF(a) ((unsigned int)(sizeof(a)/sizeof((a)[0])))
#define MIN(a,b) ((a)<(b)?(a):(b))
#define MAX(a,b) ((a)>(b)?(a):(b))
#define S_CURSOR 28
#define S_SELECT 8
#define S_CLICK 24
#define S_CLINK 23
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
#define PART_SHADE 0
#define MOUSE_POINTER_NORMAL 0
typedef uint16_t Uint16;
typedef uint16_t JE_word;
typedef int16_t JE_integer;
typedef struct {int pitch,h;uint8_t *pixels;} SDL_Surface;
static uint8_t primary_data[64000],background_data[64000];
static SDL_Surface primary={320,200,primary_data},background={320,200,background_data},*VGAScreen=&primary,*VGAScreen2=&background;
static struct {void *data;} shopSpriteSheet;
static const struct {const char *name;} scalers[]={{"None"},{"2x"},{"Scale2x"},{"hq2x"},{"3x"},{"Scale3x"},{"hq3x"},{"4x"},{"hq4x"}};
static const unsigned int scalers_count=9,ScalingMode_MAX=4;
static const char *scaling_mode_names[]={"Center","Integer","Fit 8:5","Fit 4:3"};
static int large_font,normal_font=1,left_aligned,centered=1,colors[256],mouseCursor;
static int mode,phase,steps,waits,displays,fullscreen_display,shopContentSetting,scaler,scaling_mode;
static int mouse_x,mouse_y,lastmouse_x,lastmouse_y,lastmouse_but,lastkey_scan;
static bool newkey,newmouse,music_disabled,samples_disabled,fail_scaler;
static JE_word tyrMusicVolume,fxVolume;
static int SDL_GetNumVideoDisplays(void){return displays;}
static void JE_loadCompShapes(void *s,int c){(void)s;shopSpriteSheet.data=(void*)1;printf("cursor:%c\n",c);}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static void fill_rectangle_wh(void *s,int x,int y,int w,int h,int c){(void)s;printf("fill:%d,%d,%d,%d,%d\n",x,y,w,h,c);}
static void draw_font_hv_shadow(void *s,int x,int y,const char *t,int f,int a,int hue,int b,bool black,int d){
    (void)s;(void)hue;(void)black;(void)d;printf("label:%d,%d,%s,%d,%d,%d\n",x,y,t,f,a,b);
}
static void JE_barDrawShadow(void *s,int x,int y,int r,int c,int n,int w,int h){
    (void)s;printf("gauge:%d,%d,%d,%d,%d,%d,%d\n",x,y,r,c,n,w,h);
}
static void JE_rectangle(void *s,int x1,int y1,int x2,int y2,int c){(void)s;printf("rectangle:%d,%d,%d,%d,%d\n",x1,y1,x2,y2,c);}
static void JE_textShade(void *s,int x,int y,const char *t,int h,int b,int shade){
    (void)s;printf("description:%d,%d,%s,%d,%d,%d\n",x,y,t,h,b,shade);
}
static void fade_palette(void *p,int n,int a,int b){(void)p;printf("fadein:%d,%d,%d,%d\n",n,a,b,mouseCursor);}
static void fade_black(int n){printf("fade:%d\n",n);}
static void JE_mouseStart(void){puts("mouse");}
static void JE_showVGA(void){puts("present");}
static void JE_mouseReplace(void){puts("replace");}
static void SDL_Delay(uint32_t n){printf("delay:%u\n",n);}
static void JE_playSampleNum(int n){printf("sample:%d,%d,%d\n",n,music_disabled,samples_disabled);}
static void set_volume(JE_word music,JE_word sound){printf("volume:%u,%u\n",music,sound);}
static void restart_song(void){puts("restart-song");}
static void reinit_fullscreen(int n){printf("fullscreen:%d\n",n);fullscreen_display=n;}
static bool init_scaler(int n){printf("scaler:%d\n",n);if(fail_scaler&&mode%5==0&&n!=2)return false;scaler=n;return true;}
static void JE_saveConfiguration(void){puts("save");}
static void jukebox(void){puts("jukebox");}
static void JE_destructGame(void){puts("destruct");}
static void service_SDL_events(bool clear){if(clear)newkey=newmouse=false;}
static void push_joysticks_as_keyboard(void){
    puts("joystick");
    if(phase==0){
        if(++waits<mode%4+1)return;
        if(mode==0){newkey=true;lastkey_scan=40;}
        else if(mode==1){newmouse=true;lastmouse_but=1;}
        else if(mode==2)mouse_x++;
        else if(mode==3)fullscreen_display++;
        else if(mode>=5)mouse_y++;
        return;
    }
    const int scripts[][14]={
        {41},{82,40},{40,40,81,40,41,41},
        {81,40,40,79,81,40,80,81,40,41},
        {81,81,40,41},
        {81,81,81,40,40,81,81,40,81,40,41},
        {40,81,40,81,40,41,41},
        {40,81,81,40,81,40,41,41}
    };
    newkey=true;lastkey_scan=steps<14?scripts[mode%8][steps]:41;if(lastkey_scan==0)lastkey_scan=41;steps++;
}
""" + volume + source + r"""
static void reset(void){
    displays=mode%4+1;fullscreen_display=mode%3-1;scaler=mode%9;scaling_mode=mode%4;shopContentSetting=mode%7-2;
    music_disabled=mode&1;samples_disabled=mode&2;tyrMusicVolume=(JE_word)(mode*37%256);fxVolume=(JE_word)(mode*53%256);
    mouse_x=mouse_y=10;lastmouse_x=lastmouse_y=0;newkey=newmouse=false;steps=waits=0;shopSpriteSheet.data=mode&1?(void*)1:NULL;
}
static SetupMenuState state(int menu,int selected){
    SetupMenuState s={0};s.currentMenu=(MenuId)menu;s.restart=mode&1;
    for(int i=1;i<5;i++){s.parents[i]=i==1?MENU_NONE:MENU_SETUP;s.selectedMenuItemIndexes[i]=0;}
    s.selectedMenuItemIndexes[menu]=(size_t)selected;return s;
}
static void dump(const SetupMenuState *s){
    printf("state:%d,%d,%u,%d",s->currentMenu,s->currentPicker,(unsigned int)s->pickerSelectedIndex,s->restart);
    for(int i=0;i<5;i++)printf(",%d,%u",s->parents[i],(unsigned int)s->selectedMenuItemIndexes[i]);
    printf(",%d,%d,%d,%d,%d,%d,%u,%u\n",fullscreen_display,scaler,scaling_mode,shopContentSetting,music_disabled,samples_disabled,tyrMusicVolume,fxVolume);
}
static void dump_input(SetupInput i){printf("input:%d,%d,%d\n",i.action,i.hoveredIndex==SIZE_MAX?-1:(int)i.hoveredIndex,i.value);}
static int item_count(int menu){int n=0;while(menus[menu].items[n].id!=(MenuItemId)-1)n++;return n;}
static const MenuItem *picker(int kind){
    return kind==0?&menus[MENU_GRAPHICS].items[0]:kind==1?&menus[MENU_GRAPHICS].items[1]:
        kind==2?&menus[MENU_GRAPHICS].items[2]:&menus[MENU_GAMEPLAY].items[0];
}
int main(void){
    for(mode=0;mode<16;mode++){
        reset();char buffer[100];printf("catalog:%d\n",mode);
        for(int kind=0;kind<4;kind++){const MenuItem *item=picker(kind);size_t count=item->getPickerItemsCount();
            printf("count:%d,%u\n",item->id,(unsigned int)count);
            for(size_t i=0;i<count;i++)printf("item:%d,%u,%s\n",item->id,(unsigned int)i,item->getPickerItem(i,buffer,100));}
    }
    for(mode=0;mode<256;mode++){
        reset();int menu=mode%4+1,selected=mode/4%item_count(menu);SetupMenuState s=state(menu,selected);
        const MenuItem *item=&menus[menu].items[selected];
        if(mode&32&&item->getPickerItemsCount){s.currentPicker=item->id;s.pickerSelectedIndex=mode/64%item->getPickerItemsCount();}
        size_t count=0;char buffer[100];printf("draw:%d\n",mode);
        int y=draw_setup_menu(s.currentMenu,s.selectedMenuItemIndexes,s.currentPicker,s.pickerSelectedIndex,buffer,&count);
        printf("drawstate:%d,%u\n",y,(unsigned int)count);
    }
    const int scans[]={82,81,80,79,44,40,88,41,0};
    const int xs[]={44,45,179,180,274,275,400},dy[]={-1,0,12,13};
    for(mode=0;mode<8192;mode++){
        reset();int menu=mode%4+1;SetupMenuState s=state(menu,mode/4%item_count(menu));
        newkey=mode&1;newmouse=mode&2;lastkey_scan=scans[mode/8%9];lastmouse_but=mode/72%3+1;
        mouse_x=xs[mode/216%7];mouse_y=37+21*(mode/1512%item_count(menu))+dy[mode/16%4];
        lastmouse_x=mode&16?mouse_x:400;lastmouse_y=mode&32?mouse_y:0;
        printf("menuinput:%d\n",mode);dump_input(collect_setup_menu_input(&s,menus[menu].items,item_count(menu),mode&4));
    }
    for(mode=0;mode<8192;mode++){
        reset();const MenuItem *item=picker(mode%4);int yPicker=mode/4%3*20-5;
        newkey=mode&1;newmouse=mode&2;lastkey_scan=scans[mode/8%9];lastmouse_but=mode/72%3+1;
        mouse_x=xs[mode/216%7];mouse_y=yPicker+15*(mode/1512%item->getPickerItemsCount())+dy[mode/16%4];
        lastmouse_x=mode&16?mouse_x:400;lastmouse_y=mode&32?mouse_y:0;
        printf("pickerinput:%d\n",mode);dump_input(collect_setup_picker_input(item,yPicker,mode&4));
    }
    fail_scaler=true;
    for(mode=0;mode<1024;mode++){
        reset();const MenuItem *item=picker(mode%4);int count=(int)item->getPickerItemsCount();
        SetupMenuState s=state(item->id==MENU_ITEM_SHOP_CONTENTS?MENU_GAMEPLAY:MENU_GRAPHICS,0);
        s.currentPicker=item->id;s.pickerSelectedIndex=mode/4%count;scaler=2;
        SetupInput i={(SetupInputAction)(mode/16%5),mode&128?(size_t)(mode/32%count):SIZE_MAX,0};
        printf("pickeraction:%d\n",mode);apply_setup_picker_action(&s,item,&i);dump(&s);
    }
    fail_scaler=false;
    for(mode=0;mode<156;mode++){
        reset();SetupMenuState s=state(mode/13%4+1,0);
        printf("activate:%d\n",mode);apply_setup_menu_activation(&s,(MenuItemId)(mode%13));dump(&s);
    }
    const int values[]={-300,-256,-255,-9,-1,0,1,8,255,256,300};
    for(mode=0;mode<2816;mode++){
        reset();int menu=mode%4+1;SetupMenuState s=state(menu,mode/4%item_count(menu));
        SetupInput i={(SetupInputAction)(mode/16%8),mode&128?(size_t)(mode/32%item_count(menu)):SIZE_MAX,values[mode/256%11]};
        printf("menuaction:%d\n",mode);apply_setup_menu_action(&s,menus[menu].items,item_count(menu),&i);dump(&s);
    }
    phase=0;
    for(mode=0;mode<8;mode++){
        reset();if(mode==4)mouse_x=-1;bool moved=false;printf("wait:%d\n",mode);
        wait_for_setup_input(fullscreen_display,&moved);printf("waitstate:%d,%d,%d,%d,%d\n",moved,mouse_x,mouse_y,waits,fullscreen_display);
    }
    phase=1;
    for(mode=0;mode<64;mode++){
        reset();printf("run:%d\n",mode);setupMenu();printf("runstate:%d,%d,%d,%d,%d,%d,%d,%u,%u\n",steps,
            fullscreen_display,scaler,scaling_mode,shopContentSetting,music_disabled,samples_disabled,tyrMusicVolume,fxVolume);
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
