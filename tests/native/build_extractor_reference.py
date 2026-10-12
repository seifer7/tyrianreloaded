"""Generate an SDL-free oracle from unchanged native extractor source regions.

Only SDL's integer typedefs, endian constants, and no-op shutdown are supplied.
The native binary readers, category writers, CLI and directory search are copied
verbatim; no game/assets/backend behavior is simulated. The oracle currently
targets little-endian development hosts.
"""

import argparse
from pathlib import Path
import re
import sys


def without_includes(text):
    return re.sub(r"(?m)^\s*#include[^\n]*\n", "", text)


def build_source(root):
    if sys.byteorder != "little":
        raise RuntimeError("The extractor reference generator requires a little-endian host.")
    native = root / "src"
    episodes = (native / "episodes.c").read_text(encoding="utf-8")
    files = (native / "file.c").read_text(encoding="utf-8")
    constants = re.findall(r"(?m)^#define\s+\w+_NUM\s+\d+.*$", (native / "lvlmast.h").read_text())
    output_helpers = episodes[
        episodes.index("static void read_legacy_item_data(FILE *f);"):
        episodes.index("// Fields point into the loaded file text")
    ]
    output_helpers = output_helpers[:output_helpers.rindex("#endif")]
    reader = episodes[
        episodes.index("static void read_legacy_item_data(FILE *f)\n{"):
        episodes.rindex("\n#endif")
    ]
    file_functions = files[files.index("const char *custom_data_dir"):files.index("void write_pcx_header")]
    prelude = r"""
#define DATA_EXTRACTOR
#define SDL_BYTEORDER 1234
#define SDL_BIG_ENDIAN 4321
#define TYRIAN_DIR "missing-installed-data"
#define TYRIAN_VERSION "2.1"
#define COUNTOF(x) ((unsigned)(sizeof(x) / sizeof *(x)))
#define MIN(a,b) ((a) < (b) ? (a) : (b))
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <errno.h>
#include <sys/stat.h>
#ifdef _WIN32
#define TARGET_WIN32
#include <direct.h>
#endif
typedef uint8_t Uint8;
typedef int8_t Sint8;
typedef uint16_t Uint16;
typedef int16_t Sint16;
typedef uint32_t Uint32;
typedef int32_t Sint32;
typedef unsigned int uint;
typedef Sint32 JE_longint;
typedef Sint16 JE_integer;
typedef Sint8 JE_shortint;
typedef Uint16 JE_word;
typedef Uint8 JE_byte;
typedef bool JE_boolean;
static void SDL_Quit(void) {}
"""
    state = """
JE_WeaponTypeOld oldWeapons[WEAP_NUM + 1];
JE_EnemyDatTypeOld oldEnemyDat;
JE_longint episode1DataLoc;
JE_word tempW;
JE_integer temp;
"""
    return "\n".join([
        prelude, "\n".join(constants),
        without_includes((native / "file.h").read_text()),
        without_includes((native / "episodes.h").read_text()),
        state, file_functions, output_helpers, reader,
        without_includes((native / "data_extractor.c").read_text()),
    ])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    args.output.write_text(build_source(root), encoding="utf-8")


if __name__ == "__main__":
    main()
