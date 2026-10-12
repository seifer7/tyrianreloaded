"""Generate native player/enemy collision damage, linked destruction and armor pickups."""

import argparse
from pathlib import Path
import build_combat_reference


def build_source(root):
    base = build_combat_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace("int x,y;", "int x,y,x_velocity,y_velocity;")
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    armor = source[source.index("static void collect_armor_pickup("):source.index("static void collect_bonus_level_pickup(")]
    collision = source[source.index("static void destroy_linked_enemies("):source.index("void JE_playerCollide(")]
    return base + r"""
#define S_POWERUP 29
#define S_ENEMY_HIT 3
typedef int8_t JE_shortint;
static bool twoPlayerLinked;
static JE_byte damageRate,enemyAvail[100],temp2,temp3;
static int b;
static struct {JE_integer ex,ey;JE_word mapoffset,enemytype;JE_shortint exc,eyc;JE_byte armorleft,linknum,explonum;bool enemyground;} enemy[100];
static struct {JE_byte esize;} enemyDat[3]={{0},{1},{0}};
""" + armor + collision + r"""
int main(void) {
    uint8_t pixels[65400],gamePixels[65400];
    for(int pitch=320;pitch<=327;pitch+=7)for(int mode=0;mode<4096;mode++) {
        SDL_Surface hud={320,200,pitch,pixels},game={320,200,pitch,gamePixels};
        VGAScreenSeg=&hud;game_screen=&game;VGAScreen=&game;
        for(int i=0;i<pitch*200;i++)pixels[i]=(i*17+i/7)&255;
        memset(explosions,0,sizeof explosions);memset(rep_explosions,0,sizeof rep_explosions);memset(soundQueue,0,sizeof soundQueue);
        memset(player,0,sizeof player);mt_srand(mode+7);
        twoPlayerMode=mode&1;galagaMode=mode&2;youAreCheating=mode&4;twoPlayerLinked=mode&8;
        levelTimer=true;levelEnd=0;tyrMusicVolume=180;tempVolume=42;
        for(int i=0;i<2;i++) {
            player[i].x=80+i*40;player[i].y=100-i*40;player[i].is_alive=true;
            player[i].shield=mode%29+i;player[i].armor=mode%32+i;
            player[i].shield_max=30;player[i].x_velocity=mode%13-6;player[i].y_velocity=mode%15-7;
        }
        for(int i=0;i<100;i++) {
            enemyAvail[i]=i%3;enemy[i].ex=i*3-20;enemy[i].ey=mode%221-20;
            enemy[i].mapoffset=mode%41;enemy[i].enemytype=i%3;enemy[i].exc=(mode+i*17)%256-128;
            enemy[i].eyc=127-(mode+i*31)%256;enemy[i].armorleft=(mode+i*3)%256;
            enemy[i].linknum=(mode+i*17)%256;enemy[i].explonum=mode%16;enemy[i].enemyground=mode&16;
        }
        int index=mode%100,which=mode%2;damageRate=mode*17;
        if(mode%3==0)damage_enemy_on_collision(&player[which],index);
        else if(mode%3==1)destroy_linked_enemies(index,enemy[index].linknum);
        else collect_armor_pickup(&player[which],index,20001+mode%9999);
        printf("case:%d,%d\n",mode,pitch);
        printf("combat:%d,%u,%u\n",levelTimer,levelEnd,tempVolume);
        for(int i=0;i<2;i++)printf("player:%u,%u,%d,%u,%d,%d\n",player[i].shield,player[i].armor,
            player[i].is_alive,player[i].exploding_ticks,player[i].x_velocity,player[i].y_velocity);
        for(int i=0;i<100;i++)printf("enemy:%u,%u,%u\n",enemyAvail[i],enemy[i].linknum,enemy[i].armorleft);
        for(int i=0;i<200;i++) {
            explosion_type *e=&explosions[i];
            printf("E:%u,%d,%d,%d,%d,%d,%d,%u\n",e->ttl,e->x,e->y,e->delta_x,e->delta_y,e->fixed_position,e->follow_player,e->sprite);
        }
        for(int i=0;i<20;i++) {
            rep_explosion_type *e=&rep_explosions[i];printf("R:%u,%u,%u,%u,%d\n",e->delay,e->ttl,e->x,e->y,e->big);
        }
        printf("sounds:");for(int i=0;i<8;i++)printf("%u,",soundQueue[i]);puts("");
        uint32_t hash=2166136261u;for(int i=0;i<pitch*200;i++)hash=(hash^pixels[i])*16777619u;
        printf("pixels:%u\n",hash);printf("rng:%u\n",(uint32_t)mt_rand());
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
