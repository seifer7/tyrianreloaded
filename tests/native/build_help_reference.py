"""Generate an SDL-free oracle for the unchanged native help-box wrapping."""

import argparse
from pathlib import Path


def build_source(root):
    source = (root / "src" / "helptext.c").read_text(encoding="utf-8")
    wrap = source[source.index("void JE_helpBox("):source.index("void JE_HBox(")]
    return r"""
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <stdbool.h>
#include <string.h>
typedef uint8_t JE_byte;
typedef bool JE_boolean;
typedef int SDL_Surface;
#define MIN(a,b) ((a) < (b) ? (a) : (b))
#define FULL_SHADE 1
static JE_byte verticalHeight = 7, helpBoxColor = 3, helpBoxBrightness = 5, helpBoxShadeType = 0;
static size_t SDL_strlcpy(char *target, const char *source, size_t size) {
    size_t length = strlen(source);
    if (size) {
        size_t count = MIN(length, size - 1);
        memcpy(target, source, count);
        target[count] = 0;
    }
    return length;
}
static void JE_textShade(SDL_Surface *surface, int x, int y, const char *text,
                         int hue, int brightness, int shade) {
    (void)surface; (void)x; (void)hue; (void)brightness; (void)shade;
    printf("%d=", y);
    for (const unsigned char *p = (const unsigned char *)text; *p; p++) printf("%02X", *p);
    putchar('\n');
}
""" + wrap + r"""
int main(int argc, char **argv) {
    if (argc != 3) return 2;
    char message[257] = {0};
    FILE *file = fopen(argv[1], "rb");
    if (!file) return 2;
    size_t length = fread(message, 1, 256, file);
    fclose(file);
    if (length == 1 || length > 255) return 2;
    JE_helpBox(NULL, 0, 0, message, (unsigned)strtoull(argv[2], NULL, 10));
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
