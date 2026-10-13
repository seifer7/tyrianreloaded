"""Verify unchanged score qualification, name editing and result presentation."""

import argparse
from pathlib import Path
import build_player_progression_reference
from build_joystick_reference import function


def build_source(root):
    base = build_player_progression_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace("fill_rectangle_xy", "baseline_fill_rectangle_xy")
    base = base.replace('static void JE_dString(int *screen,int x,int y,const char *text,int font) {puts("insertcoin");}',
                        'static void JE_dString(int *screen,int x,int y,const char *text,int font) {printf("title:%d,%d,%s,%d\\n",x,y,text,font);}')
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    fonts = (root / "src" / "fonthand.c").read_text(encoding="utf-8")
    mapping = fonts[fonts.index("const int font_ascii[256]"):fonts.index("};", fonts.index("const int font_ascii[256]")) + 2]
    return base + r"""
#include <ctype.h>
#undef SMALL_FONT_SHAPES
#define SMALL_FONT_SHAPES 1
#define FONT_SHAPES 0
#define TINY_FONT 2
#define FULL_SHADE 1
#define SDL_SCANCODE_BACKSPACE 42
#define SDL_SCANCODE_DELETE 76
#define SDL_SCANCODE_ESCAPE 41
#define SDL_SCANCODE_RETURN 40
#define SDL_SCANCODE_KP_ENTER 88
typedef bool JE_boolean;
typedef int32_t Sint32;
static JE_byte initial_episode_num;
static struct {JE_longint highScore1,highScore2;char highScoreName[30];JE_byte highScoreDiff,level;} saveFiles[22];
static struct {void *data;} shopSpriteSheet;
static char *episode_name[6]={"","Episode1","Episode2","Episode3","Episode4","Episode5"};
static uint8_t colors[768],palettes[23][768];
static bool newkey,newmouse,new_text,playing;
static int lastkey_scan,mouseButton,scoreMode,edit,blink;
static char last_text[32];
static unsigned int frameCountMax;
static int textGlowFont,textGlowBrightness;
static void JE_loadCompShapes(void *s,int c){shopSpriteSheet.data=(void*)1;printf("cursor:%c\n",c);}
static void wait_noinput(bool k,bool m,bool j){edit=blink=0;puts("nomouse");}
static void JE_clr256(void *s){puts("clear");}
static void JE_showVGA(void){puts("present");}
static void JE_barShade(void *s,int a,int b,int c,int d){printf("shade:%d,%d,%d,%d\n",a,b,c,d);}
static int JE_fontCenter(const char *s,int f){printf("center:%s,%d\n",s,f);return 100;}
static void JE_textShade(void *s,int x,int y,const char *t,int h,int b,int shade){printf("shaded:%d,%d,%s,%d,%d\n",x,y,t,h,b);}
static void JE_outText(void *s,int x,int y,const char *t,int h,int b){printf("name:%d,%d,%s\n",x,y,t);}
static int JE_textWidth(const char *s,int f){printf("width:%s\n",s);return strlen(s)*2;}
static void fill_rectangle_xy(void *s,int a,int b,int c,int d,int v){printf("fill:%d,%d,%d,%d,%d\n",a,b,c,d,v);}
static void setDelay(int t){printf("delay:%d\n",t);}
static void JE_mouseStart(void){puts("mouse");}
static void JE_mouseReplace(void){puts("replace");}
static void push_joysticks_as_keyboard(void){puts("joystick");}
static void fade_palette(void *p,int n,int a,int b){printf("fadein:%d\n",n);}
static void fade_black(int n){printf("fade:%d\n",n);}
static void JE_loadPic(void *s,int n,bool store){puts("picture:2");}
static void wait_input(bool k,bool m,bool j){puts("input");}
static void JE_outTextGlow(void *s,int x,int y,const char *t){
    printf("glow:%d,%d,%s,%u,%d,%d\n",x,y,t,frameCountMax,textGlowFont,textGlowBrightness);
    textGlowBrightness=6;if(scoreMode&16)frameCountMax=0;
}
static void service_SDL_events(bool clear){
    if(clear){newkey=newmouse=new_text=false;mouseButton=0;last_text[0]=0;blink=0;edit++;}
    puts("events");
}
static void service_wait_delay(void){
    puts("wait");blink++;
    if(blink<((scoreMode&32)?16:1))return;
    int kind=scoreMode%8;
    if(kind==0){newkey=true;lastkey_scan=40;}
    else if(kind==1){newkey=true;lastkey_scan=41;}
    else if(kind==2 || kind==3){
        if(edit==1){new_text=true;strcpy(last_text,"ab @cd");}
        else if(edit==2){newkey=true;lastkey_scan=kind==2?42:76;}
        else {newkey=true;lastkey_scan=88;}
    }else if(kind==4){new_text=true;strcpy(last_text,"abcdefghijklmnopqrstuvwxyz12345");if(edit>1){new_text=false;newkey=true;lastkey_scan=40;}}
    else if(kind==5){newmouse=true;mouseButton=1;mouseX=100;mouseY=130;}
    else if(kind==6){newmouse=true;mouseButton=1;mouseX=200;mouseY=130;}
    else {new_text=true;last_text[0]=(char)0x80;last_text[1]='z';last_text[2]=0;if(edit>1){new_text=false;newkey=true;lastkey_scan=40;}}
}
""" + mapping + "".join(function(source, name) for name in ("JE_sort", "JE_sortHighScores", "JE_highScoreCheck")) + r"""
int main(int argc,char **argv){
    if(argc!=2)return 2;load_typed_items(argv[1]);
    for(int i=0;i<68;i++)sprintf(pickup_text[i],"Text%d",i);
    const unsigned long cash_cases[]={0,4000,4001,UINT32_MAX,INT32_MAX,2147483648UL,3000,10000};
    for(scoreMode=0;scoreMode<512;scoreMode++){
        memset(player,0,sizeof player);shopSpriteSheet.data=scoreMode&1?(void*)1:NULL;
        twoPlayerMode=scoreMode&8;initial_episode_num=scoreMode%3+1;episodeNum=initial_episode_num;difficultyLevel=scoreMode%11;
        playing=scoreMode&64;mouseButton=0;mouseX=mouseY=0;temp3=9;frameCountMax=0;
        for(int p=0;p<2;p++){player[p].items.weapon[0].power=player[p].items.weapon[1].power=1;
            player[p].cash=scoreMode<128?1000+(scoreMode*997+p*1171)%10000:cash_cases[(scoreMode/16+p)%8];}
        for(int i=0;i<22;i++){
            saveFiles[i].highScore1=(scoreMode&64)?100000+i:((i%3)*2000);
            saveFiles[i].highScore2=123+i;sprintf(saveFiles[i].highScoreName,"Old%d",i);
            saveFiles[i].highScoreDiff=i+10;saveFiles[i].level=i;
        }
        printf("scores:%d\n",scoreMode);JE_highScoreCheck();
        for(int i=0;i<22;i++)printf("slot:%d,%d,%s,%u,%u\n",saveFiles[i].highScore1,saveFiles[i].highScore2,saveFiles[i].highScoreName,
            saveFiles[i].highScoreDiff,saveFiles[i].level);
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
