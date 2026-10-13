"""Verify unchanged new-game player initialization, shop values and difficulty progression."""

import argparse
from pathlib import Path
import build_pickups_reference
from build_joystick_reference import function


def build_source(root):
    base = build_pickups_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    base = base.replace("super_arcade_mode,generator;", "super_arcade_mode,generator,shield;")
    source = (root / "src" / "mainint.c").read_text(encoding="utf-8")
    def extract(name):
        native, output = {
            "weapon_upgrade_cost": ("long", "long"),
            "JE_getCost": ("ulong", "ulong"),
            "JE_totalScore": ("ulong", "ulong"),
            "JE_getValue": ("JE_longint", "int32_t"),
        }.get(name, ("void", "void"))
        tagged = source.replace(f"{native} {name}(", f"int {name}(")
        return function(tagged, name).replace(f"int {name}(", f"{output} {name}(", 1)
    return base + r"""
typedef unsigned long ulong;
#define miscText pickup_text
static JE_word upgradeCost,downgradeCost;
static bool gameHasRepeated;
static JE_byte mainLevel,saveLevel,secretHint;
static JE_shortint initialDifficulty;
static char lastLevelName[32];
#define FIRST_LEVEL 1
#define SA_NONE 0
#define DIFFICULTY_EASY 1
#define DIFFICULTY_NORMAL 2
#define DIFFICULTY_HARD 3
#define DIFFICULTY_IMPOSSIBLE 4
#define DIFFICULTY_INSANITY 5
#define DIFFICULTY_SUICIDE 6
#define DIFFICULTY_MANIACAL 7
#define DIFFICULTY_ZINGLON 8
#define DIFFICULTY_NORTANEOUS 9
int32_t JE_getValue(JE_byte,JE_word);
""" + "".join(extract(name) for name in
              ("weapon_upgrade_cost", "JE_getCost", "JE_getValue", "JE_totalScore", "JE_initPlayerData", "adjust_difficulty")) + r"""
static JE_longint test_cash_left(JE_byte item_type) {
    JE_longint cash_left = (JE_longint)player[0].cash;
    JE_word item_num;
    switch (item_type) {
        case 2: item_num = player[0].items.ship; break;
        case 3: item_num = player[0].items.weapon[0].id; break;
        case 4: item_num = player[0].items.weapon[1].id; break;
        case 5: item_num = player[0].items.shield; break;
        case 6: item_num = player[0].items.generator; break;
        case 7: item_num = player[0].items.sidekick[0]; break;
        case 8: item_num = player[0].items.sidekick[1]; break;
        default: return 0;
    }
    cash_left -= JE_getCost(item_type, item_num);
    JE_word accumulated_upgrade_cost = 0;
    if (item_type == 3 || item_type == 4) {
        unsigned int port = item_type - 3;
        for (unsigned int i = 1; i < player[0].items.weapon[port].power; ++i) {
            accumulated_upgrade_cost += weaponPort[item_num].cost * i;
            cash_left -= accumulated_upgrade_cost;
        }
    }
    return cash_left;
}
""" + r"""
int main(int argc,char **argv) {
    if(argc!=2)return 2;load_typed_items(argv[1]);
    strcpy(pickup_text[19],"Start");
    const long base_costs[]={INT32_MIN,INT32_MAX,-1,0,1,65535};
    for(unsigned int i=0;i<COUNTOF(base_costs);i++)for(unsigned int power=0;power<=11;power++)
        printf("upgrade:%u,%u,%ld\n",i,power,weapon_upgrade_cost(base_costs[i],power));
    for(int mode=0;mode<512;mode++) {
        memset(player,0,sizeof player);mt_srand(mode+1);
        twoPlayerMode=mode&1;onePlayerAction=true;superTyrian=true;superArcadeMode=7;gameHasRepeated=true;
        for(int i=0;i<2;i++) {
            player[i].items.sidekick_level=mode;player[i].items.sidekick_series=mode+1;
            player[i].items.super_arcade_mode=mode+2;
            player[i].items.weapon[0].power=mode%11+1;player[i].items.weapon[1].power=(mode+1)%11+1;
            player[i].cash=(mode&256)?ULONG_MAX-mode-i:mode*97+i;
        }
        printf("init:%d\n",mode);JE_initPlayerData();
        printf("flags:%d,%d,%d,%u,%d,%u,%u,%u,%s\n",gameHasRepeated,onePlayerAction,superTyrian,superArcadeMode,
            twoPlayerMode,secretHint,mainLevel,saveLevel,lastLevelName);
        for(int i=0;i<2;i++) {
            dump_items(&player[i].items);dump_items(&player[i].last_items);
            printf("extras:%u,%u,%u,%d,%lu,%u\n",player[i].items.shield,player[i].armor,player[i].weapon_mode,
                player[i].is_dragonwing,player[i].cash,*player[i].lives);
        }
        printf("rng:%u\n",(uint32_t)mt_rand());
        twoPlayerMode=mode&1;
        for(int i=0;i<2;i++){player[i].items.weapon[0].power=mode%11+1;player[i].items.weapon[1].power=(mode+3)%11+1;}
        upgradeCost=12;downgradeCost=13;
        for(int type=0;type<10;type++)for(int index=0;index<3;index++) {
            ulong cost=JE_getCost(type,index);int32_t value=JE_getValue(type,index);
            printf("value:%d,%d,%lu,%d,%u,%u\n",type,index,cost,value,upgradeCost,downgradeCost);
        }
        printf("specialship:%lu\n",JE_getCost(2,91));
        printf("total:%lu,%lu\n",JE_totalScore(&player[0]),JE_totalScore(&player[1]));
        const unsigned long scores[]={0,1,9999,10000,19999,20000,39999,40000,49999,50000,69999,70000,
            79999,80000,124999,125000,149999,150000,199999,200000,299999,300000,399999,400000,
            599999,600000,999999,1000000,1999999,2000000,2999999,3000000};
        for(int level=1;level<10;level++)for(unsigned int j=0;j<COUNTOF(scores);j++) {
            player[0].cash=scores[j];player[1].cash=j;initialDifficulty=level;difficultyLevel=mode%11;
            adjust_difficulty();printf("difficulty:%d,%u,%d\n",level,j,difficultyLevel);
        }
        const ulong cash_values[]={0,1,100000,ULONG_MAX,ULONG_MAX-1};
        const JE_byte powers[]={1,2,11};
        for(unsigned int c=0;c<COUNTOF(cash_values);c++) {
            player[0].cash=cash_values[c];
            for(unsigned int p=0;p<COUNTOF(powers);p++) {
                player[0].items.weapon[0].power=powers[p];player[0].items.weapon[1].power=powers[p];
                for(JE_byte type=2;type<=8;type++)
                    printf("cash-left:%d,%lu,%u,%u,%d\n",mode,player[0].cash,powers[p],type,test_cash_left(type));
            }
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
