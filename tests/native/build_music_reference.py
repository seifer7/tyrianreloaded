"""Compile unchanged music directory and control functions with recorded LDS calls."""

import argparse
from pathlib import Path
import build_level_map_reference


def build_source(root):
    base = build_level_map_reference.build_source(root).split("static struct JE_MegaDataType1 megaData1;")[0]
    source = (root / "src" / "loudness.c").read_text(encoding="utf-8")
    controls = source[source.index("void load_music("):source.index("void set_volume(")]
    return base + r"""
typedef uint32_t Uint32;
typedef uint16_t Uint16;
typedef uint8_t Uint8;
static FILE *music_file;
static Uint32 *song_offset;
static Uint16 song_count;
static bool audio_disabled,music_stopped=true;
static unsigned int song_playing=0;
static int audioDevice;
static void fread_u32_die(Uint32 *buffer,size_t count,FILE *file) {fread_die(buffer,4,count,file);}
static long ftell_eof(FILE *file) {long current=ftell(file);fseek(file,0,SEEK_END);long end=ftell(file);fseek(file,current,SEEK_SET);return end;}
static void SDL_LockAudioDevice(int device) {(void)device;}
static void SDL_UnlockAudioDevice(int device) {(void)device;}
static void lds_load(FILE *file,unsigned int offset,unsigned int size) {
    uint32_t hash=2166136261u;fseek(file,offset,SEEK_SET);
    for(unsigned int i=0;i<size;i++)hash=(hash^(uint8_t)fgetc(file))*16777619u;
    printf("load:%u,%u\n",size,hash);
}
static void lds_rewind(void) {puts("rewind");}
static void lds_fade(Uint8 speed) {printf("fade:%u\n",speed);}
""" + controls + r"""
static void state(int step) {printf("state:%d,%u,%d\n",step,song_playing,music_stopped);}
int main(int argc,char **argv) {
    if(argc!=2)return 2;directory=argv[1];load_music();load_music();printf("count:%u\n",song_count);
    for(int i=0;i<12;i++) {
        audio_disabled=i%5==3;
        play_song(i%3);state(i*4);
        fade_song();state(i*4+1);
        stop_song();state(i*4+2);
        restart_song();state(i*4+3);
    }return 0;
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
