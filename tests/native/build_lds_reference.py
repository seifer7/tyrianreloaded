"""Compile unchanged LDS player with recorded OPL writes and playback states."""

import argparse
import re
from pathlib import Path
import build_level_map_reference


def build_source(root):
    base = build_level_map_reference.build_source(root).split("static struct JE_MegaDataType1 megaData1;")[0]
    header = (root / "src" / "lds_play.h").read_text(encoding="utf-8")
    models = header[header.index("typedef struct {"):header.index("#endif")]
    source = (root / "src" / "lds_play.c").read_text(encoding="utf-8")
    source = re.sub(r"(?m)^#include[^\n]*\n", "", source)
    return base + r"""
typedef uint8_t Uint8;
typedef int8_t Sint8;
typedef uint16_t Uint16;
typedef uint32_t Uint32;
static void opl_init(void) {puts("reset");}
static void opl_write(unsigned char reg,unsigned char value) {printf("write:%u,%u\n",reg,value);}
void lds_rewind(void);
""" + models + source + r"""
static void dump_state(void) {
    printf("state:%u,%u,%u,%u,%u,%u,%u,%u\n",playing,songlooped,pattplay,posplay,tempo,tempo_now,allvolume,fadeonoff);
}
int main(int argc,char **argv) {
    if(argc!=3)return 2;FILE *file=fopen(argv[1],"rb");if(!file)return 3;
    fseek(file,0,SEEK_END);unsigned int length=ftell(file);int scenario=atoi(argv[2]);
    if(!lds_load(file,0,length))return 4;fclose(file);dump_state();
    for(int frame=0;frame<384;frame++) {
        printf("frame:%d\n",frame);
        if(frame==128)lds_fade(scenario%2?129:7);
        if(frame==256)lds_rewind();
        if(frame==300){lds_setregs(0x40,0x7f);lds_setregs(0x40,0x7f);lds_setregs_adv(0x40,0xc0,0x12);}
        printf("advance:%d\n",lds_update());dump_state();
    }
    puts("notes");lds_rewind();
    for(int i=0;i<64;i++) {
        printf("note:%d\n",i);lds_playsound(i,i%9,512+i*13);
        lds_update();dump_state();
    }
    lds_free();return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
