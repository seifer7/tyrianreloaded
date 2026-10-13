"""Trace the unchanged native episode-picture display routine."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void show_episode_picture(", "void show_episode_picture(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t Uint8;
typedef struct { Uint8 pixels[320 * 200]; } SDL_Surface;
static SDL_Surface primary;
static SDL_Surface *VGAScreen = &primary;
static bool constantPlay;
static int tempX;
static Uint8 pcxpal[13];
static int palettes[23][256];
static int colors[256];
static void JE_loadPCX(const char *path) { printf("pcx:%s\n", path); }
static void JE_loadPic(SDL_Surface *screen, int number, bool store) {
    (void)screen; printf("picture:%d:%d\n", number, store);
}
static void JE_clr256(SDL_Surface *screen) {
    printf("palette:%d\n", colors[0]);
    memset(screen->pixels, 0, sizeof(screen->pixels)); puts("clear");
}
static void JE_showVGA(void) { puts("present"); }
static void fade_palette(int *palette, int steps, int first, int last) {
    (void)palette; printf("fade:%d:%d:%d\n", steps, first, last);
}
""" + function(source, "show_episode_picture") + r"""
int main(void) {
    const char *values[] = {
        "0", "1", "3", "899", "900", "901", "912", "913", "-1", "-901",
        " 0", "\t+2", "17tail", "abc", "-0", "  -10x", "909",
        "-2147483648", "   ", "+", "00009", "  905x"
    };
    for (int i = 0; i < 256; i++) {
        for (int p = 0; p < 13; p++) pcxpal[p] = (Uint8)p;
        for (int p = 0; p < 23; p++) palettes[p][0] = p;
        colors[0] = -1;
        char section[128];
        snprintf(section, sizeof(section), "P?x%s", values[i % (int)(sizeof(values) / sizeof(values[0]))]);
        constantPlay = (i & 1) != 0;
        tempX = 777;
        printf("case:%d,%d,%s\n", i, constantPlay, values[i % (int)(sizeof(values) / sizeof(values[0]))]);
        show_episode_picture(section);
        printf("state:%d,%d\n", tempX, colors[0]);
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
