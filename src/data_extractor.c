/*
 * Standalone Tyrian 2.1 item-data extractor.
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */
#define SDL_MAIN_HANDLED

#include "episodes.h"
#include "file.h"

#include <stdlib.h>
#include <string.h>

static void print_usage(const char *program)
{
	printf("Usage: %s [--data DIR]\n"
	       "\n"
	       "Extract enemy, item and shot definitions from Tyrian 2.1 assets.\n"
	       "Output: data subfolders under the current working directory.\n"
	       "Existing definitions are preserved. Shapes and levels are not extracted.\n"
	       "\n"
	       "  --data DIR   Original Tyrian asset directory (default: game data search)\n"
	       "  --help       Show this help\n", program);
}

int main(int argc, char *argv[])
{
	for (int i = 1; i < argc; ++i)
	{
		if (strcmp(argv[i], "--help") == 0)
		{
			print_usage(argv[0]);
			return EXIT_SUCCESS;
		}
		if (strcmp(argv[i], "--data") == 0 && i + 1 < argc)
			custom_data_dir = argv[++i];
		else if (strncmp(argv[i], "--data=", 7) == 0)
			custom_data_dir = argv[i] + 7;
		else
		{
			fprintf(stderr, "error: unknown option or missing argument: %s\n", argv[i]);
			print_usage(argv[0]);
			return EXIT_FAILURE;
		}
		if (custom_data_dir[0] == '\0')
		{
			fprintf(stderr, "error: the asset directory must not be empty.\n");
			return EXIT_FAILURE;
		}
	}

	const char *const assetDirectory = custom_data_dir != NULL ? custom_data_dir : data_dir();
	printf("Extracting Tyrian 2.1 definitions from '%s' into './data'...\n", assetDirectory);
	extract_item_data(assetDirectory);
	printf("Extraction complete: enemy, items and shots. Existing files were preserved.\n");
	return EXIT_SUCCESS;
}
