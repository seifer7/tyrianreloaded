#define SDL_MAIN_HANDLED
#include "SDL.h"
#include "SDL_net.h"
#include <stdint.h>
#include <stdio.h>
#include <string.h>

static int fail(const char *operation)
{
    fprintf(stderr, "%s: %s\n", operation, SDLNet_GetError());
    return 2;
}

int main(void)
{
    if (SDLNet_Init() != 0) return fail("init");
    UDPsocket sender = SDLNet_UDP_Open(0), receiver = SDLNet_UDP_Open(0);
    if (!sender || !receiver) return fail("open");
    IPaddress first, second;
    Uint16 first_port = SDLNet_Read16(&SDLNet_UDP_GetPeerAddress(sender, -1)->port);
    Uint16 second_port = SDLNet_Read16(&SDLNet_UDP_GetPeerAddress(receiver, -1)->port);
    if (SDLNet_ResolveHost(&first, "127.0.0.1", first_port) ||
        SDLNet_ResolveHost(&second, "127.0.0.1", second_port)) return fail("resolve");
    if (SDLNet_UDP_Bind(sender, 0, &second) != 0 ||
        SDLNet_UDP_Bind(receiver, 0, &first) != 0) return fail("bind");
    UDPpacket *output = SDLNet_AllocPacket(256), *input = SDLNet_AllocPacket(256);
    if (!output || !input) return fail("allocate");
    for (int i = 0; i < 128; i++)
    {
        output->len = i == 127 ? 256 : 4 + i;
        for (int j = 0; j < output->len; j++) output->data[j] = (i * 31 + j * 17) & 255;
        if (SDLNet_UDP_Send(sender, 0, output) != 1) return fail("send");
        Uint32 start = SDL_GetTicks();
        int received;
        while ((received = SDLNet_UDP_Recv(receiver, input)) == 0 && SDL_GetTicks() - start < 2000)
            SDL_Delay(1);
        if (received != 1) return fail("receive");
        printf("packet:%d,%d,%d,", i, input->len, input->channel);
        for (int j = 0; j < input->len; j++) printf("%02X", input->data[j]);
        printf("\n");
        if (SDLNet_UDP_Send(receiver, 0, input) != 1) return fail("echo");
        start = SDL_GetTicks();
        while ((received = SDLNet_UDP_Recv(sender, input)) == 0 && SDL_GetTicks() - start < 2000)
            SDL_Delay(1);
        if (received != 1 || input->channel != 0 || input->len != output->len ||
            memcmp(input->data, output->data, output->len)) return fail("echo receive");
    }
    SDLNet_FreePacket(output); SDLNet_FreePacket(input);
    SDLNet_UDP_Close(sender); SDLNet_UDP_Close(receiver); SDLNet_Quit();
    return 0;
}
