"""Run unchanged processor-detail and speed controls with the native PIT timer."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    config = (root / "src" / "config.c").read_text(encoding="utf-8")
    timing = (root / "src" / "nortsong.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <assert.h>
typedef uint16_t Uint16;
typedef uint32_t Uint32;
typedef uint8_t JE_byte;
typedef unsigned int uint;
static bool wild,superWild,smoothScroll,explosionTransparent,filtrationAvail,background2,displayScore,pentiumMode;
static JE_byte processorType,gameSpeed,fastPlay;
static uint frameCountMax;
static const float pitPeriod=(12.0f/14318180.0f)*1000.0f;
static Uint16 delaySpeed=0x4300;
static float delayPeriod=0x4300*((12.0f/14318180.0f)*1000.0f);
static Uint32 target,ticks;
static Uint32 SDL_GetTicks(void){return ticks;}
""" + function(timing, "setDelaySpeed") + function(timing, "setDelay") + \
        config[config.index("void JE_initProcessorType("):config.index("void JE_encryptSaveTemp(")] + r"""
int main(void) {
    for(int cpu=0;cpu<9;cpu++)for(int speed=0;speed<8;speed++)for(int prior=0;prior<6;prior++) {
        processorType=(JE_byte)cpu;gameSpeed=(JE_byte)speed;fastPlay=(JE_byte)prior;
        wild=superWild=smoothScroll=explosionTransparent=filtrationAvail=background2=displayScore=true;
        JE_initProcessorType();
        printf("settings:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",cpu,speed,prior,
            wild,superWild,smoothScroll,explosionTransparent,filtrationAvail,background2,displayScore,fastPlay);
        ticks=12345;JE_setNewGameSpeed();
        printf("timing:%d,%d,%u,%u\n",smoothScroll,pentiumMode,frameCountMax,target);
    }
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
