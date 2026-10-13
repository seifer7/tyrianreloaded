"""Verify unchanged gamma correction/key handling and legacy high-score sorting."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    source = source.replace("JE_boolean JE_gammaCheck(", "bool JE_gammaCheck(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <math.h>
#include <string.h>
typedef uint8_t JE_byte;
typedef int32_t JE_longint;
typedef float JE_real;
typedef bool JE_boolean;
typedef struct {JE_byte r,g,b;} Color;
typedef Color Palette[256];
static Palette colors,palettes[2];
static JE_byte gammaCorrection,keysactive[512],pcxpal[3]={0,0,1};
static bool newkey;
#define SDL_SCANCODE_F11 68
static void set_palette(Palette p,int first,int last) {
    uint32_t h=2166136261u;
    for(int i=first;i<=last;i++) {h=(h^p[i].r)*16777619u;h=(h^p[i].g)*16777619u;h=(h^p[i].b)*16777619u;}
    printf("set:%d,%d,%u\n",first,last,h);
}
static unsigned int temp;
static struct {JE_longint highScore1,highScore2;char highScoreName[30];JE_byte highScoreDiff,level;} saveFiles[22];
""" + "".join(function(source, name) for name in
              ("JE_gammaCorrect_func", "JE_gammaCorrect", "JE_gammaCheck", "JE_sort", "JE_sortHighScores")) + r"""
int main(void) {
    for(int gamma=0;gamma<256;gamma++) {
        for(int i=0;i<256;i++) {colors[i].r=i;colors[i].g=255-i;colors[i].b=i*17;}
        JE_gammaCorrect(&colors,gamma);
        printf("gamma:%d",gamma);for(int i=0;i<256;i++)printf(",%u,%u,%u",colors[i].r,colors[i].g,colors[i].b);puts("");
    }
    for(int mode=0;mode<1024;mode++) {
        for(int i=0;i<256;i++){palettes[1][i].r=i+mode;palettes[1][i].g=i*3+mode;palettes[1][i].b=i*7+mode;}
        memcpy(colors,palettes[1],sizeof colors);
        gammaCorrection=mode%256;keysactive[68]=(mode&1)?2:0;newkey=(mode&2)!=0;
        printf("check:%d\n",mode);bool result=JE_gammaCheck();
        printf("keys:%d,%u,%u,%d\n",result,gammaCorrection,keysactive[68],newkey);
        uint32_t h=2166136261u;for(int i=0;i<256;i++){h=(h^colors[i].r)*16777619u;h=(h^colors[i].g)*16777619u;h=(h^colors[i].b)*16777619u;}
        printf("colors:%u\n",h);
    }
    const int32_t scores[]={INT32_MIN,INT32_MAX,0,-1,50,50,20,-40,3};
    for(int mode=0;mode<512;mode++) {
        for(int i=0;i<22;i++) {
            saveFiles[i].highScore1=scores[(mode+i*3+i/3)%9];saveFiles[i].highScore2=mode+i;
            sprintf(saveFiles[i].highScoreName,"name%d_%d",mode,i);
            saveFiles[i].highScoreDiff=mode+i;saveFiles[i].level=i;
        }
        printf("sort:%d\n",mode);
        if(mode&1)JE_sortHighScores();else {temp=mode%20;JE_sort();}
        for(int i=0;i<22;i++)printf("slot:%d,%d,%s,%u,%u\n",saveFiles[i].highScore1,saveFiles[i].highScore2,
            saveFiles[i].highScoreName,saveFiles[i].highScoreDiff,saveFiles[i].level);
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
