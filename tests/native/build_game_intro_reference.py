"""Compare native intro logo services and every screen-bleed frame/scratch buffer."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include <assert.h>
typedef uint8_t Uint8;
typedef struct {unsigned int pitch,h;uint8_t *pixels;} SDL_Surface;
static uint8_t primary_pixels[327*200],game_pixels[327*200],background_pixels[327*200];
static SDL_Surface primary={320,200,primary_pixels},game={320,200,game_pixels},background={320,200,background_pixels};
static SDL_Surface *VGAScreen=&primary,*VGAScreenSeg=&primary,*VGAScreen2=&background,*game_screen=&game;
static bool moveTyrianLogoUp;
static int colors[256],frame,exit_frame;
static unsigned int levelWarningLines;
static uint32_t hash(const uint8_t *p,unsigned int size){uint32_t h=2166136261u;for(unsigned int i=0;i<size;i++)h=(h^p[i])*16777619u;return h;}
static void bytes(const uint8_t *p,unsigned int size){fputs("bytes:",stdout);for(unsigned int i=0;i<size;i++)printf("%02X",p[i]);puts("");}
static void SDL_FillRect(SDL_Surface *s,void *rect,int value){(void)rect;memset(s->pixels,value,s->pitch*s->h);puts("clear");}
static void fade_white(int n){printf("white:%d\n",n);}
static void fade_black(int n){printf("black:%d\n",n);}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static void fade_palette(void *p,int n,int first,int last){(void)p;(void)first;(void)last;printf("fadein:%d\n",n);}
static void setDelay(int n){if(exit_frame==0)printf("delay:%d\n",n);}
static void wait_delayorinput(void){puts("waitinput");}
static void wait_delay(void){}
static void service_SDL_events(bool clear){(void)clear;}
static bool JE_anyButton(void){return frame>=exit_frame;}
static void JE_showVGA(void){
    if(exit_frame==0)puts("present");
    else {frame++;unsigned int size=primary.pitch*primary.h;
        printf("frame:%d,%u,%u,%u\n",frame,hash(primary_pixels,size),hash(game_pixels,size),hash(background_pixels,size));}
}
""" + function(source, "intro_logos") + function(source, "JE_whoa") + r"""
int main(void){
    exit_frame=0;memset(primary_pixels,123,sizeof primary_pixels);intro_logos();
    printf("introstate:%d,%u\n",moveTyrianLogoUp,hash(primary_pixels,64000));
    const int exits[]={1,2,3,27,299,300};
    for(int pitch=320;pitch<=327;pitch+=7)for(unsigned int n=0;n<sizeof exits/sizeof exits[0];n++){
        primary.pitch=game.pitch=background.pitch=pitch;exit_frame=exits[n];frame=0;levelWarningLines=9;
        unsigned int size=pitch*200;
        for(unsigned int i=0;i<size;i++){primary_pixels[i]=(uint8_t)(i*17+n*31);game_pixels[i]=0xaa;background_pixels[i]=0xbb;}
        printf("bleed:%d,%d\n",pitch,exit_frame);JE_whoa();
        printf("bleedstate:%u,%u,%u,%u\n",levelWarningLines,hash(primary_pixels,size),hash(game_pixels,size),hash(background_pixels,size));
        bytes(primary_pixels,size);bytes(game_pixels,size);bytes(background_pixels,size);
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
