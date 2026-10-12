"""Compile unchanged joystick controls, bindings, and configuration with scripted SDL devices."""

import argparse
from pathlib import Path
import re


def function(source, name):
    for match in re.finditer(r"^(?:int|bool|void|const char\s*\*)\s*" + re.escape(name) + r"\(", source, re.MULTILINE):
        brace = source.find("{", match.end())
        if ";" in source[match.end():brace]:
            continue
        end = brace + 1
        depth = 1
        while depth:
            depth += (source[end] == "{") - (source[end] == "}")
            end += 1
        return source[match.start():end] + "\n"
    raise ValueError(name)


def build_source(root):
    source = (root / "src" / "joystick.c").read_text(encoding="utf-8")
    header = (root / "src" / "joystick.h").read_text(encoding="utf-8")
    models = header[header.index("typedef enum"):header.index("extern int joystick_repeat_delay")]
    names = source[source.index("static const char* const assignment_names"):source.index("bool load_joystick_assignments(")]
    selected = [
        "joystick_axis_threshold", "joystick_axis_reduce", "joystick_analog_angle", "check_assigned",
        "poll_joystick", "poll_joysticks", "push_key", "push_joysticks_as_keyboard",
        "reset_joystick_assignments", "load_joystick_assignments", "save_joystick_assignments",
        "joystick_assignments_to_string", "code_to_assignment", "assignment_to_code", "joystick_assignment_cmp",
        "detect_joystick_assignment"
    ]
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <ctype.h>
#include <math.h>
#include "config_file.h"
#ifndef M_PI
#define M_PI 3.14159265358979323846
#define M_PI_2 1.57079632679489661923
#endif
typedef unsigned int uint;
typedef uint32_t Uint32;
typedef uint8_t Uint8;
typedef int16_t Sint16;
typedef int SDL_Scancode;
typedef struct {int axes,buttons,hats;short axis[4];uint8_t button[8],hat[2];const char *name;} SDL_Joystick;
typedef struct {int type;struct {int state;struct {int scancode,mod,sym;} keysym;} key;} SDL_Event;
enum {SDL_KEYDOWN=1,SDL_KEYUP=2,SDL_RELEASED=0,SDL_SCANCODE_RETURN=40,SDL_SCANCODE_ESCAPE=41,
      SDL_SCANCODE_UP=82,SDL_SCANCODE_RIGHT=79,SDL_SCANCODE_DOWN=81,SDL_SCANCODE_LEFT=80,
      SDL_HAT_CENTERED=0,SDL_HAT_UP=1,SDL_HAT_RIGHT=2,SDL_HAT_DOWN=4,SDL_HAT_LEFT=8};
