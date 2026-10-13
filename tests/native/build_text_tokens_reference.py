"""Trace native mutable integer/reference tokens and platform long narrowing."""

import argparse
from pathlib import Path
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "opentyr.c").read_text(encoding="utf-8")
    return r"""
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <setjmp.h>
static jmp_buf failed;
static void finish(int code){(void)code;longjmp(failed,1);}
#define exit finish
""" + function(source, "str_pop_int") + function(source, "str_pop_str") + r"""
static void hex(const char *s){for(;*s;s++)printf("%02X",(unsigned char)*s);}
int main(void){
    const char *inputs[]={""," \t ","12 34","-12+34","+ 7"," -0\n8","0x12","2147483647","2147483648",
        "-2147483648","-2147483649","4294967295","9223372036854775807","9223372036854775808",
        "-9223372036854775808","-9223372036854775809","999999999999999999999999999999999999999999",
        "A\tB C","\nA B","abcdefghij","abcdefghijk"," \t0 \tABC\t","12\0ignored","\200\377 ABC"};
    for(unsigned int mode=0;mode<192;mode++){
        char buffer[256];strcpy(buffer,inputs[mode%24]);printf("case:%u\n",mode);
        for(unsigned int step=0;step<4;step++){
            if(mode<96){int value=123;bool result=str_pop_int(buffer,&value);
                printf("int:%d,%d,",result,value);hex(buffer);puts("");}
            else{char token[256]={0};bool terminated=setjmp(failed)!=0;bool result=false;
                if(!terminated)result=str_pop_str(buffer,token,mode/24-4==0?0:mode/24-4==1?1:mode/24-4==2?11:256);
                printf("token:%d,%d,",terminated,result);hex(token);printf(",");hex(buffer);puts("");
                if(terminated)break;}
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
