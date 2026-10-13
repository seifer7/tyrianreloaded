"""Trace the unchanged native episode-completion and unlock presentation."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static bool show_episode_completion(", "bool show_episode_completion(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef unsigned int uint;
#define SA_NORTSHIPZ 7
#define SA_ENGAGE 9
#define DIFFICULTY_ZINGLON 4
#define FONT_SHAPES 0
#define SMALL_FONT_SHAPES 1
struct player_stub { unsigned long cash; };
static struct player_stub player[2];
static bool ESCPressed,twoPlayerMode,constantPlay,jumpBackToEpisode1,superTyrian;
static bool trentWin,jumpSection,isNetworkGame;
static int secretHint,initialDifficulty,superArcadeMode,mainLevel,temp;
static JE_word frameCountMax;
static JE_byte levelWarningLines;
static char levelWarningText[10][61];
static char miscText[64][64],miscTextB[16][64],pName[2][32];
static char specialName[10][32],superShips[10][32];
static JE_byte SANextShip[9]={3,9,6,2,5,1,4,3,7};
static JE_byte SAShip[7]={3,1,5,10,2,11,12};
static struct { unsigned short shipgraphic; } ships[16];
static int spriteSheet9,scenario;
static void *VGAScreen;
static unsigned long score_value;
static unsigned long JE_totalScore(struct player_stub *p) { (void)p; return score_value; }
static void read_encrypted_pascal_string(char *out,size_t size,FILE *stream)
{
    static const unsigned char key[]={204,129,63,255,71,19,25,62,1,99};
    unsigned char length,text[255];
    if(fread(&length,1,1,stream)!=1 || fread(text,1,length,stream)!=length) abort();
    for(size_t i=length;i-- > 0;){text[i]^=key[i%sizeof key];if(i)text[i]^=text[i-1];}
    if(size==0 || length>=size) abort();
    memcpy(out,text,length);out[length]=0;
}
extern void mt_srand(unsigned long);
extern unsigned long mt_rand(void);
static void JE_wipeKey(void){puts("wipe");}
static void JE_displayText(void){puts("display");}
static void fade_black(int steps){printf("fadeblack:%d\n",steps);}
static void JE_nextEpisode(void){puts("next");}
static void JE_loadPic(void *screen,int number,bool preserve){(void)screen;(void)preserve;printf("picture:%d\n",number);}
static void JE_clr256(void *screen){(void)screen;puts("clear");}
static int JE_fontCenter(const char *text,int font){printf("center:%s,%d\n",text,font);return 100;}
static void JE_dString(void *screen,int x,int y,const char *text,int font)
{(void)screen;printf("draw:%d,%d,%s,%d\n",x,y,text,font);}
static void blit_sprite2x2(void *screen,int x,int y,int sheet,int graphic)
{(void)screen;(void)sheet;printf("ship:%d,%d,%d\n",x,y,graphic);}
static void JE_showVGA(void){puts("present");}
static int colors[256];
static void fade_palette(int *palette,int steps,int first,int last)
{(void)palette;printf("palette:%d,%d,%d\n",steps,first,last);}
static void wait_input(bool events,bool keyboard,bool mouse)
{printf("wait:%d,%d,%d\n",events,keyboard,mouse);}
static void JE_readTextSync(void){puts("sync");}
static void dump_lines(void)
{
    for(int row=0;row<10;row++){
        if(row)putchar(',');
        for(const unsigned char *p=(const unsigned char *)levelWarningText[row];*p;p++)
            printf("%02X",*p);
    }
}
static void encrypt_write(FILE *stream,const unsigned char *text,size_t length)
{
    static const unsigned char key[]={204,129,63,255,71,19,25,62,1,99};
    unsigned char encrypted[255],n=(unsigned char)length,previous=0;
    fwrite(&n,1,1,stream);
    for(size_t i=0;i<length;i++){encrypted[i]=text[i]^key[i%sizeof key]^previous;previous=encrypted[i];}
    fwrite(encrypted,1,length,stream);
}
""" + function(source, "show_episode_completion") + r"""
int main(void)
{
    for(int i=0;i<64;i++)snprintf(miscText[i],sizeof miscText[i],"Cash%d",i);
    strcpy(miscText[37],"Score");strcpy(miscText[40],"Player One");strcpy(miscText[41],"Player Two");
    strcpy(miscTextB[4],"Engage");strcpy(pName[0],"Ace");
    for(int i=0;i<10;i++)snprintf(specialName[i],sizeof specialName[i],"Code%d",i);
    for(int i=0;i<10;i++)snprintf(superShips[i],sizeof superShips[i],"Ship%d",i);
    for(int i=0;i<16;i++)ships[i].shipgraphic=(unsigned short)(50+i);
    unsigned char picture[320*200];
    for(scenario=0;scenario<48;scenario++){
        unsigned int seed=(unsigned int)(1100+scenario*13);
        secretHint=scenario%3+1;twoPlayerMode=(scenario&1)!=0;
        constantPlay=(scenario&2)!=0;isNetworkGame=(scenario&4)!=0;
        jumpBackToEpisode1=(scenario&8)!=0;superTyrian=(scenario&16)!=0;
        initialDifficulty=(scenario&32)?4:2;superArcadeMode=scenario%11;
        ESCPressed=true;trentWin=false;jumpSection=false;mainLevel=3;frameCountMax=9;
        levelWarningLines=0;memset(levelWarningText,0,sizeof levelWarningText);
        player[0].cash=(unsigned long)(9000+scenario);player[1].cash=(unsigned long)(7000+scenario);
        score_value=(unsigned long)(123456+scenario);mt_srand(seed);
        FILE *episode=tmpfile();if(!episode)abort();
        for(int section=0;section<12;section++){
            char line[32];snprintf(line,sizeof line,"Hint%d",section);
            encrypt_write(episode,(const unsigned char *)line,strlen(line));
            encrypt_write(episode,(const unsigned char *)"#",1);
        }
        rewind(episode);
        char section[256],buffer[256];
        printf("case:%d,%u,%d,%d,%d,%d,%d,%d,%d,%d\n",scenario,seed,secretHint,
            twoPlayerMode,constantPlay,isNetworkGame,jumpBackToEpisode1,superTyrian,
            initialDifficulty,superArcadeMode);
        bool result=show_episode_completion(episode,section,buffer);
        printf("state:%d,%d,%u,%u,%d,%d,%d,%d,%ld,",result,ESCPressed,frameCountMax,
            levelWarningLines,jumpSection,trentWin,superArcadeMode,mainLevel,ftell(episode));
        dump_lines();putchar('\n');fclose(episode);
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
