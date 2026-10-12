/*
 * OpenTyrian conversion compatibility harness.
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */
#include "arg_parse.h"
#include "mtrand.h"
#include "config_file.h"

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static uint32_t float_bits(float value)
{
	uint32_t bits;
	memcpy(&bits, &value, sizeof bits);
	return bits;
}

int main(int argc, char *argv[])
{
	if (argc == 3 && strcmp(argv[1], "config-number") == 0)
	{
		Config config;
		config_init(&config);
		ConfigSection *section = config_add_section(&config, "test", NULL);
		config_set_string_option(section, "value", argv[2]);
		int signed_value = 0;
		unsigned int unsigned_value = 0;
		bool signed_ok = config_get_int_option(section, "value", &signed_value);
		bool unsigned_ok = config_get_uint_option(section, "value", &unsigned_value);
		printf("%d|%d|%d|%u\n", signed_ok, signed_value, unsigned_ok, unsigned_value);
		config_deinit(&config);
		return 0;
	}
	if (argc == 2 && strcmp(argv[1], "config") == 0)
	{
		Config config;
		if (!config_parse(&config, stdin))
			return 1;
		config_write(&config, stdout);
		config_deinit(&config);
		return 0;
	}
	if (argc == 3 && strcmp(argv[1], "rng") == 0)
	{
		mt_srand(strtoul(argv[2], NULL, 10));
		for (int i = 0; i < 1250; ++i)
		{
			unsigned long integer = mt_rand();
			uint32_t inclusive = float_bits(mt_rand_1());
			uint32_t exclusive = float_bits(mt_rand_lt1());
			printf("%lu|%lu|%lu\n", integer, (unsigned long)inclusive, (unsigned long)exclusive);
		}
		return 0;
	}

	if (argc >= 2 && strcmp(argv[1], "parser") == 0)
	{
		const Options options[] = {
			{ 'h', 'h', "help", false },
			{ 's', 's', "no-sound", false },
			{ 't', 't', "data", true },
			{ 256, 0, "net-player-name", true },
			{ 257, 0, "net-player-number", true },
			{ 0, 0, NULL, false }
		};
		--argc;
		++argv;
		argv[0] = "game";
		for (;;)
		{
			Option option = parse_args(argc, (const char **)argv, options);
			printf("%d|%s|%d\n", option.value, option.arg != NULL ? option.arg : "<null>", option.argn);
			if (option.value <= 0)
				break;
		}
		for (int i = 0; i < argc; ++i)
			printf("argv:%s\n", argv[i]);
		return 0;
	}

	fprintf(stderr, "Usage: core_reference rng SEED | parser [ARGUMENT...]\n");
	return 1;
}
