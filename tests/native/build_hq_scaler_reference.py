"""Wrap the complete unchanged HQ scaler with synthetic surfaces and palettes."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "video_scale_hqNx.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include ".*"$', '', source, flags=re.MULTILINE)
    # Out-of-line linkage for C99 and MSVC; interpolation bodies are unchanged.
    source = source.replace("inline ", "")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#ifdef _WIN32
#include <fcntl.h>
#include <io.h>
#endif
typedef uint8_t Uint8;
typedef uint32_t Uint32;
typedef struct {void *pixels;int pitch;} SDL_Surface;
typedef struct {void *pixels;int pitch;} SDL_Texture;
static int vga_width,vga_height;
static uint32_t rgb_palette[256],yuv_palette[256];
static int SDL_LockTexture(SDL_Texture *t,void *unused,void **p,int *pitch) {
    (void)unused;*p=t->pixels;*pitch=t->pitch;return 0;
}
static void SDL_UnlockTexture(SDL_Texture *t) {(void)t;}
""" + source + r"""
static void read_exact(FILE *file,void *buffer,size_t size) {
    if(fread(buffer,1,size,file)!=size)exit(3);
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;
#ifdef _WIN32
    _setmode(_fileno(stdout),_O_BINARY);
#endif
    FILE *input=fopen(argv[1],"rb");if(!input)return 2;
    uint32_t count;read_exact(input,&count,4);
    for(uint32_t scene=0;scene<count;scene++) {
        int dimensions[5];read_exact(input,dimensions,sizeof(dimensions));
        vga_width=dimensions[0];vga_height=dimensions[1];
        int factor=dimensions[2],source_pitch=dimensions[3],output_pitch=dimensions[4];
        read_exact(input,rgb_palette,sizeof(rgb_palette));read_exact(input,yuv_palette,sizeof(yuv_palette));
        size_t length=(size_t)output_pitch*vga_height*factor;
        Uint8 *pixels=malloc((size_t)source_pitch*vga_height);
        Uint32 *output=malloc(length*4);
        if(!pixels||!output)return 4;
        read_exact(input,pixels,(size_t)source_pitch*vga_height);
        for(size_t i=0;i<length;i++)output[i]=0x12345678;
        SDL_Surface surface={pixels,source_pitch};SDL_Texture texture={output,output_pitch*4};
        switch(factor) {
            case 2:hq2x_32(&surface,&texture);break;
            case 3:hq3x_32(&surface,&texture);break;
            case 4:hq4x_32(&surface,&texture);break;
            default:return 5;
        }
        if(fwrite(output,4,length,stdout)!=length)return 6;
        free(pixels);free(output);
    }
    fclose(input);return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
