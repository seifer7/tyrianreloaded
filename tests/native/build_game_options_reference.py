"""Compile unchanged game command-line processing with native parser and recorded state."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "params.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include[^\n]*\n', '', source, flags=re.MULTILINE)
    models = (root / "src" / "arg_parse.h").read_text(encoding="utf-8")
    models = models[models.index("typedef struct"):models.index("#endif")]
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include <assert.h>
typedef bool JE_boolean;
typedef unsigned int uint;
typedef uint16_t Uint16;
#define COUNTOF(x) (sizeof(x)/sizeof((x)[0]))
static bool audio_disabled,ignore_joystick,xmas,isNetworkGame,record_demo;
static const char *custom_data_dir;
static char *network_opponent_host,*network_player_name="";
static Uint16 network_player_port=1333,network_opponent_port=1333;
static unsigned int thisPlayerNum;
static int network_delay=2;
static size_t SDL_strlcpy(char *out,const char *in,size_t size) {
    size_t length=strlen(in),copy=length<size?length:size-1;memcpy(out,in,copy);out[copy]=0;return length;
}
""" + models + source + r"""
int main(int argc,char **argv) {
    argv[0]="OpenTyrian";JE_paramCheck(argc,argv);
    printf("flags:%d,%d,%d,%d,%d,%d,%d,%d\n",audio_disabled,ignore_joystick,xmas,
        constantPlay,constantDie,record_demo,richMode,isNetworkGame);
    printf("net:%u,%u,%u,%d\n",network_player_port,network_opponent_port,thisPlayerNum,network_delay);
    printf("data:%s\nhost:%s\nname:%s\n",custom_data_dir?custom_data_dir:"<null>",
        network_opponent_host?network_opponent_host:"<null>",network_player_name);
    for(int i=0;i<argc;i++)printf("arg:%s\n",argv[i]);
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
