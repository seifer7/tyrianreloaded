/*
 * OpenTyrian SDL event ABI and queue oracle.
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */
#define SDL_MAIN_HANDLED
#include "SDL.h"
#include <stdio.h>
#include <string.h>

static void print(const SDL_Event *event)
{
    switch (event->type)
    {
    case SDL_WINDOWEVENT: printf("window:%u\n", event->window.event); break;
    case SDL_KEYDOWN:
    case SDL_KEYUP: printf("key:%u,%d,%u\n", event->type, event->key.keysym.scancode, event->key.keysym.mod); break;
    case SDL_MOUSEMOTION: printf("motion:%d,%d,%d,%d\n", event->motion.x, event->motion.y, event->motion.xrel, event->motion.yrel); break;
    case SDL_MOUSEBUTTONDOWN:
    case SDL_MOUSEBUTTONUP: printf("button:%u,%u,%d,%d\n", event->type, event->button.button, event->button.x, event->button.y); break;
    case SDL_TEXTINPUT: printf("text:%s\n", event->text.text); break;
    case SDL_TEXTEDITING: puts("edit"); break;
    case SDL_QUIT: puts("quit"); break;
    default: puts("unknown"); break;
    }
}

int main(void)
{
    if (sizeof(SDL_Event) != 56 || SDL_InitSubSystem(SDL_INIT_EVENTS) != 0) return 2;
    SDL_Event event;
    while (SDL_PollEvent(&event)) {}
    for (int i = 0; i < 64; i++)
    {
        while (SDL_PollEvent(&event)) {}
        SDL_Event inputs[10]; memset(inputs, 0, sizeof(inputs));
        inputs[0].type = SDL_WINDOWEVENT;
        inputs[0].window.event = i % 3 == 0 ? SDL_WINDOWEVENT_FOCUS_LOST
            : i % 3 == 1 ? SDL_WINDOWEVENT_FOCUS_GAINED : SDL_WINDOWEVENT_RESIZED;
        inputs[1].type = SDL_KEYDOWN; inputs[1].key.keysym.scancode = (SDL_Scancode)(i * 7);
        inputs[1].key.keysym.mod = (Uint16)(i * 913);
        inputs[2].type = SDL_KEYUP; inputs[2].key.keysym.scancode = (SDL_Scancode)(i * 7);
        inputs[3].type = SDL_MOUSEMOTION; inputs[3].motion.x = i * 17 - 400; inputs[3].motion.y = i * 3 - 100;
        inputs[3].motion.xrel = i - 32; inputs[3].motion.yrel = 64 - i;
        inputs[4].type = SDL_MOUSEBUTTONDOWN; inputs[4].button.button = (Uint8)(i % 5 + 1);
        inputs[4].button.x = -i; inputs[4].button.y = i * 71;
        inputs[5] = inputs[4]; inputs[5].type = SDL_MOUSEBUTTONUP;
        inputs[6].type = SDL_TEXTINPUT; strcpy(inputs[6].text.text, "bytes-\xc3\xa9-123");
        inputs[7].type = SDL_TEXTEDITING; inputs[8].type = SDL_USEREVENT; inputs[9].type = SDL_QUIT;
        for (int j = 0; j < 10; j++) if (SDL_PushEvent(&inputs[j]) != 1) return 3;
        for (int j = 0; j < 10; j++) { if (!SDL_PollEvent(&event)) return 4; print(&event); }
    }
    SDL_QuitSubSystem(SDL_INIT_EVENTS);
    return 0;
}
