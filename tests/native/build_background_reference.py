"""Compile unchanged background functions without an SDL runtime or assets."""

import argparse
from pathlib import Path
import re


def build_source(root):
    background = (root / "src" / "backgrnd.c").read_text(encoding="utf-8")
    background = re.sub(r"(?m)^#include[^\n]*\n", "", background)
    return r"""
#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <stdbool.h>
#include <assert.h>
#include <string.h>
#include "mtrand.h"
typedef uint8_t Uint8;
typedef uint16_t JE_word;
typedef uint8_t JE_byte;
typedef int8_t JE_shortint;
typedef bool JE_boolean;
typedef struct { int BitsPerPixel; } SDL_PixelFormat;
typedef struct { int h, pitch; void *pixels; SDL_PixelFormat *format; } SDL_Surface;
static SDL_Surface *VGAScreen;
static JE_byte background2, processorType;
static bool smoothies[9], filterFade, filterFadeStart, filtrationAvail, explosionTransparent;
static JE_shortint levelBrightness, levelBrightnessChg, levelFilter, levelFilterNew;
static void SDL_FillRect(SDL_Surface *surface, void *rectangle, int color)
{
    (void)rectangle;
    for (int row = 0; row < surface->h; row++)
        memset((Uint8 *)surface->pixels + row * surface->pitch, color, 320);
}
""" + background + r"""
int main(int argc, char **argv)
{
    if (argc != 3) return 2;
    int mode = atoi(argv[1]), pitch = atoi(argv[2]);
    if (pitch < 320 || pitch > 400) return 2;
    SDL_PixelFormat format = { 8 };
    SDL_Surface destination = { 200, pitch, malloc(200 * pitch), &format };
    SDL_Surface source = { 200, pitch + 3, malloc(200 * (pitch + 3)), &format };
    if (!destination.pixels || !source.pixels) return 3;
    VGAScreen = &destination;
    for (int i = 0; i < 200 * pitch; i++)
        ((Uint8 *)destination.pixels)[i] = (i * 73 + 19) & 255;
    for (int i = 0; i < 200 * source.pitch; i++)
        ((Uint8 *)source.pixels)[i] = (i * 37 + 11) & 255;
    switch (mode) {
    case 0: JE_darkenBackground(65535); break;
    case 1: lava_filter(&destination, &source); break;
    case 2: smoothie_data[1] = 9; water_filter(&destination, &source); break;
    case 3: iced_blur_filter(&destination, &source); break;
    case 4: blur_filter(&destination, &source); break;
    case 5: filtrationAvail = true; explosionTransparent = true; JE_filterScreen(13, -8); break;
    case 6: filtrationAvail = true; explosionTransparent = true; JE_filterScreen(-99, 20); break;
    case 7:
        mt_srand(5489);
        initialize_starfield();
        memset(destination.pixels, 0, 200 * pitch);
        for (int frame = 0; frame < 200; frame++) {
            update_and_draw_starfield(&destination, frame % 7 - 3);
        }
        break;
    case 8:
    case 9: {
        Uint8 tile[672], *map[12];
        for (int i = 0; i < 672; i++) tile[i] = (i * 11) & 255;
        for (int i = 0; i < 12; i++) map[i] = i % 3 == 0 ? NULL : tile;
        if (mode == 8) blit_background_row(&destination, -27, -2, map);
        else blit_background_row_blend(&destination, 19, 190, map);
        break;
    }
    default: return 2;
    }
    for (int i = 0; i < 200 * pitch; i++)
        printf("%02x", ((Uint8 *)destination.pixels)[i]);
    putchar('\n');
    free(destination.pixels);
    free(source.pixels);
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
