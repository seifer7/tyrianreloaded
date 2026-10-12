"""Compile unchanged keyboard and viewport functions with recorded SDL event services."""

import argparse
from pathlib import Path
import re


def build_source(root):
    keyboard = (root / "src" / "keyboard.c").read_text(encoding="utf-8")
    keyboard = re.sub(r'^#include[^\n]*\n', '', keyboard, flags=re.MULTILINE)
    video = (root / "src" / "video.c").read_text(encoding="utf-8")
    names = video[video.index("bool set_scaling_mode_by_name("):video.index("void JE_clr256(")]
    start = video.index("static void calc_dst_render_rect(SDL_Surface *const src_surface")
    rectangle = video[start:video.index("static void scale_and_flip(SDL_Surface *src_surface)", start)]
    maps = video[video.index("/** Maps a specified point"):]
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#define WITH_NETWORK
#define SDL_VERSION_ATLEAST(a,b,c) 1
#define SDL_HINT_MOUSE_RELATIVE_SYSTEM_SCALE "scale"
#define SDL_NUM_SCANCODES 512
#define SDL_TEXTINPUTEVENT_TEXT_SIZE 32
#define SDL_TRUE 1
#define SDL_FALSE 0
#define SDL_POLL_INTERVAL 10
#define vga_width 320
#define vga_height 200
#define COUNTOF(x) (sizeof(x)/sizeof((x)[0]))
#define KMOD_ALT 0x300
#define SDL_SCANCODE_RETURN 40
typedef bool JE_boolean;
typedef uint16_t JE_word;
typedef uint8_t Uint8;
typedef int32_t Sint32;
typedef int SDL_Scancode;
typedef uint16_t SDL_Keymod;
enum {SDL_WINDOWEVENT=1,SDL_KEYDOWN,SDL_KEYUP,SDL_MOUSEMOTION,SDL_MOUSEBUTTONDOWN,
      SDL_MOUSEBUTTONUP,SDL_TEXTINPUT,SDL_TEXTEDITING,SDL_QUIT};
enum {SDL_WINDOWEVENT_FOCUS_LOST=1,SDL_WINDOWEVENT_FOCUS_GAINED,SDL_WINDOWEVENT_RESIZED};
enum {SDL_BUTTON_LEFT=1,SDL_BUTTON_MIDDLE=2,SDL_BUTTON_RIGHT=3};
typedef struct {
    int type;
    struct {int event;} window;
    struct {struct {int scancode;uint16_t mod;} keysym;} key;
    struct {int32_t x,y,xrel,yrel;} motion;
    struct {uint8_t button;int32_t x,y;} button;
    struct {char text[32];} text;
} SDL_Event;
typedef struct {int x,y,w,h;} SDL_Rect;
typedef struct {int w,h;} SDL_Surface;
typedef enum {SCALE_CENTER,SCALE_INTEGER,SCALE_ASPECT_8_5,SCALE_ASPECT_4_3,ScalingMode_MAX} ScalingMode;
static const char *scaling_mode_names[]={"Center","Integer","Fit 8:5","Fit 4:3"};
static ScalingMode scaling_mode=SCALE_INTEGER;
static SDL_Rect last_output_rect={0,0,320,200};
static SDL_Surface surface={320,200},*VGAScreen=&surface;
static int win_w,win_h,tex_w,tex_h;
static void *main_window,*main_window_texture;
static bool mouseInactive,joydown,isNetworkGame;
static SDL_Event events[128];
static int event_count,event_index;
static void service_SDL_events(bool clear_new);
static void SDL_GetWindowSize(void *window,int *w,int *h) {(void)window;*w=win_w;*h=win_h;}
static void SDL_QueryTexture(void *texture,void *format,void *access,int *w,int *h)
    {(void)texture;(void)format;(void)access;*w=tex_w;*h=tex_h;}
