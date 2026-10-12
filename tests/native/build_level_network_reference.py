"""Trace unchanged gameplay network state/request processing with wire fixtures."""

import argparse
from pathlib import Path


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source[source.index("static void update_level_network_state("):source.index("static void update_level_end_state(")]
    source = source[:source.rindex("#endif")]
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
typedef uint16_t Uint16,JE_word;
typedef struct {uint8_t data[28];} Packet;
static Packet local,remote;
static Packet *packet_state_out[16],*packet_state_in[16];
static int network_delay=1,mode;
static bool isNetworkGame,reallyEndLevel,pauseRequest,inGameMenuRequest,skipLevelRequest,nortShipRequest,helpRequest,yourInGameMenuRequest;
static bool haltGame,levelTimer,endLevel;
static JE_word levelTimerCountdown,curLoc;
static uint8_t levelEnd;
static int8_t difficultyLevel;
static int game_screen;
static struct {int x,y;struct {uint8_t ship,special;struct {uint8_t id;} weapon[2];} items;} player[2];
enum {ITEM_SHIP_NORTSHIP,ITEM_SPECIAL_ASTRALZONE,ITEM_WEAPONFRONT_NORTSHIPSUPERPULSE,ITEM_WEAPONREAR_NORTSHIPSPREADER};
static uint8_t ITEM_INDEX_STORE[4];
#define FRONT_WEAPON 0
#define REAR_WEAPON 1
#define FULL_SHADE 1
static Uint16 SDLNet_Read16(const void *p) {const uint8_t *b=p;return (b[0]<<8)|b[1];}
static void SDLNet_Write16(Uint16 v,void *p) {uint8_t *b=p;b[0]=v>>8;b[1]=v;}
static void network_state_send(void) {
    printf("send:");for(int i=0;i<28;i++)printf("%02X",local.data[i]);puts("");
    packet_state_out[1]=&local;memcpy(&remote,&local,sizeof local);
    SDLNet_Write16(mode%16,remote.data+14);
    int offset=(mode/256)%5;
    if(offset<4)SDLNet_Write16(SDLNet_Read16(remote.data+18+offset*2)+1,remote.data+18+offset*2);
}
static bool network_state_update(void) {return (mode&128)==0;}
static void JE_pauseGame(void) {puts("pause");}
static void JE_doInGameSetup(void) {printf("menu:%d\n",yourInGameMenuRequest);haltGame=(mode&64)!=0;}
static void JE_textShade(int screen,int x,int y,const char *text,int hue,int brightness,int shade) {
    (void)screen;printf("text:%d,%d,%s,%d,%d,%d\n",x,y,text,hue,brightness,shade);
}
static void JE_clearSpecialRequests(void) {pauseRequest=inGameMenuRequest=skipLevelRequest=helpRequest=nortShipRequest=false;}
""" + source + r"""
int main(int argc,char **argv) {
    if(argc!=5)return 2;for(int i=0;i<4;i++)ITEM_INDEX_STORE[i]=(uint8_t)atoi(argv[i+1]);
    for(mode=0;mode<5120;mode++) {
        int localRequests=(mode/16)%16;
        pauseRequest=localRequests&1;inGameMenuRequest=localRequests&2;skipLevelRequest=localRequests&4;nortShipRequest=localRequests&8;
        helpRequest=true;yourInGameMenuRequest=false;haltGame=false;
        isNetworkGame=(mode/1280)%2==0;reallyEndLevel=(mode/2560)!=0;
        levelTimer=false;levelTimerCountdown=123;endLevel=false;levelEnd=17;
        difficultyLevel=-3;curLoc=54321;memset(player,0,sizeof player);
        player[0].x=-7;player[1].x=65537;player[0].y=456;player[1].y=-123;
        memset(&local,0,sizeof local);SDLNet_Write16(0x41,local.data);
        packet_state_out[0]=&local;packet_state_in[0]=&remote;
        JE_word shipGr=99;
        printf("mode:%d\n",mode);
        update_level_network_state(&shipGr);
        printf("state:%d,%d,%u,%u,%d,%u,%u,%u,%u,%u\n",reallyEndLevel,levelTimer,levelTimerCountdown,levelEnd,endLevel,shipGr,
            player[0].items.ship,player[0].items.special,player[0].items.weapon[0].id,player[0].items.weapon[1].id);
        printf("requests:%d,%d,%d,%d,%d,%d\n",pauseRequest,inGameMenuRequest,skipLevelRequest,helpRequest,nortShipRequest,yourInGameMenuRequest);
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
