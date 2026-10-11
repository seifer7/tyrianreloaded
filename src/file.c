/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009  The OpenTyrian Development Team
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
#pragma warning(disable : 5105)

#include "file.h"
#include "tinydir.h"

#include "opentyr.h"
#include "varz.h"

#include "SDL.h"

#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

const char *custom_data_dir = NULL;

// finds the Tyrian data directory
const char *data_dir(void)
{
	const char *const dirs[] =
	{
		custom_data_dir,
		TYRIAN_DIR,
		"data",
		".",
	};

	static const char *dir = NULL;

	if (dir != NULL)
		return dir;

	for (uint i = 0; i < COUNTOF(dirs); ++i)
	{
		if (dirs[i] == NULL)
			continue;

		FILE *f = dir_fopen(dirs[i], "tyrian1.lvl", "rb");
		if (f)
		{
			fclose(f);

			dir = dirs[i];
			break;
		}
	}

	if (dir == NULL) // data not found
		dir = "";

	return dir;
}

// prepend directory and fopen
FILE *dir_fopen(const char *dir, const char *file, const char *mode)
{
	char *path = malloc(strlen(dir) + 1 + strlen(file) + 1);
	sprintf(path, "%s/%s", dir, file);

	FILE *f = fopen(path, mode);

	free(path);

	return f;
}

// warn when dir_fopen fails
FILE *dir_fopen_warn(const char *dir, const char *file, const char *mode)
{
	FILE *f = dir_fopen(dir, file, mode);

	if (f == NULL)
		fprintf(stderr, "warning: failed to open '%s': %s\n", file, strerror(errno));

	return f;
}

// die when dir_fopen fails
FILE *dir_fopen_die(const char *dir, const char *file, const char *mode)
{
	FILE *f = dir_fopen(dir, file, mode);

	if (f == NULL)
	{
		fprintf(stderr, "error: failed to open '%s': %s\n", file, strerror(errno));
		fprintf(stderr, "error: One or more of the required Tyrian " TYRIAN_VERSION " data files could not be found.\n"
		                "       Please read the README file.\n");
		JE_tyrianHalt(1);
	}

	return f;
}

// check if file can be opened for reading
bool dir_file_exists(const char *dir, const char *file)
{
	FILE *f = dir_fopen(dir, file, "rb");
	if (f != NULL)
		fclose(f);
	return (f != NULL);
}

// returns end-of-file position
long ftell_eof(FILE *f)
{
	long pos = ftell(f);

	fseek(f, 0, SEEK_END);
	long size = ftell(f);

	fseek(f, pos, SEEK_SET);

	return size;
}

void fread_die(void *buffer, size_t size, size_t count, FILE *stream)
{
	size_t result = fread(buffer, size, count, stream);
	if (result != count)
	{
		fprintf(stderr, "error: An unexpected problem occurred while reading from a file.\n");
		SDL_Quit();
		exit(EXIT_FAILURE);
	}
}

void fwrite_die(const void *buffer, size_t size, size_t count, FILE *stream)
{
	size_t result = fwrite(buffer, size, count, stream);
	if (result != count)
	{
		fprintf(stderr, "error: An unexpected problem occurred while writing to a file.\n");
		SDL_Quit();
		exit(EXIT_FAILURE);
	}
}

void write_pcx_header(FILE* fp, size_t width, size_t height) {
	fwrite((Uint16[1]) { 10 }, 1, 1, fp); // The fixed header field valued at a hexadecimal 0x0A (= 10 in decimal).
	fwrite((Uint16[1]) { 5 }, 1, 1, fp); // The version number referring to the Paintbrush software release,
	fwrite((Uint16[1]) { 1 }, 1, 1, fp); // The method used for encoding the image data.
	fwrite((Uint16[1]) { 8 }, 1, 1, fp); // The number of bits constituting one plane. Most often 1, 2, 4 or 8.
	fwrite((Uint16[1]) { 0 }, 2, 1, fp); // The minimum x co-ordinate of the image position.
	fwrite((Uint16[1]) { 0 }, 2, 1, fp); // The minimum y co-ordinate of the image position.
	fwrite((Uint16[1]) { width-1 }, 2, 1, fp); // The maximum x co-ordinate of the image position.
	fwrite((Uint16[1]) { height-1 }, 2, 1, fp); // The maximum y co-ordinate of the image position.
	fwrite((Uint16[1]) { 320 }, 2, 1, fp); // The horizontal image resolution in DPI.
	fwrite((Uint16[1]) { 200 }, 2, 1, fp); // The vertical image resolution in DPI.
	for (int i = 0; i < 48; i++) fwrite((Uint16[1]) { 0 }, 1, 1, fp); // The EGA palette for 16-color images.
	fwrite((Uint16[1]) { 0 }, 1, 1, fp); // The first reserved field, usually set to zero.
	fwrite((Uint16[1]) { 1 }, 1, 1, fp); // The number of color planes constituting the pixel data. Mostly chosen to be 1, 3, or 4.
	fwrite((Uint16[1]) { width }, 2, 1, fp); // The number of bytes of one color plane representing a single scan line.
	fwrite((Uint16[1]) { 1 }, 2, 1, fp); // The mode in which to construe the palette: 1 The palette contains monochrome or color information. 2 The palette contains grayscale information
	fwrite((Uint16[1]) { 0 }, 2, 1, fp); // The horizontal resolution of the source system's screen.
	fwrite((Uint16[1]) { 0 }, 2, 1, fp); // The vertical resolution of the source system's screen.
	for (int i = 0; i < 54; i++) fwrite((Uint16[1]) { 0 }, 1, 1, fp); // The second reserved field, intended for future extensions, and usually set to zero bytes.
}
