"""Generate unchanged enemy attacks, launch, update, and hostile shot oracle."""

import argparse
from pathlib import Path
import build_enemy_reference


def build_source(root):
    base = build_enemy_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    header = (root / "src" / "varz.h").read_text(encoding="utf-8")
    start = header.rfind("typedef struct {", 0, header.index("} EnemyShotType;"))
    model = header[start:header.index("} EnemyShotType;") + len("} EnemyShotType;")]
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    attacks = source[source.index("static void aim_enemy_shot("):source.index("static bool prepare_next_level(")]
    hostile = source[source.index("static void draw_enemy_shots(void)\n{"):source.index("static void draw_level_debug_and_timer(")]
    base = base.replace("static int b;", model + r"""
#define ENEMY_SHOT_MAX 60
static EnemyShotType enemyShot[60];
static bool enemyShotAvail[60],twoPlayerMode,endLevel;
static JE_word enemyOnScreen,galagaShotFreq,backMove;
static const JE_byte randomEnemyLaunchSounds[3]={13,6,26};
static int b;
""")
    base = base.replace("int delta_x_shot_move,delta_y_shot_move;",
                        "int delta_x_shot_move,delta_y_shot_move,x_velocity,y_velocity; bool is_alive; uint shot_hit_area_x,shot_hit_area_y,invulnerable_ticks;")
    base += r"""
static JE_byte JE_playerDamage(JE_byte amount, void *p) {
    printf("damage:%d:%u\n",p==&player[0]?0:1,amount);
    return amount;
}
""" + attacks + hostile + r"""
static void dump_attacks(void) {
    printf("attackstats:%u,%u,%d,%d,%d,%d\n",enemyOnScreen,totalEnemy,player[0].x,player[1].x,player[0].x_velocity,player[1].x_velocity);
    printf("sound:");for(int i=0;i<8;i++)printf("%s%u",i?",":"",soundQueue[i]);putchar('\n');
    for(int i=0;i<100;i++) { printf("slot:%d\n",i);dump_enemy(i); }
    for(int i=0;i<60;i++) {
        EnemyShotType *s=&enemyShot[i];
        printf("hostile:%d=%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%u,%u,%u,%u\n",i,enemyShotAvail[i],s->sx,s->sy,s->sxm,s->sym,s->sxc,s->syc,s->tx,s->ty,s->sgr,s->sdmg,s->duration,s->animate,s->animax);
    }
    for(int i=0;i<200;i++) {
        explosion_type *e=&explosions[i];
        printf("E:%d=%u,%d,%d,%d,%d,%d,%d,%u\n",i,e->ttl,e->x,e->y,e->delta_x,e->delta_y,e->fixed_position,e->follow_player,e->sprite);
    }
    printf("velocity:%d,%d,%d,%d\n",player[0].x_velocity,player[0].y_velocity,player[1].x_velocity,player[1].y_velocity);
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;
    load_typed_items(argv[1]);
    enum ITEM_TYPE type=ItemType_Enemy;
    int definition=item_idx(&type,"attacker");
    spriteSheet8=(int*)(uintptr_t)8;spriteSheet12=(int*)(uintptr_t)12;
    for(int i=0;i<4;i++)enemySpriteSheets[i]=(int*)(uintptr_t)8;
    for(int mode=0;mode<16;mode++) {
        memset(enemy,0,sizeof enemy);memset(enemyAvail,1,sizeof enemyAvail);
        memset(enemyShot,0,sizeof enemyShot);memset(enemyShotAvail,1,sizeof enemyShotAvail);
        memset(player,0,sizeof player);memset(explosions,0,sizeof explosions);memset(soundQueue,0,sizeof soundQueue);
        totalEnemy=0;enemyOnScreen=65530;difficultyLevel=mode%11;superArcadeMode=0;
        twoPlayerMode=mode%2;galagaMode=mode%4==3;galagaShotFreq=mode%2?400:0;tempMapXOfs=10;
        player[0].x=80;player[0].y=80;player[1].x=90;player[1].y=80;
        player[0].is_alive=mode%4!=1;player[1].is_alive=mode%4!=2;
        for(int i=0;i<2;i++) {player[i].shot_hit_area_x=12;player[i].shot_hit_area_y=10;}
        player[0].invulnerable_ticks=mode%3==0;
        mt_srand(12345);
        int slot=mode%4*25;
        JE_makeEnemy(&enemy[slot],definition,0);enemyAvail[slot]=0;
        enemy[slot].ex=55;enemy[slot].ey=80;enemy[slot].eyc=mode%3?1:0;
        enemy[slot].linknum=20;enemy[slot].launchspecial=mode%3;
        enemy[slot].iced=mode==1?2:0;enemy[slot].edamaged=mode==2;
        if(mode%2) {
            type=ItemType_Enemy;enemy[slot].launchtype=item_idx(&type,"aimedchild");
        }
        if(mode==6)for(int i=0;i<100;i++) {
            if(i==slot)continue;
            enemyAvail[i]=2;enemy[i].enemycycle=1;enemy[i].egr[0]=1;
            enemy[i].sprite2s=&enemySpriteSheets[0];enemy[i].edamaged=true;
        }
        if(mode==4)memset(enemyShotAvail,0,sizeof enemyShotAvail);
        for(int frame=0;frame<12;frame++) {
            backMove=frame%3;tempBackMove=frame%2;endLevel=frame==3;
            if(frame==5) {enemy[slot].tur[0]=251;enemy[slot].tur[1]=253;enemy[slot].tur[2]=255;}
            if(frame==8) {enemy[slot].tur[0]=252;enemy[slot].tur[1]=254;enemy[slot].tur[2]=255;}
            printf("attack:%d:%d\n",mode,frame);
            JE_drawEnemy(slot+25);
            draw_enemy_shots();
            dump_attacks();
        }
        printf("attackrng:%lu\n",mt_rand());
    }
    // Isolate every collision, clipping, duration, animation, and signed wrap boundary.
    memset(enemy,0,sizeof enemy);memset(enemyAvail,1,sizeof enemyAvail);totalEnemy=enemyOnScreen=0;
    for(int mode=0;mode<16;mode++) {
        memset(enemyShot,0,sizeof enemyShot);memset(enemyShotAvail,1,sizeof enemyShotAvail);
        memset(explosions,0,sizeof explosions);memset(soundQueue,0,sizeof soundQueue);memset(player,0,sizeof player);
        twoPlayerMode=mode%2;endLevel=mode==15;
        for(int j=0;j<2;j++) {
            player[j].x=60+j*20;player[j].y=60;player[j].is_alive=true;
            player[j].shot_hit_area_x=12;player[j].shot_hit_area_y=10;player[j].invulnerable_ticks=mode%3;
        }
        for(int j=0;j<12;j++) {
            EnemyShotType *s=&enemyShot[j];enemyShotAvail[j]=false;
            s->sx=j==0?0:j==1?275:j==2?276:j==3?1:60+j-4;
            s->sy=j==4?-14:j==5?-13:j==6?190:j==7?191:60;
            s->sxm=mode==2?32767:mode%3-1;s->sym=mode==3?-32768:1;
            s->sxc=mode==2?127:0;s->syc=mode==3?-128:0;
            s->tx=j%3;s->ty=j%4;s->duration=j==11?0:2;
            s->sdmg=j%3?5:0;s->sgr=j%2?500:1;s->animate=j%3;s->animax=j%4;
        }
        printf("hostilecase:%d\n",mode);
        draw_enemy_shots();dump_attacks();
    }
    memset(enemy,0,sizeof enemy);memset(enemyAvail,1,sizeof enemyAvail);
    memset(enemyShot,0,sizeof enemyShot);memset(enemyShotAvail,1,sizeof enemyShotAvail);
    enemyOnScreen=totalEnemy=0;tempMapXOfs=0;memset(player,0,sizeof player);
    for(int mode=0;mode<90;mode++) {
        memset(explosions,0,sizeof explosions);memset(soundQueue,0,sizeof soundQueue);
        player[0].x=mode%2?60:400;player[0].y=80;player[1].x=74;player[1].y=80;
        player[0].x_velocity=player[1].x_velocity=0;
        enemy[0].ex=60;enemy[0].ey=mode%3?80:-2;enemy[0].filter=0;
        tempX=enemy[0].ex;tempY=enemy[0].ey;backMove=mode%4;twoPlayerMode=mode%2;difficultyLevel=mode%3;
        printf("special:%d=%d\n",mode,apply_enemy_weapon_special(0,mode%3+1,251+mode%5));
        dump_attacks();
    }
    mt_srand(12345);type=ItemType_Shot;int weapon=item_idx(&type,"11");
    for(int mode=0;mode<128;mode++) {
        twoPlayerMode=mode%2;difficultyLevel=mode%11;
        player[0].x=mode%5?60:-10;player[0].y=80;player[1].x=mode%5?90:10;player[1].y=30;
        player[0].is_alive=mode%4<2;player[1].is_alive=mode%4==0||mode%4==2;
        tempMapXOfs=mode%17;
        aim_enemy_shot(0,weapon,mode%3?60:65530,mode%5?80:65530);
        printf("aimedshot:%d=%d,%d\n",mode,enemyShot[0].sxm,enemyShot[0].sym);
    }
    printf("aimrng:%lu\n",mt_rand());
    return 0;
}
"""
    return base


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
