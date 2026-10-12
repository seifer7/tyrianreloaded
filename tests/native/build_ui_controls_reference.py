"""Verify unchanged shadowed HUD bars, input polling, and keyboard no-op behavior."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    controls = (root / "src" / "nortvars.c").read_text(encoding="utf-8")
    graphics = (root / "src" / "vga256d.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t JE_byte,Uint8;
typedef uint16_t JE_word;
typedef bool JE_boolean;
typedef struct {int w,h,pitch;void *pixels;} SDL_Surface;
typedef struct {int x,y,w,h;} SDL_Rect;
static bool newkey,newmouse,mousedown,joydown,inputDetected;
static int flags;
static void poll_joysticks(void) {puts("poll");joydown=(flags&4)!=0;}
static void service_SDL_events(bool clear) {printf("events:%d\n",clear);newkey=(flags&1)!=0;newmouse=(flags&8)!=0;}
static void SDL_FillRect(SDL_Surface *surface,SDL_Rect *rect,int color) {
    int left=rect->x<0?0:rect->x,top=rect->y<0?0:rect->y;
    int right=rect->x+rect->w<surface->w?rect->x+rect->w:surface->w;
    int bottom=rect->y+rect->h<surface->h?rect->y+rect->h:surface->h;
    for(int y=top;y<bottom;y++)for(int x=left;x<right;x++)((Uint8*)surface->pixels)[y*surface->pitch+x]=(Uint8)color;
}
""" + "".join(function(graphics, name) for name in ("JE_pix", "fill_rectangle_xy", "JE_barShade")) + \
        controls[controls.index("JE_boolean JE_anyButton("):controls.index("void JE_dBar3(")] + \
        controls[controls.index("void JE_barDrawShadow("):] + r"""
int main(void) {
    for(int pitch=320;pitch<=328;pitch+=8)for(int mode=0;mode<128;mode++) {
        SDL_Surface surface={320,200,pitch,malloc(pitch*200)};
        if(!surface.pixels)return 2;
        for(int i=0;i<pitch*200;i++)((Uint8*)surface.pixels)[i]=(Uint8)(i*17+i/7);
        JE_word x=(mode&1)?310:20,y=(mode&2)?195:50,res=(mode%17)+1,col=mode*515,amt=mode%32,
            width=mode%6,height=mode%15;
        printf("bar:%d,%d\n",pitch,mode);
        JE_barDrawShadow(&surface,x,y,res,col,amt,width,height);
        uint32_t hash=2166136261u;for(int i=0;i<pitch*200;i++)hash=(hash^((Uint8*)surface.pixels)[i])*16777619u;
        printf("pixels:%u\n",hash);
        free(surface.pixels);
    }
    for(flags=0;flags<16;flags++) {
        newkey=true;newmouse=true;mousedown=(flags&2)!=0;joydown=true;
        inputDetected=(flags&1)!=0;
        bool result=JE_anyButton();
        JE_wipeKey();
        printf("button:%d,%d,%d,%d,%d,%d,%d\n",flags,result,newkey,newmouse,mousedown,joydown,inputDetected);
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
