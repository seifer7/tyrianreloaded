"""Trace the unchanged complete jukebox loop at its presentation/audio/input boundaries."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "jukebox.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include ".*"$', '', source, flags=re.MULTILINE)
    music = (root / "src" / "musmast.c").read_text(encoding="utf-8")
    sound = (root / "src" / "sndmast.c").read_text(encoding="utf-8")
    titles = music[music.index("const char musicTitle"):music.index("JE_boolean musicFade")]
    titles += sound[sound.index("const char soundTitle"):sound.index("const JE_byte windowTextSamples")]
    titles = titles.replace("musicTitle", "originalMusicTitle").replace("soundTitle", "originalSoundTitle")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mtrand.h"
typedef uint8_t Uint8;
typedef uint16_t Uint16;
typedef struct {uint8_t r,g,b;} SDL_Color;
enum {small_font=2,centered=1,MUSIC_NUM=41,SOUND_COUNT=38,
    SDL_SCANCODE_ESCAPE=41,SDL_SCANCODE_Q=20,SDL_SCANCODE_SPACE=44,SDL_SCANCODE_F=9,
    SDL_SCANCODE_N=17,SDL_SCANCODE_SLASH=56,SDL_SCANCODE_COMMA=54,SDL_SCANCODE_PERIOD=55,
    SDL_SCANCODE_SEMICOLON=51,SDL_SCANCODE_LEFT=80,SDL_SCANCODE_UP=82,SDL_SCANCODE_RETURN=40,
    SDL_SCANCODE_KP_ENTER=88,SDL_SCANCODE_RIGHT=79,SDL_SCANCODE_DOWN=81,SDL_SCANCODE_S=22,SDL_SCANCODE_R=21};
static struct {int w;} screen={320},*VGAScreen=&screen,*VGAScreenSeg=&screen;
static int vga_palette,tyrMusicVolume,fxVolume,lastkey_scan,song_playing,frame,scene;
static bool audio_disabled,songlooped,playing,newkey;
static char musicTitle[41][48],soundTitle[38][9];
static void init_step_fade_palette(void *diff,int palette,int first,int last) {(void)diff;(void)palette;printf("init:%d,%d\n",first,last);}
static void JE_starlib_init(void) {puts("stars-init");mt_rand();}
static void JE_starlib_main(void) {puts("stars");mt_rand();}
static void set_volume(int music,int effects) {printf("volume:%d,%d\n",music,effects);}
static void play_song(uint32_t song) {printf("play:%u\n",song);if(!audio_disabled){song_playing=(int)song;playing=true;songlooped=false;}}
static void stop_song(void) {puts("stop");}
static void restart_song(void) {puts("restart");}
static void JE_playSampleNum(int sample) {printf("sample:%d\n",sample);}
static void setDelay(int value) {printf("delay:%d\n",value);}
static void SDL_FillRect(void *screen,void *rect,int color) {(void)screen;(void)rect;printf("clear:%d\n",color);}
static void push_joysticks_as_keyboard(void) {
    puts("push");
    if((scene&3)==2&&frame%4==1)songlooped=true;
    if((scene&3)==3&&frame%5==2)playing=false;
}
static int script(void) {
    const int keys[]={56,54,51,55,51,44,44,80,82,79,81,40,88,22,21,9,17,17,9,0,0,41};
    if(scene&4)return frame==0?20:0;
    if(scene&8)return frame==0?41:0;
    return frame<(int)(sizeof(keys)/sizeof(keys[0]))?keys[frame]:0;
}
static void service_SDL_events(bool clear) {printf("events:%d\n",clear);if(clear){lastkey_scan=script();newkey=lastkey_scan!=0;}}
static void draw_font_hv(void *screen,int x,int y,const char *text,int font,int alignment,int hue,int value) {
    (void)screen;printf("text:%d,%d,%d,%d,%d,%d:%s\n",x,y,font,alignment,hue,value,text);
}
static void step_fade_palette(void *diff,int steps,int first,int last) {(void)diff;printf("step:%d,%d,%d\n",steps,first,last);}
static void init_step_fade_solid(void *diff,SDL_Color black,int first,int last) {(void)diff;printf("black:%u,%u,%u,%d,%d\n",black.r,black.g,black.b,first,last);}
static void JE_showVGA(void) {puts("show");}
static void wait_delay(void) {puts("wait");}
static int JE_mousePosition(Uint16 *x,Uint16 *y) {
    *x=*y=0;service_SDL_events(false);
    int button=(scene&16)&&frame==0;printf("mouse:%d\n",button);frame++;if(frame>200)exit(5);return button;
}
""" + titles + source + r"""
int main(int argc,char **argv) {
    if(argc!=2)return 2;
    if(!strcmp(argv[1],"titles")) {
        for(int i=0;i<41;i++)printf("music:%s\n",originalMusicTitle[i]);
        for(int i=0;i<38;i++)printf("sound:%s\n",originalSoundTitle[i]);return 0;
    }
    scene=atoi(argv[1]);mt_srand(44123);tyrMusicVolume=scene%4==0?5:scene%4==1?9:scene%4==2?191:255;
    fxVolume=123;song_playing=scene%2?40:0;playing=scene%3!=0;songlooped=scene%3==1;audio_disabled=scene%7==0;
    for(int i=0;i<41;i++)snprintf(musicTitle[i],48,"Song %d",i);
    for(int i=0;i<38;i++)snprintf(soundTitle[i],9,"FX %d",i);
    jukebox();printf("rng:%u\n",mt_rand());return 0;
}
"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
