"""Verify unchanged linking, inertia, level-end trails and sidekick motion."""

import argparse
from pathlib import Path
import build_shots_reference
from build_joystick_reference import function


def build_source(root):
    base = build_shots_reference.build_source(root).split("static void dump(void)")[0]
    base = base.replace("static int *VGAScreen, *spriteSheet8, *spriteSheet12;", "static int *VGAScreen, *spriteSheet8, *spriteSheet12, *spriteSheet10, *spriteSheet9;")
    base = base.replace("sheet == spriteSheet8 ? 8 : 12", "sheet == spriteSheet8 ? 8 : sheet == spriteSheet12 ? 12 : sheet == spriteSheet9 ? 9 : 10")
    base = base.replace("static struct {int delta_x_shot_move, delta_y_shot_move;} player[2];", r"""
typedef struct {
    int delta_x_shot_move,delta_y_shot_move,x,y,x_velocity,y_velocity,old_x[20],old_y[20];
    uint x_friction_ticks,y_friction_ticks,invulnerable_ticks;
    int last_x_explosion_follow,last_y_explosion_follow;
    bool is_alive;
    uint weapon_mode,superbombs;
    struct {struct {JE_byte id,power;} weapon[2];JE_byte sidekick[2],special,ship;} items;
    struct {uint style,ammo_refill_ticks,ammo_refill_ticks_max,charge,charge_ticks,animation_frame;int x,y,ammo,ammo_max;bool animation_enabled;} sidekick[2];
} Player;
static Player player[2];
""")
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    motion = source[source.index("static void update_player_linking("):source.index("static void change_player_weapon_mode(")]
    sidekicks = source[source.index("static void update_sidekick_positions("):source.index("static void fire_sidekick_weapons(")]
    main_weapons = source[source.index("static void fire_main_weapons("):source.index("static void update_super_charge_weapon(")]
    bombs = source[source.index("static void fire_superbomb("):source.index("static void update_sidekick_positions(")]
    charge = source[source.index("static void update_super_charge_weapon("):source.index("static void fire_superbomb(")]
    fire_sidekicks = source[source.index("static void fire_sidekick_weapons("):source.index("static void update_player_attacks(")]
    draw_sidekicks = source[source.index("static void draw_player_sidekicks("):source.index("static void draw_player_ship(")]
    graphics = (root / "src" / "vga256d.c").read_text(encoding="utf-8")
    gauge = function(graphics, "fill_rectangle_xy") + function(graphics, "draw_segmented_gauge")
    modes = source[source.index("static void change_player_weapon_mode("):source.index("static void fire_main_weapons(")]
    mode_buttons = function(source, "JE_drawPortConfigButtons")
    ship = source[source.index("static void draw_player_ship("):source.index("void JE_playerMovement(")]
    commands = function(source, "JE_SFCodes").replace("JE_SFCodes(", "recognize_special(")
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    combos = varz[varz.index("const JE_byte keyboardCombos["):varz.index("/*Special General Data*/")]
    return base + r"""
typedef int *Sprite2_array;
#include <assert.h>
#define LEFT_SIDEKICK 0
#define RIGHT_SIDEKICK 1
#define REAR_WEAPON 1
#define SHOT_MISC 4
#define SHOT_REAR 1
#define SHOT_P1_SUPERBOMB 6
#define SHOT_P2_CHARGE 5
#define SHOT_LEFT_SIDEKICK 2
#define SHOT_RIGHT_SIDEKICK 3
#define SHOT_SPECIAL 8
#define SHOT_SPECIAL2 10
#define SHOT_NORTSPARKS 9
#define DIFFICULTY_HARD 3
#define SA_NONE 0
#define SA_NORTSHIPZ 7
#define OPTION_SHAPES 1
static const JE_word SASpecialWeapon[]={7,8,9,10,11,12,13},SASpecialWeaponB[]={37,6,15,40,16,14,41};
static JE_byte superArcadeMode;
static bool portConfigChange,portConfigDone;
static int tempI,tempI2;
#define COUNTOF(a) (sizeof(a)/sizeof((a)[0]))
#define MIN(a,b) ((a)<(b)?(a):(b))
#define MAX(a,b) ((a)>(b)?(a):(b))
#define S_CLINK 23
#define S_WEAPON_26 26
typedef struct {JE_integer mouseXC,mouseYC,accelXC,accelYC;bool link_gun_analog;float link_gun_angle;} JE_PlayerMovementState;
static bool moveOk,twoPlayerMode,twoPlayerLinked,galagaMode,button[4],reallyEndLevel,endLevel;
static JE_byte levelEnd;
static JE_shortint levelEndWarp;
static JE_word shipGr2;
static float linkGunDirec,optionSatelliteRotate;
static bool optionAttachmentLinked,optionAttachmentReturn;
static JE_integer optionAttachmentMove,temp;
static short b;
static bool superTyrian;
#define SPECIAL_NUM 46
static JE_shortint constantLastX;
static JE_integer explosionFollowAmountX,explosionFollowAmountY,mapX2Ofs;
static bool isNetworkGame,background2,superWild,ship_trace;
static JE_word tempW;
static JE_byte chargeWait,chargeLevel,chargeMax,chargeGr,chargeGrWait;
static JE_shortint difficultyLevel;
typedef uint8_t Uint8;
typedef struct {int w,h,pitch;uint8_t *pixels;} SDL_Surface;
typedef struct {int x,y,w,h;} SDL_Rect;
static SDL_Surface *VGAScreenSeg;
static const int hud_sidekick_y[2][2]={{64,82},{108,126}};
static void SDL_FillRect(SDL_Surface *s,SDL_Rect *r,int color) {
    int x1=r->x<0?0:r->x,y1=r->y<0?0:r->y,x2=r->x+r->w>s->w?s->w:r->x+r->w,y2=r->y+r->h>s->h?s->h:r->y+r->h;
    for(int y=y1;y<y2;y++)for(int x=x1;x<x2;x++)s->pixels[y*s->pitch+x]=(uint8_t)color;
}
static void fill_rectangle_wh(SDL_Surface *s,int x,int y,uint w,uint h,Uint8 color) {
    SDL_Rect r={x,y,w,h};SDL_FillRect(s,&r,color);
}
static void blit_sprite(SDL_Surface *s,int x,int y,int table,int frame) {printf("P:%d,%d,%d\n",x,y,frame);}
static JE_word JE_portConfigs(void) {return weaponPort[player[twoPlayerMode?1:0].items.weapon[1].id].opnum;}
static bool all_players_dead(void) {return !player[0].is_alive && (!twoPlayerMode || !player[1].is_alive);}
static void JE_SFCodes(JE_byte number,int x,int y,JE_word oldx,JE_word oldy) {
    printf("codes:%u,%d,%d,%u,%u\n",number,x,y,oldx,oldy);
}
static void blit_sprite2x2(int *screen,int x,int y,Sprite2_array sheet,int graphic) {
    (void)screen;(void)sheet;
    if(ship_trace)printf("ship:%d,%d,%d,1,0\n",x,y,graphic);
    else printf("trail:%d,%d,%d\n",x,y,graphic);
}
static void blit_sprite2x2_darken(int *screen,int x,int y,Sprite2_array sheet,int graphic) {
    printf("ship:%d,%d,%d,1,5\n",x,y,graphic);
}
static void blit_sprite2x2_blend(int *screen,int x,int y,Sprite2_array sheet,int graphic) {
    printf("ship:%d,%d,%d,1,1\n",x,y,graphic);
}
""" + combos + commands + mode_buttons + modes + gauge + motion + sidekicks + main_weapons + bombs + charge + fire_sidekicks + draw_sidekicks + ship + r"""
int main(int argc,char **argv) {
    if(argc!=2)return 2;load_typed_items(argv[1]);
    spriteSheet8=(int*)(uintptr_t)8;spriteSheet12=(int*)(uintptr_t)12;
    spriteSheet10=(int*)(uintptr_t)10;spriteSheet9=(int*)(uintptr_t)9;
    uint8_t pixels[64000];SDL_Surface hud={320,200,320,pixels};VGAScreenSeg=&hud;
    for(int mode=0;mode<4096;mode++) {
        memset(pixels,69,sizeof pixels);
        memset(player,0,sizeof player);memset(playerShotData,0,sizeof playerShotData);
        memset(shotAvail,0,sizeof shotAvail);memset(shotMultiPos,0,sizeof shotMultiPos);
        memset(shotRepeat,0,sizeof shotRepeat);memset(soundQueue,0,sizeof soundQueue);memset(enemyAvail,1,sizeof enemyAvail);
        power=100;twoPlayerMode=mode&1;twoPlayerLinked=mode&2;galagaMode=mode&4;moveOk=mode&8;
        endLevel=mode&16;reallyEndLevel=false;levelEnd=mode%43;levelEndWarp=(mode%7)-2;
        shipGr2=(mode&32)?3:0;linkGunDirec=(mode%101)*0.1f-1.0f;
        optionSatelliteRotate=(mode%43)*0.2f;optionAttachmentLinked=mode&64;
        optionAttachmentReturn=mode&128;optionAttachmentMove=(mode%31)-20;
        difficultyLevel=mode%11;chargeLevel=mode%6;chargeMax=5;chargeWait=mode%7;
        chargeGr=mode%4;chargeGrWait=mode%5;
        superArcadeMode=mode%10;portConfigChange=mode&256;portConfigDone=mode&128;
        for(int i=0;i<4;i++)button[i]=(mode>>(8+i))&1;
        for(int i=0;i<2;i++) {
            player[i].is_alive=(mode&1024)==0;player[i].x=80+i*(mode%9);player[i].y=60+i*(mode%11);
            player[i].x_velocity=mode%13-6;player[i].y_velocity=mode%17-8;
            player[i].x_friction_ticks=mode%3;player[i].y_friction_ticks=mode%4;
            player[i].items.weapon[1].id=ITEM_INDEX_STORE[ITEM_WEAPONREAR_NORTSHIPSPREADER];
            player[i].items.weapon[0].id=(mode&64)?0:ITEM_INDEX_STORE[ITEM_WEAPONFRONT_NORTSHIPSUPERPULSE];
            player[i].items.weapon[0].power=player[i].items.weapon[1].power=mode%11+1;
            player[i].weapon_mode=mode%2+1;player[i].superbombs=mode%3;
            for(int j=0;j<20;j++){player[i].old_x[j]=j*3;player[i].old_y[j]=j*2;}
            for(int j=0;j<2;j++) {
                player[i].sidekick[j].style=(mode/(j?5:1))%5;
                player[i].sidekick[j].x=100+j*15;player[i].sidekick[j].y=20+j*25;
                player[i].items.sidekick[j]=ITEM_INDEX_STORE[ITEM_SIDEKICK_COMPANIONSHIPQUICKSILVER];
                player[i].sidekick[j].ammo_max=(mode&64)?25:0;player[i].sidekick[j].ammo=mode%26;
                player[i].sidekick[j].ammo_refill_ticks=mode%3;player[i].sidekick[j].ammo_refill_ticks_max=7;
                player[i].sidekick[j].charge=mode%4;player[i].sidekick[j].charge_ticks=mode%3;
                player[i].sidekick[j].animation_frame=mode%3;player[i].sidekick[j].animation_enabled=mode&128;
            }
        }
        JE_byte number=mode%2+1;Player *p=&player[number-1];
        JE_word oldx=p->x+((mode&32)?3:0),oldy=p->y+((mode&64)?4:0),graphic=mode%3;
        JE_PlayerMovementState movement={0};movement.accelXC=mode%15-7;movement.accelYC=mode%19-9;
        movement.link_gun_analog=mode&2048;movement.link_gun_angle=(mode%79)*0.1f-1;
        shotRepeat[SHOT_REAR]=(mode&128)?2:0;
        shotRepeat[6]=shotRepeat[7]=(mode&512)?2:0;
        shotRepeat[2]=shotRepeat[3]=(mode&256)?2:0;shotRepeat[5]=(mode&1024)?2:0;
        shotMultiPos[8]=shotMultiPos[10]=3;
        tempI=superArcadeMode>=1&&superArcadeMode<=7?SASpecialWeapon[superArcadeMode-1]:7;
        tempItemType=ItemType_Special;get_item_index_from_int(&tempItemType,&tempI,&tempI2);
        player[0].items.special=(mode&32)?tempI2:0;
        printf("case:%d\n",mode);
        update_player_linking(p,number,&oldx,&oldy,&movement);
        Sprite2_array sheet=0;draw_player_level_end(p,graphic,&sheet);
        update_player_movement(p,number,graphic,&oldx,&oldy,&movement);
        update_sidekick_positions(p);
        change_player_weapon_mode(p);
        fire_main_weapons(p,number,oldx,oldy);
        fire_superbomb(p,number,oldx,oldy);
        update_super_charge_weapon(p,number,graphic,oldx,oldy);
        fire_sidekick_weapons(p,number,oldx,oldy);
        draw_player_sidekicks(p,number);
        uint32_t direction,rotation;memcpy(&direction,&linkGunDirec,4);memcpy(&rotation,&optionSatelliteRotate,4);
        printf("state:%d,%d,%u,%u,%d,%d,%d\n",twoPlayerLinked,reallyEndLevel,direction,rotation,optionAttachmentLinked,optionAttachmentReturn,optionAttachmentMove);
        printf("player:%d,%d,%d,%d,%u,%u\n",p->x,p->y,p->x_velocity,p->y_velocity,p->x_friction_ticks,p->y_friction_ticks);
        for(int i=0;i<2;i++)printf("sidekick:%d,%d\n",p->sidekick[i].x,p->sidekick[i].y);
        printf("history:");for(int i=0;i<20;i++)printf("%d,%d,",p->old_x[i],p->old_y[i]);puts("");
        printf("shots:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",shotMultiPos[4],shotRepeat[1],soundQueue[2],soundQueue[3],power,
            shotRepeat[0],shotMultiPos[0],shotMultiPos[1],player[0].superbombs,player[1].superbombs,shotRepeat[6],shotRepeat[7]);
        printf("charge:%u,%u,%u,%u,%u,%u,%u\n",chargeLevel,chargeWait,chargeGr,chargeGrWait,shotRepeat[2],shotRepeat[3],shotRepeat[5]);
        printf("mode:%u,%u,%d,%d,%u,%u\n",p->weapon_mode,player[0].items.special,portConfigChange,portConfigDone,shotMultiPos[8],shotMultiPos[10]);
        for(int i=0;i<2;i++)printf("ammo:%d,%u,%u,%u,%d,%u\n",p->sidekick[i].ammo,p->sidekick[i].ammo_refill_ticks,p->sidekick[i].charge,p->sidekick[i].charge_ticks,p->sidekick[i].animation_enabled,p->sidekick[i].animation_frame);
        uint32_t hash=2166136261u;for(int i=0;i<64000;i++)hash=(hash^pixels[i])*16777619u;printf("hud:%u\n",hash);
        for(int i=0;i<10;i++) {
            PlayerShotDataType *s=&playerShotData[i];
            printf("shot:%u,%d,%d,%d,%d,%u\n",shotAvail[i],s->shotX,s->shotY,s->shotXM,s->shotYM,s->shotGr);
        }
    }
    ship_trace=true;
    for(int mode=0;mode<2048;mode++) {
        memset(player,0,sizeof player);memset(playerShotData,0,sizeof playerShotData);
        memset(shotAvail,0,sizeof shotAvail);memset(shotMultiPos,0,sizeof shotMultiPos);
        memset(shotRepeat,0,sizeof shotRepeat);memset(soundQueue,0,sizeof soundQueue);
        mt_srand(mode+19);power=100;endLevel=mode&1;isNetworkGame=mode&2;
        background2=mode&4;superWild=mode&8;constantLastX=(mode%255)-128;
        mapX2Ofs=(mode%37)-18;
        explosionFollowAmountX=-12;explosionFollowAmountY=-13;
        JE_byte number=(mode&16)?2:1;Player *p=&player[number-1];
        p->x=mode%301;p->y=mode%201-20;p->x_velocity=mode%13-6;
        p->last_x_explosion_follow=mode%310;p->last_y_explosion_follow=mode%181;
        p->invulnerable_ticks=(mode&32)?3:0;
        p->sidekick[0].style=mode%5;p->sidekick[1].style=(mode/5)%5;
        p->sidekick[0].x=1;p->sidekick[0].y=2;p->sidekick[1].x=3;p->sidekick[1].y=4;
        shotRepeat[9]=(mode&64)?2:0;
        JE_word mousex=mode%290,mousey=mode%190,graphic=mode%3;Sprite2_array sheet=spriteSheet9;
        printf("shipcase:%d\n",mode);
        draw_player_ship(p,number,graphic,&sheet,&mousex,&mousey);
        printf("shipstate:%d,%d,%d,%u,%d,%d,%d,%d,%u,%u,%u\n",p->x,p->y,constantLastX,p->invulnerable_ticks,
            explosionFollowAmountX,explosionFollowAmountY,p->last_x_explosion_follow,p->last_y_explosion_follow,
            shotRepeat[9],shotMultiPos[9],(uint32_t)mt_rand());
        printf("shipoptions:%d,%d,%d,%d\n",p->sidekick[0].x,p->sidekick[0].y,p->sidekick[1].x,p->sidekick[1].y);
        for(int i=0;i<2;i++) {
            PlayerShotDataType *s=&playerShotData[i];
            printf("spark:%u,%d,%d,%d,%d,%u\n",shotAvail[i],s->shotX,s->shotY,s->shotXM,s->shotYM,s->shotGr);
        }
    }
    for(int mode=0;mode<1768;mode++) {
        memset(SFCurrentCode,0,sizeof SFCurrentCode);memset(SFExecuted,0,sizeof SFExecuted);
        int combo=mode%26,ship=mode/26%17;JE_byte number=mode/442%2+1;superTyrian=mode/884;
        if(ship==14)ship=13;player[number-1].items.ship=ship;
        int length=0;while(keyboardCombos[combo][length]<100&&length<7)length++;
        for(int step=0;step<24;step++) {
            int key=keyboardCombos[combo][step%length];
            int variant=step/length;
            if(variant%3==1&&step%2==0)key=9;
            if(variant%3==2&&step%3==0)key=10;
            if(variant%3==2&&step%3==1)key=11;
            for(int i=0;i<4;i++)button[i]=false;button[0]=key>=5&&key<=8;
            int x=100,y=100,oldx=100,oldy=100,direction=(key-1)%4+1;
            if(key<=8) {if(direction==1)y--;if(direction==2)y++;if(direction==3)x--;if(direction==4)x++;}
            if(key==10){x++;y++;}
            if(key==11)button[0]=true;
            recognize_special(number,x,y,oldx,oldy);
            printf("code:%d,%d,%u:",mode,step,SFExecuted[number-1]);
            for(int i=0;i<21;i++)printf("%u,",SFCurrentCode[number-1][i]);puts("");
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
