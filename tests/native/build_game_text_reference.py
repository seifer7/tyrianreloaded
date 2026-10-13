"""Compare unchanged warning bars and in-game text window ordering/state."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    font = (root / "src" / "fonthand.c").read_text(encoding="utf-8")
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    return r"""
#include <stdint.h>
#include <stdio.h>
#include <stdbool.h>
#include <string.h>
typedef uint8_t JE_byte;
typedef int8_t JE_shortint;
typedef uint16_t JE_word;
typedef struct {int w,h,pitch;uint8_t *pixels;} SDL_Surface;
static uint8_t pixels[65400];
static SDL_Surface surface={320,200,327,pixels},*VGAScreenSeg=&surface;
static JE_byte warningCol,warningSoundDelay;
static JE_shortint warningColChange;
static JE_word textErase;
static uint32_t ticks,target;
#define S_WARNING 17
#define OPTION_SHAPES 5
static uint32_t getDelayTicks2(void) {int32_t delay=(int32_t)(target-ticks);return delay>0?(uint32_t)delay:0;}
static void setDelay2(int frames) {target=(uint32_t)((float)ticks+frames*(0x4300*((12.0f/14318180.0f)*1000.0f)));}
static void fill_rectangle_xy(SDL_Surface *s,int left,int top,int right,int bottom,int color) {
    for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)s->pixels[y*s->pitch+x]=(uint8_t)color;
}
static void JE_showVGA(void){puts("present");}
static void JE_playSampleNum(int sample){printf("sound:%d\n",sample);}
static void blit_sprite(SDL_Surface *s,int x,int y,unsigned int table,unsigned int frame){printf("erase:%d,%d,%u\n",x,y,frame);}
static void JE_outText(SDL_Surface *s,int x,int y,const char *text,unsigned int hue,int value) {
    printf("text:%d,%d,%s,%u,%d\n",x,y,text,hue,value);
}
""" + function(font, "JE_updateWarning") + function(source, "JE_drawTextWindow") + r"""
int main(void) {
    for(int mode=0;mode<1024;mode++) {
        warningCol=mode%256;warningColChange=(int8_t)(mode/4);warningSoundDelay=mode%16;ticks=target=0;
        memset(pixels,0x45,sizeof pixels);
        printf("warning:%d\n",mode);
        for(int frame=0;frame<32;frame++) {
            JE_updateWarning(&surface);
            uint32_t hash=2166136261u;for(unsigned int i=0;i<sizeof pixels;i++)hash=(hash^pixels[i])*16777619u;
            printf("state:%u,%d,%u,%u,%u\n",warningCol,warningColChange,warningSoundDelay,getDelayTicks2(),hash);
            ticks+=frame%3==0?100:7;
        }
    }
    const char *texts[]={"","First","A~B","Byte\x80\xff","Name\0tail"};
    for(int mode=0;mode<20;mode++) {
        textErase=(mode&1)?65535:0;
        printf("window:%d\n",mode);JE_drawTextWindow(texts[mode%5]);printf("eraseTicks:%u\n",textErase);
        JE_drawTextWindow(texts[(mode+1)%5]);printf("eraseTicks:%u\n",textErase);
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
