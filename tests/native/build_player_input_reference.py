"""Generate native player input collection with real joystick polling and demo records."""

import argparse
from pathlib import Path
import build_joystick_reference


def build_source(root):
    base = build_joystick_reference.build_source(root).split("int main(void)")[0]
    base = base.replace("static uint32_t ticks;", "static uint32_t ticks;\nstatic int mode_value,frame_value;\nstatic int32_t mouseWindowXRelative,mouseWindowYRelative;")
    base = base.replace("(void)clear;services++;newkey=", """(void)clear;services++;
    if(services==1){mouseWindowXRelative=mode_value%101*7919+frame_value*13007-16000;mouseWindowYRelative=16000-mode_value%103*1237-frame_value*7919;}
    newkey=""")
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    keyboard = (root / "src" / "keyboard.c").read_text(encoding="utf-8")
    video = (root / "src" / "video.c").read_text(encoding="utf-8")
    mouse = build_joystick_reference.function(video, "scaleWindowDistanceToScreen") + build_joystick_reference.function(keyboard, "mouseGetRelativePosition")
    collect = source[source.index("static void collect_player_movement_input("):source.index("static void update_player_linking(")]
    replay = build_joystick_reference.function(source, "replay_demo_keys").replace("replay_demo_keys(", "replay_native_keys(")
    return base + r"""
#define WITH_NETWORK
#define CURRENT_KEY_SPEED 1
#define KEY_SETTING_UP 0
#define KEY_SETTING_DOWN 1
#define KEY_SETTING_LEFT 2
#define KEY_SETTING_RIGHT 3
#define KEY_SETTING_FIRE 4
#define KEY_SETTING_CHANGE_FIRE 5
#define KEY_SETTING_LEFT_SIDEKICK 6
#define KEY_SETTING_RIGHT_SIDEKICK 7
typedef uint8_t JE_byte;
typedef uint16_t JE_word,Uint16;
typedef int16_t JE_integer;
typedef int32_t Sint32;
typedef struct {int x,y;} Player;
static Player player[2];
typedef struct {JE_integer mouseXC,mouseYC,accelXC,accelYC;bool link_gun_analog;float link_gun_angle;} JE_PlayerMovementState;
static bool isNetworkGame,endLevel,record_demo,play_demo,has_mouse,mouse_has_three_buttons;
static bool button[4],ingamemenu_pressed,pause_pressed,constantPlay,smoothies[9],mouse_pressed[3];
static JE_byte thisPlayerNum,levelEnd,demo_keys;
static JE_word demo_keys_wait;
static int keySettings[8]={0,1,2,3,4,5,6,7};
static bool keysactive[512];
static int8_t constantLastX;
static FILE *demo_file;
static struct {uint8_t data[28];} packet,*packet_state_out[1]={&packet};
static struct {int w,h;} gameSurface={320,200},*VGAScreen=&gameSurface;
static struct {int w,h;} last_output_rect={320,200};
static bool real_demo;
static bool replay_native_keys(void);
static void SDLNet_Write16(uint16_t value,void *destination) {
    uint8_t *p=destination;p[0]=value>>8;p[1]=value;
}
static bool replay_demo_keys(void) {
    if(real_demo)return replay_native_keys();
    printf("replay\n");for(int i=0;i<4;i++)button[i]=(mode_value>>(i+8))&1;return frame_value%3!=0;
}
static void fwrite_u8(uint8_t *bytes,size_t count,FILE *stream) {
    fwrite(bytes,1,count,stream);
}
static void fread_u8(uint8_t *bytes,size_t count,FILE *stream) {fread(bytes,1,count,stream);}
""" + mouse + collect + replay + r"""
int main(void) {
    SDL_Joystick devices[2];joysticks=2;
    for(int mode=0;mode<4096;mode++) {
        memset(devices,0,sizeof devices);memset(states,0,sizeof states);
        for(int j=0;j<2;j++) {
            devices[j].axes=2;devices[j].buttons=6;devices[j].hats=1;devices[j].name="fixture";
            states[j].handle=&devices[j];reset_joystick_assignments(j);states[j].analog=mode&(256<<j);
        }
        mode_value=mode;isNetworkGame=mode&2;thisPlayerNum=(mode&1024)?2:1;
        record_demo=mode&4;play_demo=mode&8;has_mouse=mode&16;mouse_has_three_buttons=mode&32;
        constantPlay=mode&128;constantLastX=mode%7-3;smoothies[8]=mode&64;
        demo_keys=mode%256;demo_keys_wait=65534;ingamemenu_pressed=false;pause_pressed=false;
        demo_file=tmpfile();if(!demo_file)return 2;
        for(int frame=0;frame<4;frame++) {
            frame_value=frame;endLevel=mode&1;levelEnd=0;services=updates=0;memset(packet.data,165,28);
            for(int i=0;i<512;i++)keysactive[i]=false;
            for(int i=0;i<8;i++)keysactive[i]=(mode>>(i%12))&1;
            for(int i=0;i<3;i++)mouse_pressed[i]=(mode>>(i+7))&1;
            for(int i=0;i<4;i++)button[i]=(mode>>(i+8))&1;
            for(int j=0;j<2;j++) {
                for(int i=0;i<2;i++)devices[j].axis[i]=(int16_t)(mode*1009+frame*7919+i*2345+j*987);
                for(int i=0;i<6;i++)devices[j].button[i]=(mode+frame+i+j)%3;
                devices[j].hat[0]=(mode*7+frame+j)&15;
            }
            ticks=frame*137;JE_byte number=(mode&2048)?2:1,device=mode%5;
            Player p={100,80};JE_word oldx=98,oldy=77;
            JE_PlayerMovementState movement={(JE_integer)(mode*73),(JE_integer)(-mode*113),-3,4,false,0.37f};
            printf("inputcase:%d,%d\n",mode,frame);
            collect_player_movement_input(&p,device,number,&oldx,&oldy,&movement);
            uint32_t angle;memcpy(&angle,&movement.link_gun_angle,4);
            printf("input:%d,%d,%u,%u,%d,%d,%d,%d,%d,%u,%u,%d,%d,%d,%d,%d,%d\n",p.x,p.y,oldx,oldy,
                movement.mouseXC,movement.mouseYC,movement.accelXC,movement.accelYC,movement.link_gun_analog,angle,
                bits(button,4),ingamemenu_pressed,pause_pressed,endLevel,levelEnd,services,updates);
            printf("packet:");for(int i=0;i<28;i++)printf("%02X",packet.data[i]);puts("");
            printf("demo:%u,%u:",demo_keys,demo_keys_wait);
            long position=ftell(demo_file);rewind(demo_file);for(long i=0;i<position;i++)printf("%02X",fgetc(demo_file));fseek(demo_file,position,SEEK_SET);puts("");
        }
        fclose(demo_file);
    }
    for(int mode=0;mode<512;mode++) {
        demo_file=tmpfile();if(!demo_file)return 2;
        for(int i=0;i<mode%22;i++) {
            uint8_t value=i%3==0?(mode+i*37)%256:i%3==1?0:(i/3)%5;
            fwrite(&value,1,1,demo_file);
        }
        rewind(demo_file);demo_keys=mode%256;demo_keys_wait=mode%4;
        player[0].x=100;player[0].y=80;player[1].x=140;player[1].y=90;
        for(int i=0;i<4;i++)button[i]=mode&(1<<i);
        for(int frame=0;frame<16;frame++) {
            bool result=replay_native_keys();
            printf("demoplay:%d,%d,%d,%d,%d,%u,%u,%u,%ld\n",mode,frame,result,player[0].x,player[0].y,
                demo_keys,demo_keys_wait,bits(button,4),ftell(demo_file));
        }
        fclose(demo_file);
    }
    real_demo=true;isNetworkGame=false;record_demo=false;play_demo=true;
    endLevel=false;constantPlay=false;smoothies[9-1]=false;has_mouse=false;
    for(int mode=0;mode<512;mode++) {
        demo_file=tmpfile();if(!demo_file)return 2;
        for(int i=0;i<mode%22;i++) {
            uint8_t value=i%3==0?(mode+i*37)%256:i%3==1?0:(i/3)%5;
            fwrite(&value,1,1,demo_file);
        }
        rewind(demo_file);demo_keys=mode%256;demo_keys_wait=mode%4;
        player[0].x=100;player[0].y=80;endLevel=false;levelEnd=0;
        for(int frame=0;frame<16;frame++) {
            for(int i=0;i<4;i++)button[i]=false;
            JE_word oldX=player[0].x,oldY=player[0].y;
            JE_PlayerMovementState movement={0};
            collect_player_movement_input(&player[0],1,1,&oldX,&oldY,&movement);
            printf("democollect:%d,%d,%d,%d,%d,%d,%u,%u,%u,%d,%u,%ld\n",
                mode,frame,player[0].x,player[0].y,movement.accelXC,movement.accelYC,
                demo_keys,demo_keys_wait,bits(button,4),endLevel,levelEnd,ftell(demo_file));
        }
        fclose(demo_file);
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
