"""Trace every frame of the unchanged native episode map-transition routine."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void play_map_transition(", "void play_map_transition(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t Uint8;
typedef struct { unsigned int pitch, h; Uint8 *pixels; } SDL_Surface;
static Uint8 primary_pixels[327 * 200], background_pixels[327 * 200];
static SDL_Surface primary = {320, 200, primary_pixels};
static SDL_Surface background = {320, 200, background_pixels};
static SDL_Surface *VGAScreen = &primary, *VGAScreen2 = &background;
static bool constantPlay, newkey, isNetworkGame;
static int tempX, scenario, delayCalls, presentCalls, waitCalls, eventCalls;
static uint32_t hash(const Uint8 *bytes, unsigned int length)
{
    uint32_t value = 2166136261u;
    for (unsigned int i = 0; i < length; i++) value = (value ^ bytes[i]) * 16777619u;
    return value;
}
static void JE_loadPic(SDL_Surface *screen, int number, bool store)
{
    (void)store;
    printf("picture:%d\n", number);
    for (unsigned int i = 0; i < screen->pitch * screen->h; i++)
        screen->pixels[i] = (Uint8)(i * 7 + number * 13);
}
static void service_SDL_events(bool clear) { eventCalls++; printf("events:%d\n", clear); }
static void setDelay(int ticks) { delayCalls++; printf("delay:%d\n", ticks); }
static void JE_showVGA(void)
{
    presentCalls++;
    printf("frame:%u,%u\n", hash(primary_pixels, primary.pitch * primary.h),
        hash(background_pixels, background.pitch * background.h));
}
static void service_wait_delay(void) { waitCalls++; puts("wait"); }
""" + function(source, "play_map_transition") + r"""
static void fill(Uint8 *pixels, unsigned int length, int seed, int multiplier)
{
    for (unsigned int i = 0; i < length; i++) pixels[i] = (Uint8)(i * multiplier + seed * 19);
}
int main(void)
{
    const char transitions[] = {'U','V','H','?'};
    const char *picture_texts[] = {"17tail", "  +3", "abc"};
    Uint8 picture[320 * 200];
    for (int pitch = 320; pitch <= 327; pitch += 7)
    for (int transition = 0; transition < 4; transition++)
    for (int key = 0; key < 2; key++) {
        scenario++;
        primary.pitch = background.pitch = (unsigned int)pitch;
        fill(primary_pixels, pitch * 200, scenario - 1, 17);
        fill(background_pixels, pitch * 200, scenario, 31);
        memset(picture, 0xa5, sizeof picture);
        newkey = key != 0;
        isNetworkGame = scenario % 2 != 0;
        constantPlay = scenario % 13 == 1;
        char section[64];
        snprintf(section, sizeof section, "M%c?%s", transitions[transition],
            picture_texts[(scenario - 1) % 3]);
        delayCalls = presentCalls = waitCalls = eventCalls = 0;
        printf("case:%d,%d,%c,%d,%d,%s\n", scenario - 1, pitch,
            transitions[transition], newkey, isNetworkGame, section);
        play_map_transition(section, picture);
        printf("state:%u,%u,%u\n", hash(primary_pixels, pitch * 200),
            hash(background_pixels, pitch * 200), hash(picture, sizeof picture));
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
