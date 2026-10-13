"""Verify unchanged next-episode selection, reward and presentation flow."""

import argparse
from pathlib import Path
import build_player_progression_reference
from build_joystick_reference import function


def build_source(root):
    base = build_player_progression_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace('static void JE_dString(int *screen,int x,int y,const char *text,int font) {puts("insertcoin");}',
                        'static void JE_dString(int *screen,int x,int y,const char *text,int font) {printf("title:%d,%d,%s,%d\\n",x,y,text,font);}')
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    episodes = (root / "src" / "episodes.c").read_text(encoding="utf-8")
    find = function(episodes.replace("unsigned int JE_findNextEpisode(", "int JE_findNextEpisode("), "JE_findNextEpisode")
    find = find.replace("int JE_findNextEpisode(", "unsigned int JE_findNextEpisode(", 1)
    return base + r"""
#undef SMALL_FONT_SHAPES
#define SMALL_FONT_SHAPES 1
#define EPISODE_AVAILABLE 4
#define EPISODE_MAX 5
static JE_byte initial_episode_num;
static bool constantPlay,jumpBackToEpisode1,gameLoaded,episodeAvail[5];
static const char *episode_name[6]={"","Episode1","Episode2","Episode3","Episode4","Episode5"};
static int transitionMode,buttons;
static uint8_t colors[768],palettes[6][768];
static void JE_highScoreCheck(void){puts("highscores");}
static void JE_playCredits(void){puts("credits");}
static void JE_initEpisode(JE_byte ep){episodeNum=ep;printf("initialize:%u\n",ep);}
static void JE_clr256(void *s){puts("clear");}
static int JE_fontCenter(const char *text,int font){printf("center:%s,%d\n",text,font);return 100;}
static void JE_showVGA(void){puts("present");}
static void fade_palette(uint8_t *p,int steps,int first,int last){printf("fadein:%d,%d,%d\n",steps,first,last);}
static void JE_wipeKey(void){puts("wipe");}
static void transition_keep(void){puts("keep");}
#define NETWORK_KEEP_ALIVE() transition_keep()
static void SDL_Delay(unsigned int ms){printf("delay:%u\n",ms);}
static bool JE_anyButton(void){buttons++;bool done=buttons==transitionMode%4+1;printf("button:%d\n",done);return done;}
static void fade_black(int steps){printf("fade:%d\n",steps);}
""" + find + function(source, "JE_nextEpisode") + r"""
int main(int argc,char **argv) {
    if(argc!=2)return 2;load_typed_items(argv[1]);strcpy(pickup_text[4],"Continue");
    for(transitionMode=0;transitionMode<1024;transitionMode++) {
        memset(player,0,sizeof player);memset(colors,0,sizeof colors);memset(palettes[5],0x47,sizeof palettes[5]);
        for(int i=0;i<2;i++){player[i].items.weapon[0].power=5;player[i].items.weapon[1].power=7;}
        episodeNum=transitionMode%5+1;initial_episode_num=(transitionMode&1)?episodeNum:1;
        gameHasRepeated=transitionMode&2;isNetworkGame=transitionMode&4;constantPlay=transitionMode&8;
        for(int i=0;i<5;i++)episodeAvail[i]=(transitionMode>>(i+4))&1;
        gameLoaded=false;mainLevel=9;saveLevel=10;buttons=0;mt_srand(transitionMode+1);
        printf("transition:%d\n",transitionMode);JE_nextEpisode();
        printf("state:%u,%d,%d,%d,%u,%u,%s,%d\n",episodeNum,gameHasRepeated,jumpBackToEpisode1,gameLoaded,mainLevel,saveLevel,lastLevelName,buttons);
        for(int i=0;i<2;i++){dump_items(&player[i].items);dump_items(&player[i].last_items);}
        printf("palette:%u,%u\n",colors[0],colors[767]);printf("rng:%u\n",(uint32_t)mt_rand());
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