static uint32_t ticks;
static int updates;
static int detect_mode=-1,phase,services,presents,waits;
static bool newkey,newmouse;
static SDL_Joystick *detect_device;
static void advance_detect(void) {
    phase++;
    if(detect_mode==0)detect_device->axis[0]=32767;
    if(detect_mode==1)detect_device->axis[0]=-32768;
    if(detect_mode==2)detect_device->button[0]=phase==1?0:1;
    if(detect_mode==3) {
        detect_device->hat[0]=phase==1?0:12;detect_device->hat[1]=phase==1?0:2;
    }
    if(detect_mode==4) {
        detect_device->axis[0]=32767;detect_device->button[0]=1;detect_device->hat[0]=8;
    }
    if(detect_mode==7)detect_device->axis[0]=phase==1?21844:21845;
}
static uint32_t SDL_GetTicks(void) {return ticks;}
static void SDL_JoystickUpdate(void) {updates++;if(detect_mode>=0)advance_detect();}
static void setDelay(int delay) {(void)delay;}
static void wait_delay(void) {waits++;}
static void service_SDL_events(bool clear) {
    (void)clear;services++;newkey=detect_mode==5 && phase>=3;newmouse=detect_mode==6 && phase>=2;
}
static void JE_showVGA(void) {presents++;}
static short SDL_JoystickGetAxis(SDL_Joystick *j,int i) {return i>=0 && i<j->axes?j->axis[i]:0;}
static uint8_t SDL_JoystickGetButton(SDL_Joystick *j,int i) {return i>=0 && i<j->buttons?j->button[i]:0;}
static uint8_t SDL_JoystickGetHat(SDL_Joystick *j,int i) {return i>=0 && i<j->hats?j->hat[i]:0;}
static int SDL_JoystickNumAxes(SDL_Joystick *j) {return j->axes;}
static int SDL_JoystickNumButtons(SDL_Joystick *j) {return j->buttons;}
static int SDL_JoystickNumHats(SDL_Joystick *j) {return j->hats;}
static const char *SDL_JoystickName(SDL_Joystick *j) {return j->name;}
static int SDL_PushEvent(SDL_Event *event) {printf("key:%d,%d\n",event->type,event->key.keysym.scancode);return 1;}
""" + models + r"""
static Joystick states[3],*joystick=states;
static int joysticks=3,joystick_repeat_delay=300;
static const int joystick_analog_max=32767;
static bool joydown;
static const char *assignment_to_code(const Joystick_assignment *);
static void code_to_assignment(Joystick_assignment *,const char *);
""" + names + "".join(function(source, name) for name in selected) + r"""
static unsigned int bits(const bool *values,int count) {
    unsigned int result=0;for(int i=0;i<count;i++)if(values[i])result|=1u<<i;return result;
}
int main(void) {
    const char *codes[]={"","AX 1-","AX 2+","BTN 6","H 1X-","H 2y+"," AX -2?","AX 0+","H 0X+",
        "AX 1","H 1X","BTN -1","AX 12345+","BTN 12345","H 123X-","ax 1+"," AX2-","H1Yx","junk","BTN +7tail","H -3q-","AX +3 -"};
    Joystick_assignment parsed[22];
    for(int i=0;i<22;i++) {
        code_to_assignment(&parsed[i],codes[i]);
        printf("code:%d,%d,%d,%d,%d,%s\n",i,parsed[i].type,parsed[i].num,parsed[i].x_axis,
            parsed[i].negative_axis,assignment_to_code(&parsed[i]));
    }
    for(int i=0;i<22;i++)for(int j=0;j<22;j++)printf("cmp:%d,%d,%d\n",i,j,joystick_assignment_cmp(&parsed[i],&parsed[j]));
    for(int mode=0;mode<32;mode++) {
        SDL_Joystick devices[3];memset(devices,0,sizeof(devices));memset(states,0,sizeof(states));
        devices[0].axes=2;devices[0].buttons=6;devices[0].hats=1;devices[0].name="Synthetic pad";
        devices[1].axes=mode%4;devices[1].buttons=mode%8;devices[1].hats=mode%2;devices[1].name="Other pad";
        states[0].handle=&devices[0];states[1].handle=&devices[1];states[2].handle=NULL;
        updates=0;joydown=false;joystick_repeat_delay=mode*17;
        reset_joystick_assignments(0);reset_joystick_assignments(1);
        for(int j=0;j<2;j++)for(int a=0;a<10;a++) {
            char buffer[64];joystick_assignments_to_string(buffer,sizeof(buffer),states[j].assignment[a]);
            printf("default:%d,%d,%d,%s\n",mode,j,a,buffer);
        }
        states[0].threshold=mode%34;states[0].sensitivity=mode%15;
        for(int frame=0;frame<48;frame++) {
            ticks=mode%7==0?0xfffffff0u+frame*17u:frame*137u;
            for(int j=0;j<2;j++) {
                for(int i=0;i<4;i++)devices[j].axis[i]=(int16_t)(frame*7919+mode*1009+i*2345+j*987);
                for(int i=0;i<8;i++)devices[j].button[i]=(frame+mode+i+j)%3;
                for(int i=0;i<2;i++)devices[j].hat[i]=(frame*7+mode+i+j)&15;
            }
            if(frame%3==0)push_joysticks_as_keyboard();else poll_joysticks();
            for(int j=0;j<3;j++)printf("state:%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%u,%u,%d,%u,%u,%d,%d\n",
                mode,frame,j,joydown,states[j].x,states[j].y,states[j].confirm,states[j].cancel,
                bits(states[j].direction,4),bits(states[j].direction_pressed,4),bits(states[j].action,6),
                bits(states[j].action_pressed,6),states[j].joystick_delay,states[j].input_pressed,
                states[j].analog_direction[0],states[j].analog_direction[1],states[j].analog_direction[2],states[j].analog_direction[3]);
            float angle=0.37f;bool has=joystick_analog_angle(0,&angle);
            printf("angle:%d,%d,%d,%.9g\n",mode,frame,has,angle);
            printf("reduce:%d,%d,%d,%d,%d\n",mode,frame,joystick_axis_threshold(0,states[0].x),
                joystick_axis_reduce(0,states[0].x),updates);
        }
        Config config;config_init(&config);
        printf("config:%d,%d\n",mode,load_joystick_assignments(&config,0));
        ConfigSection *section=config_add_section(&config,"joystick","Synthetic pad");
        config_set_bool_option(section,"analog",mode%2!=0,NO_YES);
        if(mode%2)config_set_int_option(section,"sensitivity",mode%15);
        config_set_int_option(section,"threshold",mode%34);
        ConfigOption *up=config_set_option(section,"up",NULL);
        config_add_value(up,"AX 2-");config_add_value(up,"H 1Y-");config_add_value(up,"BTN 7");
        config_set_option(section,"fire","BTN 3");
        load_joystick_assignments(&config,0);
        save_joystick_assignments(&config,0);
        config_write(&config,stdout);config_deinit(&config);
    }
    for(int mode=0;mode<8;mode++) {
        SDL_Joystick device;memset(&device,0,sizeof(device));device.axes=2;device.buttons=2;device.hats=2;
        if(mode==2)device.button[0]=1;if(mode==3)device.hat[0]=1;
        states[0].handle=&device;detect_device=&device;detect_mode=mode;
        phase=services=presents=waits=0;newkey=newmouse=false;
        Joystick_assignment assignment={NONE,44,true,false};
        bool detected=detect_joystick_assignment(0,&assignment);
        printf("detect:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",mode,detected,assignment.type,assignment.num,
            assignment.x_axis,assignment.negative_axis,phase,services,presents,waits);
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