static void SDL_ShowCursor(bool visible) {printf("cursor:%d\n",visible);}
static void SDL_SetHint(const char *name,const char *value) {(void)name;(void)value;puts("systemscale");}
static void SDL_SetRelativeMouseMode(bool enabled) {printf("relative:%d\n",enabled);}
static void video_on_win_resize(void) {puts("resize");}
static void toggle_fullscreen(void) {puts("fullscreen");}
static void network_check(void) {puts("network");}
static void SDL_Delay(unsigned int delay) {printf("delay:%u\n",delay);}
static void append(int type,int scan,int mod,int button,int x,int y,int rx,int ry,const char *text) {
    SDL_Event *event=&events[event_count++];memset(event,0,sizeof(*event));event->type=type;
    event->key.keysym.scancode=scan;event->key.keysym.mod=mod;event->button.button=button;
    event->button.x=event->motion.x=x;event->button.y=event->motion.y=y;
    event->motion.xrel=rx;event->motion.yrel=ry;
    if(text)memcpy(event->text.text,text,strlen(text)<31?strlen(text):31);
}
static int SDL_PollEvent(SDL_Event *event) {
    if(event_index==event_count)return 0;*event=events[event_index++];return 1;
}
static size_t SDL_strlcpy(char *out,const char *in,size_t size) {
    size_t length=strlen(in),copy=length<size?length:size-1;memcpy(out,in,copy);out[copy]=0;return length;
}
static void push_joysticks_as_keyboard(void) {puts("pushjoy");append(SDL_KEYDOWN,9,0,0,0,0,0,0,NULL);}
static void poll_joysticks(void) {puts("polljoy");joydown=false;append(SDL_KEYUP,9,0,0,0,0,0,0,NULL);}
""" + names + rectangle + maps + keyboard + r"""
static uint32_t hash(const void *data,int count) {
    uint32_t result=2166136261u;const uint8_t *bytes=data;
    for(int i=0;i<count;i++)result=(result^bytes[i])*16777619u;return result;
}
static void state(int mode,int step) {
    uint8_t buttons[3]={mouse_pressed[0],mouse_pressed[1],mouse_pressed[2]};
    printf("state:%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%d,%d,%d,%d,%d,%d,%u,%u,%u,%d\n",
        mode,step,newkey,newmouse,new_text,keydown,mousedown,lastkey_scan,lastkey_mod,
        lastmouse_but,lastmouse_x,lastmouse_y,mouse_x,mouse_y,windowHasFocus,mouseInactive,
        hash(keysactive,512),hash(buttons,3),hash(last_text,32),event_count-event_index);
}
int main(void) {
    for(int i=0;i<128;i++) {
        win_w=210+i*37;win_h=130+i*17;tex_w=320*(i%4+1);tex_h=200*(i%4+1);
        for(int mode=0;mode<4;mode++) {
            scaling_mode=mode;SDL_Rect rect;calc_dst_render_rect(VGAScreen,&rect);last_output_rect=rect;
            int x=i*13-500,y=i*7-300;mapScreenPointToWindow(&x,&y);
            int u=i*17-600,v=i*11-500;mapWindowPointToScreen(&u,&v);
            int dx=i*5-300,dy=i*3-200;scaleWindowDistanceToScreen(&dx,&dy);
            printf("geometry:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",
                   i,mode,rect.x,rect.y,rect.w,rect.h,x,y,u,v,dx,dy);
        }
        scaling_mode=i%4;calc_dst_render_rect(VGAScreen,&last_output_rect);
        memset(keysactive,0,sizeof(keysactive));memset(mouse_pressed,0,sizeof(mouse_pressed));memset(last_text,0,sizeof(last_text));
        new_text=false;windowHasFocus=false;mouseInactive=true;lastkey_scan=lastkey_mod=lastmouse_but=0;
        lastmouse_x=lastmouse_y=mouse_x=mouse_y=0;mouseRelativeEnabled=false;mouseWindowXRelative=mouseWindowYRelative=0;
        event_index=event_count=0;joydown=false;isNetworkGame=i%2!=0;
        init_keyboard();mouseSetRelative(i%3!=0);state(i,0);
        SDL_Event focus={0};focus.type=SDL_WINDOWEVENT;focus.window.event=SDL_WINDOWEVENT_FOCUS_GAINED;events[event_count++]=focus;
        append(SDL_MOUSEMOTION,0,0,0,i*37-20,i*17-10,i-63,31-i,NULL);
        append(SDL_MOUSEBUTTONDOWN,0,0,i%5+1,i*11-5,i*7-3,0,0,NULL);
        append(SDL_KEYDOWN,5+i,0x42,0,0,0,0,0,NULL);
        append(SDL_TEXTINPUT,0,0,0,0,0,0,0,"0123456789abcdefghijklmnopqrstu");
        append(SDL_KEYDOWN,40,0x300,0,0,0,0,0,NULL);
        append(SDL_KEYUP,5+i,0,0,0,0,0,0,NULL);
        focus.window.event=SDL_WINDOWEVENT_FOCUS_LOST;events[event_count++]=focus;
        append(SDL_MOUSEMOTION,0,0,0,-300,-300,500,500,NULL);
        focus.window.event=SDL_WINDOWEVENT_FOCUS_GAINED;events[event_count++]=focus;
        focus.window.event=SDL_WINDOWEVENT_RESIZED;events[event_count++]=focus;
        append(SDL_TEXTINPUT,0,0,0,0,0,0,0,"X");
        append(SDL_TEXTEDITING,0,0,0,0,0,0,0,NULL);
        append(SDL_MOUSEBUTTONUP,0,0,i%5+1,200,100,0,0,NULL);
        append(SDL_MOUSEMOTION,0,0,0,100,50,i*2-128,64-i,NULL);
        append(999,0,0,0,0,0,0,0,NULL);
        service_SDL_events(false);state(i,1);
        service_SDL_events(true);state(i,2);
        service_SDL_events(false);state(i,3);
        JE_word x,y,button=JE_mousePosition(&x,&y);printf("mouse:%u,%u,%u\n",x,y,button);state(i,4);
        for(int j=0;j<2;j++) {int dx,dy;mouseGetRelativePosition(&dx,&dy);printf("distance:%d,%d\n",dx,dy);state(i,5+j);}
        mouseSetRelative(false);state(i,7);
        append(SDL_KEYDOWN,34,0,0,0,0,0,0,NULL);append(SDL_KEYDOWN,35,0,0,0,0,0,0,NULL);
        append(SDL_KEYUP,34,0,0,0,0,0,0,NULL);append(SDL_MOUSEMOTION,0,0,0,-1,-1,1,1,NULL);
        append(SDL_KEYUP,35,0,0,0,0,0,0,NULL);
        wait_input(true,false,false);state(i,8);
        wait_noinput(true,false,false);state(i,9);
        wait_noinput(true,false,false);state(i,10);
        flush_events_buffer();wait_input(true,false,false);state(i,11);
        wait_noinput(true,false,false);state(i,12);
        append(SDL_KEYDOWN,99,0,0,0,0,0,0,NULL);flush_events_buffer();JE_clearKeyboard();state(i,13);
    }
    scaling_mode=SCALE_INTEGER;
    printf("names:%d,%d,%d,%d,%d\n",set_scaling_mode_by_name("Center"),set_scaling_mode_by_name("Integer"),
           set_scaling_mode_by_name("Fit 8:5"),set_scaling_mode_by_name("Fit 4:3"),set_scaling_mode_by_name("integer"));
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
