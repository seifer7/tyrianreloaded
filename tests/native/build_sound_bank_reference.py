"""Compile native sound-bank reader with a recording deterministic conversion boundary."""

import argparse
from pathlib import Path
import build_level_map_reference


def build_source(root):
    base = build_level_map_reference.build_source(root).split("static struct JE_MegaDataType1 megaData1;")[0]
    source = (root / "src" / "nortsong.c").read_text(encoding="utf-8")
    loader = source[source.index("void loadSndFile("):source.index("void setDelaySpeed(")]
    return base + r"""
typedef uint8_t Uint8;
typedef uint16_t Uint16;
typedef uint32_t Uint32;
typedef int16_t Sint16;
#define SFX_COUNT 29
#define VOICE_COUNT 9
#define SOUND_COUNT 38
#define AUDIO_S8 0x8008
#define AUDIO_S16SYS 0x8010
#define MAX(a,b) ((a)>(b)?(a):(b))
static Sint16 *soundSamples[SOUND_COUNT];
static size_t soundSampleCount[SOUND_COUNT];
static int audioSampleRate=44100;
static const JE_word fxPlayVol=4;
static void fread_u32_die(Uint32 *buffer,size_t count,FILE *file) {fread_die(buffer,4,count,file);}
typedef struct {int len_mult,len,len_cvt;Uint8 *buf;} SDL_AudioCVT;
static int SDL_BuildAudioCVT(SDL_AudioCVT *cvt,int source,int sc,int sr,int target,int tc,int tr) {
    (void)source;(void)sc;(void)sr;(void)target;(void)tc;(void)tr;cvt->len_mult=2;return 1;
}
static uint32_t hash_bytes(const Uint8 *data,size_t length) {
    uint32_t result=2166136261u;for(size_t i=0;i<length;i++)result=(result^data[i])*16777619u;return result;
}
static int SDL_ConvertAudio(SDL_AudioCVT *cvt) {
    printf("convert:%d,%u\n",cvt->len,hash_bytes(cvt->buf,cvt->len));
    for(int i=cvt->len-1;i>=0;i--)((Sint16*)cvt->buf)[i]=(int8_t)cvt->buf[i]*256;
    cvt->len_cvt=cvt->len*2;return 0;
}
static const char *SDL_GetError(void) {return "synthetic converter failure";}
static void SDL_Quit(void) {}
static void multiSamplePlay(const Sint16 *sample,size_t count,JE_byte channel,JE_byte volume) {
    printf("play:%u,%u,%lu,%u\n",channel,volume,(unsigned long)count,hash_bytes((const Uint8*)sample,count*2));
}
""" + loader + r"""
int main(int argc,char **argv) {
    if(argc!=3)return 2;directory=argv[1];loadSndFile(atoi(argv[2])!=0);
    for(int i=0;i<SOUND_COUNT;i++)printf("sample:%d,%lu,%u\n",i,(unsigned long)soundSampleCount[i],hash_bytes((const Uint8*)soundSamples[i],soundSampleCount[i]*2));
    for(int i=1;i<=SOUND_COUNT;i++)JE_playSampleNum(i);
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
