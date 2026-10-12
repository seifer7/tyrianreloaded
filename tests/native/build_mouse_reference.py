"""Compare unchanged cursor grabs, restoration, and actual clipped sprite blits."""

import argparse
from pathlib import Path
import re
import build_input_reference


def function(source, name):
    start = source.index("void " + name + "(")
    brace = source.index("{", start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end] + "\n"


def build_source(root):
    base = build_input_reference.build_source(root).split("static uint32_t hash(")[0]
    base = base.replace("static bool mouseInactive,joydown,isNetworkGame;",
                        "extern bool mouseInactive;\nstatic bool joydown,isNetworkGame;")
    base = base.replace("typedef struct {int w,h;} SDL_Surface;",
                        "typedef struct {int BitsPerPixel;} SDL_PixelFormat;\n"
                        "typedef struct {int w,h,pitch;void *pixels;SDL_PixelFormat *format;} SDL_Surface;")
    sprite = (root / "src" / "sprite.c").read_text(encoding="utf-8")
    mouse = (root / "src" / "mouse.c").read_text(encoding="utf-8")
    mouse = re.sub(r'^#include[^\n]*\n', '', mouse, flags=re.MULTILINE)
    blits = "".join(function(sprite, name) for name in (
        "blit_sprite2_clip", "blit_sprite2_filter_clip", "blit_sprite2x2_clip", "blit_sprite2x2_filter_clip"))
    return base + r"""
typedef uint16_t Uint16;
typedef uint8_t JE_byte;
typedef struct {uint8_t *data;unsigned int size;} Sprite2_array;
static Sprite2_array shopSpriteSheet;
#define MIN(a,b) ((a)<(b)?(a):(b))
#define MAX(a,b) ((a)>(b)?(a):(b))
#define SDL_SwapLE16(x) (x)
""" + blits + mouse + r"""
static uint32_t hash(const void *data,int count) {
    uint32_t result=2166136261u;const uint8_t *bytes=data;
    for(int i=0;i<count;i++)result=(result^bytes[i])*16777619u;return result;
}
int main(void) {
    uint8_t shapes[600+300*197];memset(shapes,0,sizeof(shapes));
    int position=600;
    for(int shape=0;shape<300;shape++) {
        shapes[shape*2]=position&255;shapes[shape*2+1]=position>>8;
        for(int y=0;y<14;y++) {
            shapes[position++]=0xc0;
            for(int x=0;x<12;x++)shapes[position++]=(shape*7+x*3+y*5)&255;
            shapes[position++]=0;
        }shapes[position++]=0x0f;
    }shopSpriteSheet.data=shapes;shopSpriteSheet.size=position;
    SDL_PixelFormat format={8};surface.format=&format;surface.w=320;surface.h=200;
    int coordinates[]={-20,-1,0,1,159,319,320,400};
    for(int pitch=320;pitch<=336;pitch+=16) {
        uint8_t pixels[336*200];surface.pitch=pitch;surface.pixels=pixels;
        for(int cursor=0;cursor<5;cursor++)for(int i=0;i<64;i++)for(int mode=0;mode<4;mode++) {
            for(int p=0;p<pitch*200;p++)pixels[p]=(p*13+i*7+cursor)&255;
            event_index=event_count=0;mouse_x=coordinates[i%8];mouse_y=coordinates[i/8];
            mapWindowPointToScreen(&mouse_x,&mouse_y);
            mouseCursor=cursor;has_mouse=mode!=3;mouseInactive=mode==2;
            mousedown=i%2!=0;lastmouse_but=i%5+1;mouseButton=0;
            memset(mouseGrabShape,0,sizeof(mouseGrabShape));mouseGrabX=mouseGrabY=0;
            if(mode==1)JE_mouseStartFilter((i*17)&255);else JE_mouseStart();
            uint32_t started=hash(pixels,pitch*200);
            JE_mouseReplace();
            printf("cursor:%d,%d,%d,%d,%u,%u,%u\n",pitch,cursor,i,mode,mouseButton,started,hash(pixels,pitch*200));
        }
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
