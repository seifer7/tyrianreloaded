"""Generate native shield/armor HUD and player damage oracle."""

import argparse
from pathlib import Path
import build_effects_reference


def build_source(root):
    effects = build_effects_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    graphics = (root / "src" / "vga256d.c").read_text(encoding="utf-8")
    bars = (root / "src" / "nortvars.c").read_text(encoding="utf-8")
    rectangle = graphics[graphics.index("void JE_rectangle("):graphics.index("void fill_rectangle_xy(")]
    fill = graphics[graphics.index("void fill_rectangle_xy("):graphics.index("void JE_barShade(")]
    bar = bars[bars.index("void JE_dBar3("):bars.index("void JE_barDrawShadow(")]
    combat = varz[varz.index("void JE_wipeShieldArmorBars("):varz.index("void JE_doSP(")]
    return effects + r"""
#define COUNTOF(x) (sizeof(x)/sizeof(*(x)))
#define S_SHIELD_HIT 27
#define S_HULL_HIT 19
#define S_EXPLOSION_22 22
#define REAR_WEAPON 1
typedef struct {
    uint shield, shield_max, armor, exploding_ticks;
    bool is_alive;
    int x,y;
    struct {struct {JE_byte id;} weapon[2];} items;
} Player;
static Player player[2];
static bool twoPlayerMode,galagaMode,youAreCheating,levelTimer;
static JE_byte levelEnd;
static JE_word tempW,tempVolume,tyrMusicVolume;
static SDL_Surface *VGAScreenSeg,*game_screen;
static struct {JE_byte opnum;} weaponPort[3]={{4},{2},{7}};
typedef struct {int x,y,w,h;} SDL_Rect;
static void SDL_FillRect(SDL_Surface *surface,SDL_Rect *rect,int color) {
    if(rect->w<=0 || rect->h<=0) return;
    int left=rect->x<0?0:rect->x,top=rect->y<0?0:rect->y;
    int right=rect->x+rect->w<surface->w?rect->x+rect->w:surface->w;
    int bottom=rect->y+rect->h<surface->h?rect->y+rect->h:surface->h;
    for(int y=top;y<bottom;y++) for(int x=left;x<right;x++)
        ((Uint8*)surface->pixels)[y*surface->pitch+x]=color;
}
static void JE_drawShield(void);
static void JE_drawArmor(void);
""" + rectangle + fill + bar + combat + r"""
int main(int argc,char **argv) {
    if(argc!=3) return 2;
    int mode=atoi(argv[1]),pitch=atoi(argv[2]);
    SDL_Surface hud={320,200,pitch,malloc(pitch*200)};
    SDL_Surface game={320,200,pitch,malloc(pitch*200)};
    if(!hud.pixels||!game.pixels) return 2;
    VGAScreenSeg=&hud;game_screen=&game;VGAScreen=&game;
    twoPlayerMode=mode&1;galagaMode=mode&2;youAreCheating=mode&4;
    levelTimer=true;tyrMusicVolume=180;tempVolume=42;
    player[0].items.weapon[1].id=1;
    player[1].items.weapon[1].id=2;
    player[0].x=80;player[0].y=100;player[1].x=140;player[1].y=60;
    for(int shield=0;shield<9;shield++)
    for(int armor=0;armor<5;armor++)
    for(int amount=0;amount<12;amount++) {
        memset(explosions,0,sizeof explosions);memset(soundQueue,0,sizeof soundQueue);
        levelTimer=true;levelEnd=0;tempVolume=42;
        for(int i=0;i<pitch*200;i++) ((Uint8*)hud.pixels)[i]=(i*17+i/7)&255;
        for(int i=0;i<2;i++) {
            player[i].shield=shield+i;player[i].shield_max=9+i;
            player[i].armor=armor+i;player[i].is_alive=true;player[i].exploding_ticks=0;
        }
        JE_byte damage=JE_playerDamage(amount,&player[mode&1]);
        VGAScreen=VGAScreenSeg;player[1].armor=50;JE_drawArmor();VGAScreen=game_screen;
        printf("damage:%d:%d:%d=%u,%d,%u,%u,%u,%u,%u,%u,%d,%u,%u,%u\n",
            shield,armor,amount,damage,levelTimer,levelEnd,tempVolume,
            player[0].shield,player[0].armor,player[1].shield,player[1].armor,
            player[mode&1].is_alive,player[mode&1].exploding_ticks,soundQueue[1],soundQueue[7]);
        VGAScreen=VGAScreenSeg;
        for(int i=0;i<9;i++) {
            explosion_type *e=&explosions[i];
            printf("E:%d=%u,%d,%d,%d,%d,%d,%d,%u\n",i,e->ttl,e->x,e->y,e->delta_x,e->delta_y,e->fixed_position,e->follow_player,e->sprite);
        }
        uint32_t hash=2166136261u;
        for(int i=0;i<pitch*200;i++) hash=(hash^((Uint8*)hud.pixels)[i])*16777619u;
        printf("pixels:%u\n",hash);
        VGAScreen=game_screen;
    }
    printf("port:%u\n",JE_portConfigs());
    free(hud.pixels);free(game.pixels);
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
