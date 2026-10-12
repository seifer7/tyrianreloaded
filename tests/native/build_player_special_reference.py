"""Generate unchanged native special-weapon effects, costs, flares and HUD oracle."""

import argparse
from pathlib import Path
import build_player_movement_reference
from build_joystick_reference import function


def build_source(root):
    base = build_player_movement_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace("uint weapon_mode,superbombs;", "uint weapon_mode,superbombs,armor,shield,shield_max;\nint last_x_shot_move,last_y_shot_move;")
    base = base.replace("sidekick[2],special,ship;", "sidekick[2],special,ship,sidekick_level;")
    base = base.replace("uint weapon_mode,superbombs,armor,shield,shield_max;",
                        "uint weapon_mode,superbombs,armor,shield,shield_max,exploding_ticks,initial_armor,purple_balls_needed; JE_byte *lives;")
    base = base.replace("static void JE_SFCodes(", """
static void recognize_special(JE_byte,JE_integer,JE_integer,JE_integer,JE_integer);
static void JE_SFCodes(""")
    base = base.replace("static void JE_setupExplosion(", """static bool frame_trace;
static int frame_explosion_count,frame_explosion_x[200],frame_explosion_y[200];
static void JE_setupExplosion(""")
    base = base.replace('printf("E:%d:%d:%u\\n", x, y, type);', """if(frame_trace) {
        if(y>-16&&y<190&&frame_explosion_count<200) {
            frame_explosion_x[frame_explosion_count]=x;frame_explosion_y[frame_explosion_count++]=y;
        }
    } else printf("E:%d:%d:%u\\n", x, y, type);""")
    base = base.replace('printf("codes:%u,%d,%d,%u,%u\\n",number,x,y,oldx,oldy);',
                        'if(frame_trace)recognize_special(number,x,y,oldx,oldy);else printf("codes:%u,%d,%d,%u,%u\\n",number,x,y,oldx,oldy);')
    base = base.replace("static struct {JE_integer ex, ey; bool scoreitem;} enemy[100];",
                        "static struct {JE_integer ex,ey,evalue;JE_shortint exc,eyc;bool scoreitem;} enemy[100];")
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    graphics = (root / "src" / "vga256d.c").read_text(encoding="utf-8")
    bars = (root / "src" / "nortvars.c").read_text(encoding="utf-8")
    hud = "".join(function(varz, name) for name in ("JE_wipeShieldArmorBars", "JE_drawShield", "JE_drawArmor"))
    drawing = function(graphics, "JE_rectangle") + function(graphics, "JE_barBright") + function(bars, "JE_dBar3")
    specials = varz[varz.index("void JE_specialComplete("):varz.index("void JE_setupExplosion(")]
    mainint = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    attacks = mainint[mainint.index("static void update_player_attacks("):mainint.index("static void draw_player_sidekicks(")]
    options = function(varz, "JE_drawOptionLevel") + function(varz, "JE_drawOptions")
    death = mainint[mainint.index("static bool update_player_death("):mainint.index("static void collect_player_movement_input(")]
    purple = function((root / "src" / "player.c").read_text(encoding="utf-8"), "calc_purple_balls_needed")
    frame = function(mainint, "JE_playerMovement")
    players_frame = function(mainint, "JE_mainGamePlayerFunctions")
    return base + r"""
#define FRONT_WEAPON 0
#define ENEMY_SHOT_MAX 60
#define SA 7
#define S_SOUL_OF_ZINGLON 21
#define S_POWERUP 29
static struct {JE_integer sx,sy,sxm,sym;} enemyShot[60];
static bool enemyShotAvail[60],flareStart,linkToPlayer,spraySpecial,fireButtonHeld,filterActive;
static JE_byte temp2,nextSpecialWait,specialWait,zinglonDuration,astralDuration;
static JE_shortint specialWeaponFilter,specialWeaponFreq,flareColChg,levelFilter,levelBrightness;
static JE_word flareDuration,specialWeaponWpn;
static JE_word mouseX,mouseY;
static SDL_Surface *game_screen;
#define S_EXPLOSION_9 9
#define S_EXPLOSION_11 11
#define SMALL_FONT_SHAPES 0
static JE_word levelEndFxWait;
static bool onePlayerAction,allPlayersGone,constantDie,play_demo;
static JE_byte episodeNum;
static JE_byte inputDevice[2];
static JE_word shipGr,tempX,mouseXB,mouseYB,mapXOfs,oldMapXOfs,mapX3Ofs,mapXPos,mapX2Pos,mapX3Pos;
static intptr_t mapXbpPos,mapX2bpPos,mapX3bpPos;
static bool background3x1;
static Sprite2_array frame_sheet;
static Sprite2_array *shipGrPtr=&frame_sheet,*shipGr2ptr=&frame_sheet;
static const char *miscText[8]={"","","","","","","","insert coin"};
static void JE_dString(int *screen,int x,int y,const char *text,int font) {puts("insertcoin");}
static void JE_setupExplosionLarge(bool ground,JE_byte type,int x,int y) {assert(!"Explosion frames use the separate lifecycle oracle");}
static int frame_mode;
#define WITH_NETWORK
typedef uint16_t Uint16;
typedef int16_t Sint16;
static JE_byte thisPlayerNum;
static int network_delay;
static bool frame_reset;
static struct {uint8_t data[28];} frame_out[16],frame_in[1],*packet_state_out[16],*packet_state_in[1];
static Uint16 SDLNet_Read16(const void *p) {const uint8_t *b=p;return (b[0]<<8)|b[1];}
static void SDLNet_Write16(Uint16 value,void *p) {uint8_t *b=p;b[0]=value>>8;b[1]=value;}
static bool network_state_is_reset(void) {return frame_reset;}
static void network_state_prepare(void) {
    memset(frame_out[0].data,0,28);SDLNet_Write16(0x41,frame_out[0].data);
    SDLNet_Write16(frame_reset?0:network_delay,frame_out[0].data+2);
}
static void collect_player_movement_input(Player *p,JE_byte device,JE_byte number,JE_word *oldx,JE_word *oldy,JE_PlayerMovementState *m) {
    if(isNetworkGame && number!=thisPlayerNum)return;
    Player *target=play_demo?&player[0]:p;
    target->x+=(frame_mode&64)?1:-1;target->y+=(frame_mode&128)?1:-1;
    m->accelXC=p->x-*oldx;m->accelYC=p->y-*oldy;
    for(int i=0;i<4;i++)button[i]=(frame_mode>>(i+8))&1;
    if(isNetworkGame) {
        SDLNet_Write16(p->x-*oldx,frame_out[0].data+4);SDLNet_Write16(p->y-*oldy,frame_out[0].data+6);
        SDLNet_Write16(m->accelXC,frame_out[0].data+8);SDLNet_Write16(m->accelYC,frame_out[0].data+10);
        Uint16 bits=0;for(int i=0;i<4;i++)bits|=button[i]<<i;
        SDLNet_Write16(bits,frame_out[0].data+12);
        p->x=*oldx;p->y=*oldy;m->accelXC=m->accelYC=0;memset(button,0,sizeof button);
    }
}
""" + drawing + hud + options + specials + attacks + purple + death + frame + players_frame + r"""
static uint32_t hash(SDL_Surface *s) {
    uint32_t h=2166136261u;for(int i=0;i<s->pitch*s->h;i++)h=(h^s->pixels[i])*16777619u;return h;
}
int main(int argc,char **argv) {
    if(argc!=2)return 2;load_typed_items(argv[1]);
    uint8_t gamePixels[65400],hudPixels[65400];
    SDL_Surface game={320,200,327,gamePixels},hudSurface={320,200,327,hudPixels};
    game_screen=&game;VGAScreenSeg=&hudSurface;VGAScreen=(int*)&game;
    spriteSheet8=(int*)(uintptr_t)8;spriteSheet9=(int*)(uintptr_t)9;spriteSheet12=(int*)(uintptr_t)12;spriteSheet10=(int*)(uintptr_t)10;
    for(int mode=0;mode<20480;mode++) {
        memset(player,0,sizeof player);memset(playerShotData,0,sizeof playerShotData);memset(shotAvail,0,sizeof shotAvail);
        memset(shotMultiPos,3,sizeof shotMultiPos);memset(shotRepeat,0,sizeof shotRepeat);memset(soundQueue,0,sizeof soundQueue);
        memset(SFExecuted,0,sizeof SFExecuted);memset(gamePixels,166,sizeof gamePixels);memset(hudPixels,69,sizeof hudPixels);
        mt_srand(mode+31);power=100;twoPlayerMode=mode&1;galagaMode=mode&2;
        superArcadeMode=mode%10;temp2=mode%256;nextSpecialWait=17;specialWait=(mode&4)?2:0;
        zinglonDuration=(mode&8)?mode%51:0;astralDuration=mode%3;
        flareDuration=(mode&16)?mode%4:0;flareStart=mode&32;linkToPlayer=mode&64;spraySpecial=mode&128;
        fireButtonHeld=mode&256;filterActive=mode&512;specialWeaponFilter=(mode&1024)?7:-99;
        specialWeaponFreq=mode%11-2;flareColChg=-1;levelFilter=(mode&2048)?7:-99;levelBrightness=(mode&4096)?-99:mode%19-9;
        for(int i=0;i<2;i++) {
            player[i].x=100+i*40;player[i].y=80+i*10;player[i].armor=mode%30;player[i].shield=mode%31;player[i].shield_max=31;
            player[i].items.weapon[0].power=mode%11+1;player[i].items.sidekick[0]=(mode&64)?ITEM_INDEX_STORE[ITEM_SIDEKICK_COMPANIONSHIPQUICKSILVER]:0;
            player[i].items.sidekick_level=mode%8+98;
            player[i].items.weapon[1].power=mode%11+1;player[i].weapon_mode=mode%2+1;
            player[i].items.weapon[0].id=ITEM_INDEX_STORE[ITEM_WEAPONFRONT_NORTSHIPSUPERPULSE];
            player[i].items.weapon[1].id=ITEM_INDEX_STORE[ITEM_WEAPONREAR_NORTSHIPSPREADER];
            player[i].is_alive=(mode&32)==0;player[i].superbombs=mode%3;
            player[i].last_x_shot_move=97+i*30;player[i].last_y_shot_move=78+i*10;
            if(mode>=8192)for(int j=0;j<2;j++){
                player[i].items.sidekick[j]=ITEM_INDEX_STORE[ITEM_SIDEKICK_COMPANIONSHIPQUICKSILVER];
                player[i].sidekick[j].x=90+j*20;player[i].sidekick[j].y=80;player[i].sidekick[j].style=mode%5;
                player[i].sidekick[j].ammo_max=25;player[i].sidekick[j].ammo=mode%26;
                player[i].sidekick[j].ammo_refill_ticks=mode%3;player[i].sidekick[j].ammo_refill_ticks_max=7;
                player[i].sidekick[j].charge=mode%4;
            }
        }
        for(int i=0;i<100;i++) {
            enemyAvail[i]=i%3;enemy[i].scoreitem=i%2;enemy[i].evalue=i%4;
            enemy[i].ex=100+i%5-2;enemy[i].ey=80+i%7-3;enemy[i].exc=(JE_shortint)(mode%256-128);enemy[i].eyc=(JE_shortint)(127-mode%256);
        }
        for(int i=0;i<60;i++) {
            enemyShotAvail[i]=i%2;enemyShot[i].sx=100+i%5-2;enemyShot[i].sy=80+i%7-3;
            enemyShot[i].sxm=mode%65536-32768;enemyShot[i].sym=32767-mode%65536;
        }
        char id[40];sprintf(id,"case%d",mode%114);enum ITEM_TYPE type=ItemType_Special;int index=item_idx(&type,id);
        type=ItemType_Shot;specialWeaponWpn=item_idx(&type,"147");
        player[0].items.special=(mode&2048)?index:0;JE_byte number=mode%2+1;SFExecuted[number-1]=(mode&1024)?index:0;
        shotRepeat[8]=(mode&512)?1:0;shotRepeat[10]=2;shotAvail[80]=9;
        button[0]=mode&128;button[1]=mode&256;button[2]=mode&512;button[3]=false;
        mouseX=98;mouseY=77;
        moveOk=mode&8;endLevel=mode&16;chargeLevel=mode%6;chargeWait=mode%7;chargeMax=5;
        chargeGr=mode%4;chargeGrWait=mode%5;difficultyLevel=mode%11;
        portConfigChange=mode&256;portConfigDone=mode&128;optionSatelliteRotate=0;
        optionAttachmentLinked=optionAttachmentReturn=false;optionAttachmentMove=0;
        printf("specialcase:%d\n",mode);
        if(mode>=12288) {
            for(int i=0;i<2;i++) {
                player[i].lives=&player[i].items.weapon[i].power;*player[i].lives=3;
                player[i].initial_armor=57;player[i].items.ship=0;
                player[i].weapon_mode=(i==0&&twoPlayerMode?mode%2:mode%4)+1;
                player[i].last_x_explosion_follow=90;player[i].last_y_explosion_follow=75;
            }
            frame_mode=mode;frame_trace=ship_trace=true;frame_explosion_count=0;episodeNum=(mode&128)?4:1;
            onePlayerAction=mode&64;allPlayersGone=false;constantDie=false;
            isNetworkGame=mode<16384&&(mode&2048);thisPlayerNum=(mode&256)?2:1;network_delay=mode%3+1;frame_reset=mode&64;
            for(int i=0;i<16;i++) {
                memset(frame_out[i].data,0,28);packet_state_out[i]=&frame_out[i];
                SDLNet_Write16(0x41,frame_out[i].data);SDLNet_Write16(0,frame_out[i].data+2);
                SDLNet_Write16(2,frame_out[i].data+4);SDLNet_Write16((Uint16)-3,frame_out[i].data+6);
                SDLNet_Write16(1,frame_out[i].data+8);SDLNet_Write16((Uint16)-1,frame_out[i].data+10);
                SDLNet_Write16(mode%16,frame_out[i].data+12);SDLNet_Write16(mode%11,frame_out[i].data+16);
            }
            memcpy(frame_in[0].data,frame_out[0].data,28);packet_state_in[0]=&frame_in[0];
            play_demo=mode<16384&&!isNetworkGame&&(mode&128);superTyrian=false;superWild=mode&256;
            background2=mode&512;mapX2Ofs=mode%43;levelEnd=17;levelEndWarp=3;
            reallyEndLevel=false;twoPlayerLinked=mode&1024;
            linkGunDirec=0;constantLastX=0;
            memset(SFCurrentCode,0,sizeof SFCurrentCode);
            explosionFollowAmountX=5;explosionFollowAmountY=6;shipGr2=3;
            Sprite2_array sheet=spriteSheet9;mouseXB=98;mouseYB=77;
            if(mode>=16384) {
                frame_sheet=sheet;shipGr=mode%3;shipGr2=(mode+1)%3;inputDevice[0]=inputDevice[1]=1;
                mapXOfs=oldMapXOfs=0;mapX2Ofs=mode%43;mapX3Ofs=0;background3x1=mode&64;
                levelEndWarp=(mode&128)?127:-128;
                JE_mainGamePlayerFunctions();
                JE_mainGamePlayerFunctions();
                printf("parallax:%u,%u,%u,%u,%u,%u,%lld,%lld,%lld,%u,%u,%u,%u,%d\n",
                    oldMapXOfs,mapXOfs,mapX2Ofs,mapX3Ofs,mapXPos,mapX2Pos,
                    (long long)mapXbpPos,(long long)mapX2bpPos,(long long)mapX3bpPos,
                    mapX3Pos,mouseX,mouseY,mouseXB,levelEndWarp);
                printf("secondold:%u\n",mouseYB);
                for(int i=0;i<2;i++)printf("paired:%d,%d,%d,%d,%d,%d,%d\n",
                    player[i].x,player[i].y,player[i].x_velocity,player[i].y_velocity,
                    player[i].is_alive,player[i].delta_x_shot_move,player[i].delta_y_shot_move);
            }
            else JE_playerMovement(&player[number-1],1,number,mode%3,&sheet,number==1?&mouseX:&mouseXB,number==1?&mouseY:&mouseYB);
            for(int i=0;i<frame_explosion_count;i++)printf("E:%d:%d:0\n",frame_explosion_x[i],frame_explosion_y[i]);
            if(isNetworkGame&&thisPlayerNum==number) {
                printf("framepacket:");for(int i=0;i<28;i++)printf("%02X",frame_out[0].data[i]);puts("");
            }
            printf("framedifficulty:%d\n",difficultyLevel);
            uint32_t frame_direction;memcpy(&frame_direction,&linkGunDirec,4);
            printf("framedirection:%u\n",frame_direction);
            printf("frame:%u,%u,%d,%d,%d,%u,%d,%u,%u,%u,%u,%u,%d,%d\n",
                number==1?mouseX:mouseXB,number==1?mouseY:mouseYB,moveOk,twoPlayerLinked,reallyEndLevel,levelEnd,endLevel,
                *player[0].lives,*player[1].lives,player[0].purple_balls_needed,player[1].purple_balls_needed,
                player[number-1].is_alive,explosionFollowAmountX,explosionFollowAmountY);
            Player *p=&player[number-1];printf("movement:%d,%d,%d,%d,%u,%u,%d,%d\n",
                p->x,p->y,p->x_velocity,p->y_velocity,p->x_friction_ticks,p->y_friction_ticks,
                p->last_x_explosion_follow,p->last_y_explosion_follow);
            printf("history:");for(int i=0;i<20;i++)printf("%d,%d,",p->old_x[i],p->old_y[i]);puts("");
            for(int i=0;i<21;i++)printf("combo:%u,%u\n",SFCurrentCode[0][i],SFCurrentCode[1][i]);
            frame_trace=ship_trace=false;
        }
        else if(mode<4096)JE_specialComplete(number,index);
        else if(mode<8192)JE_doSpecialShot(number,&player[number-1].armor,&player[number-1].shield);
        else update_player_attacks(&player[number-1],number,mode%3,&mouseX,&mouseY);
        printf("special:%u,%u,%u,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%d,%d,%d\n",
            nextSpecialWait,specialWait,zinglonDuration,astralDuration,flareDuration,flareStart,linkToPlayer,spraySpecial,fireButtonHeld,
            filterActive,specialWeaponFilter,specialWeaponFreq,flareColChg,levelFilter,specialWeaponWpn,temp2,levelBrightness,
            SFExecuted[0],SFExecuted[1]);
        for(int i=0;i<2;i++)printf("owner:%u,%u,%u,%u,%u\n",player[i].armor,player[i].shield,player[i].invulnerable_ticks,player[i].items.sidekick[0],player[i].items.sidekick[1]);
        for(int j=0;j<2;j++)for(int i=0;i<2;i++){
            printf("initialized:%d,%d,%u,%u,%u,%u,%d,%u,%u\n",player[j].sidekick[i].ammo,player[j].sidekick[i].ammo_max,
                player[j].sidekick[i].ammo_refill_ticks,player[j].sidekick[i].ammo_refill_ticks_max,player[j].sidekick[i].style,
                player[j].sidekick[i].animation_frame,player[j].sidekick[i].animation_enabled,player[j].sidekick[i].charge,player[j].sidekick[i].charge_ticks);
        }
        if(mode>=8192) {
            Player *p=&player[number-1];
            uint32_t rotation;memcpy(&rotation,&optionSatelliteRotate,4);
            printf("attack:%d,%d,%u,%u,%u,%u,%u,%u,%d,%d,%d\n",p->delta_x_shot_move,p->delta_y_shot_move,
                p->weapon_mode,p->superbombs,chargeLevel,chargeWait,chargeGr,chargeGrWait,portConfigChange,portConfigDone,rotation);
            for(int i=0;i<2;i++)printf("optionstate:%d,%d,%d,%u,%u,%u,%d\n",p->sidekick[i].x,p->sidekick[i].y,p->sidekick[i].ammo,
                p->sidekick[i].ammo_refill_ticks,p->sidekick[i].charge,p->sidekick[i].charge_ticks,p->sidekick[i].animation_enabled);
        }
        printf("shotstate:%u",power);for(int i=0;i<11;i++)printf(",%u,%u",shotRepeat[i],shotMultiPos[i]);puts("");
        printf("sounds:");for(int i=0;i<8;i++)printf("%u,",soundQueue[i]);puts("");
        printf("pixels:%u,%u\n",hash(&game),hash(&hudSurface));
        for(int i=0;i<100;i++)printf("attract:%d,%d\n",enemy[i].exc,enemy[i].eyc);
        for(int i=0;i<60;i++)printf("repel:%d,%d\n",enemyShot[i].sxm,enemyShot[i].sym);
        for(int i=0;i<MAX_PWEAPON;i++) {
            PlayerShotDataType *s=&playerShotData[i];
            printf("projectile:%u,%d,%d,%d,%d,%u,%u\n",shotAvail[i],s->shotX,s->shotY,s->shotXM,s->shotYM,s->shotGr,s->playerNumber);
        }
        printf("rng:%u\n",(uint32_t)mt_rand());
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
