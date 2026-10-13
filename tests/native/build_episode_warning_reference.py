"""Trace the unchanged native episode-warning state and encrypted text reader."""

import argparse
from pathlib import Path

from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "tyrian2.c").read_text(encoding="utf-8")
    source = source.replace("static void show_episode_warning(", "void show_episode_warning(")
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int8_t JE_shortint;
typedef FILE JE_FILE;
static bool constantPlay, ESCPressed, newkey, warningRed, levelWarningDisplay;
static JE_byte warningCol, warningSoundDelay, levelWarningLines;
static JE_shortint warningColChange;
static JE_word frameCountMax;
static char levelWarningText[10][61];
static void JE_wipeKey(void) { puts("wipe"); }
static void setDelay2(int frames) { printf("delay2:%d\n", frames); }
static void JE_displayText(void) { puts("display"); }
static void read_encrypted_pascal_string(char *out, size_t size, FILE *stream)
{
    static const unsigned char key[] = {204,129,63,255,71,19,25,62,1,99};
    unsigned char length, text[255];
    fread(&length, 1, 1, stream);
    fread(text, 1, length, stream);
    for (size_t i = length; i-- > 0;) {
        text[i] ^= key[i % sizeof(key)];
        if (i != 0) text[i] ^= text[i-1];
    }
    if (size == 0) return;
    if (length >= size) abort();
    memcpy(out, text, length);
    out[length] = 0;
}
""" + function(source, "show_episode_warning") + r"""
static void encrypt_write(FILE *stream, const unsigned char *text, size_t length)
{
    static const unsigned char key[] = {204,129,63,255,71,19,25,62,1,99};
    unsigned char encrypted[255], n = (unsigned char)length;
    fwrite(&n, 1, 1, stream);
    unsigned char previous = 0;
    for (size_t i = 0; i < length; i++) {
        encrypted[i] = text[i] ^ key[i % sizeof(key)] ^ previous;
        previous = encrypted[i];
    }
    fwrite(encrypted, 1, length, stream);
}
static void trace_lines(void)
{
    for (unsigned int i = 0; i < levelWarningLines; i++) {
        if (i) putchar(',');
        for (const unsigned char *p = (const unsigned char *)levelWarningText[i]; *p; p++)
            printf("%02X", *p);
    }
    putchar('\n');
}
int main(void)
{
    const char *counts[] = {
        "0","1","9","10","27"," 15tail","-1","65537","65539","abc","+","  -10","32767","999"
    };
    for (int mode = 0; mode < 256; mode++) {
        constantPlay = (mode & 1) != 0;
        ESCPressed = (mode & 2) != 0;
        newkey = (mode & 4) != 0;
        warningRed = (mode & 8) != 0;
        levelWarningDisplay = (mode & 16) != 0;
        warningCol = (JE_byte)(mode * 3);
        warningColChange = (JE_shortint)(mode - 128);
        warningSoundDelay = (JE_byte)(mode * 5);
        frameCountMax = (JE_word)(mode % 7 + 1);
        levelWarningLines = (JE_byte)(mode % 3);
        memset(levelWarningText, 0, sizeof(levelWarningText));
        strcpy(levelWarningText[0], "stale-a");
        strcpy(levelWarningText[1], "stale-b");
        FILE *stream = tmpfile();
        for (int i = 0; i < mode % 3 + 1; i++) {
            char line[64]; snprintf(line, sizeof(line), "Warning %03d-%d", mode, i);
            encrypt_write(stream, (unsigned char *)line, strlen(line));
        }
        encrypt_write(stream, (const unsigned char *)"#", 1);
        rewind(stream);
        char section[64], count_section[32];
        strcpy(count_section, counts[mode % (int)(sizeof(counts)/sizeof(counts[0]))]);
        snprintf(section, sizeof(section), "W?%c?%s", (mode & 64) ? 'y' : 'n', count_section);
        printf("case:%d,%d,%d,%s\n", mode, constantPlay, ESCPressed, section);
        show_episode_warning(stream, section);
        printf("state:%u,%d,%d,%u,%d,%u,%u,%ld,%u,", frameCountMax,warningRed,
            levelWarningDisplay,warningCol,warningColChange,warningSoundDelay,newkey,(long)ftell(stream),levelWarningLines);
        trace_lines();
        fclose(stream);
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
