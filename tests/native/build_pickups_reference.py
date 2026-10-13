"""Generate unchanged pickups and complete player collision iteration with real effects."""

import argparse
from pathlib import Path
import build_player_special_reference
from build_joystick_reference import function


def build_source(root):
    base = build_player_special_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace("bool is_alive;", "bool is_alive,is_dragonwing;unsigned long cash;")
    base = base.replace("struct {struct {JE_byte id,power;} weapon[2];", "struct PickupItems {struct {JE_byte id,power;} weapon[2];")
    base = base.replace("ship,sidekick_level;} items;", "ship,sidekick_level,sidekick_series,super_arcade_mode,generator;} items;struct PickupItems last_items;")
    base = base.replace("JE_integer ex,ey,evalue;JE_shortint exc,eyc;bool scoreitem;",
                        "JE_integer ex,ey,evalue;JE_shortint exc,eyc;bool scoreitem,enemyground;JE_byte explonum,linknum,armorleft;JE_word mapoffset,enemytype;")
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    header = (root / "src" / "varz.h").read_text(encoding="utf-8")
    start = header.rfind("typedef struct {", 0, header.index("} explosion_type;"))
    models = header[start:header.index("extern JE_integer tempDat")]
    setup = varz[varz.index("void JE_setupExplosion("):varz.index("void JE_wipeShieldArmorBars(")]
    start = base.index("static void JE_setupExplosion(")
    end = base.index("static void JE_doSP(", start)
    base = base[:start] + r"""
#define MAX_EXPLOSIONS 200
#define MAX_REPEATING_EXPLOSIONS 20
#define MAX_SUPERPIXELS 101
typedef bool JE_boolean;
""" + models.replace("Uint8", "uint8_t") + r"""
static explosion_type explosions[200];
static rep_explosion_type rep_explosions[20];
void JE_setupExplosion(signed int,signed int,signed int,unsigned int,bool,bool);
void JE_setupExplosionLarge(bool,JE_byte,JE_integer,JE_integer);
""" + base[end:]
    stub = "static void JE_setupExplosionLarge(bool ground,JE_byte type,int x,int y) {assert(!\"Explosion frames use the separate lifecycle oracle\");}"
    base = base.replace(stub, "")
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    player = (root / "src" / "player.c").read_text(encoding="utf-8")
    tables = varz[varz.index("const JE_word SAWeapon["):varz.index("const JE_word PGR[")]
    pickups = source[source.index("static void collect_arcade_pickup("):source.index("void JE_playerCollide(")]
    return base + r"""
#undef COUNTOF
#define COUNTOF(x) ((unsigned)(sizeof(x) / sizeof *(x)))
#define PORT_NUM 42
#define SHOT_FRONT 0
#define S_ITEM 18
#define V_DATA_CUBE 37
#define S_SHIELD_HIT 27
#define S_HULL_HIT 19
#define S_EXPLOSION_22 22
#define S_EXPLOSION_4 4
#define S_ENEMY_HIT 3
static bool bonusLevel,youAreCheating,levelTimer;
static JE_word nextLevel,displayTime,cubeMax,tempVolume,tyrMusicVolume;
static JE_byte damageRate,temp2_collision,temp3;
static char network_player_name[32],network_opponent_name[32];
static char pickup_text[80][32];
static const char *miscTextB[4]={"","","","got"};
static void JE_drawTextWindow(const char *text) {printf("text:%s\n",text);}
static void play_song(uint song) {printf("song:%u\n",song);}
""" + tables + setup + function(varz.replace("JE_byte JE_playerDamage(", "int JE_playerDamage("), "JE_playerDamage") + function(player, "power_up_weapon") + \
        function(player, "handle_got_purple_ball") + \
        function(source, "JE_getName").replace("miscText", "pickup_text") + \
        pickups.replace("miscText[", "pickup_text[").replace("temp2 =", "temp2_collision =").replace(
            "temp2++", "temp2_collision++").replace("temp2 <", "temp2_collision <").replace(
            "[temp2]", "[temp2_collision]").replace("temp2 !=", "temp2_collision !=").replace(
            "temp2;", "temp2_collision;") + function(source, "JE_playerCollide") + r"""
static const int values[]={1,-1,-2,-3,-4,-5,0,50,9999,-6,10001,20001,29999,30000,30001,30003,30031,31001,32001,32002,32015,32101};
static void dump_items(struct PickupItems *p) {
    printf("items:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",
        p->weapon[0].id,p->weapon[0].power,p->weapon[1].id,p->weapon[1].power,p->sidekick[0],p->sidekick[1],
        p->special,p->ship,p->sidekick_level,p->sidekick_series,p->super_arcade_mode,
        p->generator,0,0,0);
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;load_typed_items(argv[1]);
    uint8_t pixels[65400],gamePixels[65400];SDL_Surface hud={320,200,327,pixels},game={320,200,327,gamePixels};
    VGAScreenSeg=&hud;game_screen=&game;VGAScreen=(int*)&game;
    for(int i=0;i<80;i++)sprintf(pickup_text[i],"text%d",i);
    for(int mode=0;mode<8192;mode++) {
        memset(player,0,sizeof player);memset(playerShotData,0,sizeof playerShotData);memset(shotAvail,0,sizeof shotAvail);
        memset(explosions,0,sizeof explosions);memset(rep_explosions,0,sizeof rep_explosions);
        memset(soundQueue,0,sizeof soundQueue);memset(shotMultiPos,3,sizeof shotMultiPos);memset(shotRepeat,4,sizeof shotRepeat);
        memset(pixels,69,sizeof pixels);mt_srand(mode+7);power=100;
        twoPlayerMode=mode&1;galagaMode=mode&2;twoPlayerLinked=mode&4;onePlayerAction=mode&8;
        isNetworkGame=mode&16;thisPlayerNum=(mode&32)?2:1;
        strcpy(network_player_name,(mode&64)?"local":"");strcpy(network_opponent_name,(mode&128)?"opponent":"");
        bonusLevel=mode&256;nextLevel=17;displayTime=19;cubeMax=65535;
        superArcadeMode=(mode&512)?mode%7+1:0;
        levelTimer=true;youAreCheating=mode&1024;levelEnd=0;tempVolume=42;tyrMusicVolume=180;
        mouseX=98;mouseY=77;damageRate=mode%13;
        for(int i=0;i<2;i++) {
            Player *p=&player[i];p->lives=&p->items.weapon[i].power;p->is_dragonwing=i==1;
            p->is_alive=true;p->x=100+i*4;p->y=80+i*4;p->cash=(mode&2048)?ULONG_MAX-50:100;
            p->items.weapon[0].id=ITEM_INDEX_STORE[ITEM_WEAPONFRONT_NORTSHIPSUPERPULSE];
            p->items.weapon[1].id=ITEM_INDEX_STORE[ITEM_WEAPONREAR_NORTSHIPSPREADER];
            if(mode&1024)p->items.weapon[0].id=0;
            p->items.weapon[0].power=mode%12;p->items.weapon[1].power=(mode+1)%12;
            p->purple_balls_needed=mode%5;p->weapon_mode=3;p->superbombs=mode%12;
            p->items.sidekick_level=mode%3+101;p->items.sidekick_series=mode%16;
            p->shield=mode%9;p->armor=mode%30;p->shield_max=31;p->invulnerable_ticks=(mode&2048)?1:0;
        }
        for(int i=0;i<100;i++) {
            memset(&enemy[i],0,sizeof enemy[i]);enemyAvail[i]=1;enemy[i].enemytype=0;
            enemy[i].ey=80;enemy[i].ex=100;enemy[i].explonum=mode%53;
        }
        int index=mode%81,number=(mode/2)%2+1;Player *p=&player[number-1];
        int value=values[mode%COUNTOF(values)];
        if(superArcadeMode&&value>30000)value=30001+mode%5;
        char buffer[256];
        printf("pickupcase:%d\n",mode);printf("name:%s,%s\n",JE_getName(1),JE_getName(2));
        enemyAvail[index]=mode%3==0?0:2;
        if(mode<4096) {
            if(value>=30000)collect_arcade_pickup(p,number,index,value,buffer);
            else if(value>20000)collect_armor_pickup(p,index,value);
            else if(value>10000)collect_bonus_level_pickup(index,value);
            else collect_score_item(p,number,index,value,100,buffer);
        } else {
            for(int i=0;i<12;i++) {
                enemyAvail[i]=i%3;enemy[i].ex=100+i%4*4;enemy[i].ey=80+i%5*4;
                enemy[i].evalue=values[(mode+i)%COUNTOF(values)];
                if(superArcadeMode&&enemy[i].evalue>30000)enemy[i].evalue=30001+i%5;
                enemy[i].scoreitem=i%2;enemy[i].armorleft=mode%5;enemy[i].linknum=0;
                enemy[i].exc=2;enemy[i].eyc=-1;enemy[i].enemyground=i%3==0;
                if(enemy[i].evalue==-3 && i==0)shotAvail[0]=7;
            }
            JE_playerCollide(p,number);
        }
        printf("state:%d,%d,%d,%u,%u,%u,%d,%u,%u\n",twoPlayerMode,twoPlayerLinked,bonusLevel,nextLevel,displayTime,cubeMax,
            levelTimer,levelEnd,tempVolume);
        for(int i=0;i<2;i++) {
            Player *p=&player[i];printf("owner:%llu,%u,%u,%u,%u,%u,%d,%d,%d,%u\n",(unsigned long long)p->cash,p->purple_balls_needed,
                p->weapon_mode,p->superbombs,p->armor,p->shield,p->x_velocity,p->y_velocity,p->is_alive,p->exploding_ticks);
            dump_items(&p->items);dump_items(&p->last_items);
        }
        for(int i=0;i<100;i++)printf("enemy:%u,%u,%u\n",enemyAvail[i],enemy[i].armorleft,enemy[i].linknum);
        for(int i=0;i<200;i++) {
            explosion_type *e=&explosions[i];printf("E:%u,%d,%d,%d,%d,%d,%d,%u\n",e->ttl,e->x,e->y,e->delta_x,e->delta_y,e->fixed_position,e->follow_player,e->sprite);
        }
        for(int i=0;i<20;i++){rep_explosion_type *e=&rep_explosions[i];printf("R:%u,%u,%u,%u,%d\n",e->delay,e->ttl,e->x,e->y,e->big);}
        printf("shots:%u",power);for(int i=0;i<11;i++)printf(",%u,%u",shotRepeat[i],shotMultiPos[i]);puts("");
        for(int i=0;i<MAX_PWEAPON;i++) {
            PlayerShotDataType *s=&playerShotData[i];printf("projectile:%u,%d,%d,%d,%d,%u,%u\n",shotAvail[i],s->shotX,s->shotY,s->shotXM,s->shotYM,s->shotGr,s->playerNumber);
        }
        printf("sounds:");for(int i=0;i<8;i++)printf("%u,",soundQueue[i]);puts("");
        printf("pixels:%u\n",hash(&hud));printf("rng:%u\n",(uint32_t)mt_rand());
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
