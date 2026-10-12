/*
 * OpenTyrian SDL audio compatibility oracle.
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */
#define SDL_MAIN_HANDLED
#include "SDL.h"
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static uint32_t hash(const void *data, size_t count)
{
    uint32_t result = 2166136261u;
    const Uint8 *bytes = data;
    for (size_t i = 0; i < count; i++)
        result = (result ^ bytes[i]) * 16777619u;
    return result;
}

int main(void)
{
    const int rates[] = { 8000, 11025, 22050, 44100, 48000, 96000 };
    const int sizes[] = { 0, 1, 2, 7, 31, 256, 1024, 65535 };
    SDL_AudioCVT cvt;
    if (offsetof(SDL_AudioCVT, buf) != 16 ||
        offsetof(SDL_AudioCVT, len) != 16 + sizeof(void *) ||
        offsetof(SDL_AudioCVT, len_cvt) != 20 + sizeof(void *) ||
        offsetof(SDL_AudioCVT, len_mult) != 24 + sizeof(void *) ||
        sizeof(cvt) > (sizeof(void *) == 8 ? 136 : 88))
        return 2;
    for (size_t r = 0; r < sizeof(rates) / sizeof(rates[0]); r++)
    {
        for (size_t s = 0; s < sizeof(sizes) / sizeof(sizes[0]); s++)
        {
            for (int pattern = 0; pattern < 4; pattern++)
            {
                memset(&cvt, 0, sizeof(cvt));
                if (SDL_BuildAudioCVT(&cvt, AUDIO_S8, 1, 11025, AUDIO_S16SYS, 1, rates[r]) < 0)
                {
                    fprintf(stderr, "%s\n", SDL_GetError());
                    return 3;
                }
                cvt.len = sizes[s];
                cvt.buf = malloc(cvt.len * cvt.len_mult + 1);
                if (cvt.buf == NULL)
                    return 4;
                for (int i = 0; i < cvt.len; i++)
                    cvt.buf[i] = pattern == 0 ? (Uint8)(i * 73 + 128)
                        : pattern == 1 ? 0 : pattern == 2 ? 128 : (i % 2 ? 127 : 128);
                if (SDL_ConvertAudio(&cvt) != 0)
                {
                    fprintf(stderr, "%s\n", SDL_GetError());
                    free(cvt.buf);
                    return 5;
                }
                printf("convert:%d,%d,%d,%d,%u\n", rates[r], sizes[s], pattern,
                    cvt.len_cvt / 2, hash(cvt.buf, cvt.len_cvt));
                free(cvt.buf);
            }
        }
    }
    return 0;
}
