"""Generate native enemy creation/scaling and complete slot state oracle."""

import argparse
import re
from pathlib import Path
import build_items_reference


FIELDS = {
    "ex": "X", "ey": "Y", "exc": "XSpeed", "eyc": "YSpeed",
    "exca": "XRandomAcceleration", "eyca": "YRandomAcceleration",
    "excc": "XFixedAcceleration", "eycc": "YFixedAcceleration",
    "exccw": "XAccelerationWait", "eyccw": "YAccelerationWait",
    "armorleft": "Armor", "enemycycle": "Cycle", "ani": "AnimationLength",
    "size": "Size", "linknum": "LinkNumber", "aniactive": "AnimationActive",
    "animax": "AnimationMax", "aniwhenfire": "AnimationWhenFire",
    "exrev": "XReverse", "eyrev": "YReverse", "exccadd": "XAccelerationAdd",
    "eyccadd": "YAccelerationAdd", "exccwmax": "XAccelerationWaitMax",
    "eyccwmax": "YAccelerationWaitMax", "edamaged": "Damaged", "enemytype": "Type",
    "animin": "AnimationMin", "edgr": "DamageGraphic", "edlevel": "DamageLevel",
    "edani": "DamageAnimation", "filter": "Filter", "evalue": "Value",
    "fixedmovey": "FixedMoveY", "launchwait": "LaunchWait", "launchtype": "LaunchType",
    "launchtypepos": "LaunchTypePosition", "launchfreq": "LaunchFrequency",
    "xaccel": "XAcceleration", "yaccel": "YAcceleration", "enemydie": "EnemyDie",
    "enemyground": "Ground", "explonum": "ExplosionNumber", "mapoffset": "MapOffset",
    "scoreitem": "ScoreItem", "special": "Special", "flagnum": "FlagNumber",
    "setto": "SetTo", "iced": "Iced", "launchspecial": "LaunchSpecial",
    "xminbounce": "XMinBounce", "xmaxbounce": "XMaxBounce",
    "yminbounce": "YMinBounce", "ymaxbounce": "YMaxBounce",
}
ARRAYS = {"eshotwait": ("ShotWait", 3), "eshotmultipos": ("ShotMultiPosition", 3),
          "egr": ("Graphics", 20), "freq": ("Frequency", 3), "tur": ("Turrets", 3)}


