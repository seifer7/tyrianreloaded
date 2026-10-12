"""Generate an unchanged native explosion/superpixel state and pixel oracle."""

import argparse
from pathlib import Path


def build_source(root):
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    header = (root / "src" / "varz.h").read_text(encoding="utf-8")
    tyrian = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    start = header.rfind("typedef struct {", 0, header.index("} explosion_type;"))
    models = header[start:header.index("extern JE_integer tempDat")]
    setup = varz[varz.index("void JE_setupExplosion("):varz.index("void JE_wipeShieldArmorBars(")]
    superpixels = varz[varz.index("void JE_doSP("):varz.index("\n}", varz.index("void JE_drawSP(")) + 3]
    update = tyrian[tyrian.index("static void update_level_explosions("):tyrian.index("static void draw_armor_warning(")]
    return r"""
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include "mtrand.h"
#define M_PI 3.14159265358979323846
#define MAX_EXPLOSIONS 200
#define MAX_REPEATING_EXPLOSIONS 20
#define MAX_SUPERPIXELS 101
#define S_EXPLOSION_11 11
#define S_EXPLOSION_9 9
#define S_EXPLOSION_4 4
typedef uint8_t Uint8;
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int16_t JE_integer;
typedef bool JE_boolean;
typedef float JE_real;
typedef unsigned int uint;
typedef struct {int w,h,pitch; void *pixels;} SDL_Surface;
""" + models + r"""
static explosion_type explosions[MAX_EXPLOSIONS];
static rep_explosion_type rep_explosions[MAX_REPEATING_EXPLOSIONS];
static superpixel_type superpixels[MAX_SUPERPIXELS];
static unsigned int last_superpixel;
static JE_byte temp, soundQueue[8];
static JE_word tempX, tempY, backMove2, explodeMove;
static JE_integer explosionFollowAmountX, explosionFollowAmountY;
static bool enemyStillExploding, explosionTransparent;
static SDL_Surface *VGAScreen;
static int *explosionSpriteSheet;
static void blit_sprite2(SDL_Surface *screen, int x, int y, int *sheet, int frame) {
    (void)screen;(void)sheet;
    printf("D:%d:%d:%d:0\n",x,y,frame);
}
static void blit_sprite2_blend(SDL_Surface *screen, int x, int y, int *sheet, int frame) {
    (void)screen;(void)sheet;
    printf("D:%d:%d:%d:1\n",x,y,frame);
}
""" + setup + superpixels + update + r"""
static void dump_state(void) {
    printf("state:%u:%d",last_superpixel,enemyStillExploding);
    for (int i=0;i<8;i++) printf(",%u",soundQueue[i]);
    putchar('\n');
    for (int i=0;i<MAX_EXPLOSIONS;i++) {
        explosion_type *e=&explosions[i];
        printf("E:%d=%u,%d,%d,%d,%d,%d,%d,%u\n",i,e->ttl,e->x,e->y,e->delta_x,e->delta_y,e->fixed_position,e->follow_player,e->sprite);
    }
    for (int i=0;i<MAX_REPEATING_EXPLOSIONS;i++) {
        rep_explosion_type *e=&rep_explosions[i];
        printf("R:%d=%u,%u,%u,%u,%d\n",i,e->delay,e->ttl,e->x,e->y,e->big);
    }
    for (int i=0;i<MAX_SUPERPIXELS;i++) {
        superpixel_type *p=&superpixels[i];
        printf("P:%d=%u,%u,%u,%d,%d,%u\n",i,p->x,p->y,p->z,p->delta_x,p->delta_y,p->color);
    }
    uint32_t hash=2166136261u;
    for (int i=0;i<VGAScreen->pitch*VGAScreen->h;i++)
        hash=(hash^((Uint8*)VGAScreen->pixels)[i])*16777619u;
    printf("pixels:%u\n",hash);
}
int main(int argc,char **argv) {
    if (argc!=3) return 2;
    int mode=atoi(argv[1]),pitch=atoi(argv[2]);
    SDL_Surface surface={320,200,pitch,NULL};
    surface.pixels=malloc(pitch*200);
    if (!surface.pixels) return 2;
    for (int i=0;i<pitch*200;i++) ((Uint8*)surface.pixels)[i]=(i*17+i/7)&255;
    VGAScreen=&surface;
    mt_srand(12345);
    for (int type=0;type<53;type++)
        JE_setupExplosion(type*3-10,type*3-15,type%3-1,type,type%2,type%3==0);
    JE_setupExplosion(10,10,0,98,false,false);
    JE_setupExplosion(1,-16,0,0,false,false);
    JE_setupExplosion(1,190,0,0,false,false);
    JE_setupExplosionLarge(false,12,-5,5);
    JE_setupExplosionLarge(true,3,130,160);
    JE_setupExplosionLarge(false,1,10,-1);
    JE_doSP(0,0,150,8,0xA0);
    JE_doSP(319,199,40,4,0xF8);
    JE_doSP(100,100,10,0,0x40);
    if (mode==1) {
        for(int i=0;i<500;i++) JE_setupExplosionLarge(i%2,15,80,50);
    }
    dump_state();
    for (int frame=0;frame<35;frame++) {
        backMove2=frame%3; explodeMove=frame%2;
        explosionFollowAmountX=frame%2?3:-2; explosionFollowAmountY=-1;
        explosionTransparent=frame%2;
        if (frame==8) JE_doSP(65535,65535,255,255,0xF0);
        update_level_explosions();
        JE_drawSP();
        dump_state();
    }
    printf("rng:%lu\n",mt_rand());
    free(surface.pixels);
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
