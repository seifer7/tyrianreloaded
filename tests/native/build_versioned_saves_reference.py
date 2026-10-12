"""Run unchanged .tyrsav writer/loader with numeric items and bounded fixture strings."""

import argparse
from pathlib import Path
import build_legacy_saves_reference


def build_source(root):
    config = (root / "src" / "config.c").read_text(encoding="utf-8")
    prefix = build_legacy_saves_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    functions = config[config.index("bool saveGameOperation("):config.index("void JE_saveGame(")]
    return prefix + r"""
typedef enum {ItemType_Ship,ItemType_WeaponFront,ItemType_WeaponRear,ItemType_Generator,
    ItemType_Shield,ItemType_SidekickLeft,ItemType_SidekickRight,ItemType_Special} ItemType;
static ItemType tempItemType;
static struct {char id[11];} ships[256],weaponPort[256],powerSys[256],shields[256],options[256],special[256];
static bool gameJustLoaded,gameLoaded;
static int item_idx(ItemType *type,const char *id) {
    (void)type;char *end;long index=strtol(id,&end,10);return *end||index<0||index>255?-1:(int)index;
}
static FILE *stored_file,*loaded_file;
static const char *get_user_directory(void) {return ".";}
static FILE *dir_fopen(const char *directory,const char *name,const char *mode) {
    (void)directory;(void)name;
    if(mode[0]=='w')return stored_file=tmpfile();
    loaded_file=tmpfile();if(!loaded_file||!stored_file)exit(2);
    rewind(stored_file);int byte;while((byte=fgetc(stored_file))!=EOF)fputc(byte,loaded_file);
    rewind(loaded_file);return loaded_file;
}
static int close_fixture(FILE *file) {if(file==stored_file){fflush(file);rewind(file);return 0;}return fclose(file);}
static void fread_die(void *data,size_t size,size_t count,FILE *file) {if(fread(data,size,count,file)!=count)exit(3);}
static void fwrite_die(const void *data,size_t size,size_t count,FILE *file) {if(fwrite(data,size,count,file)!=count)exit(3);}
static void fread_u8_die(JE_byte *data,size_t count,FILE *file) {fread_die(data,1,count,file);}
static void fread_s8_die(JE_shortint *data,size_t count,FILE *file) {fread_die(data,1,count,file);}
static void fread_u16_die(JE_word *data,size_t count,FILE *file) {fread_die(data,2,count,file);}
static void fwrite_u8_die(const JE_byte *data,size_t count,FILE *file) {fwrite_die(data,1,count,file);}
static void fwrite_s8_die(const JE_shortint *data,size_t count,FILE *file) {fwrite_die(data,1,count,file);}
static void fwrite_u16_die(const JE_word *data,FILE *file) {fwrite_die(data,2,1,file);}
static void *terminated_fixture_malloc(size_t size) {return calloc(size+1,1);}
#define malloc terminated_fixture_malloc
#define fclose close_fixture
""" + functions + r"""
#undef malloc
#undef fclose
static void fill(int mode) {
    difficultyLevel=(JE_shortint)(mode*17-128);initialDifficulty=(JE_shortint)(mode*11-64);
    saveLevel=(JE_byte)(mode*31+2);mainLevel=42;lastCubeMax=(JE_word)(mode*5001+255);cubeMax=777;
    initial_episode_num=(JE_byte)(mode*3+1);episodeNum=(JE_byte)(mode%5+1);gameHasRepeated=mode&1;
    superTyrian=mode%8==4;onePlayerAction=mode%8==3||mode%8==6;
    superArcadeMode=mode%8==2?7:mode%8==5?255:0;twoPlayerMode=mode&1;extraGame=galagaMode=true;
    gameJustLoaded=gameLoaded=false;
    memset(lastLevelName,0,11);memset(levelName,0,11);
    if(mode%3==0)strcpy(lastLevelName,"Completed");else snprintf(lastLevelName,11,"LEVEL%02d",mode);
    strcpy(levelName,"Old");
    for(int p=0;p<2;p++) {
        JE_PItemsType bytes;for(int i=0;i<12;i++)bytes[i]=(JE_byte)(mode*17+p*53+i*29);
        pitems_to_playeritems(&player[p].items,bytes,NULL);
        for(int i=0;i<12;i++)bytes[i]=(JE_byte)(mode*13+p*67+i*31);
        pitems_to_playeritems(&player[p].last_items,bytes,NULL);
        player[p].items.weapon[0].power=(JE_byte)(mode*19+p*7);
        player[p].items.weapon[1].power=(JE_byte)(mode*23+p*11);
        player[p].last_items.weapon[0].power=(JE_byte)(mode+3);
        player[p].last_items.weapon[1].power=(JE_byte)(mode+9);
        player[p].cash=(unsigned long)(mode*10001+p*321+17);
    }
}
static void dump(void) {
    printf("state:%d,%d,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",
        difficultyLevel,initialDifficulty,mainLevel,saveLevel,cubeMax,lastCubeMax,gameHasRepeated,
        initial_episode_num,episodeNum,superTyrian,onePlayerAction,superArcadeMode,twoPlayerMode,
        extraGame,galagaMode,gameLoaded,gameJustLoaded);
    printf("name:");hex(levelName,(int)strlen(levelName));printf("last:");hex(lastLevelName,(int)strlen(lastLevelName));
    for(int p=0;p<2;p++) {
        JE_PItemsType bytes;printf("items:%d:",p);playeritems_to_pitems(bytes,&player[p].items,0);hex(bytes,12);
        printf("powers:%u,%u\n",player[p].items.weapon[0].power,player[p].items.weapon[1].power);
        printf("previous:");playeritems_to_pitems(bytes,&player[p].last_items,0);hex(bytes,12);
        printf("previous-powers:%u,%u\n",player[p].last_items.weapon[0].power,player[p].last_items.weapon[1].power);
        printf("cash:%lu\n",player[p].cash);
    }
}
int main(void) {
    for(int i=0;i<256;i++) {
        snprintf(ships[i].id,11,"%d",i);strcpy(weaponPort[i].id,ships[i].id);
        strcpy(powerSys[i].id,ships[i].id);strcpy(shields[i].id,ships[i].id);
        strcpy(options[i].id,ships[i].id);strcpy(special[i].id,ships[i].id);
    }
    for(int mode=0;mode<64;mode++) {
        printf("case:%d\n",mode);fill(mode);
        if(!saveGameOperation("fixture"))return 2;
        fseek(stored_file,0,SEEK_END);long size=ftell(stored_file);rewind(stored_file);
        unsigned char bytes[2048]={0};fread_die(bytes,1,(size_t)size,stored_file);
        char decoded[2048]={0};memcpy(decoded,bytes+157,(size_t)size-157);xor(decoded,"TYRIANRELOADED");
        if(size==157||decoded[size-158]!='E') {puts("rejected:NUL");fclose(stored_file);stored_file=NULL;continue;}
        printf("saved:");hex(bytes,(int)size);fill(mode+32);
        if(!loadGameOperation("fixture"))return 4;
        dump();
        fclose(stored_file);fclose(loaded_file);stored_file=loaded_file=NULL;
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
