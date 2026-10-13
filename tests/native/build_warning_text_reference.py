"""Trace original warning text composition and disabled synchronization."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <setjmp.h>
#define TINY_FONT 2
#define VGAScreen NULL
#define NETWORK_KEEP_ALIVE() keep_alive()
static unsigned int frameCountMax;
static uint16_t temp,tempY,tempW;
static unsigned int textGlowFont,mode,draws,waits,buttons;
static bool warningRed,ESCPressed,haltGame,levelWarningDisplay;
static uint8_t levelWarningLines;
static char levelWarningText[10][61];
static const char *miscText[5]={"","","","","Continue?"};
static jmp_buf halted;
static void JE_outCharGlow(uint16_t x,uint16_t y,const char *t){
    printf("draw:%u,%u,%s\n",x,y,t);draws++;
    if((mode&32)&&draws==(mode>>8)%11+1)frameCountMax=0;
    if((mode&64)&&draws==1)ESCPressed=true;
}
static void JE_tyrianHalt(int code){printf("halt:%d\n",code);longjmp(halted,1);}
static int JE_fontCenter(const char *t,int f){printf("center:%s,%d\n",t,f);return 160-(int)strlen(t);}
static void JE_updateWarning(void *s){(void)s;puts("warning");}
static void setDelay(int frames){printf("delay:%d\n",frames);}
static void keep_alive(void){puts("keepalive");}
static void wait_delay(void){puts("wait");waits++;if((mode&128)&&waits==2)ESCPressed=true;}
static bool JE_anyButton(void){buttons++;bool value=buttons==mode%3+1;printf("button:%d\n",value);return value;}
""" + function(source, "JE_readTextSync") + function(source, "JE_displayText") + r"""
int main(void){
    for(mode=0;mode<2816;mode++){
        frameCountMax=(mode&4)?4:0;warningRed=mode&1;levelWarningDisplay=mode&2;haltGame=mode&8;
        ESCPressed=mode&16;draws=waits=buttons=0;textGlowFont=1;levelWarningLines=(uint8_t)(mode>>8);
        for(unsigned int i=0;i<levelWarningLines;i++)
            if(i%4==0)strcpy(levelWarningText[i],"");
            else snprintf(levelWarningText[i],61,"Warning %u",i);
        printf("case:%u\n",mode);
        bool terminated=setjmp(halted)!=0;
        if(!terminated){JE_readTextSync();JE_displayText();JE_readTextSync();}
        printf("state:%u,%u,%d,%d,%u,%u,%u,%d\n",frameCountMax,textGlowFont,levelWarningDisplay,ESCPressed,draws,waits,buttons,terminated);
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
