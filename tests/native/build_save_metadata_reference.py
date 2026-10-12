"""Compile unchanged save metadata/list functions with real tinydir enumeration."""

import argparse
from pathlib import Path


def build_source(root):
    config = (root / "src" / "config.c").read_text(encoding="utf-8")
    header = (root / "src" / "config.h").read_text(encoding="utf-8")
    model = header[header.index("typedef struct {"):header.index("typedef JE_SaveFileType")]
    functions = config[config.index("bool read_savegame_metadata("):config.index("void JE_loadGame(")]
    return r"""
#define _DEFAULT_SOURCE 1
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "tinydir.h"
#ifndef _WIN32
typedef char TCHAR;
#endif
#ifndef min
#define min(a,b) ((a)<(b)?(a):(b))
#endif
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int8_t JE_shortint;
typedef bool JE_boolean;
static JE_byte temp;
""" + model + r"""
static const char *get_user_directory(void) {return ".";}
static FILE *dir_fopen(const char *directory,const char *name,const char *mode) {(void)directory;return fopen(name,mode);}
static void fread_die(void *data,size_t size,size_t count,FILE *file) {if(fread(data,size,count,file)!=count)exit(3);}
static void fread_u16_die(JE_word *data,size_t count,FILE *file) {fread_die(data,2,count,file);}
static void fread_u8_die(JE_byte *data,size_t count,FILE *file) {fread_die(data,1,count,file);}
""" + functions + r"""
static void hex(const char *text) {for(size_t i=0;i<strlen(text);i++)printf("%02X",(unsigned char)text[i]);}
static void print_metadata(SavedGame *save) {
    printf("metadata:");hex(save->name);putchar(',');hex(save->levelName);
    printf(",%u,%u,%u\n",save->gameMode,save->twoPlayerMode,save->episode);
}
int main(int argc,char **argv) {
    if(argc==3 && !strcmp(argv[1],"list")) {
        SavedGame *list=build_save_game_list((JE_byte)atoi(argv[2]));
        printf("count:%d\n",list->count);
        for(int i=0;i<list->count;i++)print_metadata(list+i);
        free(list);return 0;
    }
    if(argc!=2)return 2;
    SavedGame metadata={0};
    bool success=read_savegame_metadata(argv[1],&metadata);
    printf("success:%d\n",success);
    if(success)print_metadata(&metadata);
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
