"""Run complete unchanged legacy configuration load/save with isolated stream fixtures."""

import argparse
from pathlib import Path
import build_legacy_saves_reference


def build_source(root):
    config = (root / "src" / "config.c").read_text(encoding="utf-8")
    prefix = build_legacy_saves_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    prefix = prefix.replace(
        'static void JE_saveConfiguration(void) {\n    serialize();JE_encryptSaveTemp();printf("game-save:");hex(saveTemp,2502);JE_decryptSaveTemp();\n}',
        'static void JE_saveConfiguration(void);')
    constants = config[config.index("const char defaultHighScoreNames"):config.index("JE_boolean smoothies")]
    directory = config[config.index("const char *get_user_directory("):config.index("// for compatibility")]
    runtime = config[config.index("void JE_initProcessorType("):config.index("void JE_encryptSaveTemp(")]
    return r"""
#if defined(_WIN32)
#define TARGET_WIN32
#else
#include <unistd.h>
#endif
#include <stdarg.h>
#include <assert.h>
#include "mtrand.h"
""" + prefix + r"""
typedef JE_byte JE_EditorItemAvailType[100];
typedef JE_byte DosKeySettings[8];
typedef uint16_t Uint16;
#define SAVE_FILES_SIZE 2398
static const DosKeySettings defaultDosKeySettings={72,80,75,77,57,28,29,56};
static DosKeySettings dosKeySettings;
static bool background2,wild,superWild,smoothScroll,explosionTransparent,filtrationAvail,displayScore,pentiumMode;
static JE_byte gameSpeed,inputDevice_,jConfigure,versionNum,processorType,midiPort,soundEffects,gammaCorrection,fastPlay;
static JE_byte joyButtonAssign[4]={1,4,5,5};
static JE_word tyrMusicVolume,fxVolume;
static uint frameCountMax;
static void setDelaySpeed(Uint16 speed) {(void)speed;}
static void setDelay(int delay) {(void)delay;}
""" + constants + directory + r"""
static void set_volume(JE_word music,JE_word effects) {printf("volume:%u,%u\n",music,effects);}
static bool load_opentyrian_config(void) {return true;}
static bool save_opentyrian_config(void) {return true;}
static int current_mode;
static JE_byte stored_save[2502];
static FILE *output_cfg,*output_save;
static long ftell_eof(FILE *file) {long position=ftell(file);fseek(file,0,SEEK_END);long size=ftell(file);fseek(file,position,SEEK_SET);return size;}
static FILE *dir_fopen_warn(const char *directory,const char *name,const char *mode) {
    (void)directory;
    if(!strcmp(mode,"wb"))return !strcmp(name,"tyrian.cfg")?(output_cfg=tmpfile()):(output_save=tmpfile());
    if(!strcmp(name,"tyrian.cfg")) {
        if(current_mode%3==0)return NULL;
        FILE *file=tmpfile();if(!file)exit(2);
        int size=current_mode%3==1?28:27;
        for(int i=0;i<size;i++) {
            int value=(current_mode*13+i*31)&255;
            if(i==0)value=current_mode%2;
            if(i==1||i==5)value=current_mode;
            if(current_mode==4&&i>=14&&i<18)value=i==14?23:i==16?255:0;
            fputc(value,file);
        }
        rewind(file);return file;
    }
    if(!(current_mode&4))return NULL;
    FILE *file=tmpfile();if(!file)exit(2);fwrite(stored_save,1,2502,file);rewind(file);return file;
}
static int close_fixture(FILE *file) {
    if(file==output_cfg||file==output_save){fflush(file);rewind(file);return 0;}return fclose(file);
}
#define fclose close_fixture
static int fixture_mkdir(const char *path,...) {(void)path;return 0;}
#define mkdir fixture_mkdir
static void fread_die(void *data,size_t size,size_t count,FILE *file) {if(fread(data,size,count,file)!=count)exit(3);}
static void fwrite_die(const void *data,size_t size,size_t count,FILE *file) {if(fwrite(data,size,count,file)!=count)exit(3);}
static void fread_u8_die(JE_byte *data,size_t count,FILE *file) {fread_die(data,1,count,file);}
static void fread_s8_die(JE_shortint *data,size_t count,FILE *file) {fread_die(data,1,count,file);}
static void fread_u16_die(JE_word *data,size_t count,FILE *file) {fread_die(data,2,count,file);}
static void fread_bool_die(bool *data,FILE *file) {int byte=fgetc(file);if(byte==EOF)exit(3);*data=byte!=0;}
static void fwrite_u8_die(const JE_byte *data,size_t count,FILE *file) {fwrite_die(data,1,count,file);}
static void fwrite_s8_die(const JE_shortint *data,size_t count,FILE *file) {fwrite_die(data,1,count,file);}
static void fwrite_u16_die(const JE_word *data,FILE *file) {fwrite_die(data,2,1,file);}
static void fwrite_bool_die(const bool *data,FILE *file) {fputc(*data?1:0,file);}
""" + runtime + config[config.index("void JE_loadConfiguration("):] + r"""
#undef fclose
int main(int argc,char **argv) {
    (void)argv;
    if(argc==2){puts(get_user_directory());return 0;}
    for(current_mode=0;current_mode<8;current_mode++) {
        printf("case:%d\n",current_mode);
        memset(saveFiles,0,sizeof(saveFiles));memset(saveTemp,0,sizeof(saveTemp));
        for(int z=0;z<22;z++){saveFiles[z].level=current_mode+z;saveFiles[z].highScore1=100+z;}
        for(int i=0;i<100;i++)saveTemp[2398+i]=(JE_byte)(i*current_mode);
        editorLevel=500+current_mode;serialize();JE_encryptSaveTemp();memcpy(stored_save,saveTemp,2502);
        memset(saveFiles,0,sizeof(saveFiles));memset(saveTemp,0,sizeof(saveTemp));memset(dosKeySettings,0,8);
        gameSpeed=inputDevice_=jConfigure=versionNum=processorType=soundEffects=gammaCorrection=fastPlay=0;
        difficultyLevel=0;midiPort=1;inputDevice[0]=1;inputDevice[1]=2;
        joyButtonAssign[0]=1;joyButtonAssign[1]=4;joyButtonAssign[2]=joyButtonAssign[3]=5;
        tyrMusicVolume=fxVolume=0;output_cfg=output_save=NULL;
        mt_srand(789);
        JE_loadConfiguration();
        printf("settings:%d,%d,%d,%d,%d,%d,%d,%d,%u\n",wild,superWild,smoothScroll,explosionTransparent,filtrationAvail,displayScore,fastPlay,background2,editorLevel);
        JE_saveConfiguration();
        JE_byte bytes[2502];
        if(!output_cfg||!output_save)return 2;
        fread_die(bytes,1,28,output_cfg);printf("dos:");hex(bytes,28);
        fread_die(bytes,1,2502,output_save);printf("saved:");hex(bytes,2502);
        fclose(output_cfg);fclose(output_save);
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
