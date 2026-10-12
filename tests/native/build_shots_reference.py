"""Generate a native projectile state/effect oracle using unchanged C code."""

import argparse
from pathlib import Path
import build_items_reference


def build_source(root):
    items = build_items_reference.build_source(root).split("int main(int argc, char **argv)")[0]
    header = (root / "src" / "shots.h").read_text(encoding="utf-8")
    structure = header[header.index("typedef struct {"):header.index("#define MAX_PWEAPON")]
    structure = structure.replace("Uint8", "uint8_t")
    source = (root / "src" / "shots.c").read_text(encoding="utf-8")
    shots = source[source.index("// I'm pretty sure"):]
    return items + r"""
#include <math.h>
#ifndef M_PI
#define M_PI 3.14159265358979323846
#define M_PI_2 (M_PI / 2)
#endif
#define MAX_PWEAPON 81
#define OPTION_SHAPES 5
typedef float JE_real;
static uint power;
static JE_word x, y;
static JE_byte shotMultiPos[11], shotRepeat[11], soundQueue[8], enemyAvail[100];
static bool background2 = true;
static const uint shadowYDist = 10;
static struct {int delta_x_shot_move, delta_y_shot_move;} player[2];
static struct {JE_integer ex, ey; bool scoreitem;} enemy[100];
static int *VGAScreen, *spriteSheet8, *spriteSheet12;
static struct {JE_word width, height;} special_sprite = {13,17};
static void blit_sprite2(int *screen, int x, int y, int *sheet, int frame) {
    (void)screen;
    printf("D:%d:%d:%d:%d:0\n", sheet == spriteSheet8 ? 8 : 12, x, y, frame);
}
static void blit_sprite2_darken(int *screen, int x, int y, int *sheet, int frame) {
    (void)screen;
    printf("D:%d:%d:%d:%d:1\n", sheet == spriteSheet8 ? 8 : 12, x, y, frame);
}
static void blit_sprite_blend(int *screen, int x, int y, int table, int frame) {
    (void)screen; (void)table;
    printf("S:%d:%d:%d\n", x, y, frame);
}
#define sprite(table, frame) (&special_sprite)
static void JE_setupExplosion(int x, int y, int dy, unsigned type, bool fixed, bool follow) {
    (void)dy; (void)fixed; (void)follow;
    printf("E:%d:%d:%u\n", x, y, type);
}
static void JE_doSP(JE_word x, JE_word y, JE_word count, JE_byte width, JE_byte color) {
    printf("P:%u:%u:%u:%u:%u\n", x, y, count, width, color);
}
""" + structure + shots + r"""
static void dump(void) {
    printf("state:%u", power);
    for (int i = 0; i < 11; i++) printf(",%u,%u", shotMultiPos[i], shotRepeat[i]);
    for (int i = 0; i < 8; i++) printf(",%u", soundQueue[i]);
    putchar('\n');
    for (int i = 0; i < MAX_PWEAPON; i++) {
        PlayerShotDataType *s = &playerShotData[i];
        printf("%d:%u=%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",
            i,shotAvail[i],s->shotX,s->shotY,s->shotXM,s->shotYM,s->shotXC,s->shotYC,
            s->shotComplicated,s->shotDevX,s->shotDirX,s->shotDevY,s->shotDirY,s->shotCirSizeX,s->shotCirSizeY,
            s->shotTrail,s->shotGr,s->shotAni,s->shotAniMax,s->chainReaction,s->shotDmg,s->shotBlastFilter,
            s->playerNumber,s->aimAtEnemy,s->aimDelay,s->aimDelayMax,s->iceticks,s->infinite);
    }
}
int main(int argc, char **argv) {
    if (argc != 3) return 2;
    load_typed_items(argv[1]);
    int mode = atoi(argv[2]);
    spriteSheet8 = (int *)(uintptr_t)8;
    spriteSheet12 = (int *)(uintptr_t)12;
    power = 100;
    player[0].delta_x_shot_move=2; player[0].delta_y_shot_move=-3;
    player[1].delta_x_shot_move=-4; player[1].delta_y_shot_move=5;
    memset(enemyAvail,1,sizeof enemyAvail);
    enemyAvail[0]=0; enemy[0].ex=70; enemy[0].ey=60;
    enemyAvail[1]=2; enemy[1].ex=65; enemy[1].ey=65; enemy[1].scoreitem=true;
    enemyAvail[99]=2; enemy[99].ex=65; enemy[99].ey=65;
    if (mode==3) memset(shotAvail,10,sizeof shotAvail);
    enum ITEM_TYPE type=ItemType_Shot; int shot=item_idx(&type,"late");
    type=ItemType_WeaponFront; int port=item_idx(&type,"late");
    printf("created:%d\n",player_shot_create(port,3,mode==5?65535:60,mode==5?65535:60,70,50,shot,2));
    if (mode==4) {
        shotAvail[79]=1;
        playerShotData[79].shotX=10; playerShotData[79].shotY=20;
        playerShotData[79].shotXM=32767; playerShotData[79].shotXC=1;
        playerShotData[79].playerNumber=1; playerShotData[79].shotTrail=255;
    }
    shotAvail[80]=3;
    dump();
    for (int frame=0;frame<12;frame++) {
        if (mode==3) break;
        if (frame==4) enemyAvail[99]=1;
        if (mode==1) simulate_player_shots();
        else for (int i=0;i<MAX_PWEAPON;i++) if (shotAvail[i]) {
            bool special=true; int sx=-20,sy=-30; JE_integer damage=-2;
            JE_byte filter=4,player_number=5; JE_word chain=6,rw=99,rh=88;
            bool result=player_shot_move_and_draw(i,&special,&sx,&sy,&damage,&filter,&chain,&player_number,&rw,&rh);
            printf("R:%d:%d:%d:%d:%d:%d:%u:%u:%u:%u:%u\n",
                i,result,special,sx,sy,damage,filter,chain,player_number,rw,rh);
        }
        dump();
    }
    for (int wi=0;wi<4;wi++) for (int angle=0;angle<17;angle++) {
        const char *ids[]={"27","28","30","22"};
        type=ItemType_WeaponFront; int weapon=item_idx(&type,ids[wi]);
        playerShotData[0].shotYM=7;
        player_shot_set_direction(0,weapon,(float)(angle*(2*M_PI/16)));
        printf("direction:%d:%d=%d,%d,%d,%u\n",wi,angle,playerShotData[0].shotXM,
            playerShotData[0].shotYM,playerShotData[0].shotYC,playerShotData[0].shotGr);
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
