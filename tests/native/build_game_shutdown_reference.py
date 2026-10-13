"""Trace original resource shutdown, configuration, farewell and process exit."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "varz.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <setjmp.h>
#define SOUND_COUNT 38
typedef uint8_t JE_byte;
static bool trentWin;
static void *soundSamples[SOUND_COUNT];
static int shopSpriteSheet,explosionSpriteSheet,destructSpriteSheet;
static jmp_buf ended;
static void deinit_audio(void){puts("audio");}
static void deinit_video(void){puts("video");}
static void deinit_joysticks(void){puts("joysticks");}
static void free_main_shape_tables(void){puts("mainshapes");}
static void free_sprite2s(int *sheet){printf("sheet:%ld\n",(long)(sheet==&shopSpriteSheet?0:sheet==&explosionSpriteSheet?1:2));}
static void release_sample(void *sample){printf("sample:%u\n",*(uint8_t*)sample);}
static void JE_saveConfiguration(void){puts("save");}
static void SDL_Quit(void){puts("sdlquit");}
static void finish(int code){printf("exit:%d\n",code);longjmp(ended,1);}
#define free release_sample
#define exit finish
""" + function(source, "JE_tyrianHalt") + r"""
int main(void){
    uint8_t samples[SOUND_COUNT];
    for(unsigned int i=0;i<SOUND_COUNT;i++){samples[i]=(uint8_t)i;soundSamples[i]=&samples[i];}
    for(unsigned int mode=0;mode<512;mode++){
        trentWin=mode&256;printf("case:%u\n",mode);
        if(!setjmp(ended))JE_tyrianHalt((JE_byte)mode);
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
