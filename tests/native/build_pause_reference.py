"""Trace the complete unchanged pause loop with scripted input/network services."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = function((root / "src" / "mainint.c").read_text(encoding="utf-8"), "JE_pauseGame")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
typedef uint16_t JE_word;
typedef bool JE_boolean;
typedef int SDL_Surface;
#define WITH_NETWORK
#define FONT_SHAPES 1
#define PACKET_GAME_PAUSE 0x31
#define PACKET_WAITING 0x21
#define SDL_SCANCODE_LCTRL 224
#define SDL_SCANCODE_RCTRL 228
#define SDL_SCANCODE_LALT 226
#define SDL_SCANCODE_RALT 230
static SDL_Surface primary,game,*VGAScreen=&game,*VGAScreenSeg=&primary;
static const char *miscText[23];
static bool superPause,isNetworkGame,newkey;
static int lastkey_scan,mode,frame,checks;
static int tyrMusicVolume=191,fxVolume=123;
static uint32_t ticks,target;
static struct Packet {uint8_t data[4];} received,*packet_in[1];
static bool sync;
static uint16_t SDLNet_Read16(const uint8_t *p) {return (p[0]<<8)|p[1];}
static void mouseSetRelative(bool enabled) {printf("relative:%d\n",enabled);}
static void JE_dString(SDL_Surface *screen,int x,int y,const char *text,int font) {
    (void)text;printf("draw:%d,%d,%d,%d\n",screen==&primary,x,y,font);
}
static void JE_showVGA(void) {puts("present");}
static void set_volume(int music,int effects) {printf("volume:%d,%d\n",music,effects);}
static void service_SDL_events(bool clear) {(void)clear;}
static void network_prepare(int type) {printf("prepare:%d\n",type);received.data[0]=0;received.data[1]=(uint8_t)type;}
static bool network_send(int length) {printf("send:%d\n",length);sync=false;return true;}
static bool network_update(void) {printf("update:%d\n",packet_in[0]!=NULL);packet_in[0]=NULL;return true;}
static int network_check(void) {
    puts("check");checks++;sync=true;
    if(checks==1 || ((mode&8)!=0 && frame==3)) {
        received.data[0]=0;received.data[1]=(uint8_t)(checks==1?PACKET_GAME_PAUSE:PACKET_WAITING);packet_in[0]=&received;
    }
    return 1;
}
static bool network_is_sync(void) {return sync;}
static void SDL_Delay(uint32_t delay) {printf("delay:%u\n",delay);ticks+=delay;}
static void wait_noinput(bool keyboard,bool mouse,bool joystick) {(void)keyboard;(void)mouse;(void)joystick;}
static void setDelay(int delay) {target=(uint32_t)((float)ticks+delay*(0x4300*((12.0f/14318180.0f)*1000.0f)));}
static void push_joysticks_as_keyboard(void) {
    frame++;printf("push:%d\n",frame);newkey=true;
    int keys[]={224,228,226,230,40};lastkey_scan=keys[(frame-1)%5];
    if(mode&8)newkey=false;
}
static JE_word JE_mousePosition(JE_word *x,JE_word *y) {
    *x=*y=0;return (mode&4) && frame==3 ? 1 : 0;
}
static void wait_delay(void) {if(target>ticks)SDL_Delay(target-ticks);}
""" + source + r"""
int main(int argc,char **argv) {
    if(argc!=2)return 2;mode=atoi(argv[1]);
    superPause=mode&1;isNetworkGame=mode&2;sync=true;
    JE_pauseGame();printf("restored:%d\n",VGAScreen==&game);return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
