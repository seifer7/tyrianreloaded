"""Trace the unchanged native level-entry flag and state initializer."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void initialize_level_flags(", "void initialize_level_flags(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#define COUNTOF(array) (sizeof(array) / sizeof((array)[0]))
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int8_t JE_shortint;
typedef bool JE_boolean;
typedef struct { JE_byte link_num, armor, color; } boss_bar_t;
static bool newkey,newmouse,bonusLevel,readyToEndLevel,firstGameOver,enemiesActive,stopBackgrounds;
static bool background3x1,background3x1b,topEnemyOver,skyEnemyOverAll,smallEnemyAdjust,starActive;
static bool enemyContinualDamage,quitRequested,forceEvents,filterActive,filterFade,filterFadeStart;
static bool background2notTransparent;
static JE_byte levelEnd,warningCol,warningSoundDelay,stopBackgroundNum,background3over,background2over;
static JE_shortint levelEndWarp,warningColChange,levelFilter,levelBrightness,levelBrightnessChg;
static JE_word levelEndFxWait,armorShipDelay,eventLoc,curLoc,backMove,backMove2,backMove3,explodeMove;
static JE_word levelEnemyFrequency,superEnemy254Jump;
static JE_boolean button[3];
static int temp;
static boss_bar_t boss_bar[2];
""" + function(source, "initialize_level_flags") + r"""
int main(void)
{
 for (unsigned int mode=0;mode<16384;mode++) {
  newkey=newmouse=bonusLevel=readyToEndLevel=firstGameOver=enemiesActive=stopBackgrounds=mode&1;
  background3x1=background3x1b=topEnemyOver=skyEnemyOverAll=smallEnemyAdjust=starActive=mode&2;
  enemyContinualDamage=quitRequested=forceEvents=filterActive=filterFade=filterFadeStart=mode&4;
  background2notTransparent=mode&8;
  levelEnd=(JE_byte)mode;levelEndWarp=(JE_shortint)(mode*17);levelEndFxWait=(JE_word)(mode*19);
  warningCol=(JE_byte)(mode*23);warningColChange=(JE_shortint)(mode*29);warningSoundDelay=(JE_byte)(mode*31);
  armorShipDelay=(JE_word)(mode*37);eventLoc=(JE_word)(mode*41);curLoc=(JE_word)(mode*43);
  backMove=(JE_word)(mode*47);backMove2=(JE_word)(mode*53);backMove3=(JE_word)(mode*59);
  explodeMove=(JE_word)(mode*61);stopBackgroundNum=(JE_byte)(mode*67);
  background3over=(JE_byte)(mode*71);background2over=(JE_byte)(mode*73);
  levelEnemyFrequency=(JE_word)(mode*79);superEnemy254Jump=(JE_word)(mode*83);
  levelFilter=(JE_shortint)(mode*3);levelBrightness=(JE_shortint)(mode*5);levelBrightnessChg=(JE_shortint)(mode*7);
  for (unsigned int i=0;i<3;i++) button[i]=(mode+i)&2;
  for (unsigned int i=0;i<COUNTOF(boss_bar);i++) {
   boss_bar[i].link_num=(JE_byte)(mode+i+1);boss_bar[i].armor=(JE_byte)(mode+i+10);boss_bar[i].color=(JE_byte)(mode+i+20);
  }
  printf("case:%u\n",mode);initialize_level_flags();
  printf("core:%u,%u,%u,%d,%u,%u,%d,%u,%u\n",newkey,newmouse,levelEnd,levelEndWarp,
   levelEndFxWait,warningCol,warningColChange,warningSoundDelay,armorShipDelay);
  printf("level:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",
   bonusLevel,readyToEndLevel,firstGameOver,eventLoc,curLoc,enemiesActive,stopBackgrounds,stopBackgroundNum,
   background3x1,background3x1b,background3over,background2over,topEnemyOver,skyEnemyOverAll,smallEnemyAdjust,
   starActive,enemyContinualDamage,levelEnemyFrequency,quitRequested,forceEvents,superEnemy254Jump,
   filterActive,background2notTransparent);
  printf("background:%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%d,%d\n",
   backMove,backMove2,backMove3,explodeMove,filterFade,filterFadeStart,
   button[0],button[1],button[2],levelFilter,levelBrightness,levelBrightnessChg);
  printf("boss:%u,%u,%u,%u,%u,%u\n",boss_bar[0].link_num,boss_bar[0].armor,boss_bar[0].color,
   boss_bar[1].link_num,boss_bar[1].armor,boss_bar[1].color);
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
