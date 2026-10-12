"""Trace unchanged network queues/retries/XOR with deterministic UDP/tick fixtures."""

import argparse
from pathlib import Path
import re


def build_source(root):
    source = (root / "src" / "network.c").read_text(encoding="utf-8")
    halt = source[source.index("void network_tyrian_halt(unsigned int err, bool attempt_sync)\n{"):source.index("int network_init(void)")]
    halt = halt.replace("void network_tyrian_halt(", "static void run_network_halt(")
    clear = source[source.index("void JE_clearSpecialRequests(void)"):]
    source = source[source.index("#define NET_VERSION"):source.index("void network_tyrian_halt(unsigned int err, bool attempt_sync)\n{")]
    header = (root / "src" / "network.h").read_text(encoding="utf-8")
    constants = "\n".join(re.findall(r"^#define PACKET_\w+\s+0x[0-9A-Fa-f]+", header, re.MULTILINE))
    return r"""
#include <stdint.h>
#include <stdbool.h>
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define WITH_NETWORK
#define fprintf(f,...) printf(__VA_ARGS__)
typedef uint8_t Uint8;typedef uint16_t Uint16;typedef uint32_t Uint32;
typedef unsigned int uint;typedef bool JE_boolean;
typedef void *UDPsocket;
typedef struct {Uint32 host;Uint16 port;} IPaddress;
typedef struct {int channel;Uint8 *data;int len,maxlen,status;IPaddress address;} UDPpacket;
static Uint32 ticks;
static int scene,phase,delay_frames;
static int connect_sends;
static bool episodeAvail[5];
static bool newkey;
static int lastkey_scan;
#define EPISODE_MAX 5
#define SDL_SCANCODE_ESCAPE 41
static void push_joysticks_as_keyboard(void) {if(phase==3)printf("push\n");}
static void service_SDL_events(bool unused) {
    if(phase==3){printf("events:%d\n",unused?1:0);newkey=(scene&8)!=0;lastkey_scan=41;}
}
static bool tracing=true;
static UDPpacket *received[256],*history[64];
static int head,tail;
static Uint16 SDLNet_Read16(const void *p) {const Uint8 *b=p;return (b[0]<<8)|b[1];}
static void SDLNet_Write16(Uint16 v,void *p) {Uint8 *b=p;b[0]=v>>8;b[1]=v;}
static Uint32 SDL_GetTicks(void) {return ticks;}
static void SDL_Delay(Uint32 amount) {if(phase==3)printf("wait:%u\n",amount);ticks+=amount;}
static const char *SDL_GetError(void) {return "fixture";}
static UDPpacket *SDLNet_AllocPacket(int size) {
    UDPpacket *p=calloc(1,sizeof(*p));p->data=calloc(size,1);p->maxlen=size;return p;
}
static void SDLNet_FreePacket(UDPpacket *p) {if(p){free(p->data);free(p);}}
static UDPpacket *clone(UDPpacket *p) {
    UDPpacket *copy=SDLNet_AllocPacket(256);memcpy(copy->data,p->data,p->len);copy->len=p->len;copy->channel=p->channel;return copy;
}
static void queue(UDPpacket *p) {received[tail++%256]=clone(p);}
static void dump(const char *label,UDPpacket *p) {
    printf("%s:",label);
    if(p)for(int i=0;i<p->len;i++)printf("%02X",p->data[i]);else printf("-");
    printf("\n");
}
static int SDLNet_UDP_Send(UDPsocket s,int channel,UDPpacket *p) {
    (void)s;(void)channel;
    if(tracing)dump("send",p);
    Uint16 type=SDLNet_Read16(p->data),sequence=SDLNet_Read16(p->data+2);
    if(phase==3 && type==0x10) {
        if(connect_sends++==0) {
            UDPpacket *reply=SDLNet_AllocPacket(256);reply->len=19;
            SDLNet_Write16(0x10,reply->data);SDLNet_Write16(0,reply->data+2);
            SDLNet_Write16((scene&1)?3:2,reply->data+4);
            SDLNet_Write16(delay_frames+((scene&2)?1:0),reply->data+6);
            SDLNet_Write16((scene%2)?0x0b:0x17,reply->data+8);
            SDLNet_Write16((scene&16)?1:2,reply->data+10);
            strcpy((char*)reply->data+12,"Remote");queue(reply);
            if(scene&4)queue(reply);SDLNet_FreePacket(reply);
        }
        UDPpacket *ack=SDLNet_AllocPacket(256);ack->len=4;SDLNet_Write16(0,ack->data);
        SDLNet_Write16(sequence,ack->data+2);queue(ack);SDLNet_FreePacket(ack);
    }
    if(phase==2) {
        if(type==0x41) {
            SDLNet_FreePacket(history[sequence%64]);history[sequence%64]=clone(p);
            if(!(scene%2 && sequence%delay_frames==0))queue(p);
        } else if(type==0x42)queue(p);
        else if(type==0x40 && history[sequence%64])queue(history[sequence%64]);
    }
    return 1;
}
static int SDLNet_UDP_Recv(UDPsocket s,UDPpacket *p) {
    (void)s;if(head==tail)return 0;
    UDPpacket *q=received[head++%256];p->len=q->len;p->channel=q->channel;memcpy(p->data,q->data,q->len);SDLNet_FreePacket(q);return 1;
}
bool network_is_sync(void);bool network_state_is_reset(void);
static void network_tyrian_halt(unsigned int code,bool sync) {printf("halt:%u,%d\n",code,sync?1:0);if(phase==3)exit(0);}
static int SDLNet_ResolveHost(IPaddress *address,const char *host,Uint16 port) {(void)address;(void)host;(void)port;return 0;}
static int SDLNet_UDP_Bind(UDPsocket s,int channel,IPaddress *address) {(void)s;(void)channel;(void)address;return 0;}
""" + constants + "\n" + source + "\n#endif\n" + r"""
#define COUNTOF(a) (sizeof(a)/sizeof((a)[0]))
#define SMALL_FONT_SHAPES 2
static int *VGAScreen, screen_marker, *VGAScreenSeg=&screen_marker, colors[256];
static int shutdown_polls;
static void fade_black(int steps) {printf("fade-black:%d\n",steps);}
static void JE_loadPic(int *screen,int number,bool store) {assert(screen==VGAScreenSeg);printf("load:%d,%d\n",number,store?1:0);}
static int JE_fontCenter(const char *text,int font) {
    (void)font;int width=0;while(*text)width+=*text++==' '?6:1;return 160-width/2;
}
static void JE_dString(int *screen,int x,int y,const char *text,int font) {
    (void)font;assert(screen==VGAScreenSeg);printf("text:%d,%d,%s\n",x,y,text);
}
static void JE_showVGA(void) {printf("present\n");}
static void fade_palette(int *palette,int steps,int first,int last) {
    (void)palette;printf("fade-palette:%d,%d,%d\n",steps,first,last);
}
static bool JE_anyButton(void) {
    printf("poll\n");service_SDL_events(true);return ++shutdown_polls==3;
}
static void SDLNet_Quit(void) {printf("release\n");}
static void JE_tyrianHalt(int code) {printf("game-halt:%d\n",code);}
""" + halt + clear + r"""
static void inject(Uint16 type,Uint16 sequence,int length) {
    UDPpacket *p=SDLNet_AllocPacket(256);p->len=length;
    SDLNet_Write16(type,p->data);SDLNet_Write16(sequence,p->data+2);
    for(int i=4;i<length;i++)p->data[i]=(sequence*13+i*7)&255;
    queue(p);SDLNet_FreePacket(p);
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;scene=atoi(argv[1]);delay_frames=network_delay=1+scene%7;
    net_initialized=true;packet_out_temp=SDLNet_AllocPacket(256);packet_temp=SDLNet_AllocPacket(256);
    if(scene>=200) {
        int mask=scene-200;pauseRequest=mask&1;inGameMenuRequest=mask&2;
        skipLevelRequest=mask&4;helpRequest=mask&8;nortShipRequest=mask&16;yourInGameMenuRequest=true;
        JE_clearSpecialRequests();
        printf("requests:%d,%d,%d,%d,%d,%d\n",pauseRequest,inGameMenuRequest,skipLevelRequest,helpRequest,nortShipRequest,yourInGameMenuRequest);
        return 0;
    }
    if(scene>=100) {
        scene-=100;phase=3;bool sync=scene>=8;int code=scene%8;
        if(scene/8==1)ticks=15984;
        if(scene/8==2){queue_out_sync=1;last_ack_sync=0;}
        if(scene/8==3) {
            tracing=false;network_prepare(PACKET_WAITING);network_send(4);
            inject(PACKET_ACKNOWLEDGE,0,4);tracing=true;
        }
        run_network_halt(code,sync);return 0;
    }
    if(scene>=10) {
        scene-=10;phase=3;delay_frames=network_delay=1+scene%7;thisPlayerNum=1;
        char name[]="Local player name too long";network_player_name=name;
        for(int i=0;i<5;i++)episodeAvail[i]=(i+scene)%3!=0;
        network_connect();printf("connected:%d,%s\n",connected?1:0,network_opponent_name);
        printf("episodes:");for(int i=0;i<5;i++)printf("%d",episodeAvail[i]?1:0);printf("\n");return 0;
    }
    if(scene==8) {
        tracing=false;
        for(int i=0;i<65530;i++) {
            network_prepare(PACKET_WAITING);network_send(4);inject(PACKET_ACKNOWLEDGE,i,4);network_check();
        }
        tracing=true;
        for(int i=0;i<12;i++) {
            network_prepare(PACKET_WAITING);Uint16 seq=SDLNet_Read16(packet_out_temp->data+2);
            printf("sent:%d\n",network_send(4)?1:0);inject(PACKET_ACKNOWLEDGE,seq,4);
            printf("check:%d\n",network_check());printf("sync:%d\n",network_is_sync()?1:0);
        }
        return 0;
    }
    for(int step=0;step<64;step++) {
        ticks=step*113;
        if(step%4==0) {
            network_prepare(PACKET_DETAILS);
            SDLNet_Write16(step,packet_out_temp->data+4);SDLNet_Write16(scene,packet_out_temp->data+6);
            printf("sent:%d\n",network_send(8)?1:0);
            memset(packet_out_temp->data+4,255,4);
        }
        switch(step%8) {
            case 0:inject(PACKET_ACKNOWLEDGE,step/4-1,4);break;
            case 1:inject(PACKET_DETAILS,step/8,8);break;
            case 2:inject(PACKET_WAITING,step/8,4);break;
            case 3:inject(PACKET_GAME_PAUSE,step/8+1,4);break;
            case 4:inject(PACKET_ACKNOWLEDGE,step/4,4);break;
            case 5:inject(0x99,step,4);break;
            case 6:inject(PACKET_KEEP_ALIVE,0,4);break;
            case 7:inject(PACKET_STATE_RESEND,0,4);break;
        }
        printf("check:%d\n",network_check());dump("control",packet_in[0]);
        printf("update:%d\n",network_update()?1:0);printf("sync:%d\n",network_is_sync()?1:0);
    }
    ticks=10000;phase=2;network_state_reset();
    for(int frame=0;frame<20;frame++) {
        network_state_prepare();if(frame==0)network_state_prepare();
        for(int i=4;i<28;i++)packet_state_out[0]->data[i]=(scene*17+frame*7+i*13)&255;
        network_state_send();
        while(network_check()!=0) {}
        printf("reset:%d\n",network_state_is_reset()?1:0);
        printf("stateupdate:%d\n",network_state_update()?1:0);
        dump("state",packet_state_in[0]);dump("delayed",packet_state_out[network_delay]);
        ticks+=11;
    }
    network_state_reset();printf("reset:%d\n",network_state_is_reset()?1:0);
    dump("state",packet_state_in[0]);dump("delayed",packet_state_out[network_delay]);
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
