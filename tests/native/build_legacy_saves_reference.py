"""Run unchanged DOS save cipher, item layout and legacy record serialization regions."""

import argparse
from pathlib import Path


def build_source(root):
    config = (root / "src" / "config.c").read_text(encoding="utf-8")
    header = (root / "src" / "config.h").read_text(encoding="utf-8")
    model = header[header.index("typedef struct\n{"):header.index("typedef struct {")]
    cipher = config[config.index("void JE_encryptSaveTemp("):config.index("const char *get_user_directory(")]
    items = config[config.index("static void playeritems_to_pitems("):config.index("bool saveGameOperation(")]
    xor = config[config.index("char* xor ("):config.index("bool load_opentyrian_config(")]
    save_game = config[config.index("void JE_saveGame("):config.index("bool read_savegame_metadata(")]
    load_game = config[config.index("void JE_loadGame("):config.index("void JE_initProcessorType(")]
    write = config[config.index("\tp = saveTemp;", config.index("void JE_saveConfiguration(")):config.index("\tJE_encryptSaveTemp();", config.index("void JE_saveConfiguration("))]
    read = config[config.index("\t\tp = saveTemp;", config.index("void JE_loadConfiguration(")):config.index("\n\t\tfclose(fi);", config.index("\t\tp = saveTemp;", config.index("void JE_loadConfiguration(")))]
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t JE_byte,Uint8;
typedef uint16_t JE_word;
typedef int32_t JE_longint;
typedef int8_t JE_shortint;
typedef char JE_char;
typedef bool JE_boolean;
typedef unsigned int uint;
typedef JE_byte JE_PItemsType[12];
typedef JE_byte JE_SaveGameTemp[2502];
#define SAVE_FILE_SIZE 2498
#define SIZEOF_SAVEGAMETEMP 2502
#define SAVE_FILES_NUM 22
#define FRONT_WEAPON 0
#define REAR_WEAPON 1
#define LEFT_SIDEKICK 0
#define RIGHT_SIDEKICK 1
#define EPISODE_AVAILABLE 4
#define SA_NONE 0
#define SA_NORTSHIPZ 7
#define SA_SUPERTYRIAN 254
#define SA_ARCADE 255
#define SDL_SwapLE16(x) (x)
#define SDL_SwapLE32(x) (x)
static const JE_byte cryptKey[10]={15,50,89,240,147,34,86,9,32,208};
typedef struct {
    struct {JE_byte id,power;} weapon[2];
    JE_byte super_arcade_mode,sidekick[2],generator,sidekick_level,sidekick_series,shield,special,ship;
} PlayerItems;
""" + model + r"""
static JE_SaveGameTemp saveTemp;
static JE_SaveFileType saveFiles[22];
static JE_word editorLevel;
static struct {PlayerItems items,last_items;unsigned long cash;} player[2];
static bool superTyrian,onePlayerAction,twoPlayerMode,extraGame,galagaMode,gameHasRepeated;
static JE_byte superArcadeMode,saveLevel,mainLevel,episodeNum,initial_episode_num,secretHint,inputDevice[2],temp;
static JE_shortint initialDifficulty,difficultyLevel;
static JE_word lastCubeMax,cubeMax;
static char lastLevelName[11],levelName[11];
static void serialize(void);
static void hex(const void *,int);
static void JE_encryptSaveTemp(void);
static void JE_decryptSaveTemp(void);
static void JE_saveConfiguration(void) {
    serialize();JE_encryptSaveTemp();printf("game-save:");hex(saveTemp,2502);JE_decryptSaveTemp();
}
static void JE_initEpisode(int episode) {printf("game-episode:%d\n",episode);episodeNum=(JE_byte)episode;}
""" + cipher + items + xor + r"""
static void serialize(void) {
    JE_byte *p;int z;
""" + write + r"""
}
static void deserialize(void) {
    JE_byte *p;int z;
""" + read + r"""
}
static void hex(const void *data,int size) {
    const unsigned char *bytes=data;for(int i=0;i<size;i++)printf("%02X",bytes[i]);puts("");
}
""" + save_game + load_game + r"""
int main(int argc,char **argv) {
    if(argc==2) {
        for(int i=0;i<2498;i++)saveTemp[i]=(JE_byte)(i*37+i/3);
        JE_encryptSaveTemp();
        int index=atoi(argv[1]);saveTemp[index]^=1;JE_decryptSaveTemp();return 0;
    }
    for(int mode=0;mode<32;mode++) {
        for(int i=0;i<2498;i++)saveTemp[i]=(JE_byte)(mode==0?0:mode==1?255:i*(mode*17+1)+i/(mode+1));
        JE_encryptSaveTemp();printf("cipher:%d:",mode);hex(saveTemp,2502);
        JE_decryptSaveTemp();printf("plain:%d:",mode);hex(saveTemp,2498);
    }
    for(int mode=0;mode<256;mode++) {
        PlayerItems items;memset(&items,0,sizeof(items));
        JE_PItemsType bytes;
        for(int i=0;i<12;i++)bytes[i]=(JE_byte)(mode*17+i*31);
        items.weapon[0].power=3;items.weapon[1].power=11;
        JE_byte episode=0;
        pitems_to_playeritems(&items,bytes,&episode);
        playeritems_to_pitems(bytes,&items,episode);
        printf("items:%d:",mode);hex(bytes,12);
        printf("power:%d,%d\n",items.weapon[0].power,items.weapon[1].power);
        episode=99;pitems_to_playeritems(&items,bytes,NULL);
        playeritems_to_pitems(bytes,&items,episode);
        printf("null-episode:%d:",mode);hex(bytes,12);
    }
    for(int mode=0;mode<16;mode++) {
        memset(saveTemp,0,sizeof(saveTemp));memset(saveFiles,0,sizeof(saveFiles));
        for(int z=0;z<22;z++) {
            JE_SaveFileType *s=&saveFiles[z];
            s->encode=(JE_word)(mode*1001+z);s->level=(JE_word)(mode*100+z);
            for(int i=0;i<12;i++){s->items[i]=(JE_byte)(mode+z+i);s->lastItems[i]=(JE_byte)(mode*17+z+i*5);}
            s->score=mode*1234567+z;s->score2=-mode*7654321-z;
            int length=(mode+z)%10;memset(s->levelName,'A'+z%26,length);
            for(int i=0;i<14;i++)s->name[i]=(char)(mode*23+z+i);
            s->cubes=(JE_byte)(mode+z);s->power[0]=(JE_byte)z;s->power[1]=(JE_byte)mode;
            s->episode=(JE_byte)(z%4+1);s->difficulty=(JE_byte)mode;s->secretHint=(JE_byte)z;
            s->input1=1;s->input2=2;s->gameHasRepeated=z%2;s->initialDifficulty=3;
            s->highScore1=1000*z;s->highScore2=-2000*z;
            memset(s->highScoreName,'Z'-z%26,(mode*3+z)%30);s->highScoreDiff=(JE_byte)(mode+z);
        }
        for(int i=0;i<100;i++)saveTemp[2398+i]=(JE_byte)(mode*17+i);
        editorLevel=(JE_word)(mode*1703+800);
        serialize();
        JE_encryptSaveTemp();printf("archive:%d:",mode);hex(saveTemp,2502);
        JE_decryptSaveTemp();deserialize();
        printf("editor:%d,%u\n",mode,editorLevel);
        for(int z=0;z<22;z++)printf("slot:%d,%d,%u,%u,%d,%d,%s,%s,%u\n",
            mode,z,saveFiles[z].encode,saveFiles[z].level,saveFiles[z].score,saveFiles[z].score2,
            saveFiles[z].levelName,saveFiles[z].highScoreName,saveFiles[z].gameHasRepeated);
    }
    const char *keys[]={"TYRIANRELOADED","ABCD","x"};
    for(int mode=0;mode<12;mode++) {
        char bytes[33]={0};for(int i=0;i<32;i++)bytes[i]=(char)(mode*17+i+1);
        xor(bytes,keys[mode%3]);printf("xor:%d:",mode);hex(bytes,33);
    }
    for(int mode=0;mode<8;mode++)for(int slot=1;slot<=22;slot++) {
        printf("game:%d,%d\n",mode,slot);
        memset(saveFiles,0,sizeof(saveFiles));memset(saveTemp,0,sizeof(saveTemp));memset(player,0,sizeof(player));
        editorLevel=800;
        memset(saveFiles[slot-1].name,'Z',14);saveFiles[slot-1].name[14]=0;
        initialDifficulty=-3;difficultyLevel=-2;gameHasRepeated=mode%2;
        saveLevel=(JE_byte)(200+mode);episodeNum=(JE_byte)(mode%4+1);initial_episode_num=3;
        lastCubeMax=300;secretHint=7;inputDevice[0]=1;inputDevice[1]=2;
        twoPlayerMode=mode&1;superTyrian=mode&2;onePlayerAction=mode&4;
        superArcadeMode=mode==3?7:mode==7?9:0;extraGame=galagaMode=true;
        memset(lastLevelName,0,11);memset(levelName,0,11);
        strcpy(lastLevelName,mode%2?"Completed":"LevelA");
        for(int p=0;p<2;p++) {
            JE_PItemsType bytes;for(int i=0;i<12;i++)bytes[i]=(JE_byte)(mode*13+p*19+i);
            pitems_to_playeritems(&player[p].items,bytes,NULL);
            pitems_to_playeritems(&player[p].last_items,bytes,NULL);
            player[p].items.weapon[0].power=(JE_byte)((mode+p)%12);
            player[p].items.weapon[1].power=(JE_byte)((mode+p+3)%12);
            player[p].last_items.weapon[0].power=9;player[p].last_items.weapon[1].power=10;
            player[p].cash=0xF0000000u+mode*123+p;
        }
        JE_saveGame((JE_byte)slot,mode%2?"ABCDEFGHIJKLMN":"Hi");
        saveFiles[slot-1].level+=256;
        JE_loadGame((JE_byte)slot);
        printf("game-state:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%s,%s\n",
            superTyrian,onePlayerAction,superArcadeMode,twoPlayerMode,extraGame,galagaMode,
            initialDifficulty,difficultyLevel,gameHasRepeated,initial_episode_num,
            mainLevel,saveLevel,cubeMax,lastCubeMax,secretHint,inputDevice[0],inputDevice[1],episodeNum,
            player[1].items.sidekick_level,lastLevelName,levelName);
        for(int p=0;p<2;p++) {
            JE_PItemsType bytes;
            playeritems_to_pitems(bytes,&player[p].items,0);
            printf("game-player:%d,%lu,%d,%d:",p,player[p].cash,player[p].items.weapon[0].power,player[p].items.weapon[1].power);hex(bytes,12);
            playeritems_to_pitems(bytes,&player[p].last_items,0);
            printf("game-last:%d,%d,%d:",p,player[p].last_items.weapon[0].power,player[p].last_items.weapon[1].power);hex(bytes,12);
        }
    }
    return 0;
}
"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
