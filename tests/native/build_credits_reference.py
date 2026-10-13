"""Trace unchanged credits scrolling, picture fades, ship movement and exit flow."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mtrand.h"
typedef uint8_t JE_byte;
typedef int8_t JE_shortint;
typedef int16_t JE_integer;
typedef unsigned int uint;
typedef uint8_t Uint8;
#define EXTRA_SHAPES 7
#define SMALL_FONT_SHAPES 1
static int screen,*VGAScreen=&screen,*VGAScreenSeg=&screen,spriteSheet9;
static struct {int count;} sprite_table[8];
static struct {uint16_t width,height;} pictures[4];
static struct {uint16_t shipgraphic;} ships[12];
static uint8_t colors[768],palettes[6][768];
static const char *miscText[55];
static int mode,frame,limit,line,clock_ticks,delay2;
static void load_sprites_file(int t,const char *s){printf("load:%s\n",s);}
static void free_sprites(int t){puts("free");}
static void setDelay2(int t){delay2=clock_ticks+t;printf("delay2:%d\n",t);}
static void setDelay(int t){printf("delay:%d\n",t);}
static void play_song(uint s){printf("song:%u\n",s);}
static FILE *dir_fopen_die(const char *d,const char *n,const char *m){line=0;puts("textfile");return tmpfile();}
static const char *data_dir(void){return "";}
static void read_encrypted_pascal_string(char *s,size_t size,FILE *f){
    if(line%17==0)strcpy(s,".");else if(line%13==0)s[0]=0;
    else snprintf(s,size,"%cLine%d",65+line%16,line);line++;
}
static void JE_clr256(int *s){puts("clear");}
static void JE_showVGA(void){puts("present");}
static void fade_palette(uint8_t *p,int n,int a,int b){printf("fadein:%d\n",n);}
#define sprite(t,i) (&pictures[i])
static void blit_sprite_hv(int *s,int x,int y,int t,int i,int h,int v){printf("picture:%d,%d,%d,%d\n",i,x,y,v);}
static int getDelayTicks2(void){return delay2>clock_ticks?delay2-clock_ticks:0;}
static void blit_sprite2x2(int *s,int x,int y,int t,uint g){printf("ship:%d,%d,%u\n",x,y,g);}
static int JE_textWidth(const char *s,int f){printf("width:%s\n",s);return strlen(s)*2;}
static void JE_outTextAdjust(int *s,int x,int y,const char *text,Uint8 color,int b,int f,bool shadow){
    printf("text:%d,%d,%s,%u,%d\n",x,y,text,color,b);
}
static void fill_rectangle_xy(int *s,int a,int b,int c,int d,int color){printf("fill:%d,%d,%d,%d\n",a,b,c,d);}
static void fade_song(void){puts("fadesong");}
static void keep(void){puts("keep");}
#define NETWORK_KEEP_ALIVE() keep()
static void wait_delay(void){clock_ticks++;puts("wait");}
static bool JE_anyButton(void){bool done=++frame==limit;printf("button:%d\n",done);return done;}
static void fade_black(int n){printf("fade:%d\n",n);}
""" + function(source, "JE_playCredits") + r"""
int main(void){
    int limits[]={1,200,1001,7381,7900,8000};
    for(mode=0;mode<6;mode++){
        frame=clock_ticks=0;limit=limits[mode];sprite_table[7].count=mode%4+1;
        for(int i=0;i<4;i++){pictures[i].width=10+i*3;pictures[i].height=20+i*5;}
        for(int i=0;i<12;i++)ships[i].shipgraphic=100+i*10;
        miscText[54]="Episode";mt_srand(mode+1);printf("credits:%d\n",mode);JE_playCredits();
        printf("rng:%u\n",(uint32_t)mt_rand());
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
