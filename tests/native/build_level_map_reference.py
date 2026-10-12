"""Compile unchanged binary level header, shapes, maps, and selected-level loader."""

import argparse
from pathlib import Path


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    header = (root / "src" / "varz.h").read_text(encoding="utf-8")
    structures = header[header.index("typedef JE_byte JE_DanCShape"):header.index("\n};", header.index("struct JE_MegaDataType3")) + 4]
    loaders = source[source.index("static void read_level_header_and_events("):source.index("static void play_map_transition(")]
    selected = source[source.index("static void load_selected_level_map("):source.index("static void configure_episode_mode(")]
    return r"""
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <stdbool.h>
#include <string.h>
#include <ctype.h>
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int16_t JE_integer;
typedef int8_t JE_shortint;
typedef char JE_char;
typedef bool JE_boolean;
#define COUNTOF(a) (sizeof(a)/sizeof((a)[0]))
static uint16_t SDL_Swap16(uint16_t value) {return (value >> 8) | (value << 8);}
static void fread_die(void *buffer,size_t size,size_t count,FILE *file) {
    if(fread(buffer,size,count,file)!=count){fputs("truncated level\n",stderr);exit(3);}
}
static void fread_u16_die(JE_word *buffer,size_t count,FILE *file) {fread_die(buffer,2,count,file);}
static void fread_s16_die(JE_integer *buffer,size_t count,FILE *file) {fread_die(buffer,2,count,file);}
static void fread_u8_die(JE_byte *buffer,size_t count,FILE *file) {fread_die(buffer,1,count,file);}
static void fread_s8_die(JE_shortint *buffer,size_t count,FILE *file) {fread_die(buffer,1,count,file);}
static void fread_bool_die(JE_boolean *buffer,FILE *file) {JE_byte value;fread_u8_die(&value,1,file);*buffer=value!=0;}
static const char *directory;
static const char *data_dir(void) {return directory;}
static FILE *dir_fopen_die(const char *dir,const char *name,const char *mode) {
    char path[4096];snprintf(path,sizeof path,"%s/%s",dir,name);FILE *file=fopen(path,mode);
    if(!file){fprintf(stderr,"cannot open %s\n",path);exit(4);}return file;
}
""" + structures + r"""
static struct JE_MegaDataType1 megaData1;
static struct JE_MegaDataType2 megaData2;
static struct JE_MegaDataType3 megaData3;
static JE_word mapX,mapX2,mapX3,levelEnemyMax,levelEnemy[40],maxEvent;
static struct JE_EventRecType eventRec[2500];
static char tempStr[31],levelFile[]="tyrian1.lvl";
static int32_t lvlPos[43];
static JE_byte lvlFileNum;
""" + loaders + selected + r"""
static uint32_t hash(const JE_byte *data,size_t length) {
    uint32_t result=2166136261u;for(size_t i=0;i<length;i++)result=(result^data[i])*16777619u;return result;
}
int main(int argc,char **argv) {
    if(argc!=3)return 2;directory=argv[1];lvlFileNum=atoi(argv[2]);
    FILE *archive=dir_fopen_die(directory,levelFile,"rb");JE_word count;
    fread_u16_die(&count,1,archive);fread_die(lvlPos,4,count,archive);fclose(archive);
    JE_byte *refs[3][128]={{0}};load_selected_level_map(refs);
    printf("header:%u,%u,%u,%u,%u\n",mapX,mapX2,mapX3,levelEnemyMax,maxEvent);
    printf("enemies:");for(int i=0;i<levelEnemyMax;i++)printf("%s%u",i?",":"",levelEnemy[i]);putchar('\n');
    for(int i=0;i<=maxEvent;i++) {
        struct JE_EventRecType *e=&eventRec[i];
        printf("event:%u,%u,%d,%d,%d,%u,%d,%d\n",e->eventtime,e->eventtype,e->eventdat,e->eventdat2,e->eventdat3,e->eventdat4,e->eventdat5,e->eventdat6);
    }
    for(int layer=0;layer<3;layer++)for(int i=0;i<72;i++) {
        JE_byte fill=layer==0||!refs[layer][i]?0:layer==1?megaData2.shapes[i].fill:megaData3.shapes[i].fill;
        printf("shape:%d,%d,%u,%u\n",layer,i,refs[layer][i]?hash(refs[layer][i],672):0,fill);
    }
    JE_byte **maps[]={(JE_byte **)megaData1.mainmap,(JE_byte **)megaData2.mainmap,(JE_byte **)megaData3.mainmap};
    const int lengths[]={4200,8400,9000};
    for(int layer=0;layer<3;layer++) {
        printf("map:%d",layer);
        for(int i=0;i<lengths[layer];i++) {
            int index=-1;for(int j=0;j<72;j++)if(maps[layer][i] && maps[layer][i]==refs[layer][j]){index=j;break;}
            printf(",%d",index);
        }putchar('\n');
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
