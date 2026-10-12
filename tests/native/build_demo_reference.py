"""Generate unchanged demo loading/recording with native item ID resolution."""

import argparse
from pathlib import Path
import build_items_reference
from build_joystick_reference import function


def build_source(root):
    base = build_items_reference.build_source(root).split("int main(int argc, char **argv)")[0]
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    game = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    record = game[game.index("static void record_level_demo("):game.index("static bool run_level_frame(")]
    return base + r"""
#define FRONT_WEAPON 0
#define REAR_WEAPON 1
#define LEFT_SIDEKICK 0
#define RIGHT_SIDEKICK 1
#define DIFFICULTY_NORMAL 2
typedef uint8_t Uint8;
typedef struct {
    struct {
        struct {JE_byte id,power;} weapon[2];
        JE_byte super_arcade_mode,sidekick[2],generator,sidekick_level,sidekick_series,shield,special,ship;
    } items;
} Player;
static Player player[2];
static const char *directory;
static Uint8 demo_num,demo_keys,episodeNum,initial_episode_num,lvlFileNum,levelSong;
static JE_word demo_keys_wait,mainLevel;
static JE_shortint difficultyLevel;
static bool play_demo,bonusLevelCurrent;
static FILE *demo_file;
static char levelName[11],tempStr[256];
static const char *data_dir(void) {return directory;}
static const char *get_user_directory(void) {return directory;}
static FILE *dir_fopen(const char *dir,const char *name,const char *mode) {
    char path[1024];snprintf(path,sizeof path,"%s/%s",dir,name);return fopen(path,mode);
}
static FILE *dir_fopen_warn(const char *dir,const char *name,const char *mode) {return dir_fopen(dir,name,mode);}
static bool dir_file_exists(const char *dir,const char *name) {
    FILE *file=dir_fopen(dir,name,"rb");if(!file)return false;fclose(file);return true;
}
static void fread_die(void *data,size_t size,size_t count,FILE *file) {if(fread(data,size,count,file)!=count)exit(3);}
static void fread_u8_die(Uint8 *data,size_t count,FILE *file) {fread_die(data,1,count,file);}
static void fread_u8(Uint8 *data,size_t count,FILE *file) {fread(data,1,count,file);}
static void fwrite_u8_die(Uint8 *data,size_t count,FILE *file) {if(fwrite(data,1,count,file)!=count)exit(3);}
static void JE_initEpisode(Uint8 episode) {episodeNum=episode;printf("init:%u\n",episode);}
""" + function(source, "load_next_demo") + record + r"""
static void hex(const Uint8 *data,size_t count) {for(size_t i=0;i<count;i++)printf("%02X",data[i]);}
int main(int argc,char **argv) {
    if(argc!=4)return 2;load_typed_items(argv[1]);directory=argv[2];
    if(!strcmp(argv[3],"wrap"))demo_num=255;
    if(!strcmp(argv[3],"record")) {
        for(int mode=0;mode<64;mode++) {
            for(int i=0;i<10;i++)levelName[i]=(char)(mode*13+i*7+65);levelName[10]=0;
            if(mode&1)levelName[mode%11]=0;
            episodeNum=mode%5+1;initial_episode_num=mode%7+1;lvlFileNum=mode*3;levelSong=mode*7;
            JE_byte *p=(JE_byte*)&player[0].items;
            for(size_t i=0;i<sizeof player[0].items;i++)p[i]=mode*7+i*3;
            player[0].items.weapon[0].power=mode%11+1;player[0].items.weapon[1].power=mode%9+1;
            demo_keys=255;demo_keys_wait=65535;
            record_level_demo();fflush(demo_file);fclose(demo_file);demo_file=NULL;
            FILE *read=dir_fopen(directory,tempStr,"rb");if(!read)return 2;
            Uint8 bytes[30];fread_u8_die(bytes,30,read);fclose(read);
            printf("record:%s,%u,%u:",tempStr,demo_keys,demo_keys_wait);hex(bytes,30);putchar('\n');
        }
    } else {
        for(int mode=0;mode<5;mode++) {
            play_demo=true;mainLevel=17;difficultyLevel=9;bonusLevelCurrent=true;
            bool result=load_next_demo();
            printf("load:%d,%u,%d,%u,%d,%d,%u,%u,%u,%u,%u,%ld:",result,demo_num,play_demo,mainLevel,bonusLevelCurrent,
                difficultyLevel,episodeNum,initial_episode_num,lvlFileNum,levelSong,demo_keys_wait,demo_file?ftell(demo_file):-1L);
            hex((Uint8*)levelName,strlen(levelName));putchar('\n');
            Player *p=&player[0];
            printf("items:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",
                p->items.weapon[0].id,p->items.weapon[1].id,p->items.super_arcade_mode,
                p->items.sidekick[0],p->items.sidekick[1],p->items.generator,p->items.sidekick_level,
                p->items.sidekick_series,initial_episode_num,p->items.shield,p->items.special,p->items.ship,
                p->items.weapon[0].power,p->items.weapon[1].power);
            if(demo_file){fclose(demo_file);demo_file=NULL;}
        }
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
