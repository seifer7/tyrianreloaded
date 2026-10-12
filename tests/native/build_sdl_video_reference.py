"""Compile unchanged video/window and all scaler sources against real SDL2."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "video.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include[^\n]*\n', '', source, flags=re.MULTILINE)
    scale = (root / "src" / "video_scale.c").read_text(encoding="utf-8")
    scale = re.sub(r'^#include ".*"$', '', scale, flags=re.MULTILINE)
    hq = (root / "src" / "video_scale_hqNx.c").read_text(encoding="utf-8")
    hq = re.sub(r'^#include ".*"$', '', hq, flags=re.MULTILINE).replace("inline ", "")
    palette = (root / "src" / "palette.c").read_text(encoding="utf-8")
    yuv = palette[palette.index("static Uint32 rgb_to_yuv(int r, int g, int b)\n{"):]
    return r"""
#define SDL_MAIN_HANDLED
#include "SDL.h"
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define vga_width 320
#define vga_height 200
#define COUNTOF(a) (sizeof(a)/sizeof((a)[0]))
typedef unsigned int uint;
typedef enum {SCALE_CENTER,SCALE_INTEGER,SCALE_ASPECT_8_5,SCALE_ASPECT_4_3,ScalingMode_MAX} ScalingMode;
typedef void (*ScalerFunction)(SDL_Surface *,SDL_Texture *);
struct Scalers {int width,height;ScalerFunction scaler16,scaler32;const char *name;};
static uint32_t rgb_palette[256],yuv_palette[256];
void reinit_fullscreen(int display);
void JE_clr256(SDL_Surface *surface);
bool init_scaler(unsigned int new_scaler);
""" + scale + hq + yuv + source + r"""
static uint32_t hash(const void *data,int count) {
    uint32_t result=2166136261u;const uint8_t *bytes=data;
    for(int i=0;i<count;i++)result=(result^bytes[i])*16777619u;return result;
}
static void state(int stage) {
    int w,h;SDL_GetWindowSize(main_window,&w,&h);
    uint32_t *pixels=malloc(w*h*4);
    if(!pixels || SDL_RenderReadPixels(main_window_renderer,NULL,SDL_PIXELFORMAT_RGB888,pixels,w*4)!=0)exit(4);
    printf("frame:%d,%d,%d,%d,%d,%d,%d,%d,%u\n",stage,w,h,fullscreen_display,
        last_output_rect.x,last_output_rect.y,last_output_rect.w,last_output_rect.h,hash(pixels,w*h*4));
    free(pixels);
}
int main(void) {
    fullscreen_display=-1;init_video();
    for(int i=0;i<256;i++) {
        rgb_palette[i]=(i<<16)|((255-i)<<8)|(i^85);
        yuv_palette[i]=rgb_to_yuv(i,255-i,i^85);
    }
    for(int i=0;i<64000;i++)((uint8_t*)VGAScreen->pixels)[i]=(i*17+i/320*3)&255;
    int stage=0;
    for(int s=0;s<9;s++) {
        init_scaler(s);
        for(int mode=0;mode<4;mode++) {
            scaling_mode=mode;SDL_SetWindowSize(main_window,853,517);JE_showVGA();state(stage++);
        }
        SDL_SetWindowSize(main_window,17,23);video_on_win_resize();JE_showVGA();state(stage++);
    }
    reinit_fullscreen(999);JE_showVGA();state(stage++);
    toggle_fullscreen();JE_showVGA();state(stage++);
    JE_clr256(VGAScreen);JE_showVGA();state(stage++);
    deinit_video();return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