def build_source(root):
    items = build_items_reference.build_source(root).split("int main(int argc, char **argv)")[0]
    header = (root / "src" / "varz.h").read_text(encoding="utf-8")
    structure = header[header.index("struct JE_SingleEnemyType\n"):header.index("typedef struct JE_SingleEnemyType JE_MultiEnemyType")]
    config = (root / "src" / "config.h").read_text(encoding="utf-8")
    start = config.rfind("enum\n", 0, config.index("DIFFICULTY_WIMP"))
    difficulty = config[start:config.index("};", start) + 2]
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    creation = source[source.index("static int scale_enemy_score("):source.index("void JE_createNewEventEnemy(")]
    collision = source[source.index("static bool player_shot_hits_enemy("):source.index("static void draw_enemy_shots(")]
    start = header.rfind("typedef struct {", 0, header.index("} explosion_type;"))
    effect_models = header[start:header.index("extern JE_integer tempDat")]
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    effect_setup = varz[varz.index("void JE_setupExplosion("):varz.index("void JE_wipeShieldArmorBars(")]
    particles = varz[varz.index("void JE_doSP("):varz.index("void JE_drawSP(")]
    shots_header = (root / "src" / "shots.h").read_text(encoding="utf-8")
    shot_models = shots_header[shots_header.index("typedef struct {"):shots_header.index("#define MAX_PWEAPON")]
    shot_source = (root / "src" / "shots.c").read_text(encoding="utf-8")
    shot_code = shot_source[shot_source.index("// I'm pretty sure"):]
    movement_start = re.search(r"inline static void blit_enemy\([^\n]+\)\n\{", source).start()
    movement = source[movement_start:source.index("static void aim_enemy_shot(", movement_start)]
    dump = ["static void dump_enemy(int id) {", "struct JE_SingleEnemyType *e=&enemy[id];"]
    for field, name in FIELDS.items():
        dump.append(f'printf("{name}=%d\\n",(int)e->{field});')
    for field, (name, count) in ARRAYS.items():
        dump.append(f'printf("{name}=");for(int i=0;i<{count};i++)printf("%s%u",i?",":"",(unsigned)e->{field}[i]);putchar(\'\\n\');')
    dump.append('printf("Availability=%u\\n",enemyAvail[id]);')
    dump.append('printf("Definition=%d\\n",e->enemydatofs?(int)((JE_EnemyDatType*)e->enemydatofs-enemyDat):-1);')
    dump.append('printf("Sprites=%d\\n",sprite_identity(e->sprite2s));')
    dump.append('printf("Total=%u\\n",totalEnemy);')
    dump.append("}")
    return items + r"""
typedef int16_t Sint16;
#include <assert.h>
typedef uint16_t Uint16;
typedef uint8_t Uint8;
typedef int8_t JE_shortint;
typedef int *Sprite2_array;
typedef int SDL_Surface;
#ifdef _MSC_VER
#define inline __inline
#endif
#include <math.h>
#include "mtrand.h"
#define M_PI 3.14159265358979323846
#define M_PI_2 (M_PI / 2)
#define SA_NONE 0
""" + structure + difficulty + r"""
static struct JE_SingleEnemyType enemy[100];
static JE_byte enemyAvail[100],superArcadeMode;
static JE_shortint difficultyLevel;
static JE_word totalEnemy;
static Uint8 enemySpriteSheetIds[4]={3,7,9,12};
static Sprite2_array enemySpriteSheets[4],spriteSheet10,spriteSheet11;
static int tempI,tempI2;
static JE_byte temp,temp2,temp3,zinglonDuration,superArcadePowerUp,soundQueue[8],shotMultiPos[11],shotRepeat[11];
static JE_word tempX,tempY,tempW,enemyKilled,cubeMax,superEnemy254Jump;
static JE_word x,y,mouseX,mouseY;
static JE_word tempMapXOfs,tempBackMove;
static uint power;
static bool background2;
static const uint shadowYDist=10;
static bool enemyContinualDamage,globalFlags[10],galagaMode;
static bool infiniteShot;
static JE_byte doIced;
static int b;
static void JE_eventJump(JE_word target) { printf("jump:%u\n",target); }
static struct { int x,y; unsigned long cash; int delta_x_shot_move,delta_y_shot_move; } player[2];
typedef struct { JE_byte link_num,armor,color; } boss_bar_t;
static boss_bar_t boss_bar[2];
#define MAX_PWEAPON 81
#define MAX_EXPLOSIONS 200
#define MAX_REPEATING_EXPLOSIONS 20
#define MAX_SUPERPIXELS 101
#define S_EXPLOSION_9 9
#define S_EXPLOSION_8 8
#define S_ENEMY_HIT 3
#define SHOT_MISC 4
#define OPTION_SHAPES 5
#define MAX(a,b) ((a)>(b)?(a):(b))
typedef float JE_real;
""" + effect_models + r"""
static explosion_type explosions[MAX_EXPLOSIONS];
static rep_explosion_type rep_explosions[MAX_REPEATING_EXPLOSIONS];
static superpixel_type superpixels[MAX_SUPERPIXELS];
static unsigned int last_superpixel;
static int *VGAScreen,*spriteSheet8,*spriteSheet12;
static struct {JE_word width,height;} special_sprite={13,17};
#define sprite(table,frame) (&special_sprite)
static void blit_sprite2(int *screen,int x,int y,int *sheet,int frame) {
    (void)screen;printf("D:%d:%d:%d:%d:0\n",(int)(uintptr_t)sheet,x,y,frame);
}
static void blit_sprite2_darken(int *screen,int x,int y,int *sheet,int frame) {
    (void)screen;printf("D:%d:%d:%d:%d:1\n",(int)(uintptr_t)sheet,x,y,frame);
}
static void blit_sprite2_filter(int *screen,int x,int y,int *sheet,int frame,JE_byte filter) {
    (void)screen;printf("F:%d:%d:%d:%d:%u\n",(int)(uintptr_t)sheet,x,y,frame,filter);
}
static void blit_sprite_blend(int *screen,int x,int y,int table,int frame) {
    (void)screen;(void)table;printf("S:%d:%d:%d\n",x,y,frame);
}
static Sint16 JE_newEnemy(int,int,Sint16);
static uint JE_makeEnemy(struct JE_SingleEnemyType *,Uint16,Sint16);
static int sprite_identity(Sprite2_array *sprite) {
    if(sprite==NULL) return -1;
    if(sprite==&spriteSheet11)return 4;
    if(sprite==&spriteSheet10)return 5;
    for(int i=0;i<4;i++)if(sprite==&enemySpriteSheets[i])return i;
    return -2;
}
""" + effect_setup + particles + shot_models + shot_code + creation + movement + collision + "\n".join(dump) + r"""
static void dump_effects(void) {
    printf("stats:%u,%u,%u,%lu,%lu,%u\n",enemyKilled,cubeMax,superArcadePowerUp,player[0].cash,player[1].cash,soundQueue[6]);
    printf("flags:");for(int i=0;i<10;i++)printf("%d",globalFlags[i]);putchar('\n');
    for(int i=0;i<200;i++) {
        explosion_type *e=&explosions[i];
        printf("E:%d=%u,%d,%d,%d,%d,%d,%d,%u\n",i,e->ttl,e->x,e->y,e->delta_x,e->delta_y,e->fixed_position,e->follow_player,e->sprite);
    }
    for(int i=0;i<20;i++) {
        rep_explosion_type *e=&rep_explosions[i];
        printf("R:%d=%u,%u,%u,%u,%d\n",i,e->delay,e->ttl,e->x,e->y,e->big);
    }
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;
    load_typed_items(argv[1]);
    memset(enemyAvail,1,sizeof enemyAvail);
    mt_srand(5489);
    for(int d=-1;d<=11;d++) {
        difficultyLevel=d;
        for(int i=0;i<12;i++) {
            char id[11];sprintf(id,"variant%d",i);
            enum ITEM_TYPE type=ItemType_Enemy;
            int definition=item_idx(&type,id);
            enemyAvail[0]=1;
            enemy[0].flagnum=5;enemy[0].setto=true;enemy[0].scoreitem=i%2;
            enemy[0].eshotmultipos[0]=7;
            int created=JE_newEnemy(0,definition,i==4?21:i==5?26:i==6?7:0);
            printf("created:%d:%d=%d\n",d,i,created);
            dump_enemy(0);
        }
    }
    superArcadeMode=1;
    enemyAvail[25]=1;
    printf("legacy:%d\n",JE_newEnemyOld(25,534,0));
    dump_enemy(25);
    memset(enemyAvail,0,sizeof enemyAvail);
    printf("full:%d\n",JE_newEnemy(50,0,0));
    printf("rng:%lu\n",mt_rand());
    for(int i=0;i<5000;i++) {
        int slot=i%100,mode=i%3;
        enemy[slot].ex=(i%57)-28;enemy[slot].ey=(i%61)-30;
        enemy[slot].mapoffset=i%17;enemy[slot].enemycycle=i%2;
        player[0].x=(i%31)-15;zinglonDuration=i%256;
        JE_word chain=123;JE_integer damage=-12;temp2=4;
        int hits=player_shot_hits_enemy(mode==2?80:0,slot,mode==1,(i%51)-25,(i%53)-26,i%37,i%41,&chain,&damage);
        printf("hit:%d=%d,%u,%d,%u\n",i,hits,chain,damage,temp2);
    }
    for(int i=0;i<100;i++) {
        enemy[i].linknum=i%7;enemy[i].filter=2;enemy[i].iced=3;
        enemy[i].enemyground=i%2;enemy[i].armorleft=i==0?255:20;
        enemyAvail[i]=i%3;
    }
    for(int i=0;i<16;i++) {
        boss_bar[0].link_num=i%7;boss_bar[0].color=1;
        boss_bar[1].link_num=(i+1)%7;boss_bar[1].color=1;
        mark_player_shot_linked_enemies(i,i%7,300-i,i%3?0:10);
        printf("mark:%d=%u,%u\n",i,boss_bar[0].color,boss_bar[1].color);
        for(int j=0;j<100;j++)printf("marked:%d:%d=%u,%u\n",i,j,enemy[j].filter,enemy[j].iced);
    }
    for(int mode=0;mode<8;mode++) {
        memset(enemy,0,sizeof enemy);memset(enemyAvail,1,sizeof enemyAvail);
        memset(explosions,0,sizeof explosions);memset(rep_explosions,0,sizeof rep_explosions);
        enemyContinualDamage=mode&1;galagaMode=mode&2;superArcadeMode=mode&4;
        enemyKilled=65530;cubeMax=65535;superArcadePowerUp=5;superEnemy254Jump=123;
        totalEnemy=65530;difficultyLevel=2;
        player[0].cash=~0ul;player[1].cash=~0ul;
        memset(globalFlags,0,sizeof globalFlags);memset(soundQueue,0,sizeof soundQueue);
        for(int j=0;j<16;j++) {
            enum ITEM_TYPE type=ItemType_Enemy;
            char id[11];sprintf(id,"variant%d",j%12);
            JE_makeEnemy(&enemy[j],item_idx(&type,id),0);
            enemyAvail[j]=j%3==0?1:0;
            enemy[j].ex=60+j;enemy[j].ey=j%2?-2:60;enemy[j].mapoffset=j%3?5:65530;
            enemy[j].linknum=j%4==0?120:j%4==1?20:j%4==2?110:255;
            enemy[j].edlevel=j%3==0?-1:j%3==1?10:0;
            enemy[j].edani=j%4==0?0:j%4==1?-128:5;
            enemy[j].edgr=j%4==0?0:300;
            enemy[j].special=true;enemy[j].flagnum=j%10+1;enemy[j].setto=true;
            enemy[j].evalue=j%3==0?1:j%3==1?20:9999;
            sprintf(id,"%d",535+j%3);type=ItemType_Enemy;enemy[j].enemydie=item_idx(&type,id);
        }
        printf("damage:%d\n",mode);
        animate_player_shot_damaged_enemies(5,mode&1?255:120);
        for(int j=0;j<16;j++) { printf("slot:%d\n",j);dump_enemy(j); }
        dump_effects();
        printf("destroy:%d\n",mode);
        destroy_player_shot_linked_enemies(5,2,mode&1?254:120);
        for(int j=0;j<100;j++) { printf("slot:%d\n",j);dump_enemy(j); }
        dump_effects();
    }
    for(int mode=0;mode<13;mode++) {
        memset(enemy,0,sizeof enemy);memset(enemyAvail,1,sizeof enemyAvail);
        memset(explosions,0,sizeof explosions);memset(rep_explosions,0,sizeof rep_explosions);
        memset(superpixels,0,sizeof superpixels);last_superpixel=0;
        memset(playerShotData,0,sizeof playerShotData);memset(shotAvail,0,sizeof shotAvail);
        memset(shotMultiPos,0,sizeof shotMultiPos);memset(shotRepeat,0,sizeof shotRepeat);
        memset(soundQueue,0,sizeof soundQueue);memset(globalFlags,0,sizeof globalFlags);
        totalEnemy=enemyKilled=cubeMax=0;superArcadeMode=superArcadePowerUp=0;superEnemy254Jump=0;enemyContinualDamage=false;
        memset(boss_bar,0,sizeof boss_bar);
        galagaMode=false;power=100;difficultyLevel=2;player[0].cash=player[1].cash=0;player[0].x=60;
        mt_srand(5489);zinglonDuration=26;mouseX=70;mouseY=50;
        spriteSheet8=(int*)(uintptr_t)8;spriteSheet12=(int*)(uintptr_t)12;
        for(int j=0;j<4;j++) {
            enum ITEM_TYPE type=ItemType_Enemy;
            int definition=item_idx(&type,"variant2");
            JE_makeEnemy(&enemy[j],definition,0);enemyAvail[j]=0;
            enemy[j].ex=60+j;enemy[j].ey=60;enemy[j].mapoffset=0;enemy[j].enemycycle=1;
            enemy[j].armorleft=mode==3||mode==12?255:5+j*5;
            enemy[j].edani=1;enemy[j].edgr=2;enemy[j].edlevel=mode==5?15:0;
            enemy[j].edamaged=false;enemy[j].enemydie=0;enemy[j].evalue=20;
            enemy[j].linknum=mode==4?120:0;
        }
        int index=mode==12?80:0;
        PlayerShotDataType *s=&playerShotData[index];
        s->shotX=60;s->shotY=60;s->shotTrail=255;s->shotGr=mode==2?60001:1;
        s->shotAniMax=1;s->playerNumber=2;s->shotDmg=5+mode*5;s->shotBlastFilter=10;
        s->iceticks=7;s->infinite=mode%3;
        if(mode==1) { enum ITEM_TYPE type=ItemType_Shot;s->chainReaction=item_idx(&type,"late"); }
        shotAvail[index]=10;
        printf("process:%d\n",mode);
        process_player_shot(index);
        for(int j=0;j<100;j++) { printf("slot:%d\n",j);dump_enemy(j); }
        dump_effects();
        printf("shotstate:%u",power);
        for(int j=0;j<11;j++)printf(",%u,%u",shotMultiPos[j],shotRepeat[j]);
        for(int j=0;j<8;j++)printf(",%u",soundQueue[j]);putchar('\n');
        for(int j=0;j<81;j++) {
            PlayerShotDataType *p=&playerShotData[j];
            printf("shot:%d=%u,%d,%d,%u,%u,%u,%u,%u\n",j,shotAvail[j],p->shotX,p->shotY,p->shotAni,p->shotGr,p->shotDmg,p->playerNumber,p->infinite);
        }
        for(int j=0;j<101;j++) {
            superpixel_type *p=&superpixels[j];
            printf("P:%d=%u,%u,%u,%d,%d,%u\n",j,p->x,p->y,p->z,p->delta_x,p->delta_y,p->color);
        }
        printf("ring:%u:%lu\n",last_superpixel,mt_rand());
    }
    totalEnemy=0;mt_srand(12345);
    const int xs[]={-81,-80,-29,-24,-5,60,245,295,299,340,341,32767};
    const int ys[]={-113,-112,-27,-26,-13,-12,0,181,182,190,191,32767};
    const int accel[]={0,1,88,89,90,100,255};
    for(int mode=0;mode<32;mode++) {
        memset(&enemy[0],0,sizeof enemy[0]);enemyAvail[0]=0;
        struct JE_SingleEnemyType *e=&enemy[0];
        enemySpriteSheets[0]=(int*)(uintptr_t)8;
        e->sprite2s=mode%7?&enemySpriteSheets[0]:NULL;
        e->ex=xs[mode%12];e->ey=ys[(mode*7)%12];
        e->exc=mode==4?-128:mode%7-3;e->eyc=3;
        e->xaccel=accel[mode%7];e->yaccel=accel[(mode+3)%7];
        e->excc=mode%2?3:-3;e->eycc=mode%3?2:-2;
        e->exccw=mode==4?-128:1;e->eyccw=0;
        e->exccwmax=mode==4?128:3;e->eyccwmax=2;
        e->exccadd=mode%2?1:-1;e->eyccadd=1;
        e->exrev=mode%2?2:-2;e->eyrev=4;
        e->xminbounce=-5;e->xmaxbounce=245;e->yminbounce=0;e->ymaxbounce=180;
        e->fixedmovey=mode%3-1;e->scoreitem=mode%2;
        e->enemycycle=1;e->ani=3;e->animin=1;e->aniactive=1;e->animax=mode%3?0:2;e->aniwhenfire=2;
        e->egr[0]=1;e->egr[1]=mode%5?2:999;e->egr[2]=3;e->size=mode%2;
        for(int frame=0;frame<8;frame++) {
            player[0].x=frame%2?40:-40;player[0].y=frame%2?80:-80;
            tempMapXOfs=frame%3?0:10;tempBackMove=frame%3;e->filter=frame%2?0:0xA0;
            printf("movement:%d:%d\n",mode,frame);
            update_enemy_acceleration(0);
            bool drawn=animate_and_draw_enemy(0);
            bool positioned=update_enemy_position(0);
            printf("moved:%d,%d\n",drawn,positioned);
            dump_enemy(0);
        }
    }
    for(int i=0;i<1000;i++) {
        tempMapXOfs=i%17;
        JE_integer vx,vy;
        calculate_enemy_aim_velocity(i%101-50,i%91-45,i%111-55,i%121-60,i%256,&vx,&vy);
        printf("aim:%d=%d,%d\n",i,vx,vy);
    }
    printf("movementrng:%lu\n",mt_rand());
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
