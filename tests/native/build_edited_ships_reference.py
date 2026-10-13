"""Verify unchanged custom ship archive reads and four-checksum decryption."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "editship.c").read_text(encoding="utf-8")
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef bool JE_boolean;
typedef unsigned int uint;
typedef JE_byte JE_ShipsType[154];
#define SAS (sizeof(JE_ShipsType)-4)
static const JE_byte extraCryptKey[10]={58,23,16,192,254,82,113,147,62,99};
static JE_boolean extraAvail;
static JE_ShipsType extraShips;
static void *extraShapes;
static JE_word extraShapeSize;
static const char *path;
static const char *get_user_directory(void){return "";}
static FILE *dir_fopen(const char *d,const char *n,const char *mode){return fopen(path,mode);}
static long ftell_eof(FILE *f){long old=ftell(f);fseek(f,0,SEEK_END);long n=ftell(f);fseek(f,old,SEEK_SET);return n;}
static void fread_die(void *p,size_t size,size_t count,FILE *f){if(fread(p,size,count,f)!=count)exit(1);}
""" + function(source, "JE_decryptShips") + function(source, "JE_loadExtraShapes") + r"""
int main(int argc,char **argv){
    if(argc!=2)return 2;path=argv[1];JE_loadExtraShapes();
    printf("archive:%d,%u\n",extraAvail,extraShapeSize);
    if(extraAvail){uint32_t hash=2166136261u;uint8_t *s=extraShapes;
        for(unsigned int i=0;i<extraShapeSize;i++)hash=(hash^s[i])*16777619u;
        printf("shapes:%u\n",hash);printf("records");
        for(unsigned int i=0;i<SAS;i++)printf(",%u",extraShips[i]);puts("");}
    free(extraShapes);return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
