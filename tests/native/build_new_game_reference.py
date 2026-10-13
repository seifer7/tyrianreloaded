"""Compare native normal, super-arcade and SuperTyrian new-game flows."""

import argparse
from pathlib import Path
import build_player_progression_reference
from build_joystick_reference import function


def build_source(root):
    base = build_player_progression_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace('static void JE_dString(int *screen,int x,int y,const char *text,int font) {puts("insertcoin");}',
                        'static void JE_dString(int *screen,int x,int y,const char *text,int font) {printf("title:%d,%s,%d\\n",y,text,font);}')
    base = base.replace('static const char *miscTextB[4]={"","","","got"};',
                        'static char secondary_text[5][11];\n#define miscTextB secondary_text')
    start = base.index("static void blit_sprite2x2(")
    end = base.index("static void blit_sprite2x2_darken(", start)
    base = base[:start] + r"""
static void blit_sprite2x2(int *s,int x,int y,Sprite2_array sheet,int graphic){(void)s;(void)x;(void)y;(void)sheet;printf("ship:%d\n",graphic);}
""" + base[end:]
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    ships = varz[varz.index("const JE_byte SAShip["):varz.index("const JE_word SAWeapon[")]
    return base + r"""
#undef FONT_SHAPES
#undef SMALL_FONT_SHAPES
#define FONT_SHAPES 0
#define SMALL_FONT_SHAPES 1
#define SDL_SCANCODE_SCROLLLOCK 71
#define SA_NORTSHIPZ 7
#define V_DANGER 35
#define EPISODE_AVAILABLE 4
static bool gameLoaded,richMode,constantDie;
static int mode,keysactive[512];
static uint8_t inputDevice[2],colors[256];
static char superShips[11][26],pName[21][16];
static bool gameplaySelect(void){puts("gameplay");return (mode&1)!=0;}
static bool episodeSelect(void){puts("episode");return (mode&2)!=0;}
static bool difficultySelect(void){puts("difficulty");return (mode&4)!=0;}
static void JE_loadPic(void *s,int n,bool store){(void)s;(void)store;printf("picture:%d\n",n);}
static void JE_clr256(void *s){(void)s;puts("clear");}
static int JE_fontCenter(const char *s,int f){(void)s;(void)f;return 100;}
static void JE_showVGA(void){puts("present");}
static void fade_palette(void *p,int n,int first,int last){(void)p;(void)first;(void)last;printf("fadein:%d\n",n);}
static void fade_black(int n){printf("fade:%d\n",n);}
static void wait_input(bool k,bool m,bool j){(void)k;(void)m;(void)j;puts("input");}
static void wait_noinput(bool k,bool m,bool j){(void)k;(void)m;(void)j;puts("noinput");}
static void JE_outText(void *s,int x,int y,const char *t,int h,int b){(void)s;(void)x;(void)h;(void)b;printf("text:%d,%s\n",y,t);}
static void JE_playSampleNum(int n){printf("sample:%d\n",n);}
static void JE_initEpisode(int n){printf("init:%d\n",n);episodeNum=n;}
""" + ships + "".join(function(source, name) for name in ("newGame", "newSuperArcadeGame", "newSuperTyrianGame")) + r"""
static void reset(int m){
    mode=m;memset(player,0,sizeof player);
    gameLoaded=mode&8;onePlayerAction=mode&16;twoPlayerMode=mode&32;richMode=mode&64;
    superTyrian=mode&128;superArcadeMode=5;constantDie=true;
    difficultyLevel=(int8_t)((mode/8)%256);initialDifficulty=-3;episodeNum=mode%4+1;
    inputDevice[0]=7;inputDevice[1]=8;keysactive[71]=(mode&128)!=0;
    for(int p=0;p<2;p++){player[p].cash=999+p;player[p].items.ship=ITEM_INDEX_STORE[ITEM_SHIP_USPTALON];
        player[p].items.weapon[0].id=ITEM_INDEX_STORE[ITEM_WEAPONFRONT_PULSECANNON];
        player[p].items.special=3;player[p].items.sidekick[0]=4;player[p].items.sidekick[1]=5;}
}
static void dump(void){
    printf("state:%d,%d,%d,%d,%u,%d,%d,%d,%u,%u,%u\n",gameLoaded,onePlayerAction,twoPlayerMode,superTyrian,superArcadeMode,
        constantDie,difficultyLevel,initialDifficulty,episodeNum,inputDevice[0],inputDevice[1]);
    for(int p=0;p<2;p++)printf("player:%lu,%u,%u,%u,%u,%u\n",player[p].cash,player[p].items.ship,
        player[p].items.weapon[0].id,player[p].items.special,player[p].items.sidekick[0],player[p].items.sidekick[1]);
}
int main(int argc,char **argv){
    if(argc!=2)return 2;load_typed_items(argv[1]);
    for(int i=0;i<11;i++)sprintf(superShips[i],"Super%d",i);
    strcpy(miscTextB[4],"0123456789");strcpy(pName[0],"ABCDEFGHIJKLMNO");
    for(int m=0;m<2048;m++){reset(m);printf("normal:%d\n",m);printf("result:%d\n",newGame());dump();}
    for(int m=0;m<256;m++)for(unsigned int i=0;i<7;i++){reset(m);printf("arcade:%d,%u\n",m,i);
        printf("result:%d\n",newSuperArcadeGame(i));dump();}
    for(int m=0;m<256;m++){reset(m);printf("super:%d\n",m);newSuperTyrianGame();dump();}
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
