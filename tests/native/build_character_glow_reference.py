"""Compare unchanged character-by-character text glow including escape/skip/warning ordering."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    font = (root / "src" / "fonthand.c").read_text(encoding="utf-8")
    table = font[font.index("const int font_ascii["):font.index("/* shape constants")]
    return r"""
#include <stdint.h>
#include <stdio.h>
#include <stdbool.h>
#include <string.h>
typedef int16_t JE_integer;
typedef uint16_t JE_word;
typedef uint8_t JE_byte;
typedef int8_t JE_shortint;
typedef int SDL_Surface;
static SDL_Surface surface,*VGAScreen=&surface;
static bool warningRed,useLastBank,ESCPressed,levelWarningDisplay;
static unsigned int frameCountMax;
static int mode,buttons,delays,frames;
#define TINY_FONT 2
#define PART_SHADE 0
struct SpriteWidth {unsigned int width;};
static struct SpriteWidth glyph;
static struct SpriteWidth *sprite(int font,int id){glyph.width=id%5;printf("width:%d,%u\n",id,glyph.width);return &glyph;}
static void setDelay2(int f){printf("delay2:%d\n",f);}
static void setDelay(int f){frames=f;delays=0;printf("delay:%d\n",f);}
static void JE_textShade(SDL_Surface *s,int x,int y,const char *text,unsigned int hue,int b,int shade){printf("shade:%d,%d,%s,%u\n",x,y,text,hue);}
static void blit_sprite_hv(SDL_Surface *s,int x,int y,int font,int id,uint8_t hue,int8_t value){printf("glyph:%d,%d,%d,%u,%d\n",x,y,id,hue,value);}
static void blit_sprite_dark(SDL_Surface *s,int x,int y,int font,int id,bool black){printf("shadow:%d,%d,%d\n",x,y,id);}
static void keep_alive(void){puts("keep");}
#define NETWORK_KEEP_ALIVE() keep_alive()
static bool JE_anyButton(void){buttons++;bool b=buttons==mode%25;if((mode&8)&&buttons==mode%7)ESCPressed=true;printf("button:%d\n",b);return b;}
static void JE_updateWarning(SDL_Surface *s){puts("warning");}
static void SDL_Delay(unsigned int ms){delays++;printf("sleep:%u\n",ms);}
static unsigned int getDelayTicks(void){unsigned int r=frames>0&&delays<2?1:0;printf("remaining:%u\n",r);return r;}
static void JE_showVGA(void){puts("present");}
""" + table + function(source, "JE_outCharGlow") + r"""
int main(void) {
    const char *texts[]={"","A B","~Hi!","Byte\x80\xff","@A&","Name\0tail","A                                     B","ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz12345678"};
    for(mode=0;mode<1024;mode++) {
        buttons=delays=frames=0;warningRed=mode&1;useLastBank=mode&2;frameCountMax=(mode&4)?0:4;
        ESCPressed=(mode&64)!=0;levelWarningDisplay=mode&16;
        printf("chars:%d\n",mode);JE_outCharGlow((JE_word)(mode*79),(JE_word)(mode*51),texts[mode%8]);
        printf("state:%u,%d,%d\n",frameCountMax,buttons,ESCPressed);
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
