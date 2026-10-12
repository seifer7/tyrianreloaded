"""Generate unchanged event enemy creation, search, jump, and control oracle."""

import argparse
from pathlib import Path
import build_enemy_reference


def build_source(root):
    base = build_enemy_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    header = (root / "src" / "varz.h").read_text(encoding="utf-8")
    model = header[header.index("struct JE_EventRecType\n"):header.index("struct JE_MegaDataType1")]
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    creation = source[source.index("void JE_createNewEventEnemy("):source.index("static void handle_environment_event(")]
    controls = source[source.index("static void handle_enemy_global_move_event("):source.index("static void handle_level_event(")]
    base = base.replace('static void JE_eventJump(JE_word target) { printf("jump:%u\\n",target); }',
                        model + r"""
static struct JE_EventRecType eventRec[5];
static JE_word eventLoc,curLoc,returnLoc,mapX,mapX3,backMove,backMove2,backMove3,explodeMove;
static JE_byte newPL[10],background3over,map1YDelay,map1YDelayMax,map2YDelay,map2YDelayMax;
static bool background3x1,background3x1b,smallEnemyAdjust,topEnemyOver,twoPlayerMode,onePlayerAction,superTyrian;
static const JE_byte superTyrianSpecials[4]={1,2,4,5};
static void JE_eventJump(JE_word);
""")
    base = base.replace("int delta_x_shot_move,delta_y_shot_move;",
                        "int delta_x_shot_move,delta_y_shot_move; JE_byte *lives;")
    return base + creation + controls + r"""
static void dump_event(void) {
    struct JE_EventRecType *e=&eventRec[0];
    printf("event:%u,%u,%d,%d,%d,%u,%d,%d\n",e->eventtime,e->eventtype,e->eventdat,e->eventdat2,e->eventdat3,e->eventdat4,e->eventdat5,e->eventdat6);
    printf("environment:%d,%u,%d,%u,%u,%u,%u,%u,%u,%u,%u\n",smallEnemyAdjust,background3over,topEnemyOver,map1YDelay,map1YDelayMax,map2YDelay,map2YDelayMax,backMove,backMove2,backMove3,explodeMove);
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;
    load_typed_items(argv[1]);
    static JE_byte lives;player[0].lives=&lives;
    mt_srand(12345);difficultyLevel=2;
    for(int mode=0;mode<64;mode++) {
        memset(enemy,0,sizeof enemy);memset(enemyAvail,1,sizeof enemyAvail);totalEnemy=0;
        memset(eventRec,0,sizeof eventRec);eventLoc=1;mapX=mode%3;mapX3=mode%4;
        smallEnemyAdjust=mode%2;background3x1=mode%3==0;background3x1b=mode%3==1;
        eventRec[0].eventdat=1000;eventRec[0].eventdat2=mode%3==0?-99:mode%3==1?120:32767;
        eventRec[0].eventdat3=mode%2?127:-128;eventRec[0].eventdat4=20;
        eventRec[0].eventdat5=mode%2?5:-5;eventRec[0].eventdat6=-2;
        if(mode==63)memset(enemyAvail,0,sizeof enemyAvail);
        JE_createNewEventEnemy(mode%4,mode%4*25,mode%5==0?21:0);
        printf("spawn:%d=%d\n",mode,b);if(b>0)dump_enemy(b-1);
    }
    memset(eventRec,0,sizeof eventRec);
    const int times[]={0,10,20,30,65535};
    for(int i=0;i<5;i++)eventRec[i].eventtime=times[i];
    const int targets[]={0,5,10,21,65534,65535};
    for(int i=0;i<6;i++) {
        curLoc=65535;returnLoc=20;JE_eventJump(targets[i]);
        printf("jumpcase:%d=%u,%u,%u\n",i,curLoc,returnLoc,eventLoc);
    }
    memset(enemy,0,sizeof enemy);memset(enemyAvail,0,sizeof enemyAvail);totalEnemy=0;
    for(int i=0;i<100;i++){enemy[i].linknum=i%4;enemy[i].evalue=20000;enemyAvail[i]=i%3;}
    for(int mode=0;mode<8;mode++) {
        galagaMode=mode%2;JE_byte found=200;
        bool result=JE_searchFor(mode%5,&found);
        printf("search:%d=%d,%u\n",mode,result,found);
        for(int i=0;i<100;i++){printf("slot:%d\n",i);dump_enemy(i);}
    }
    for(int type=19;type<=33;type++)for(int mode=0;mode<20;mode++) {
        memset(enemy,0,sizeof enemy);memset(enemyAvail,0,sizeof enemyAvail);totalEnemy=0;
        memset(eventRec,0,sizeof eventRec);eventLoc=1;
        mapX=1;mapX3=2;smallEnemyAdjust=false;background3x1=false;background3x1b=false;topEnemyOver=true;background3over=2;
        map1YDelay=map1YDelayMax=map2YDelay=map2YDelayMax=3;backMove=backMove2=backMove3=explodeMove=2;
        galagaMode=mode%2;difficultyLevel=mode%11;twoPlayerMode=mode%4==0;onePlayerAction=mode%4==1;
        superTyrian=mode%4==2;superArcadeMode=mode%4==3;lives=mode%12;
        for(int i=0;i<10;i++)newPL[i]=i+1;
        for(int i=0;i<100;i++) {
            enemyAvail[i]=i%3;enemy[i].linknum=i%5;
            enemy[i].exc=3;enemy[i].eyc=4;enemy[i].excc=2;enemy[i].eycc=3;
            enemy[i].exccw=4;enemy[i].eyccw=5;enemy[i].exccwmax=4;enemy[i].eyccwmax=5;
            enemy[i].exccadd=enemy[i].eyccadd=1;enemy[i].fixedmovey=2;
            enemy[i].ani=3;enemy[i].animin=1;enemy[i].enemycycle=1;enemy[i].animax=2;
            enemy[i].aniactive=2;enemy[i].aniwhenfire=2;enemy[i].armorleft=10;
            enemy[i].exrev=5;enemy[i].eyrev=-5;enemy[i].launchtype=i%2;
        }
        struct JE_EventRecType *e=&eventRec[0];e->eventtype=type;
        e->eventdat=mode%4==0?-99:mode%4==1?-128:mode%4==2?300:10;
        e->eventdat2=mode%3==0?-99:mode%3==1?0:257;
        const int groups[]={0,1,2,3,99,80,89,90,-1,5};e->eventdat3=groups[mode%10];
        e->eventdat4=mode%3==0?0:mode%3==1?3:99;e->eventdat5=mode%3==0?-1:mode%3==1?0:5;
        e->eventdat6=mode%3==0?-99:mode%3==1?0:5;
        if(type==25)e->eventdat=20;
        if(type==23||type==32) {e->eventdat=1000;e->eventdat2=120;e->eventdat3=1;e->eventdat5=-5;}
        if(type==33) {
            e->eventdat=mode%4==0?512:mode%4==1?513:mode%4==2?533:534;
            twoPlayerMode=(mode/4)&1;onePlayerAction=(mode/4)&2;superTyrian=(mode/4)&4;superArcadeMode=mode%5==0;
        }
        printf("control:%d:%d\n",type,mode);
        handle_enemy_event();dump_event();
        for(int i=0;i<100;i++){printf("slot:%d\n",i);dump_enemy(i);}
    }
    for(int life=0;life<12;life++)for(int i=0;i<8;i++) {
        lives=life;eventRec[0].eventdat=533;apply_event_random_special_drop();
        printf("drop:%d:%d=%d\n",life,i,eventRec[0].eventdat);
    }
    printf("eventrng:%lu\n",mt_rand());
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
