"""Compile unchanged level completion and queued sound dispatch functions."""

import argparse
from pathlib import Path
import build_level_events_reference


def build_source(root):
    base = build_level_events_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    completion = source[source.index("static void update_level_end_state("):source.index("static void record_level_demo(")]
    audio = source[source.index("static void play_level_sounds("):source.index("static bool player_shot_hits_enemy(")]
    return base + r"""
static bool enemyStillExploding,waitToEndLevel;
static JE_word enemyOnScreen;
static JE_byte *soundSamples[38];
static size_t soundSampleCount[38];
static const JE_word fxPlayVol=4;
#define S_NONE 0
static void multiSamplePlay(const JE_byte *sample,size_t length,JE_byte channel,JE_byte volume) {
    (void)length;printf("sample:%u,%u,%u\n",*sample,channel,volume);
}
""" + completion + audio + r"""
int main(void) {
    for(int mode=0;mode<1024;mode++) {
        stopBackgroundNum=(mode&1)?9:1;backMove=(mode&2)?1:0;backMove2=5;backMove3=6;explodeMove=7;
        enemyStillExploding=(mode&4)!=0;waitToEndLevel=(mode&8)!=0;allPlayersGone=(mode&16)!=0;
        endLevel=(mode&32)!=0;enemyOnScreen=(mode&64)?1:0;readyToEndLevel=(mode&128)!=0;
        levelTimerCountdown=(mode&256)?10:0;stopBackgrounds=(mode&512)!=0;
        levelTimer=true;levelEnd=13;reallyEndLevel=false;
        update_level_end_state();
        printf("end:%d,%u,%u,%u,%u,%u,%d,%d,%d,%d,%u,%d\n",mode,stopBackgroundNum,backMove,backMove2,backMove3,explodeMove,stopBackgrounds,endLevel,readyToEndLevel,reallyEndLevel,levelEnd,levelTimer);
    }
    JE_byte identities[38];for(int i=0;i<38;i++){identities[i]=i+1;soundSamples[i]=&identities[i];soundSampleCount[i]=1;}
    for(int mode=0;mode<76;mode++) {
        firstGameOver=mode%2;for(int i=0;i<8;i++)soundQueue[i]=(mode+i)%39;
        printf("audio:%d\n",mode);play_level_sounds();
        printf("queue:");for(int i=0;i<8;i++)printf("%s%u",i?",":"",soundQueue[i]);putchar('\n');
    }return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
