"""Trace the unchanged native episode presentation command dispatcher."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static bool handle_episode_presentation(", "bool handle_episode_presentation(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t Uint8;
typedef FILE JE_FILE;
static bool isNetworkGame, constantPlay, completionResult;
static int episodePosition;
static void show_episode_picture(const char *section) { printf("picture:%s,%d\n",section,constantPlay); }
static void play_map_transition(const char *section, Uint8 picture[320*200]) {
    (void)picture; printf("transition:%s,%d\n",section,constantPlay);
}
static void show_episode_warning(FILE *episode, char *section) {
    (void)episode; printf("warning:%d,%s,%d\n",episodePosition,section,constantPlay);
}
static bool show_episode_completion(FILE *episode, char *section, char buffer[256]) {
    (void)episode; printf("completion:%d,%s,256\n",episodePosition,section);
    return completionResult;
}
static void fade_black(int steps) { printf("fadeblack:%d\n",steps); }
static void fade_white(int steps) { printf("fadewhite:%d\n",steps); }
static void JE_clr256(void *screen) { (void)screen; puts("clear"); }
static void JE_showVGA(void) { puts("present"); }
static uint8_t colors[256], palettes[8][256];
static uint8_t screen;
static uint8_t *VGAScreen=&screen;
static void set_palette(uint8_t *palette,int first,int last) {
    (void)palette; (void)first; (void)last; puts("palette:7");
}
""" + function(source, "handle_episode_presentation") + r"""
int main(void)
{
    const char commands[]={'P','U','V','R','C','B','F','Q','W','?'};
    char section[64],buffer[256];
    uint8_t picture[320*200];
    int scenario=0;
    for(unsigned int i=0;i<sizeof(commands);i++)
    for(int network=0;network<2;network++)
    for(int constant=0;constant<2;constant++,scenario++) {
        FILE *episode=tmpfile();
        fputc(1,episode); fputc(2,episode); fputc(3,episode); rewind(episode);
        episodePosition=0;
        snprintf(section,sizeof section,"X%c?17",commands[i]);
        isNetworkGame=network!=0; constantPlay=constant!=0;
        completionResult=(scenario&1)!=0;
        printf("case:%d,%c,%d,%d\n",scenario,commands[i],network,constant);
        bool result=handle_episode_presentation(commands[i],episode,section,buffer,picture);
        printf("state:%d,%ld\n",result,ftell(episode));
        fclose(episode);
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
