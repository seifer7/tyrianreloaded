"""Compare unchanged player death/respawn with native effects and HUD rendering."""

import argparse
from pathlib import Path
import build_combat_reference


def build_source(root):
    base = build_combat_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace("uint shield, shield_max, armor, exploding_ticks;", "uint shield, shield_max, armor, exploding_ticks, initial_armor, invulnerable_ticks, purple_balls_needed; JE_byte *lives;")
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    source = source[source.index("static bool update_player_death("):source.index("static void collect_player_movement_input(")]
    return base + r"""
#define S_EXPLOSION_9 9
#define S_EXPLOSION_11 11
static JE_word levelEndFxWait;
static bool onePlayerAction,reallyEndLevel,twoPlayerLinked,endLevel,allPlayersGone,isNetworkGame,constantDie;
static JE_byte shotMultiPos[2],episodeNum;
static void calc_purple_balls_needed(Player *p) {
    static const uint counts[]={1,1,2,4,8,12,16,20,25,30,40,50};
    p->purple_balls_needed=counts[*p->lives];
}
""" + source + r"""
int main(void) {
    for(int pitch=320;pitch<=327;pitch+=7)for(int mode=0;mode<2048;mode++)for(int which=0;which<2;which++) {
        SDL_Surface hud={320,200,pitch,malloc(pitch*200)},game={320,200,pitch,malloc(pitch*200)};
        VGAScreenSeg=&hud;game_screen=&game;VGAScreen=&game;
        for(int i=0;i<pitch*200;i++)((Uint8*)hud.pixels)[i]=(i*17+i/7)&255;
        memset(player,0,sizeof player);memset(explosions,0,sizeof explosions);memset(soundQueue,0,sizeof soundQueue);
        twoPlayerMode=mode&1;onePlayerAction=mode&2;galagaMode=mode&4;episodeNum=(mode&8)?4:1;
        constantDie=mode&32;allPlayersGone=mode&256;isNetworkGame=mode&512;
        reallyEndLevel=endLevel=twoPlayerLinked=true;levelEnd=17;levelEndFxWait=(mode&1024)?2:0;
        JE_byte lives[2]={11,11};shotMultiPos[0]=7;shotMultiPos[1]=8;
        for(int i=0;i<2;i++) {
            player[i].lives=&lives[i];player[i].armor=(mode%3)?40:0;player[i].initial_armor=57;
            player[i].shield=9;player[i].shield_max=11;player[i].x=80+i*40;player[i].y=170+i*13;
            player[i].is_alive=mode&16;player[i].exploding_ticks=(mode&64)?2:0;
        }
        lives[which]=(mode&128)?3:1;
        mt_srand(98765+mode);
        bool result=update_player_death(&player[which],which+1);
        printf("case:%d,%d,%d\n",pitch,mode,which);
        printf("state:%d,%d,%d,%d,%d,%u,%u,%u,%u,%u,%u\n",result,twoPlayerMode,reallyEndLevel,endLevel,twoPlayerLinked,
            levelEnd,levelEndFxWait,shotMultiPos[0],shotMultiPos[1],lives[0],lives[1]);
        for(int i=0;i<2;i++)printf("player:%d,%d,%u,%u,%u,%u,%u,%d\n",i,player[i].is_alive,player[i].exploding_ticks,
            player[i].invulnerable_ticks,player[i].armor,player[i].shield,player[i].purple_balls_needed,player[i].y);
        for(int i=0;i<8;i++) {
            explosion_type *e=&explosions[i];
            printf("explosion:%u,%d,%d,%u,%d,%d\n",e->ttl,e->x,e->y,e->sprite,e->fixed_position,e->follow_player);
        }
        uint32_t hash=2166136261u;for(int i=0;i<pitch*200;i++)hash=(hash^((Uint8*)hud.pixels)[i])*16777619u;
        printf("pixels:%u\n",hash);printf("sounds:%u,%u\n",soundQueue[5],soundQueue[6]);
        printf("rng:%u\n",mt_rand());free(hud.pixels);free(game.pixels);
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
