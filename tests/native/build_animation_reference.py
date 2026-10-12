"""Play synthetic LPF files through the complete unchanged native animation implementation."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "animlib.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include ".*"$', '', source, flags=re.MULTILINE)
    buffers = (root / "src" / "sizebuf.c").read_text(encoding="utf-8")
    buffers = re.sub(r'^#include ".*"$', '', buffers, flags=re.MULTILINE)
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t Uint8,JE_byte;
typedef uint16_t Uint16;
typedef uint32_t Uint32;
#define SDL_SwapLE16(value) (value)
typedef struct {Uint8 *data;unsigned int bufferLen,bufferPos;bool error;} sizebuf_t;
""" + buffers + r"""
static uint8_t pixels[64000];
static struct {int h,pitch;uint8_t *pixels;} surface={200,320,pixels},*VGAScreen=&surface;
static struct {Uint8 r,g,b;} colors[256];
static int shown,interrupt_frame;
static bool newkey;
static FILE *dir_fopen(const char *directory,const char *filename,const char *mode) {(void)directory;return fopen(filename,mode);}
static const char *data_dir(void) {return ".";}
static long ftell_eof(FILE *file) {long position=ftell(file);fseek(file,0,SEEK_END);long size=ftell(file);fseek(file,position,SEEK_SET);return size;}
static void fread_die(void *data,size_t size,size_t count,FILE *file) {if(fread(data,size,count,file)!=count)exit(3);}
static void fread_u16_die(Uint16 *data,size_t count,FILE *file) {fread_die(data,2,count,file);}
static void fread_u32_die(Uint32 *data,size_t count,FILE *file) {fread_die(data,4,count,file);}
static void fread_u8_die(Uint8 *data,size_t count,FILE *file) {fread_die(data,1,count,file);}
static void set_palette(const void *data,int first,int last) {
    (void)data;printf("palette:%d,%d:",first,last);
    for(int i=0;i<256;i++)printf("%02X%02X%02X",colors[i].r,colors[i].g,colors[i].b);puts("");
}
static void JE_clr256(void *screen) {(void)screen;memset(pixels,0,sizeof(pixels));}
static void JE_showVGA(void) {
    uint32_t hash=2166136261u;for(int i=0;i<64000;i++)hash=(hash^pixels[i])*16777619u;
    printf("show:%08X\n",hash);shown++;
}
static void setDelay(int speed) {printf("delay:%d\n",speed);}
static void wait_delay(void) {puts("wait");}
static void service_SDL_events(bool clear) {printf("events:%d\n",clear);newkey=shown-1==interrupt_frame;}
#define NETWORK_KEEP_ALIVE() puts("network")
""" + source + r"""
int main(int argc,char **argv) {
    if(argc!=5)return 2;
    interrupt_frame=atoi(argv[4]);memset(pixels,193,sizeof(pixels));
    JE_playAnim(argv[1],(JE_byte)atoi(argv[2]),(JE_byte)atoi(argv[3]));
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
