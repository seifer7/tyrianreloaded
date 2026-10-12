"""Run unchanged OpenTyrian configuration load/save with real SDL keyboard names."""

import argparse
from pathlib import Path


def build_source(root):
    source = (root / "src" / "config.c").read_text(encoding="utf-8")
    selected = source[source.index("bool load_opentyrian_config("):source.index("static void playeritems_to_pitems(")]
    return r"""
#define SDL_MAIN_HANDLED
#include <SDL.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>
#include "config_file.h"
#if !defined(_WIN32)
#include <unistd.h>
#endif
#define TARGET_WIN32
#define COUNTOF(a) (sizeof(a)/sizeof((a)[0]))
static const SDL_Scancode defaultKeySettings[8]={82,81,80,79,44,40,224,226};
static const char *keySettingNames[]={"up","down","left","right","fire","change fire","left sidekick","right sidekick"};
static SDL_Scancode keySettings[8];
static const struct {const char *name;} scalers[]={{"None"},{"2x"},{"Scale2x"},{"hq2x"},{"3x"},{"Scale3x"},{"hq3x"},{"4x"},{"hq4x"}};
static const char *scaling_mode_names[]={"Center","Integer","Fit 8:5","Fit 4:3"};
static int fullscreen_display,scaler,scaling_mode,shopContentSetting;
static Config opentyrian_config;
static FILE *input_file,*output_file;
static const char *get_user_directory(void){return ".";}
static FILE *dir_fopen_warn(const char *directory,const char *name,const char *mode) {(void)directory;(void)name;(void)mode;return input_file;}
static FILE *dir_fopen(const char *directory,const char *name,const char *mode) {
    (void)directory;(void)name;(void)mode;return output_file=tmpfile();
}
static int fixture_mkdir(const char *path) {(void)path;return 0;}
#define mkdir fixture_mkdir
static bool set_scaler_by_name(const char *name) {
    for(int i=0;i<9;i++)if(!strcmp(name,scalers[i].name)){scaler=i;return true;}return false;
}
static bool set_scaling_mode_by_name(const char *name) {
    for(int i=0;i<4;i++)if(!strcmp(name,scaling_mode_names[i])){scaling_mode=i;return true;}return false;
}
static int close_fixture(FILE *file) {if(file==output_file){fflush(file);rewind(file);return 0;}return fclose(file);}
#define fclose close_fixture
""" + selected + r"""
#undef fclose
int main(void) {
    const char *names[]={"Up","Down","Left","Right","Space","Return","Left Ctrl","Left Alt","A","Keypad Enter","invalid","","a"};
    for(int mode=0;mode<32;mode++) {
        config_init(&opentyrian_config);
        input_file=tmpfile();output_file=NULL;scaler=0;scaling_mode=3;
        if(!input_file)return 2;
        fprintf(input_file,"section \"video\"\n");
        if(mode%4)fprintf(input_file,"\titem \"fullscreen\" \"%d\"\n",mode-3);
        fprintf(input_file,"\titem \"scaler\" \"%s\"\n",mode%5?scalers[mode%9].name:"invalid");
        fprintf(input_file,"\titem \"scaling_mode\" \"%s\"\n",mode%3?scaling_mode_names[mode%4]:"invalid");
        fprintf(input_file,"section \"keyboard\"\n");
        for(int i=0;i<8;i++)fprintf(input_file,"\titem \"%s\" \"%s\"\n",keySettingNames[i],names[(mode+i)%13]);
        fprintf(input_file,"section \"gameplay\"\n\titem \"shop_content\" \"%s\"\n",mode%2?"0x15":"invalid");
        fprintf(input_file,"section \"joystick\" \"Synthetic pad\"\n\titem \"threshold\" \"3\"\n");
        rewind(input_file);
        bool result=load_opentyrian_config();
        printf("load:%d,%d,%d,%s,%s,%d",mode,result,fullscreen_display,scalers[scaler].name,scaling_mode_names[scaling_mode],shopContentSetting);
        for(int i=0;i<8;i++)printf(",%d",keySettings[i]);puts("");
        if(mode%2)keySettings[4]=SDL_SCANCODE_UNKNOWN;
        if(!save_opentyrian_config()||!output_file)return 2;
        printf("write:%d\n",mode);
        int c;while((c=fgetc(output_file))!=EOF)putchar(c);
        fclose(output_file);config_deinit(&opentyrian_config);
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
