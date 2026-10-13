"""Trace the unchanged native level combat-state initializer."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void initialize_level_combat_state(", "void initialize_level_combat_state(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
typedef uint8_t JE_byte;
typedef int8_t JE_shortint;
typedef uint16_t JE_word;
typedef int16_t JE_integer;
typedef uint32_t uint;
#define COUNTOF(array) (sizeof(array) / sizeof((array)[0]))
#define DIFFICULTY_NORMAL 2
#define V_GOOD_LUCK 33
#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif
typedef struct { JE_byte lives; } player_items_t;
typedef struct { player_items_t items; uint purple_balls_needed, exploding_ticks; } player_t;
static player_t player[2];
static const uint purple_required[12]={1,1,2,4,8,12,16,20,25,30,40,50};
static void calc_purple_balls_needed(player_t *p){p->purple_balls_needed=purple_required[p->items.lives];}
static bool isNetworkGame,galagaMode;
static bool twoPlayerLinked,portConfigChange,yourInGameMenuRequest,optionAttachmentLinked;
static bool editShip1,editShip2,levelTimer,randomExplosions,returnActive;
static bool button[4],globalFlags[10],smoothies[9];
static float linkGunDirec;
static JE_byte damageRate,chargeWait,chargeLevel,chargeMax,chargeGr,chargeGrWait;
static JE_word totalEnemy,enemyKilled,galagaShotFreq;
static JE_byte astralDuration,superArcadePowerUp,zinglonDuration;
static JE_shortint constantLastX;
static JE_byte specialWait,nextSpecialWait;
static JE_integer optionAttachmentMove,difficultyLevel;
static int32_t galagaLife,last_superpixel;
static JE_byte enemyAvail[100],enemyShotAvail[60],shotAvail[81],shotMultiPos[11],shotRepeat[11];
static uint8_t playerShotData[82][128],explosions[200][64],rep_explosions[20][64];
static JE_byte soundQueue[8],enemySpriteSheetIds[4],SFCurrentCode[2][21],SFExecuted[2];
static uint8_t enemy[100][128],superpixels[101][64];
typedef struct { uint8_t mainmap[300][14]; } first_map_t;
typedef struct { uint8_t mainmap[600][14]; } second_map_t;
typedef struct { uint8_t mainmap[600][15]; } third_map_t;
static first_map_t megaData1;
static second_map_t megaData2;
static third_map_t megaData3;
static uint8_t *BKwrap1,*BKwrap1to,*BKwrap2,*BKwrap2to,*BKwrap3,*BKwrap3to;
static void JE_drawOptionLevel(void){puts("draw");}
""" + function(source, "initialize_level_combat_state") + r"""
static int all_zero(const void *data,size_t length){
 const uint8_t *bytes=(const uint8_t*)data;
 for(size_t i=0;i<length;i++)if(bytes[i]!=0)return 0;
 return 1;
}
static int all_value(const void *data,size_t length,uint8_t value){
 const uint8_t *bytes=(const uint8_t*)data;
 for(size_t i=0;i<length;i++)if(bytes[i]!=value)return 0;
 return 1;
}
int main(void){
 for(unsigned int mode=0;mode<256;mode++){
  isNetworkGame=(mode&1)!=0;galagaMode=(mode&2)!=0;
  twoPlayerLinked=true;linkGunDirec=0;damageRate=(JE_byte)(mode+1);
  chargeWait=chargeLevel=chargeMax=chargeGr=chargeGrWait=0;
  portConfigChange=true;totalEnemy=enemyKilled=(JE_word)(mode+1);
  astralDuration=(JE_byte)(mode+1);superArcadePowerUp=(JE_byte)(mode+3);
  yourInGameMenuRequest=true;constantLastX=(JE_shortint)mode;
  difficultyLevel=(JE_integer)(mode%11);zinglonDuration=specialWait=nextSpecialWait=77;
  optionAttachmentMove=(JE_integer)(mode+1);optionAttachmentLinked=false;
  editShip1=editShip2=levelTimer=randomExplosions=returnActive=true;
  last_superpixel=(int32_t)(mode+1);galagaShotFreq=(JE_word)(mode+1);galagaLife=(int32_t)(mode+1);
  for(unsigned int i=0;i<2;i++){
   player[i].items.lives=(JE_byte)((mode*5+i*3)%12);
   player[i].purple_balls_needed=99;player[i].exploding_ticks=mode+i+1;
  }
  memset(enemyAvail,(int)(mode%3),sizeof(enemyAvail));
  memset(enemyShotAvail,0,sizeof(enemyShotAvail));memset(playerShotData,0x5a,sizeof(playerShotData));
  memset(shotAvail,0x5a,sizeof(shotAvail));memset(shotMultiPos,0x5a,sizeof(shotMultiPos));
  memset(shotRepeat,0x5a,sizeof(shotRepeat));memset(button,1,sizeof(button));
  memset(globalFlags,1,sizeof(globalFlags));memset(explosions,0x5a,sizeof(explosions));
  memset(rep_explosions,0x5a,sizeof(rep_explosions));memset(soundQueue,0x5a,sizeof(soundQueue));
  memset(enemySpriteSheetIds,0x5a,sizeof(enemySpriteSheetIds));memset(enemy,0x5a,sizeof(enemy));
  memset(SFCurrentCode,0x5a,sizeof(SFCurrentCode));memset(SFExecuted,0x5a,sizeof(SFExecuted));
  memset(smoothies,1,sizeof(smoothies));memset(superpixels,0x5a,sizeof(superpixels));
  BKwrap1=&megaData1.mainmap[2][0];BKwrap1to=&megaData1.mainmap[3][0];
  BKwrap2=&megaData2.mainmap[2][0];BKwrap2to=&megaData2.mainmap[3][0];
  BKwrap3=&megaData3.mainmap[2][0];BKwrap3to=&megaData3.mainmap[3][0];
  printf("case:%u\n",mode);
  initialize_level_combat_state();
  printf("state:");
#define COMMA() putchar(',')
#define SIGNED(value) printf("%d",(int)(value))
#define UNSIGNED(value) printf("%u",(unsigned)(value))
  SIGNED(twoPlayerLinked);COMMA();SIGNED((int)(linkGunDirec*1000));COMMA();UNSIGNED(damageRate);
  COMMA();UNSIGNED(chargeWait);COMMA();UNSIGNED(chargeLevel);COMMA();UNSIGNED(chargeMax);
  COMMA();UNSIGNED(chargeGr);COMMA();UNSIGNED(chargeGrWait);COMMA();SIGNED(portConfigChange);
  COMMA();UNSIGNED(totalEnemy);COMMA();UNSIGNED(enemyKilled);COMMA();UNSIGNED(astralDuration);
  COMMA();UNSIGNED(superArcadePowerUp);COMMA();SIGNED(yourInGameMenuRequest);COMMA();SIGNED(constantLastX);
  COMMA();SIGNED(difficultyLevel);COMMA();SIGNED(optionAttachmentMove);COMMA();SIGNED(optionAttachmentLinked);
  COMMA();SIGNED(editShip1);COMMA();SIGNED(editShip2);COMMA();SIGNED(levelTimer);COMMA();SIGNED(randomExplosions);
  COMMA();SIGNED(returnActive);COMMA();UNSIGNED(galagaShotFreq);COMMA();SIGNED(galagaLife);
  COMMA();SIGNED(BKwrap1-&megaData1.mainmap[0][0]);COMMA();SIGNED(BKwrap1to-&megaData1.mainmap[0][0]);
  COMMA();SIGNED(BKwrap2-&megaData2.mainmap[0][0]);COMMA();SIGNED(BKwrap2to-&megaData2.mainmap[0][0]);
  COMMA();SIGNED(BKwrap3-&megaData3.mainmap[0][0]);COMMA();SIGNED(BKwrap3to-&megaData3.mainmap[0][0]);
  COMMA();UNSIGNED(soundQueue[3]);COMMA();UNSIGNED(player[0].purple_balls_needed);
  COMMA();UNSIGNED(player[0].exploding_ticks);COMMA();UNSIGNED(player[1].purple_balls_needed);
  COMMA();UNSIGNED(player[1].exploding_ticks);COMMA();SIGNED(all_value(enemyAvail,sizeof(enemyAvail),1));
  COMMA();SIGNED(all_value(enemyShotAvail,sizeof(enemyShotAvail),1));
  COMMA();SIGNED(all_zero(playerShotData,sizeof(playerShotData)));
  COMMA();SIGNED(all_zero(shotAvail,sizeof(shotAvail))&&all_zero(shotMultiPos,sizeof(shotMultiPos))&&
   all_value(shotRepeat,sizeof(shotRepeat),1)&&all_zero(button,sizeof(button)));
  COMMA();SIGNED(all_zero(globalFlags,sizeof(globalFlags))&&all_zero(explosions,sizeof(explosions))&&
   all_zero(rep_explosions,sizeof(rep_explosions))&&all_zero(enemySpriteSheetIds,sizeof(enemySpriteSheetIds))&&
   all_zero(enemy,sizeof(enemy))&&all_zero(SFCurrentCode,sizeof(SFCurrentCode))&&
   all_zero(SFExecuted,sizeof(SFExecuted))&&all_zero(smoothies,sizeof(smoothies))&&
   all_zero(superpixels,sizeof(superpixels)));
  COMMA();SIGNED(last_superpixel);putchar('\n');
#undef COMMA
#undef SIGNED
#undef UNSIGNED
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
