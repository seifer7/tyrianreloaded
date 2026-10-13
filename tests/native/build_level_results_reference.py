"""Trace unchanged end-level results with real progression and scripted presentation."""

import argparse
from pathlib import Path
import build_player_progression_reference
from build_joystick_reference import function


def build_source(root):
    base = build_player_progression_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    return base + r"""
#undef SMALL_FONT_SHAPES
#define SMALL_FONT_SHAPES 1
#define SAVE_FILES_SIZE 2398
#define V_LEVEL_END 34
typedef int8_t Sint8;
typedef struct {uint8_t r,g,b;} SDL_Color;
static uint8_t saveTemp[SAVE_FILES_SIZE+104];
static JE_word editorLevel,totalEnemy,enemyKilled,levelTimerCountdown;
static bool constantPlay;
static char levelName[32];
static JE_byte textGlowFont;
static unsigned int frameCountMax;
static int resultsMode,buttonCalls,glowCalls;
static bool all_players_alive(void){return player[0].is_alive&&(!twoPlayerMode||player[1].is_alive);}
static void JE_wipeKey(void){puts("wipe");}
static void set_colors(SDL_Color color,int first,int last){printf("white:%d,%d\n",first,last);}
static void JE_playSampleNum(int sample){printf("sample:%d\n",sample);}
static void JE_outTextGlow(SDL_Surface *s,int x,int y,const char *text) {
    glowCalls++;printf("glow:%d,%d,%s,%u,%u\n",x,y,text,frameCountMax,textGlowFont);
    if((resultsMode&128)&&glowCalls==2)frameCountMax=0;
    if((resultsMode&256)&&y>=150)frameCountMax=0;
}
static void JE_drawCube(SDL_Surface *s,JE_word x,JE_word y,JE_byte hue,JE_byte brightness){printf("cube:%u,%u\n",x,y);}
static void blit_sprite_hv(SDL_Surface *s,int x,int y,unsigned int table,unsigned int frame,uint8_t hue,int8_t value){
    printf("cubehue:%d,%d,%d\n",x,y,value);
}
static void results_keep(void){puts("keep");}
#define NETWORK_KEEP_ALIVE() results_keep()
static void JE_showVGA(void){puts("present");}
static void setDelay(int frames){printf("delay:%d\n",frames);}
static bool JE_anyButton(void){
    buttonCalls++;bool result=buttonCalls%7==0;printf("button:%d\n",result);return result;
}
static void wait_delay(void){puts("wait");}
static void wait_noinput(bool keys,bool mouse,bool joystick){printf("release:%d,%d,%d\n",keys,mouse,joystick);}
static void fade_black(int steps){printf("fade:%d\n",steps);}
static void JE_clr256(void *s){puts("clear");}
""" + function(source, "JE_endLevelAni") + r"""
int main(int argc,char **argv) {
    if(argc!=2)return 2;load_typed_items(argv[1]);
    for(int i=0;i<80;i++)sprintf(pickup_text[i],"text%d",i);
    for(resultsMode=0;resultsMode<512;resultsMode++) {
        memset(player,0,sizeof player);memset(saveTemp,0,sizeof saveTemp);
        for(int i=0;i<2;i++){
            Player *p=&player[i];p->lives=&p->items.weapon[i].power;
            p->items.weapon[0].power=resultsMode%11+1;p->items.weapon[1].power=(resultsMode+1)%11+1;
            p->cash=resultsMode*179+i*127;p->is_alive=(resultsMode&(i?16:8))==0;
            p->items.ship=0;p->items.weapon[0].id=1;p->items.weapon[1].id=2;p->items.sidekick[0]=1;p->items.sidekick[1]=2;
        }
        player[0].items.special=resultsMode%24;
        constantPlay=resultsMode&1;twoPlayerMode=resultsMode&2;onePlayerAction=resultsMode&4;bonusLevel=resultsMode&32;
        initialDifficulty=resultsMode%9+1;difficultyLevel=resultsMode%10;
        editorLevel=65530;totalEnemy=resultsMode%13;enemyKilled=resultsMode%23;cubeMax=resultsMode%7;
        levelTimer=resultsMode&64;levelTimerCountdown=resultsMode%3;episodeNum=resultsMode%4+1;
        strcpy(levelName,"Level");buttonCalls=glowCalls=0;
        printf("results:%d\n",resultsMode);JE_endLevelAni();
        printf("state:%d,%u,%u,%u,%u,%s,%d,%d\n",difficultyLevel,editorLevel,cubeMax,frameCountMax,textGlowFont,lastLevelName,buttonCalls,glowCalls);
        dump_items(&player[0].last_items);
        printf("unlock:");for(int i=0;i<104;i++)printf("%u,",saveTemp[SAVE_FILES_SIZE+i]);puts("");
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
