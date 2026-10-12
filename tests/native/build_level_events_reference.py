"""Generate complete unchanged native event dispatch oracle with host callbacks."""

import argparse
from pathlib import Path
import build_enemy_events_reference


def build_source(root):
    base = build_enemy_events_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace("eventRec[5]", "eventRec[32]")
    base = base.replace("JE_byte *lives;", "JE_byte *lives; uint invulnerable_ticks; struct {JE_byte special;} items;")
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    environment = source[source.index("static void handle_environment_event("):source.index("static void handle_enemy_global_move_event(")]
    controls = source[source.index("static void handle_level_event("):source.index("void JE_whoa(")]
    return base + r"""
static int starfield_speed;
static JE_integer tempDat,tempDat2,tempDat3;
static bool stopBackgrounds,starActive,enemiesActive,allPlayersGone,reallyEndLevel,readyToEndLevel,endLevel,firstGameOver,musicFade;
static JE_byte stopBackgroundNum,levelEnd,background2over,damageRate;
static JE_word tyrMusicVolume,fxVolume,tempVolume,levelEnemyFrequency,maxEvent,galagaShotFreq;
static bool filterActive,filterFade,filterFadeStart,background2notTransparent,forceEvents;
static JE_shortint levelFilter,levelBrightness,levelFilterNew,levelBrightnessChg,initialDifficulty;
static bool levelTimer,randomExplosions,skyEnemyOverAll,returnActive,smoothies[9];
static JE_byte smoothie_data[9];
static JE_word levelTimerCountdown,levelTimerJumpTo;
static const char shapeFile[]="2478ABCDEFGHIJKLMNOPQRSTU5#V0@3^59";
static const JE_byte windowTextSamples[]={35,31,32,30,35,36,38,35,32};
static const char *outputs[]={"1","2","3","4","5","6","7","8","9"};
static struct {JE_byte *mainmap[300][14];} megaData1;
static struct {JE_byte *mainmap[600][14];} megaData2;
static JE_byte **mapYPos,**mapY2Pos,**BKwrap2,**BKwrap2to;
#define SHOT_SPECIAL 8
#define SHOT_SPECIAL2 10
static void JE_loadCompShapes(Sprite2_array *sheet,char file) {
    printf("load:%d:%c\n",sprite_identity(sheet),file);
}
static void free_sprite2s(Sprite2_array *sheet) {(void)sheet;}
static void JE_drawTextWindow(const char *text) { printf("text:%s\n",text); }
static void play_song(int index) { printf("song:%d\n",index); }
static void set_volume(JE_word music,JE_word effects) {printf("volume:%u,%u\n",music,effects);}
""" + environment + controls + r"""
static void dump_dispatch(void) {
    printf("cursor:%u,%u,%u\n",curLoc,returnLoc,eventLoc);
    printf("state:%d,%d,%u,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%u,%u,%u,%u,%u,%d,%d,%u,%d,%d\n",
        starfield_speed,stopBackgrounds,stopBackgroundNum,starActive,enemiesActive,allPlayersGone,reallyEndLevel,readyToEndLevel,endLevel,firstGameOver,levelEnd,musicFade,filterActive,damageRate,levelEnemyFrequency,tempVolume,background2over,background3over,background2notTransparent,forceEvents,superEnemy254Jump,enemyContinualDamage,returnActive);
    printf("render:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",background3x1,background3x1b,topEnemyOver,smallEnemyAdjust,skyEnemyOverAll,filterFade,filterFadeStart,levelFilter,levelBrightness,levelFilterNew);
    printf("filterchange:%d\n",levelBrightnessChg);
    printf("timer:%d,%u,%u,%d\n",levelTimer,levelTimerCountdown,levelTimerJumpTo,randomExplosions);
    printf("scroll:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",map1YDelay,map1YDelayMax,map2YDelay,map2YDelayMax,backMove,backMove2,backMove3,explodeMove,mapX,mapX3);
    printf("maps:%ld,%ld,%ld,%ld\n",(long)(mapYPos-&megaData1.mainmap[0][0]),(long)(mapY2Pos-&megaData2.mainmap[0][0]),(long)(BKwrap2-&megaData2.mainmap[0][0]),(long)(BKwrap2to-&megaData2.mainmap[0][0]));
    printf("difficulty:%d,%d\n",difficultyLevel,initialDifficulty);
    printf("player:%u,%u,%u\n",player[0].invulnerable_ticks,player[1].invulnerable_ticks,player[0].items.special);
    printf("boss:%u,%u,%u\n",boss_bar[0].link_num,boss_bar[1].link_num,galagaShotFreq);
    printf("sound:");for(int i=0;i<8;i++)printf("%s%u",i?",":"",soundQueue[i]);putchar('\n');
    printf("sheets:");for(int i=0;i<4;i++)printf("%s%u",i?",":"",enemySpriteSheetIds[i]);putchar('\n');
    printf("smooth:");for(int i=0;i<9;i++)printf("%s%d,%u",i?",":"",smoothies[i],smoothie_data[i]);putchar('\n');
    printf("links:");for(int i=0;i<10;i++)printf("%s%u",i?",":"",newPL[i]);putchar('\n');
    printf("shots:");for(int i=0;i<11;i++)printf("%s%u,%u",i?",":"",shotMultiPos[i],shotRepeat[i]);putchar('\n');
    printf("zero:%u,%u\n",enemyDat[0].armor,enemyDat[0].egraphic[0]);
    for(int i=0;i<100;i++){printf("slot:%d\n",i);dump_enemy(i);}
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;load_typed_items(argv[1]);static JE_byte lives=11;player[0].lives=&lives;
    mt_srand(12345);
    for(int type=0;type<=83;type++)for(int mode=0;mode<4;mode++) {
        memset(enemy,0,sizeof enemy);memset(enemyAvail,0,sizeof enemyAvail);totalEnemy=0;
        memset(eventRec,0,sizeof eventRec);maxEvent=32;for(int i=0;i<32;i++)eventRec[i].eventtime=i*10;
        eventRec[31].eventtime=65535;eventLoc=1;curLoc=0;returnLoc=10;
        mapX=1;mapX3=2;mapYPos=&megaData1.mainmap[0][0]+100;mapY2Pos=&megaData2.mainmap[0][0]+100;
        BKwrap2=&megaData2.mainmap[0][0]+90;BKwrap2to=&megaData2.mainmap[0][0]+80;
        for(int i=0;i<100;i++) {
            enemyAvail[i]=i%3;enemy[i].linknum=i%5+1;enemy[i].eyc=mode==1?1:0;enemy[i].evalue=20;
            enemy[i].enemycycle=1;enemy[i].egr[0]=1;
        }
        memset(soundQueue,0,sizeof soundQueue);memset(smoothies,0,sizeof smoothies);memset(smoothie_data,0,sizeof smoothie_data);
        memset(globalFlags,0,sizeof globalFlags);globalFlags[0]=mode%2;
        memset(newPL,0,sizeof newPL);memset(boss_bar,0,sizeof boss_bar);
        memset(shotMultiPos,7,sizeof shotMultiPos);memset(shotRepeat,8,sizeof shotRepeat);
        enemyDat[0].armor=0;enemyDat[0].egraphic[0]=0;
        const int sheets[]={3,7,9,12};for(int i=0;i<4;i++)enemySpriteSheetIds[i]=sheets[i];
        background3x1=background3x1b=smallEnemyAdjust=topEnemyOver=skyEnemyOverAll=false;
        background3over=background2over=0;map1YDelay=map1YDelayMax=map2YDelay=map2YDelayMax=0;
        backMove=backMove2=backMove3=explodeMove=0;
        starfield_speed=0;stopBackgrounds=false;stopBackgroundNum=0;starActive=enemiesActive=false;
        allPlayersGone=mode==3;reallyEndLevel=readyToEndLevel=endLevel=false;
        firstGameOver=mode%2;levelEnd=0;musicFade=false;tyrMusicVolume=100;fxVolume=200;tempVolume=0;
        levelEnemyFrequency=0;filterActive=filterFade=filterFadeStart=false;
        levelFilter=levelBrightness=levelFilterNew=levelBrightnessChg=0;
        damageRate=0;background2notTransparent=forceEvents=false;superEnemy254Jump=0;enemyContinualDamage=false;
        levelTimer=randomExplosions=returnActive=false;levelTimerCountdown=levelTimerJumpTo=0;
        galagaShotFreq=mode==3?10:0;twoPlayerMode=mode%2;onePlayerAction=mode==2;superTyrian=mode==3;
        superArcadeMode=0;galagaMode=false;difficultyLevel=mode+1;initialDifficulty=mode+1;
        player[0].invulnerable_ticks=player[1].invulnerable_ticks=0;player[0].items.special=0;
        struct JE_EventRecType *e=&eventRec[0];e->eventtype=type;e->eventdat=mode;e->eventdat2=mode+2;
        e->eventdat3=mode;e->eventdat4=1;e->eventdat5=2;e->eventdat6=mode;
        if(type==5){e->eventdat=21;e->eventdat2=0;e->eventdat3=26;e->eventdat4=3;}
        if(type==6||type==7||type==10||type==12||type==15||type==17||type==18||type==23||type==32||type==56) {
            e->eventdat=1000;e->eventdat2=120;
        }
        if(type==16)e->eventdat=mode==3?10:mode+1;
        if(type==33||type==45)e->eventdat=533;
        if(type==49||type==50||type==51||type==52){e->eventdat=2;e->eventdat3=21;e->eventdat6=5;}
        if(type==54||type==70||type==71)e->eventdat=30;
        if(type==61){e->eventdat=1;e->eventdat2=1;}
        if(type==64)e->eventdat=mode==3?6:mode+1;
        if(type==75){e->eventdat=1;e->eventdat2=5;e->eventdat3=80;e->eventdat4=2;}
        if(type==82)e->eventdat=13;
        printf("dispatch:%d:%d\n",type,mode);JE_eventSystem();
        printf("record:%u,%u,%d,%d,%d,%u,%d,%d\n",e->eventtime,e->eventtype,e->eventdat,e->eventdat2,e->eventdat3,e->eventdat4,e->eventdat5,e->eventdat6);
        dump_dispatch();
    }
    printf("dispatchrng:%lu\n",mt_rand());return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
