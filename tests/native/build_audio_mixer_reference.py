"""Compile unchanged audio callback and channel dispatch with deterministic FM output."""

import argparse
from pathlib import Path


def build_source(root):
    source = (root / "src" / "loudness.c").read_text(encoding="utf-8")
    callback = source[source.index("static void audioCallback(void *userdata, Uint8 *stream, int size)\n{"):source.index("void deinit_audio(")]
    operations = source[source.index("void set_volume("):]
    return r"""
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <stdbool.h>
#include <string.h>
#include <math.h>
#include <assert.h>
#include <limits.h>
typedef uint8_t Uint8;
typedef int16_t Sint16;
typedef int32_t Sint32;
#define MIN(a,b) ((a)<(b)?(a):(b))
#define MAX(a,b) ((a)>(b)?(a):(b))
#define FIXED_TO_INT(x) ((Sint32)((x)>>12))
#define CHANNEL_COUNT 8
#define CHANNEL_VOLUME_LEVELS 8
static int audioDevice;
static bool audio_disabled,music_disabled,samples_disabled,music_stopped;
static Uint8 musicVolume,sampleVolume;
static Sint32 volumeFactorTable[256];
static const int ldsUpdate2Rate=139;
static int samplesPerLdsUpdate,samplesPerLdsUpdateFrac,samplesUntilLdsUpdate,samplesUntilLdsUpdateFrac;
static const Sint16 *channelSamples[8];
static size_t channelSampleCount[8];
static Uint8 channelVolume[8];
static uint32_t generated,updates;
static void SDL_LockAudioDevice(int device) {(void)device;}
static void SDL_UnlockAudioDevice(int device) {(void)device;}
static void lds_update(void) {updates++;}
static void opl_update(Sint16 *samples,int count) {
    for(int i=0;i<count;i++)samples[i]=(Sint16)((generated++*7919u+updates*97u)&65535u);
}
""" + callback + operations + r"""
int main(void) {
    volumeFactorTable[0]=0;
    for(int i=1;i<256;i++)volumeFactorTable[i]=(Sint32)(powf(10,(255-i)*(-30.0f/(20.0f*255)))*4096);
    for(int i=0;i<256;i++)printf("factor:%d,%d\n",i,volumeFactorTable[i]);
    Sint16 channels[8][257],out[1024];
    for(int mode=0;mode<256;mode++) {
        memset(channelSampleCount,0,sizeof channelSampleCount);memset(channelVolume,0,sizeof channelVolume);
        generated=updates=0;samplesUntilLdsUpdate=samplesUntilLdsUpdateFrac=0;
        const int rates[]={44100,48000,11025,139,22050};
        int rate=rates[mode%5];samplesPerLdsUpdate=2*(rate/139);samplesPerLdsUpdateFrac=2*(rate%139);
        music_disabled=(mode&1)!=0;music_stopped=(mode&2)!=0;samples_disabled=false;audio_disabled=false;
        set_volume(mode,(mode*73)%256);
        for(int c=0;c<8;c++) {
            for(int i=0;i<257;i++)channels[c][i]=(Sint16)((i*1777+c*8191+mode*31)&65535);
            multiSamplePlay(channels[c],(mode+c*37)%258,c,(mode+c)%8);
        }
        samples_disabled=(mode&4)!=0;
        printf("mode:%d,%d\n",mode,rate);
        const int sizes[]={0,1,127,3,1024,13};
        for(int frame=0;frame<6;frame++) {
            if(frame==2) {
                audio_disabled=(mode&8)!=0;
                set_volume((mode+11)%256,(mode+23)%256);
                multiSamplePlay(channels[3],17,3,7);
                audio_disabled=false;
            }
            audioCallback(NULL,(Uint8*)out,sizes[frame]*2);
            printf("frame:%d,%u,%u,%d,%d",frame,generated,updates,samplesUntilLdsUpdate,samplesUntilLdsUpdateFrac);
            for(int c=0;c<8;c++)printf(",%lu",(unsigned long)channelSampleCount[c]);
            putchar('\n');printf("pcm:");
            for(int i=0;i<sizes[frame];i++)printf("%s%d",i?",":"",out[i]);putchar('\n');
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
