"""Compile native linked boss-bar selection, placement, shading, and flash decay."""

import argparse
from pathlib import Path
import build_enemy_reference


def build_source(root):
    base = build_enemy_reference.build_source(root).split("int main(int argc,char **argv)")[0]
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    hud = source[source.index("static void JE_barX("):]
    return base + r"""
static bool levelTimer;
static void fill_rectangle_xy(SDL_Surface *surface,int x1,int y1,int x2,int y2,JE_byte color) {
    (void)surface;printf("fill:%d,%d,%d,%d,%u\n",x1,y1,x2,y2,color);
}
""" + hud + r"""
int main(void) {
    for(int mode=0;mode<512;mode++) {
        memset(enemy,0,sizeof enemy);memset(enemyAvail,1,sizeof enemyAvail);
        boss_bar[0].link_num=(mode&1)?2:0;boss_bar[1].link_num=(mode&2)?3:0;
        boss_bar[0].armor=42;boss_bar[1].armor=43;
        boss_bar[0].color=mode%256;boss_bar[1].color=(mode+127)%256;
        levelTimer=(mode&4)!=0;
        for(int i=0;i<12;i++) {
            enemyAvail[i]=(mode&8)?1:i%3;enemy[i].linknum=i%2+2;
            enemy[i].armorleft=(mode&16)?255:(mode&32)?0:(mode+i*37)%255+1;
        }
        printf("bars:%d\n",mode);draw_boss_bar();
        printf("state:%u,%u,%u,%u,%u,%u\n",boss_bar[0].link_num,boss_bar[0].armor,boss_bar[0].color,boss_bar[1].link_num,boss_bar[1].armor,boss_bar[1].color);
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
