"""Run unchanged selection menus with scripted input and traced presentation services."""

import argparse
from pathlib import Path
import re
from build_joystick_reference import function


def build_source(root):
    source = (root / "src" / "menus.c").read_text(encoding="utf-8")
    source = re.sub(r'^#include.*$', '', source, flags=re.MULTILINE)
    game_menu = (root / "src" / "game_menu.c").read_text(encoding="utf-8")
    game_menu = game_menu.replace("JE_boolean JE_quitRequest(", "bool JE_quitRequest(", 1)
    function_start = game_menu.index("static int get_menu_choice_y(")
    function_open = game_menu.index("{", function_start)
    depth = 0
    function_end = function_open
    while function_end < len(game_menu):
        if game_menu[function_end] == "{":
            depth += 1
        elif game_menu[function_end] == "}":
            depth -= 1
            if depth == 0:
                function_end += 1
                break
        function_end += 1
    choice_y = game_menu[function_start:function_end].replace(
        "static int get_menu_choice_y(JE_byte menu, JE_byte choice)",
        "static int get_menu_choice_y(uint8_t menu, uint8_t choice)", 1)
    game_menu = game_menu.replace("static void draw_menu_choice(", "void draw_menu_choice(", 1)
    menu_draw = "\n".join(function(game_menu, name) for name in
        ("draw_menu_choice", "JE_drawMenuChoices", "JE_drawMenuHeader", "JE_drawMainMenuHelpText",
         "JE_initWeaponView",
         "JE_drawDots", "JE_drawPlanet", "JE_scaleBitmap", "JE_scaleInPicture", "JE_doShipSpecs",
         "JE_drawLines", "JE_drawNavLines", "JE_drawScore"))
    menu_draw += "\n" + function(game_menu, "JE_genItemMenu")
    quit_request = function(game_menu, "JE_quitRequest").replace("#ifdef WITH_NETWORK", "").replace("#endif", "")
    menu_draw += "\n" + quit_request
    for name in ("setup_next_level_menu", "start_current_level", "request_menu_quit",
                 "return_to_options_menu", "change_two_player_input_device",
                 "change_upgrade_weapon_power", "handle_item_screen_horizontal_key",
                 "activate_item_screen_selection", "cancel_item_screen_selection",
                 "show_item_screen_help", "open_quick_save_menu",
                 "select_item_screen_menu_option", "adjust_item_screen_weapon_power",
                 "adjust_item_screen_volume", "handle_item_screen_input",
                 "handle_item_screen_key", "move_item_screen_selection",
                 "handle_item_screen_mouse_input", "initialize_item_screen",
                 "prepare_item_screen_frame", "draw_item_screen"):
        game_menu = game_menu.replace(f"static void {name}(", f"void {name}(")
    game_menu = game_menu.replace("static void draw_item_screen_status(",
                                  "void draw_item_screen_status(", 1)
    game_menu = game_menu.replace("static int get_item_screen_mouse_selection(",
                                  "int get_item_screen_mouse_selection(", 1)
    game_menu = game_menu.replace("static bool wait_for_item_screen_input(",
                                  "bool wait_for_item_screen_input(", 1)
    key_handler = function(game_menu, "handle_item_screen_key")
    for original, replacement in (
        ("activate_item_screen_selection(", "key_activate("),
        ("cancel_item_screen_selection(", "key_cancel("),
        ("show_item_screen_help(", "key_help("),
        ("move_item_screen_selection(", "key_move("),
        ("handle_item_screen_horizontal_key(", "key_horizontal("),
    ):
        key_handler = key_handler.replace(original, replacement)
    input_dispatch = function(game_menu, "handle_item_screen_input").replace(
        "handle_item_screen_key(", "dispatch_item_screen_key(").replace(
        "handle_item_screen_mouse_input(", "dispatch_item_screen_mouse_input(")
    mouse_handler = function(game_menu, "handle_item_screen_mouse_input")
    for original, replacement in (
        ("adjust_item_screen_volume()", "trace_mouse_volume()"),
        ("select_item_screen_menu_option(", "trace_mouse_select("),
        ("wait_noinput(", "trace_mouse_wait("),
        ("adjust_item_screen_weapon_power(", "trace_mouse_power("),
    ):
        mouse_handler = mouse_handler.replace(original, replacement)
    prepare_frame = function(game_menu, "prepare_item_screen_frame")
    prepare_frame = prepare_frame.replace("JE_getShipInfo();", "trace_get_ship_info();").replace(
        "JE_updateNavScreen();", "trace_update_navigation();")
    draw_item = function(game_menu, "draw_item_screen")
    for original, replacement in (
        ("JE_drawMenuHeader()", "trace_item_header()"),
        ("JE_drawMenuChoices()", "trace_item_choices()"),
        ("blit_sprite_dark(", "trace_item_cube_dark("),
        ("blit_sprite(", "trace_item_cube("),
        ("draw_item_screen_load_save()", "trace_item_load_save()"),
        ("draw_item_screen_keyboard_config()", "trace_item_keyboard()"),
        ("draw_item_screen_joystick_config()", "trace_item_joystick()"),
        ("draw_item_screen_upgrade_menu()", "trace_item_upgrade()"),
        ("draw_item_screen_status()", "trace_item_status()"),
        ("draw_item_screen_data_cubes()", "trace_item_cubes()"),
        ("draw_item_screen_footer()", "trace_item_footer()"),
    ):
        draw_item = draw_item.replace(original, replacement)
    draw_status = function(game_menu, "draw_item_screen_status")
    draw_status = draw_status.replace("JE_textShade(VGAScreen,", "trace_status_text(")
    draw_status = draw_status.replace("JE_barDrawShadow(VGAScreen,", "trace_status_bar(")
    draw_status = draw_status.replace("JE_helpBox(VGAScreen,", "trace_status_help(")
    draw_status = draw_status.replace("draw_ship_illustration();", "trace_status_ship();")
    data_cube_source = game_menu.replace("static void draw_item_screen_data_cubes(",
                                         "void draw_item_screen_data_cubes(", 1)
    draw_data_cubes = function(data_cube_source, "draw_item_screen_data_cubes")
    for original, replacement in (
        ("fill_rectangle_xy(", "trace_data_fill("),
        ("blit_sprite(", "trace_data_blit("),
        ("JE_drawCube(", "trace_data_cube("),
        ("JE_helpBox(", "trace_data_help("),
        ("JE_textShade(", "trace_data_text("),
        ("sprite(", "trace_data_sprite("),
    ):
        draw_data_cubes = draw_data_cubes.replace(original, replacement)
    footer_source = game_menu.replace("static void draw_item_screen_footer(",
                                      "void draw_item_screen_footer(", 1)
    draw_footer = function(footer_source, "draw_item_screen_footer")
    for original, replacement in (
        ("JE_dString(", "trace_footer_device("),
        ("JE_drawScore()", "trace_footer_score()"),
        ("JE_drawMainMenuHelpText()", "trace_footer_help()"),
        ("set_palette(", "trace_footer_palette("),
        ("JE_textWidth(", "trace_footer_width("),
        ("JE_textShade(", "trace_footer_text("),
    ):
        draw_footer = draw_footer.replace(original, replacement)
    load_save_source = game_menu.replace("static void draw_item_screen_load_save(",
                                          "void draw_item_screen_load_save(", 1)
    draw_load_save = function(load_save_source, "draw_item_screen_load_save")
    for original, replacement in (
        ("build_save_game_list(", "trace_load_save_list("),
        ("JE_textShade(", "trace_load_save_text("),
        ("JE_drawMenuHeader()", "trace_load_save_header()"),
        ("free(savedGames)", "trace_free_saves(savedGames)"),
    ):
        draw_load_save = draw_load_save.replace(original, replacement)
    keyboard_source = game_menu.replace("static void draw_item_screen_keyboard_config(",
                                        "void draw_item_screen_keyboard_config(", 1)
    draw_keyboard = function(keyboard_source, "draw_item_screen_keyboard_config")
    draw_keyboard = draw_keyboard.replace("JE_textShade(", "trace_keyboard_text(")
    draw_keyboard = draw_keyboard.replace("SDL_GetScancodeName(", "trace_keyboard_key_name(")
    joystick_source = (root / "src" / "joystick.c").read_text(encoding="utf-8")
    joystick_assignment_code = function(joystick_source, "assignment_to_code")
    joystick_assignment_text = function(joystick_source, "joystick_assignments_to_string")
    joystick_assignment_text = joystick_assignment_text.replace(
        "COUNTOF(*joystick->assignment)", "COUNTOF(*joystick[joystick_config].assignment)")
    joystick_menu_source = game_menu.replace("static void draw_item_screen_joystick_config(",
                                              "void draw_item_screen_joystick_config(", 1)
    draw_joystick = function(joystick_menu_source, "draw_item_screen_joystick_config")
    draw_joystick = draw_joystick.replace("JE_textShade(", "trace_joystick_text(")
    upgrade_menu_source = game_menu.replace("static void draw_item_screen_upgrade_menu(",
                                             "void draw_item_screen_upgrade_menu(", 1)
    draw_upgrade = function(upgrade_menu_source, "draw_item_screen_upgrade_menu")
    for original, replacement in (
        ("JE_dString(", "trace_upgrade_string("),
        ("JE_textShade(", "trace_upgrade_text("),
        ("JE_getShipInfo()", "trace_upgrade_ship_info()"),
        ("fill_rectangle_xy(", "trace_upgrade_fill("),
        ("blit_sprite2(", "trace_upgrade_marker("),
        ("JE_drawItem(", "trace_upgrade_item("),
    ):
        draw_upgrade = draw_upgrade.replace(original, replacement)
    frame_update_source = game_menu.replace("static void update_item_screen_frame(",
                                             "void update_item_screen_frame(", 1)
    frame_update = function(frame_update_source, "update_item_screen_frame")
    for original, replacement in (
        ("NETWORK_KEEP_ALIVE()", "trace_update_keep_alive()"),
        ("fill_rectangle_xy(", "trace_update_fill("),
        ("JE_outTextAndDarken(", "trace_update_text("),
        ("JE_dString(", "trace_update_string("),
        ("fade_palette(", "trace_update_fade("),
        ("JE_mouseStart()", "trace_update_mouse_start()"),
        ("JE_showVGA()", "trace_update_present()"),
        ("JE_mouseReplace()", "trace_update_mouse_replace()"),
        ("setDelay(", "trace_update_delay("),
        ("JE_updateNavScreen()", "trace_update_navigation()"),
        ("JE_drawMainMenuHelpText()", "trace_update_help()"),
        ("JE_drawMenuHeader()", "trace_update_header()"),
        ("JE_drawMenuChoices()", "trace_update_choices()"),
        ("blit_sprite_hv_blend(", "trace_update_cube_flash("),
        ("JE_weaponSimUpdate()", "trace_update_weapon_sim()"),
        ("JE_drawScore()", "trace_update_score()"),
        ("service_SDL_events(", "trace_update_events("),
        ("set_palette(", "trace_update_palette("),
    ):
        frame_update = frame_update.replace(original, replacement)
    cube_loader = function(game_menu, "load_cube").replace(
        "dir_fopen_die(data_dir(), cube_file, \"rb\")", "fopen(cube_test_filename, \"rb\")")
    cube_loader = cube_loader.replace("JE_textWidth(", "trace_cube_text_width(")
    helptext_source = (root / "src" / "helptext.c").read_text(encoding="utf-8")
    helptext_source = helptext_source.replace("static void decrypt_string(", "void decrypt_string(", 1)
    cube_decode = function(helptext_source, "decrypt_string") + "\n" + function(
        helptext_source, "read_encrypted_pascal_string")
    opentyr_source = (root / "src" / "opentyr.c").read_text(encoding="utf-8")
    parse_int = function(opentyr_source, "str_pop_int")
    menu_helpers = r"""
static void trace_item_header(void) {puts("item-header");}
static void trace_item_choices(void) {puts("item-choices");}
static void trace_item_cube_dark(void *surface,int x,int y,int sheet,int sprite,bool black) {
    printf("item-cube-dark:%d,%d,%d,%d,%d\n",x,y,sheet,sprite,black);(void)surface;
}
static void trace_item_cube(void *surface,int x,int y,int sheet,int sprite) {
    printf("item-cube:%d,%d,%d,%d\n",x,y,sheet,sprite);(void)surface;
}
static void trace_item_load_save(void) {puts("item-load-save");}
static void trace_item_keyboard(void) {puts("item-keyboard");}
static void trace_item_joystick(void) {puts("item-joystick");}
static void trace_item_upgrade(void) {puts("item-upgrade");}
static void trace_item_status(void) {puts("item-status");}
static void trace_item_cubes(void) {puts("item-cubes");}
static void trace_item_footer(void) {puts("item-footer");}
static void trace_status_text(int x,int y,const char *text,int font,int shade,int darken) {
    printf("status-text:%d,%d,%s,%d,%d,%d\n",x,y,text,font,shade,darken);
}
static void trace_status_bar(int x,int y,int color,int height,int value,int shade,int darken) {
    printf("status-bar:%d,%d,%d,%d,%d,%d,%d\n",x,y,color,height,value,shade,darken);
}
static void trace_status_help(int x,int y,const char *text,int width) {
    printf("status-help:%d,%d,%s,%d,%d,%d\n",x,y,text,width,helpBoxColor,helpBoxBrightness);
}
static void trace_status_ship(void) {puts("status-ship");}
static void trace_data_fill(SDL_Surface *surface,int x1,int y1,int x2,int y2,int color) {
    printf("cube-fill:%d,%d,%d,%d,%d\n",x1,y1,x2,y2,color);(void)surface;
}
static void trace_data_blit(void *surface,int x,int y,int sheet,int sprite) {
    printf("cube-blit:%d,%d,%d,%d\n",x,y,sheet,sprite);(void)surface;
}
static void trace_data_cube(void *surface,int x,int y,int filter,int brightness) {
    printf("cube-draw:%d,%d,%d,%d\n",x,y,filter,brightness);(void)surface;
}
static void trace_data_help(SDL_Surface *surface,int x,int y,const char *text,int width) {
    printf("cube-help:%d,%d,%s,%d,%d,%d,%d\n",x,y,text,width,
        helpBoxColor,helpBoxBrightness,helpBoxShadeType);(void)surface;
}
static void trace_data_text(SDL_Surface *surface,int x,int y,const char *text,int color,int brightness,int shade) {
    printf("cube-text:%d,%d,%s,%d,%d,%d\n",x,y,text,color,brightness,shade);(void)surface;
}
static void trace_footer_device(SDL_Surface *surface,int x,int y,const char *text,int font) {
    printf("footer-device:%d,%d,%s,%d\n",x,y,text,font);(void)surface;
}
static void trace_footer_score(void) {puts("footer-score");}
static void trace_footer_help(void) {puts("footer-help");}
static void trace_footer_palette(uint8_t *palette,int first,int last) {
    uint32_t hash=2166136261u;
    for(int i=0;i<=last;i++)hash=(hash^palette[i])*16777619u;
    printf("footer-palette:%d,%d,%u\n",first,last,hash);
}
static int trace_footer_width(const char *text,int font) {
    printf("footer-width:%s,%d\n",text,font);return 40;
}
static void trace_footer_text(SDL_Surface *surface,int x,int y,const char *text,int color,int brightness,int shade) {
    printf("footer-text:%d,%d,%s,%d,%d,%d\n",x,y,text,color,brightness,shade);(void)surface;
}
static SavedGame *trace_load_save_list(uint8_t players) {
    printf("save-list:%u,%d\n",players,save_game_count);
    save_records[0].count=save_game_count;
    return save_records;
}
static void trace_load_save_text(SDL_Surface *surface,int x,int y,const char *text,int color,int brightness,int shade) {
    printf("load-save-text:%d,%d,%s,%d,%d,%d\n",x,y,text,color,brightness,shade);(void)surface;
}
static void trace_load_save_header(void) {puts("load-save-header");}
static void trace_free_saves(SavedGame *saves) {(void)saves;}
static void trace_keyboard_text(SDL_Surface *surface,int x,int y,const char *text,int color,int brightness,int shade) {
    printf("keyboard-text:%d,%d,%s,%d,%d,%d\n",x,y,text,color,brightness,shade);(void)surface;
}
static const char *trace_keyboard_key_name(int scancode) {
    return scancode>=0&&scancode<8?key_name_values[scancode]:"";
}
static void trace_keyboard_key(int x,int y,const char *text,int color,int brightness,int shade) {
    printf("keyboard-key:%d,%d,%s,%d,%d,%d\n",x,y,text,color,brightness,shade);
}
static void trace_joystick_text(SDL_Surface *surface,int x,int y,const char *text,int color,int brightness,int shade) {
    printf("joystick-text:%d,%d,%s,%d,%d,%d\n",x,y,text,color,brightness,shade);(void)surface;
}
static void trace_upgrade_string(SDL_Surface *surface,int x,int y,const char *text,int font) {
    printf("upgrade-string:%d,%d,%s,%d\n",x,y,text,font);(void)surface;
}
static void trace_upgrade_text(SDL_Surface *surface,int x,int y,const char *text,int font,int shade,int darken) {
    printf("upgrade-text:%d,%d,%s,%d,%d,%d\n",x,y,text,font,shade,darken);(void)surface;
}
static void trace_upgrade_ship_info(void) {puts("upgrade-ship-info");}
static void trace_upgrade_fill(SDL_Surface *surface,int x1,int y1,int x2,int y2,int color) {
    printf("upgrade-fill:%d,%d,%d,%d,%d\n",x1,y1,x2,y2,color);(void)surface;
}
static void trace_upgrade_marker(SDL_Surface *surface,int x,int y,Sheet sheet,int sprite) {
    printf("upgrade-marker:%d,%d,%d\n",x,y,sprite);(void)surface;(void)sheet;
}
static void trace_upgrade_item(int type,int item,int x,int y) {
    printf("upgrade-item:%d,%d,%d,%d\n",type,item,x,y);
}
static void trace_update_keep_alive(void) {puts("update-keep");}
static void trace_update_fill(SDL_Surface *surface,int x1,int y1,int x2,int y2,int color) {
    printf("update-fill:%d,%d,%d,%d,%d\n",x1,y1,x2,y2,color);(void)surface;
}
static void trace_update_text(SDL_Surface *surface,int x,int y,const char *text,int hue,int brightness,int font) {
    printf("update-text:%d,%d,%s,%d,%d,%d\n",x,y,text,hue,brightness,font);(void)surface;
}
static void trace_update_string(SDL_Surface *surface,int x,int y,const char *text,int font) {
    printf("update-string:%d,%d,%s,%d\n",x,y,text,font);(void)surface;
}
static void trace_update_fade(uint8_t *palette,int steps,int first,int last) {
    printf("update-fade:%d,%d,%d\n",steps,first,last);(void)palette;
}
static void trace_update_mouse_start(void) {puts("update-mouse-start");}
static void trace_update_present(void) {puts("update-present");}
static void trace_update_mouse_replace(void) {puts("update-mouse-replace");}
static void trace_update_delay(int ticks) {printf("update-delay:%d\n",ticks);}
static void trace_update_navigation(void) {puts("update-navigation");}
static void trace_update_help(void) {puts("update-help");}
static void trace_update_header(void) {puts("update-header");}
static void trace_update_choices(void) {puts("update-choices");}
static void trace_update_cube_flash(void *surface,int x,int y,int sheet,int sprite,int hue,int brightness) {
    printf("update-cube-flash:%d,%d,%d,%d,%d,%d\n",x,y,sheet,sprite,hue,brightness);(void)surface;
}
static void trace_update_weapon_sim(void) {puts("update-weapon-sim");}
static void trace_update_score(void) {puts("update-score");}
static void trace_update_events(bool clear) {printf("update-events:%d\n",clear);}
static void trace_update_palette(uint8_t *palette,int first,int last) {
    printf("update-palette:%d,%d\n",first,last);(void)palette;
}
static const uint cube_line_chars=35,cube_line_width=150;
static char cube_test_filename[4096];
static int trace_cube_text_width(const char *text,int font) {(void)font;return (int)strlen(text)*6;}
static void fread_die(void *buffer,size_t size,size_t count,FILE *stream) {
    if(fread(buffer,size,count,stream)!=count){fprintf(stderr,"native cube fixture ended unexpectedly\n");exit(2);}
}
static void fread_u8_die(Uint8 *buffer,size_t count,FILE *stream) {fread_die(buffer,sizeof(Uint8),count,stream);}
static void write_cube_string(FILE *stream,const char *text) {
    static const uint8_t key[]={204,129,63,255,71,19,25,62,1,99};
    const size_t length=strlen(text);if(length>255)exit(2);
    fputc((int)length,stream);
    uint8_t previous=0;
    for(size_t i=0;i<length;i++) {
        const uint8_t encoded=(uint8_t)text[i]^key[i%sizeof(key)]^previous;
        fputc(encoded,stream);previous=encoded;
    }
}
static void print_cube_hex(const char *text) {
    for(const unsigned char *p=(const unsigned char *)text;*p;p++)printf("%02X",*p);
}
#define MIN(a,b) ((a)<(b)?(a):(b))
static SpriteInfo data_face_sprite_info;
static SpriteInfo *trace_data_sprite(int sheet,int index) {
    data_face_sprite_info.width=16+(index%5)*3;
    data_face_sprite_info.height=10+(index%7)*2;
    (void)sheet;
    return &data_face_sprite_info;
}
static void load_cubes(void) {puts("load-cubes");}
static void trace_get_ship_info(void) {puts("ship-info");}
static void set_palette(uint8_t *palette,int first,int last) {
    printf("set-palette:%d,%d\n",first,last);(void)palette;
}
static void adjust_joystick_config_setting(uint8_t selection,int direction) {
    printf("adjust-joystick:%u,%d\n",selection,direction);
}
static void key_activate(int temporary_power[7]) {(void)temporary_power;puts("key-activate");}
static void key_cancel(void) {puts("key-cancel");}
static void key_help(void) {puts("key-help");}
static void key_move(int direction,int temporary_power[7]) {(void)temporary_power;printf("key-move:%d\n",direction);}
static void key_horizontal(int direction,int temporary_power[7]) {(void)temporary_power;printf("key-horizontal:%d\n",direction);}
static void dispatch_item_screen_key(int temporary_power[7]) {(void)temporary_power;puts("dispatch:key");}
static void dispatch_item_screen_mouse_input(int temporary_power[7]) {(void)temporary_power;puts("dispatch:mouse");}
static void trace_mouse_volume(void) {printf("mouse-volume:%d,%d\n",mouseX,mouseY);}
static void trace_mouse_select(int selection,int temporary_power[7]) {
    printf("mouse-select:%d,%d\n",curMenu,selection);(void)temporary_power;
}
static void trace_mouse_wait(bool keyboard,bool mouse,bool joystick) {
    printf("wait-noinput:%d,%d,%d\n",keyboard,mouse,joystick);
}
static void trace_mouse_power(int temporary_power[7]) {
    printf("mouse-power:%d,%d,%d\n",curMenu,mouseX,mouseY);(void)temporary_power;
}
void handle_item_screen_key(int temporary_power[7]);
static void JE_changeVolume(JE_word *music,int music_delta,JE_word *sample,int sample_delta) {
    printf("volume:%d,%d\n",music_delta,sample_delta);
    *music=(JE_word)(*music+music_delta);*sample=(JE_word)(*sample+sample_delta);
}
static void restart_song(void) {puts("restart-song");}
static void set_volume(JE_word music,JE_word effects) {
    printf("set-volume:%u,%u\n",music,effects);
}
static int cost_multiplier;
static JE_longint JE_getCost(JE_byte upgrade_selection,JE_byte item_id) {
    if(upgrade_selection==3||upgrade_selection==4) {
        upgradeCost=(JE_longint)(item_id*cost_multiplier*2);
        downgradeCost=(JE_longint)(item_id*cost_multiplier);
    }
    return item_id*cost_multiplier;
}
""" + "\n".join(function(game_menu, name) for name in
        ("setup_next_level_menu", "start_current_level", "request_menu_quit", "return_to_options_menu",
         "change_two_player_input_device", "change_upgrade_weapon_power",
         "handle_item_screen_horizontal_key", "activate_item_screen_selection",
         "cancel_item_screen_selection", "show_item_screen_help", "open_quick_save_menu",
         "wait_for_item_screen_input",
         "get_item_screen_mouse_selection", "select_item_screen_menu_option",
         "adjust_item_screen_weapon_power", "adjust_item_screen_volume",
         "move_item_screen_selection", "initialize_item_screen")) + "\n" + input_dispatch + "\n" + key_handler + "\n" + mouse_handler + "\n" + prepare_frame + "\n" + draw_item + "\n" + draw_status
    menu_helpers += "\n" + cube_decode + "\n" + parse_int + "\n" + cube_loader
    menu_helpers += "\n" + joystick_assignment_code + "\n" + joystick_assignment_text + "\n" + draw_joystick
    menu_helpers += "\n" + draw_data_cubes + "\n" + draw_footer + "\n" + draw_load_save + "\n" + draw_keyboard + "\n" + draw_upgrade + "\n" + frame_update
    menu_dispatch = r"""
static void handle_full_game_selection(JE_byte selection) {printf("dispatch:full:%u\n",selection);}
static void handle_upgrade_menu_selection(JE_byte selection) {printf("dispatch:upgrade:%u\n",selection);}
static void handle_options_menu_selection(JE_byte selection) {printf("dispatch:options:%u\n",selection);}
static void handle_next_level_menu_selection(JE_word selection) {printf("dispatch:next:%u\n",selection);}
static void handle_upgrade_submenu_selection(void) {puts("dispatch:upgrade-submenu");}
static void handle_keyboard_config_selection(JE_word selection) {printf("dispatch:keyboard:%u\n",selection);}
static void handle_load_save_menu_selection(JE_word selection) {printf("dispatch:load-save:%u\n",selection);}
static void handle_data_cube_menu_selection(JE_word selection) {printf("dispatch:data-cube:%u\n",selection);}
static void handle_two_player_menu_selection(void) {puts("dispatch:two-player");}
static void handle_one_player_menu_selection(void) {puts("dispatch:one-player");}
static void handle_limited_options_selection(JE_byte selection) {printf("dispatch:limited:%u\n",selection);}
static void handle_joystick_config_selection(JE_byte selection) {printf("dispatch:joystick:%u\n",selection);}
static void handle_super_tyrian_menu_selection(void) {puts("dispatch:super-tyrian");}
""" + function(game_menu, "JE_menuFunction")
    part_way_start = game_menu.index("JE_integer JE_partWay(")
    part_way_open = game_menu.index("{", part_way_start)
    part_way_end = part_way_open
    depth = 0
    while part_way_end < len(game_menu):
        if game_menu[part_way_end] == "{":
            depth += 1
        elif game_menu[part_way_end] == "}":
            depth -= 1
            if depth == 0:
                part_way_end += 1
                break
        part_way_end += 1
    part_way = game_menu[part_way_start:part_way_end]
    compute_dots = function(game_menu, "JE_computeDots")
    update_nav = function(game_menu, "JE_updateNavScreen")
    planet_coordinates = "\n".join(
        re.search(rf"static const JE_word planet{axis}\[21\] = \{{[^}}]*\}};", game_menu).group(0)
        .replace("JE_word", "uint16_t", 1)
        for axis in ("X", "Y")
    )
    varz = (root / "src" / "varz.c").read_text(encoding="utf-8")
    planet_graphics = "\n".join(
        re.search(rf"const JE_(?:word|byte) {name}\[21\][^=]*=\s*\{{[^}}]*\}};", varz).group(0)
        .replace("JE_word", "uint16_t", 1).replace("JE_byte", "uint8_t", 1)
        for name in ("PGR", "PAni")
    )
    return r"""
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
 #include <assert.h>
typedef uint8_t Uint8;
typedef uint16_t Uint16;
typedef unsigned int uint;
typedef int SDL_Scancode;
typedef struct {void *data;} Sheet;
typedef struct {int pitch,h;uint8_t *pixels;int w;} SDL_Surface;
enum {large_font=0,normal_font=1,centered=1,left_aligned=0,EPISODE_AVAILABLE=4,
    MOUSE_POINTER_NORMAL=0,MOUSE_POINTER_UP=1,MOUSE_POINTER_DOWN=2,
    S_CURSOR=28,S_SELECT=8,S_SPRING=16,S_ITEM=9,KMOD_SHIFT=3,
    S_CLICK=8,FULL_SHADE=0,SDL_SCANCODE_LEFT=80,SDL_SCANCODE_RIGHT=79,SDL_SCANCODE_TAB=43,
    DIFFICULTY_EASY=1,DIFFICULTY_NORMAL=2,DIFFICULTY_HARD=3,DIFFICULTY_IMPOSSIBLE=4,
    DIFFICULTY_SUICIDE=6,DIFFICULTY_ZINGLON=8,SDL_BUTTON_LEFT=1,SDL_BUTTON_RIGHT=3,songBuy=5,
    SDL_SCANCODE_UP=82,SDL_SCANCODE_DOWN=81,SDL_SCANCODE_S=22,SDL_SCANCODE_PAGEUP=75,
    SDL_SCANCODE_PAGEDOWN=78,SDL_SCANCODE_LALT=226,SDL_SCANCODE_RALT=230,
    SDL_SCANCODE_SPACE=44,SDL_SCANCODE_RETURN=40,SDL_SCANCODE_MINUS=45,
    SDL_SCANCODE_EQUALS=46,SDL_SCANCODE_SLASH=56,SDL_SCANCODE_F1=58,
    SDL_SCANCODE_HOME=74,SDL_SCANCODE_END=77,
    SDL_SCANCODE_KP_ENTER=88,SDL_SCANCODE_ESCAPE=41,SDL_SCANCODE_L=15,SDL_SCANCODE_O=18,
    SDL_SCANCODE_R=21,SDL_SCANCODE_D=7,SDL_SCANCODE_G=10,SDL_SCANCODE_RIGHTBRACKET=48};
#define COUNTOF(a) (sizeof(a)/sizeof((a)[0]))
static uint8_t front[64000],back[64000],work_pixels[64000],keysactive[512];
#define MENU_MAX 14
static SDL_Surface surfaces[]={{320,200,front,320},{320,200,back,320},{320,200,work_pixels,320}};
static SDL_Surface *VGAScreen=&surfaces[0],*VGAScreen2=&surfaces[1];
static SDL_Surface *game_screen=&surfaces[2];
static Sheet shopSpriteSheet;
static uint8_t colors[1024];
static uint8_t palettes[20][1024];
static uint8_t facepal[256];
static int curPal,mouseCursor,mouse_x,mouse_y,mouseX,mouseY,mouseButton,lastmouse_x,lastmouse_y,lastmouse_but,lastkey_scan;
static int currentCube,yLoc,yChg;
static bool inputDetected;
static bool newkey,newmouse,mousedown,has_mouse,onePlayerAction,twoPlayerMode,episodeAvail[4];
static bool backFromHelp;
static int episodeNum,initial_episode_num,difficultyLevel;
static bool quit_confirmation;
typedef struct {int key,mod,x,y,cx,cy,button,held;} Frame;
static Frame frames[128];
static int frame_count,frame_index,modifiers;
static bool ready;
static bool scale_trace;
static int scale_button_after,scale_button_calls;
static void JE_tyrianHalt(int code) {printf("quit-halt:%d\n",code);}
enum {PACKET_QUIT=32};
static void network_prepare(int packet) {printf("quit-network-prepare:%d\n",packet);}
static void network_send(int length) {printf("quit-network-send:%d\n",length);}
static void network_tyrian_halt(int code,bool sync) {printf("quit-network-halt:%d,%d\n",code,sync);}
static void key(int k,int mod,int held) {frames[frame_count++]=(Frame){k,mod,-999,0,0,0,0,held};}
static void mouse(int x,int y,int cx,int cy,int button,int k) {
    frames[frame_count++]=(Frame){k,0,x,y,cx,cy,button,0};
}
static void script(int kind,int mode) {
    frame_count=frame_index=0;
    int x=kind==1?20:140,y=kind==1?50:54,dy=kind==1?30:24;
    switch(mode) {
    case 0:key(40,0,0);break;
    case 1:key(41,0,0);break;
    case 2:key(82,0,0);key(40,0,0);break;
    case 3:key(81,0,0);key(40,0,0);break;
    case 4:key(88,0,0);break;
    case 5:key(44,0,0);break;
    case 6:mouse(0,0,0,0,3,0);break;
    case 7:mouse(x,y,x,y,1,0);break;
    case 8:mouse(x,y+2*dy,x,y+2*dy,1,0);break;
    case 9:mouse(x+40,y,x+40,y,1,0);break;
    case 10:mouse(x,y+13,x,y+13,1,0);break;
    case 11:mouse(x,y+dy,0,0,1,0);break;
    case 12:mouse(x,y+2*dy,x,y+2*dy,3,40);break;
    case 13:mouse(x,y+dy,x,y+dy,1,82);break;
    case 14:key(0,0,0);key(0,0,0);key(40,0,0);break;
    case 15:key(10,1,0);for(int i=0;i<2;i++)key(81,0,0);key(40,0,0);break;
    case 16:key(48,1,0);key(10,1,0);key(48,1,0);for(int i=0;i<3;i++)key(81,0,0);key(40,0,0);break;
    case 17:case 18:case 19:
        key(10,1,0);key(48,1,0);
        if(mode==18)key(0,0,15);
        else {key(15,0,0);key(18,0,0);if(mode==19)key(5,0,0);key(21,0,0);key(7,0,0);}
        for(int i=0;i<4;i++)key(81,0,0);key(40,0,0);break;
    case 20:key(10,0,0);key(48,0,0);key(82,0,0);key(40,0,0);break;
    case 21:mouse(x,y+dy,x,y+dy,0,0);key(40,0,0);break;
    case 22:mouse(-1,-1,-1,-1,1,0);break;
    case 23:mouse(x+39,y+12,x+39,y+12,1,0);break;
    default:for(int i=0;i<mode-21;i++)key(mode%2?82:81,0,0);key(40,0,0);break;
    }
    key(41,0,0);
}
static void service_SDL_events(bool clear) {
    if(clear)newkey=newmouse=false;
    if(!ready)return;
    ready=false;
    Frame f=frames[frame_index-1];
    if(f.x!=-999){mouse_x=f.x;mouse_y=f.y;}
    if(f.button){newmouse=mousedown=true;lastmouse_but=f.button;lastmouse_x=f.cx;lastmouse_y=f.cy;}
    if(f.key){newkey=true;lastkey_scan=f.key;keysactive[f.key]=1;}
}
static void SDL_Delay(int ticks) {
    if(scale_trace){printf("delay:%d\n",ticks);return;}
    if(frame_index>=frame_count){fprintf(stderr,"Menu script exhausted\n");exit(2);}
    Frame f=frames[frame_index++];ready=true;modifiers=f.mod;
    memset(keysactive,0,sizeof(keysactive));
    int lord[]={15,18,21,7};for(int i=0;i<4;i++)if(f.held&(1<<i))keysactive[lord[i]]=1;
    printf("delay:%d\n",ticks);
}
static void setDelay(int ticks) {SDL_Delay(ticks);}
static void wait_delay(void) {puts("wait-delay");}
static int JE_mousePosition(int *x,int *y) {*x=mouseX;*y=mouseY;return mouseButton;}
static int SDL_GetModState(void) {return modifiers;}
static void push_joysticks_as_keyboard(void) {puts("joystick");}
static void keep_alive(void) {puts("keep");}
#define NETWORK_KEEP_ALIVE() keep_alive()
static void JE_loadCompShapes(Sheet *sheet,char code) {puts("cursor-load");sheet->data=sheet;(void)code;}
static void JE_loadPic(SDL_Surface *surface,int pic,bool palette) {
    puts("picture");memset(surface->pixels,0xA8,64000);(void)pic;(void)palette;
}
static void JE_drawShipSpecs(SDL_Surface *surface,SDL_Surface *temp) {
    puts("draw-specs");
    for(int i=0;i<surface->pitch*surface->h;i++)surface->pixels[i]=(uint8_t)(i*11+17);
    memset(temp->pixels,0x5A,temp->pitch*temp->h);
}
static void wait_noinput(bool keyboard,bool mouse,bool joystick) {
    puts("wait-noinput");(void)keyboard;(void)mouse;(void)joystick;
}
static void JE_clearKeyboard(void) {puts("clear-keyboard");}
static void JE_wipeKey(void) {puts("wipe-key");}
static void JE_barShade(SDL_Surface *surface,int left,int top,int right,int bottom) {
    printf("quit-shade:%d,%d,%d,%d\n",left,top,right,bottom);(void)surface;
}
static void wait_input(bool keyboard,bool mouse,bool joystick) {
    puts("wait-input");(void)keyboard;(void)mouse;(void)joystick;
}
static int JE_textWidth(const char *text,int font) {(void)text;(void)font;return 40;}
static void draw_font_hv_shadow(SDL_Surface *surface,int x,int y,const char *text,int font,int align,int hue,int value,bool black,int distance) {
    printf("draw:%d,%d,%d,%s,%d,%d,%d\n",surface==VGAScreen2,x,y,text,font,align,value);
    int left=x-(align?20:0);
    if(left>=0&&left<320&&y>=0&&y<200)surface->pixels[y*320+left]=(uint8_t)(hue*16+value+8);
    (void)black;(void)distance;
}
static void fade_palette(int palette,int steps,int first,int last) {puts("fade-in");(void)palette;(void)steps;(void)first;(void)last;}
static void fade_black(int steps) {printf("black:%d\n",steps);}
static void play_song(int song) {printf("song:%d\n",song);}
static void JE_helpSystem(int topic) {printf("help-system:%d\n",topic);}
static void JE_playSampleNum(int sample) {printf("sound:%d\n",sample);}
static void JE_mouseStart(void) {service_SDL_events(false);puts("cursor-start");}
static void JE_showVGA(void) {
    uint32_t hash=2166136261u;for(int i=0;i<64000;i++)hash=(hash^front[i])*16777619u;
    printf("show:%u\n",hash);
}
static bool JE_anyButton(void) {return scale_button_calls++ >= scale_button_after;}
static void JE_mouseReplace(void) {puts("cursor-restore");}
static void JE_initEpisode(int episode) {printf("episode:%d\n",episode);episodeNum=episode;}
""" + source + r"""
typedef uint8_t JE_byte;
typedef uint16_t JE_word;
typedef int16_t JE_integer;
typedef int32_t JE_longint;
typedef bool JE_boolean;
enum {LEFT_SIDEKICK=0,RIGHT_SIDEKICK=1,MAX_PWEAPON=81};
typedef struct {uint8_t id,power;} MenuWeapon;
typedef struct {
    uint8_t ship,shield,generator,special;
    MenuWeapon weapon[2];
    uint8_t sidekick[2];
} PlayerItems;
typedef struct {int x,y;uint32_t charge_ticks,charge,style,animation_frame;bool animation_enabled;} WeaponViewSidekick;
typedef struct {char name[15];char levelName[11];uint8_t gameMode;bool twoPlayerMode;
    uint8_t episode;int count;} SavedGame;
typedef struct {
    WeaponViewSidekick sidekick[2];
    int x,y,delta_x_shot_move,delta_y_shot_move,last_x_explosion_follow,last_y_explosion_follow;
    PlayerItems items;
    PlayerItems last_items;
    uint32_t armor,cash;
    uint32_t weapon_mode;
} WeaponViewPlayer;
static WeaponViewPlayer player[2];
static PlayerItems old_items[2];
static const uint8_t itemAvailMap[7]={1,2,3,9,4,6,7};
static const uint8_t menuChoicesDefault[14]={7,9,8,0,0,11,13,0,0,6,4,6,7,5};
static uint8_t itemAvail[9][10],itemAvailMax[9];
static struct {char name[64];int mpwr;} ships[256],shields[256],powerSys[256];
typedef struct {char name[64];int mpwr;uint32_t tr;uint8_t ani;} JE_OptionType;
static JE_OptionType options[256];
static struct {char name[64];int opnum;uint16_t op[2][11];} weaponPort[256];
static struct {char name[64];} special[256];
static char superShips[16][64];
static int superArcadeMode,SA,helpBoxColor,helpBoxBrightness=1,helpBoxShadeType;
static uint8_t temp3,temp;
static uint16_t temp2,tempW;
static uint8_t *playeritem_map(PlayerItems *items,unsigned int index) {
    switch(index) {
    case 0:return &items->ship;case 1:return &items->weapon[0].id;case 2:return &items->weapon[1].id;
    case 3:return &items->shield;case 4:return &items->generator;case 5:return &items->sidekick[0];
    case 6:return &items->sidekick[1];default:abort();
    }
}
static uint8_t shotAvail[MAX_PWEAPON],shotRepeat[11],shotMultiPos[11];
static unsigned int power,lastPower;
static void initialize_starfield(void) {puts("starfield-init");}
static void fill_rectangle_xy(SDL_Surface *surface,int x1,int y1,int x2,int y2,int color) {
    printf("%s:%d,%d,%d,%d,%d\n",x1==8&&y1==8?"weapon-fill":"fill",x1,y1,x2,y2,color);
    for(int y=y1;y<=y2;y++)memset(surface->pixels+y*surface->pitch+x1,color,x2-x1+1);
}
enum {MENU_FULL_GAME=0,MENU_UPGRADES=1,MENU_LOAD_SAVE=6,MENU_DATA_CUBES=7,MENU_DATA_CUBE_SUB=8,
    MENU_PLAY_NEXT_LEVEL=3,MENU_2_PLAYER_ARCADE=9,MENU_1_PLAYER_ARCADE=10,
    MENU_OPTIONS=2,MENU_UPGRADE_SUB=4,MENU_KEYBOARD_CONFIG=5,MENU_LIMITED_OPTIONS=11,MENU_JOYSTICK_CONFIG=12,
    MENU_SUPER_TYRIAN=13,SMALL_FONT_SHAPES=1,TINY_FONT=2,FONT_SHAPES=0,DARKEN=0,S_CLINK=23,SA_NONE=0};
static uint8_t menuChoices[14];
static uint8_t curSel[14];
static uint8_t curItemType,curItem,cursor;
static int curMenu;
static int col,colC;
static bool keyboardUsed,paletteChanged,superTyrian,flash,extraGame;
static bool firstMenu9;
static int lastDirection;
void JE_menuFunction(JE_byte select);
static const uint8_t menuEsc[14]={0,1,1,1,2,3,3,1,8,0,0,11,3,0};
static uint8_t lastCurSel;
static uint8_t oldMenu,oldPal;
static bool quikSave;
static uint8_t saveGamePageIndex;
static char menuInt[15][16][64];
static char inputDevices[3][64];
static int keySettings[8];
static char key_name_values[8][32];
static char tempStr[256];
static char mainMenuHelp[34][66];
static uint8_t menuHelp[14][11];
static bool leftPower,rightPower;
static bool rightPowerAfford,music_disabled,samples_disabled;
static JE_word tyrMusicVolume,fxVolume;
static int cubeMax;
static JE_longint cash_left_value;
static JE_longint upgradeCost,downgradeCost;
static int performSave;
static char pName[21][64],miscText[80][64],miscTextB[4][64];
static SavedGame save_records[32];
static int save_game_count;
static void print_upgrade_labels(unsigned int count) {
    for(unsigned int i=0;i<16;i++) {
        printf("upgrade-label:%u,",i);
        for(const unsigned char *p=(const unsigned char *)menuInt[5][i];*p;p++)printf("%02X",*p);
        puts("");
    }
    printf("upgrade-state:%u,%u,%u\n",count,menuChoices[MENU_UPGRADE_SUB],curSel[MENU_UPGRADE_SUB]);
}
static uint8_t mapSection[5];
static int newPal,mainLevel;
static bool gameLoaded;
static bool jumpSection,isNetworkGame;
static uint8_t inputDevice[2]={1,2};
static int joysticks;
static int joystick_config;
enum {NONE=0,AXIS=1,BUTTON=2,HAT=3};
typedef struct {int type,num;bool x_axis,negative_axis;} Joystick_assignment;
struct joystick_input {
    bool direction[4],analog;int sensitivity,threshold;
    Joystick_assignment assignment[10][2];
};
static struct joystick_input joystick[4];
struct cube_struct {char title[81];char header[13];int face_sprite;char text[90][36];int last_line;};
static struct cube_struct cube[16];
static void JE_dString(SDL_Surface *surface,int x,int y,const char *text,int font) {
    printf("menu-draw:%d,%d,%s,%d\n",x,y,text,font);(void)surface;
}
static int JE_fontCenter(const char *text,int font) {(void)font;return 160-(int)strlen(text)*6/2;}
static void JE_textShade(SDL_Surface *surface,int x,int y,const char *text,int font,int shade,int darken) {
    printf("%s:%d,%d,%s\n",y==173?"score-draw":"help-draw",x,y,text);
    (void)surface;(void)font;(void)shade;(void)darken;
}
static void JE_helpBox(SDL_Surface *surface,int x,int y,const char *text,int width) {
    printf("quit-help:%d,%d,%s,%d\n",x,y,text,width);(void)surface;
}
static void JE_outTextAdjust(SDL_Surface *surface,int x,int y,const char *text,int font,int brightness,int shape,bool black) {
    printf("quit-choice:%d,%d,%s,%d\n",x,y,text,brightness);
    (void)surface;(void)font;(void)shape;(void)black;
}
static JE_longint JE_cashLeft(void) {return cash_left_value;}
static bool JE_anyButton(void);
""" + planet_coordinates + r"""
""" + planet_graphics + r"""
static uint8_t mapPNum,mapOrigin,mapPlanet[5],planetDots[5];
static int16_t planetDotX[5][10],planetDotY[5][10];
static int16_t tempNavX,tempNavY;
static uint8_t planetAni,planetAniWait,currentDotNum,currentDotWait;
static float navX,navY,newNavX,newNavY;
static void *VGAScreenSeg;
enum {OPTION_SHAPES=4,PLANET_SHAPES=3,FACE_SHAPES=12,FRONT_WEAPON=0,REAR_WEAPON=1};
typedef struct {int width,height;} SpriteInfo;
static SpriteInfo planet_sprite_info;
static SpriteInfo *sprite(int sheet,int index) {
    planet_sprite_info.width=16+(index%5)*3;
    planet_sprite_info.height=10+(index%7)*2;
    (void)sheet;
    return &planet_sprite_info;
}
static void blit_sprite(void *surface,int x,int y,int sheet,int sprite) {
    const char *kind=sheet==PLANET_SHAPES?"planet":(sheet==OPTION_SHAPES&&x==50&&y==50&&sprite==35)?"quit-box":"nav-dot";
    printf("%s:%d,%d,%d\n",kind,x,y,sprite);(void)surface;
}
static void blit_sprite_dark(void *surface,int x,int y,int sheet,int sprite,bool black) {
    printf("planet-dark:%d,%d,%d,%d\n",x,y,sprite,black);(void)surface;(void)sheet;
}
static void JE_rectangle(SDL_Surface *surface,int x1,int y1,int x2,int y2,int color) {
    printf("rectangle:%d,%d,%d,%d,%d\n",x1,y1,x2,y2,color);(void)surface;
}
static void JE_pix3(SDL_Surface *surface,int x,int y,int color) {
    printf("pixel:%d,%d,%d\n",x,y,color);(void)surface;
}
""" + choice_y + r"""
""" + menu_draw + r"""
""" + part_way + r"""
""" + compute_dots + r"""
""" + update_nav + r"""
""" + menu_helpers + r"""
""" + menu_dispatch + r"""
int main(int argc,char **argv) {
    if(argc<2)return 2;
#ifdef _WIN32
    snprintf(cube_test_filename,sizeof cube_test_filename,"%s\\opentyrian-cube-reference.dat",argv[1]);
#else
    snprintf(cube_test_filename,sizeof cube_test_filename,"%s/opentyrian-cube-reference.dat",argv[1]);
#endif
    memset(itemAvail,0,sizeof itemAvail);memset(itemAvailMax,0,sizeof itemAvailMax);
    itemAvailMax[0]=1;itemAvail[0][0]=5;
    itemAvailMax[1]=1;itemAvail[1][0]=3;
    itemAvailMax[2]=5;for(unsigned int i=0;i<5;i++)itemAvail[2][i]=(uint8_t)(i+1);
    itemAvailMax[3]=1;itemAvail[3][0]=4;
    itemAvailMax[8]=1;itemAvail[8][0]=0;
    player[0].last_items=(PlayerItems){.ship=5,.shield=0,.generator=4,
        .weapon={{9,0},{9,0}},.sidekick={7,2}};
    memset(menuChoices,0xA5,sizeof menuChoices);memset(curSel,0xA5,sizeof curSel);
    for(unsigned int i=0;i<64000;i++) {
        front[i]=(uint8_t)(i*29+7);back[i]=0xCC;work_pixels[i]=0x33;
    }
    for(unsigned int i=0;i<sizeof colors;i++)colors[i]=(uint8_t)(i*3+1);
    VGAScreenSeg=&surfaces[2];shopSpriteSheet.data=NULL;
    curPal=6;newPal=9;col=44;gameLoaded=true;curItemType=9;cursor=7;curItem=99;curMenu=11;
    keyboardUsed=firstMenu9=backFromHelp=true;
    puts("item-init-missing-sheet-case");
    initialize_item_screen();
    for(unsigned int pass=0;pass<2;pass++) {
        uint32_t choice_hash=2166136261u,selection_hash=2166136261u,background_hash=2166136261u;
        for(unsigned int i=0;i<14;i++) {
            choice_hash=(choice_hash^menuChoices[i])*16777619u;
            selection_hash=(selection_hash^curSel[i])*16777619u;
        }
        for(unsigned int i=0;i<64000;i++)background_hash=(background_hash^back[i])*16777619u;
        printf("item-init-state:%d,%d,%d,%u,%u,%u,%u,%d,%u,%u,%u,%u,%u,%u,%u\n",
            curPal,newPal,col,gameLoaded,curItemType,cursor,curItem,curMenu,keyboardUsed,
            firstMenu9,backFromHelp,shopSpriteSheet.data!=NULL,choice_hash,selection_hash,background_hash);
        for(unsigned int row=0;row<9;row++) {
            printf("item-init-row:%u,%u",row,itemAvailMax[row]);
            for(unsigned int slot=0;slot<10;slot++)printf(",%u",itemAvail[row][slot]);
            puts("");
        }
        if(pass==0) {
            puts("item-init-cached-sheet-case");
            initialize_item_screen();
        }
    }
    const int prepare_cases[][8]={
        {0,1,1,1,1,0,2,1},{8,0,0,0,0,0,2,0},{8,0,0,0,0,1,2,0},
        {1,0,0,0,0,0,3,0},{3,0,0,0,0,0,2,1}
    };
    VGAScreen=&surfaces[0];VGAScreen2=&surfaces[1];
    for(unsigned int c=0;c<COUNTOF(prepare_cases);c++) {
        const int *test=prepare_cases[c];curMenu=test[0];twoPlayerMode=test[1];isNetworkGame=test[2];
        onePlayerAction=test[3];superTyrian=test[4];firstMenu9=test[5];
        memset(curSel,2,sizeof curSel);curSel[1]=(uint8_t)test[6];keyboardUsed=test[7];
        paletteChanged=true;leftPower=rightPower=true;planetAni=planetAniWait=currentDotNum=currentDotWait=99;
        memset(front,0x35,sizeof front);memset(back,0xC7,sizeof back);
        for(unsigned int i=0;i<7;i++)itemAvailMax[i]=0;
        itemAvailMax[1]=3;itemAvail[1][0]=4;itemAvail[1][1]=8;itemAvail[1][2]=9;
        player[0].items.weapon[0].id=8;player[0].items.weapon[0].power=4;
        player[0].items.weapon[1].id=9;player[0].items.weapon[1].power=5;
        int temporary_power[7]={77,77,77,77,77,77,77};
        printf("item-prepare-case:%u\n",c);
        prepare_item_screen_frame(temporary_power);
        uint32_t screen_hash=2166136261u,background_hash=2166136261u,power_hash=2166136261u;
        for(unsigned int i=0;i<64000;i++) {
            screen_hash=(screen_hash^front[i])*16777619u;
            background_hash=(background_hash^back[i])*16777619u;
        }
        for(unsigned int i=0;i<7;i++)power_hash=(power_hash^(uint8_t)temporary_power[i])*16777619u;
        printf("item-prepare-state:%d,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u\n",
            curMenu,paletteChanged,leftPower,rightPower,keyboardUsed,planetAni,currentDotNum,
            currentDotWait,planetAniWait,temporary_power[0],temporary_power[1],temporary_power[2],
            temporary_power[3],temporary_power[4]);
        printf("item-prepare-hash:%u,%u,%u\n",screen_hash,background_hash,power_hash);
    }
    const int draw_cases[][2]={{0,0},{0,2},{4,3},{3,1},{9,1},{11,1},{12,1},{13,1},{8,1}};
    for(unsigned int c=0;c<COUNTOF(draw_cases);c++) {
        curMenu=draw_cases[c][0];cubeMax=draw_cases[c][1];
        printf("item-draw-case:%d,%d\n",curMenu,cubeMax);
        draw_item_screen();
    }
    const int status_cases[][10]={
        {0,0,0,0,2,0,0,144,96,1},{1,1,0,0,2,0,0,144,96,2},
        {2,0,0,0,2,1,0,120,84,0},{3,0,0,0,2,0,0,144,96,0},
        {4,0,0,0,2,0,0,144,96,2},{4,0,0,0,3,0,0,144,96,1},
        {4,0,0,0,5,0,0,144,96,0},{5,0,0,0,2,0,0,144,96,0},
        {6,1,0,0,2,0,0,144,96,0},{7,0,0,0,2,0,0,144,96,0},
        {9,1,0,0,2,0,0,144,96,0},{10,0,0,0,2,0,0,144,96,0},
        {11,0,0,0,2,0,1,101,77,0},{12,0,0,0,2,0,0,144,96,0},
        {13,0,0,1,2,0,0,144,96,0},{0,0,2,0,2,0,0,144,96,0}
    };
    strcpy(miscText[40],"P1");strcpy(miscText[41],"P2");
    for(unsigned int i=0;i<COUNTOF(superShips);i++)snprintf(superShips[i],sizeof superShips[i],"Ship%u",i);
    for(unsigned int i=0;i<256;i++) {
        snprintf(weaponPort[i].name,sizeof weaponPort[i].name,"Weapon%u",i);
        snprintf(special[i].name,sizeof special[i].name,"Special%u",i);
    }
    for(unsigned int c=0;c<COUNTOF(status_cases);c++) {
        const int *test=status_cases[c];curMenu=test[0];twoPlayerMode=test[1];
        superArcadeMode=test[2];superTyrian=test[3];curSel[MENU_UPGRADES]=test[4];
        music_disabled=test[5];samples_disabled=test[6];tyrMusicVolume=(JE_word)test[7];
        fxVolume=(JE_word)test[8];player[0].items.shield=(uint8_t)test[9];
        player[0].cash=12345;player[1].cash=67890;player[0].armor=73;
        shields[test[9]].mpwr=17;player[0].items.weapon[0].id=8;player[0].items.special=3;
        helpBoxColor=6;helpBoxBrightness=7;
        printf("item-status-case:%u\n",c);
        draw_item_screen_status();
        printf("item-status-state:%d,%d\n",helpBoxColor,helpBoxBrightness);
    }
    strcpy(miscText[15],"No cubes");strcpy(miscText[5],"Done");
    for(unsigned int p=0;p<COUNTOF(palettes);p++)
        for(unsigned int i=0;i<COUNTOF(palettes[0]);i++)
            palettes[p][i]=(uint8_t)(p*13+i*7);
    for(unsigned int i=0;i<COUNTOF(cube);i++) {
        snprintf(cube[i].title,sizeof cube[i].title,"Cube%u",i);
        cube[i].face_sprite=i==0?4:i==1?-1:i==2?7:2;
    }
    for(unsigned int i=0;i<COUNTOF(facepal);i++)facepal[i]=(uint8_t)(i%20);
    const int cube_cases[][5]={{7,0,2,0,0},{7,3,2,0,0},{7,3,3,0,0},
        {7,3,4,0,0},{8,3,3,1,0},{8,3,2,0,1},{8,3,3,0,0},{0,3,2,0,0}};
    for(unsigned int c=0;c<COUNTOF(cube_cases);c++) {
        const int *test=cube_cases[c];curMenu=test[0];cubeMax=test[1];
        curSel[MENU_DATA_CUBES]=(uint8_t)test[2];firstMenu9=test[3];backFromHelp=test[4];
        memset(menuChoices,0xA5,sizeof menuChoices);paletteChanged=false;newPal=11;
        tempW=91;temp2=92;temp=93;helpBoxColor=6;helpBoxBrightness=7;helpBoxShadeType=8;
        for(unsigned int i=0;i<sizeof colors;i++)colors[i]=(uint8_t)(i*3+1);
        printf("item-cubes-case:%u\n",c);
        draw_item_screen_data_cubes();
        uint32_t color_hash=2166136261u;
        for(unsigned int i=0;i<sizeof colors;i++)color_hash=(color_hash^colors[i])*16777619u;
        printf("item-cubes-state:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u\n",
            curMenu,firstMenu9,backFromHelp,menuChoices[MENU_DATA_CUBES],
            curSel[MENU_DATA_CUBES],paletteChanged,newPal,tempW,temp2,temp,
            helpBoxColor,helpBoxBrightness,helpBoxShadeType,color_hash);
    }
    strcpy(inputDevices[0],"Keyboard");strcpy(inputDevices[1],"Mouse");strcpy(inputDevices[2],"Joystick");
    for(unsigned int i=0;i<COUNTOF(cube);i++)
        snprintf(cube[i].header,sizeof cube[i].header,"Header%u",i);
    const int footer_cases[][7]={{9,0,4,4,0,2,5},{9,1,3,1,0,4,6},
        {9,2,4,3,2,3,5},{7,0,1,2,0,2,4},{8,1,3,4,4,4,5},{0,0,1,2,0,2,4}};
    for(unsigned int c=0;c<COUNTOF(footer_cases);c++) {
        const int *test=footer_cases[c];curMenu=test[0];joysticks=test[1];
        inputDevice[0]=(uint8_t)test[2];inputDevice[1]=(uint8_t)test[3];
        newPal=test[4];curSel[MENU_DATA_CUBES]=(uint8_t)test[5];
        menuChoices[MENU_DATA_CUBES]=(uint8_t)test[6];curPal=5;flash=true;
        memset(shotMultiPos,0xA5,sizeof shotMultiPos);
        for(unsigned int p=0;p<COUNTOF(palettes);p++)
            for(unsigned int i=0;i<COUNTOF(palettes[0]);i++)
                palettes[p][i]=(uint8_t)(p*13+i*7);
        for(unsigned int i=0;i<sizeof colors;i++)colors[i]=(uint8_t)(i*3+1);
        printf("item-footer-case:%u\n",c);
        draw_item_screen_footer();
        uint32_t color_hash=2166136261u,shot_hash=2166136261u;
        for(unsigned int i=0;i<sizeof colors;i++)color_hash=(color_hash^colors[i])*16777619u;
        for(unsigned int i=0;i<sizeof shotMultiPos;i++)shot_hash=(shot_hash^shotMultiPos[i])*16777619u;
        printf("item-footer-state:%d,%d,%d,%d,%d,%u,%u\n",curPal,newPal,flash,
            inputDevice[0],inputDevice[1],shot_hash,color_hash);
    }
    strcpy(miscText[5],"Return");strcpy(miscTextB[0],"EP ");
    for(unsigned int i=0;i<COUNTOF(save_records);i++) {
        snprintf(save_records[i].name,sizeof save_records[i].name,"Save%u",i);
        snprintf(save_records[i].levelName,sizeof save_records[i].levelName,"Level%u",i);
        save_records[i].episode=(uint8_t)(i+1);
    }
    const int load_save_cases[][6]={{6,0,1,0,2,3},{6,0,1,1,3,15},
        {6,0,0,0,13,2},{6,1,0,1,12,13},{6,0,0,0,4,2},{5,1,1,0,2,1}};
    for(unsigned int c=0;c<COUNTOF(load_save_cases);c++) {
        const int *test=load_save_cases[c];curMenu=test[0];twoPlayerMode=test[1];
        performSave=test[2];saveGamePageIndex=(uint8_t)test[3];
        curSel[MENU_LOAD_SAVE]=(uint8_t)test[4];save_game_count=test[5];temp2=88;
        printf("item-load-save-case:%u\n",c);
        draw_item_screen_load_save();
        printf("item-load-save-state:%u\n",temp2);
    }
    for(unsigned int i=0;i<8;i++) {
        keySettings[i]=(int)i;
        snprintf(key_name_values[i],sizeof key_name_values[i],"Scan%u",i);
    }
    for(unsigned int i=0;i<16;i++)snprintf(menuInt[6][i],sizeof menuInt[6][i],"Key%u",i);
    const int keyboard_cases[][3]={{5,2,0},{5,9,0},{5,10,0},{5,11,0},{4,7,0}};
    for(unsigned int c=0;c<COUNTOF(keyboard_cases);c++) {
        const int *test=keyboard_cases[c];curMenu=test[0];
        curSel[curMenu]=(uint8_t)test[1];memset(menuChoices,0xA5,sizeof menuChoices);temp2=88;
        printf("item-keyboard-case:%u\n",c);
        draw_item_screen_keyboard_config();
        printf("item-keyboard-state:%u,%u\n",menuChoices[MENU_KEYBOARD_CONFIG],temp2);
    }
    for(unsigned int i=0;i<16;i++)snprintf(menuInt[6][i],sizeof menuInt[6][i],"JoystickLabel%u",i);
    const int joystick_cases[][4]={{12,0,0,4},{12,1,0,2},{12,1,0,4},
        {12,2,1,9},{11,0,0,6}};
    for(unsigned int c=0;c<COUNTOF(joystick_cases);c++) {
        const int *test=joystick_cases[c];curMenu=test[0];joysticks=test[1];
        joystick_config=test[2];curSel[curMenu]=(uint8_t)test[3];
        memset(menuChoices,0xA5,sizeof menuChoices);memset(joystick,0,sizeof joystick);
        joystick[0].sensitivity=7;joystick[0].threshold=3;
        joystick[0].assignment[0][0]=(Joystick_assignment){AXIS,0,false,true};
        joystick[0].assignment[0][1]=(Joystick_assignment){BUTTON,2,false,false};
        joystick[0].assignment[1][0]=(Joystick_assignment){HAT,1,true,false};
        for(unsigned int action=2;action<10;action++)
            joystick[0].assignment[action][0]=(Joystick_assignment){BUTTON,(int)action,false,false};
        joystick[1].analog=true;joystick[1].sensitivity=10;joystick[1].threshold=8;
        joystick[1].assignment[0][0]=(Joystick_assignment){AXIS,1,true,false};
        printf("item-joystick-case:%u\n",c);
        draw_item_screen_joystick_config();
        printf("item-joystick-state:%u\n",menuChoices[MENU_JOYSTICK_CONFIG]);
    }
    memset(front,0,sizeof front);
    memset(back,0,sizeof back);memset(work_pixels,0,sizeof work_pixels);
    VGAScreen=&surfaces[0];VGAScreenSeg=NULL;
    const uint8_t quit_modes[]={0,1,2,3,4,5,6,7,8,9};
    for(unsigned int test=0;test<COUNTOF(quit_modes);test++) {
        const uint8_t mode=quit_modes[test];
        frame_count=frame_index=0;ready=false;newkey=newmouse=mousedown=false;
        memset(keysactive,0,sizeof(keysactive));        has_mouse=mode==8;isNetworkGame=mode==9;
        strcpy(miscText[28],"Quit title");strcpy(miscText[30],"Quit message");
        strcpy(miscText[9],"Yes");strcpy(miscText[10],"No");
        switch(mode) {
        case 0:key(40,0,0);break;
        case 1:key(43,0,0);key(40,0,0);break;
        case 2:key(41,0,0);break;
        case 3:key(82,0,0);key(40,0,0);break;
        case 4:mouse(100,130,57,124,1,0);break;
        case 5:mouse(200,130,152,124,1,0);break;
        case 6:mouse(100,123,100,123,1,0);key(40,0,0);break;
        case 7:key(80,0,0);key(44,0,0);break;
        case 8:mouse(141,130,142,130,1,0);key(40,0,0);break;
        case 9:key(40,0,0);break;
        }
        printf("quit-confirm-case:%u\n",mode);
        const bool quit_result=JE_quitRequest();
        printf("quit-confirm-result:%u,%u\n",quit_result,frame_index);
    }
    for(unsigned int mode=0;mode<8;mode++) {
        const bool confirm=(mode&1)!=0,halt=(mode&2)!=0,network=(mode&4)!=0;
        frame_count=frame_index=0;ready=false;newkey=newmouse=mousedown=false;
        has_mouse=false;isNetworkGame=network;gameLoaded=false;mainLevel=10+(int)mode;
        key(confirm?40:41,0,0);
        printf("quit-case:%u,%u,%u\n",confirm,halt,network);
        request_menu_quit(halt);
        printf("quit-state:%u,%d\n",gameLoaded,mainLevel);
    }
    const uint8_t shop_rows[]={0,1,2,8,3,5,6};
    for(unsigned int type=0;type<5;type++)for(unsigned int item=0;item<256;item++) {
        char *name=type==0?ships[item].name:type==1?weaponPort[item].name:
            type==2?shields[item].name:type==3?powerSys[item].name:options[item].name;
        snprintf(name,64,"Byte\x80\xff");
    }
    for(unsigned int menu=2;menu<=8;menu++) {
        const unsigned int row=shop_rows[menu-2],count=menu%2?2:3;
        memset(menuInt[5],0,sizeof menuInt[5]);
        for(unsigned int i=0;i<16;i++)snprintf(menuInt[5][i],64,"stale%u",i);
        snprintf(menuInt[2][menu-1],64,"header");
        strcpy(miscText[13],"Done");
        itemAvailMax[row]=count;itemAvail[row][0]=1;itemAvail[row][1]=0;itemAvail[row][2]=1;
        *playeritem_map(&player[0].items,menu-2)=menu%2?2:1;
        JE_genItemMenu((uint8_t)menu);
        print_upgrade_labels(count);
    }
    for(unsigned int i=0;i<256;i++) {
        snprintf(ships[i].name,sizeof ships[i].name,"Ship%u",i);
        snprintf(weaponPort[i].name,sizeof weaponPort[i].name,"Weapon%u",i);
        snprintf(shields[i].name,sizeof shields[i].name,"Shield%u",i);
        snprintf(powerSys[i].name,sizeof powerSys[i].name,"Generator%u",i);
        snprintf(options[i].name,sizeof options[i].name,"Sidekick%u",i);
        options[i].tr=i+100;options[i].ani=(uint8_t)(i%4);
    }
    const int upgrade_cases[][13]={
        {2,2,4,1,200,1,4,92,0,0,92,1,0},
        {2,2,5,1,5,2,4,3,4,0,4,0,0},
        {3,2,5,1,500,10,7,2,4,0,2,1,0},
        {4,5,5,1,500,10,5,5,3,0,3,1,1},
        {5,3,5,1,500,1,5,1,2,0,2,0,0},
        {6,2,4,1,500,1,5,3,0,0,3,0,0},
        {7,3,5,1,500,1,5,1,2,0,2,0,0},
        {8,2,5,1,500,1,5,3,4,0,4,0,0}
    };
    const uint8_t upgrade_rows[]={0,1,2,8,3,5,6};
    for(unsigned int c=0;c<COUNTOF(upgrade_cases);c++) {
        const int *test=upgrade_cases[c];const int menu=test[0],row=upgrade_rows[menu-2];
        curMenu=MENU_UPGRADE_SUB;curSel[MENU_UPGRADES]=(uint8_t)menu;
        curSel[MENU_UPGRADE_SUB]=(uint8_t)test[1];menuChoices[MENU_UPGRADE_SUB]=(uint8_t)test[2];
        lastDirection=test[3];player[0].cash=(uint32_t)test[4];cost_multiplier=test[5];
        player[0].items.weapon[0].power=1;player[0].items.weapon[1].power=1;
        player[0].items.weapon[menu==3?0:1].power=(uint8_t)test[6];
        memset(player[0].sidekick,0,sizeof player[0].sidekick);
        player[0].items.ship=22;player[0].items.weapon[0].id=23;player[0].items.weapon[1].id=24;
        player[0].items.shield=25;player[0].items.generator=26;player[0].items.sidekick[0]=27;player[0].items.sidekick[1]=28;
        old_items[0]=player[0].items;*playeritem_map(&old_items[0],menu-2)=(uint8_t)test[10];
        *playeritem_map(&player[0].items,menu-2)=(uint8_t)test[11];
        for(unsigned int i=0;i<COUNTOF(upgrade_rows);i++) {
            const int selectedMenu=(int)i+2,selectedRow=upgrade_rows[i];
            memset(itemAvail[selectedRow],0,sizeof itemAvail[selectedRow]);
            itemAvail[selectedRow][0]=1;itemAvail[selectedRow][1]=2;
            itemAvail[selectedRow][2]=0;itemAvail[selectedRow][3]=0;
            if(selectedMenu==menu) {
                itemAvail[selectedRow][0]=(uint8_t)test[7];
                itemAvail[selectedRow][1]=(uint8_t)test[8];
                itemAvail[selectedRow][2]=(uint8_t)test[9];
                itemAvail[selectedRow][3]=0;
            }
            snprintf(menuInt[2][selectedMenu-1],sizeof menuInt[0][0],"Title%u",selectedMenu);
        }
        strcpy(miscText[13],"Done");rightPowerAfford=test[12]!=0;
        cash_left_value=200;upgradeCost=downgradeCost=0;
        printf("item-upgrade-case:%u\n",c);
        draw_item_screen_upgrade_menu();
        printf("item-upgrade-state:%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%d,%d,%u,%u,%u,%d,%u,%d,%d\n",
            curSel[MENU_UPGRADE_SUB],player[0].items.ship,player[0].items.weapon[0].id,
            player[0].items.weapon[0].power,player[0].items.weapon[1].id,player[0].items.weapon[1].power,
            player[0].items.shield,player[0].items.generator,player[0].items.sidekick[0],
            player[0].items.sidekick[1],leftPower,rightPower,rightPowerAfford,
            player[0].sidekick[(menu==8)?1:0].charge_ticks,
            player[0].sidekick[(menu==8)?1:0].charge,player[0].sidekick[(menu==8)?1:0].style,
            player[0].sidekick[(menu==8)?1:0].animation_enabled,
            player[0].sidekick[(menu==8)?1:0].animation_frame,upgradeCost,downgradeCost);
    }
    strcpy(miscText[11],"Read");strcpy(miscText[12],"Done");strcpy(miscText[67],"Extra Game");
    for(unsigned int c=0;c<2;c++) {
        for(unsigned int line=0;line<20;line++)
            snprintf(cube[c].text[line],sizeof cube[c].text[line],"Cube%uLine%u",c,line+1);
        cube[c].last_line=(int)(c?20:16);
    }
    for(unsigned int i=0;i<1024;i++)colors[i]=(uint8_t)(i*3+1);
    for(unsigned int p=0;p<20;p++)for(unsigned int i=0;i<1024;i++)palettes[p][i]=(uint8_t)(p*13+i*7);
    const int update_cases[][18]={
        {8,0,2,1,16,20,5,7,180,120,1,0,0,3,4,1,0,5},
        {8,0,3,0,12,12,2,-6,164,47,1,0,0,7,7,0,0,5},
        {3,0,2,0,20,16,0,0,0,0,0,1,2,4,4,7,1,4},
        {4,3,2,0,20,16,0,0,0,0,1,1,2,6,0,0,0,4},
        {7,0,3,0,20,16,0,0,0,0,0,0,0,6,6,-1,0,5},
        {2,0,2,1,10,16,0,0,0,0,0,0,0,-2,-1,0,0,5}
    };
    for(unsigned int c=0;c<COUNTOF(update_cases);c++) {
        const int *test=update_cases[c];curMenu=test[0];curSel[MENU_UPGRADES]=(uint8_t)test[1];
        curSel[MENU_DATA_CUBES]=(uint8_t)test[2];currentCube=test[3];
        cube[0].last_line=test[4];cube[1].last_line=test[5];yLoc=test[6];yChg=test[7];
        mouseX=test[8];mouseY=test[9];backFromHelp=test[10];paletteChanged=test[11];
        newPal=test[12];curPal=test[13];col=test[14];colC=test[15];extraGame=test[16];
        menuChoices[MENU_DATA_CUBES]=(uint8_t)test[17];
        printf("item-update-case:%u\n",c);
        update_item_screen_frame();
        printf("item-update-state:%d,%d,%d,%d,%d,%d,%d,%d,%d\n",col,colC,mouseCursor,
            yLoc,yChg,backFromHelp,curPal,newPal,paletteChanged);
    }
    FILE *cube_fixture=fopen(cube_test_filename,"wb");
    if(!cube_fixture)return 2;
    const char *cube_strings[]={"*   12","Flight log","Chapter 4",
        "alpha beta gamma delta epsilon","","","paragraph word",
        "*   13","Flight log 2","Chapter 5","second record","*"};
    for(unsigned int i=0;i<COUNTOF(cube_strings);i++)write_cube_string(cube_fixture,cube_strings[i]);
    fclose(cube_fixture);
    for(int i=0;i<2;i++) {
        memset(&cube[i],0,sizeof cube[i]);
        if(!load_cube(i,i+1))return 2;
        printf("cube-loaded:%d,%d,%d,",i,cube[i].face_sprite,cube[i].last_line);
        print_cube_hex(cube[i].title);putchar(',');
        print_cube_hex(cube[i].header);putchar('\n');
        for(int line=0;line<cube[i].last_line;line++) {
            printf("cube-line:%d,%d,",i,line);print_cube_hex(cube[i].text[line]);putchar('\n');
        }
    }
    remove(cube_test_filename);
    for(int kind=0;kind<3;kind++)for(int mode=0;mode<40;mode++) {
        printf("case:%d,%d\n",kind,mode);
        for(int i=0;i<6;i++)snprintf(episode_name[i],31,"E%d",i);
        for(int i=0;i<7;i++)snprintf(difficulty_name[i],21,"D%d",i);
        for(int i=0;i<5;i++)snprintf(gameplay_name[i],26,"G%d",i);
        memset(front,0,64000);memset(back,0,64000);memset(keysactive,0,512);
        shopSpriteSheet.data=mode%2?&shopSpriteSheet:NULL;
        newkey=newmouse=ready=false;mouse_x=mouse_y=0;modifiers=0;
        onePlayerAction=true;twoPlayerMode=true;episodeNum=7;initial_episode_num=9;difficultyLevel=10;
        for(int i=0;i<4;i++)episodeAvail[i]=(mode%4==0 || i%2==0);
        script(kind,mode);
        bool result=kind==0?gameplaySelect():kind==1?episodeSelect():difficultySelect();
        printf("result:%d,%d,%d,%d,%d,%d,%d\n",result,onePlayerAction,twoPlayerMode,episodeNum,initial_episode_num,difficultyLevel,frame_index);
    }
    const uint8_t menus[]={0,3,9,10},choices[]={0,1,2,3,4,5,6,7,8,255},selected[]={0,1,7,255};
    for(unsigned int m=0;m<COUNTOF(menus);m++)
        for(unsigned int c=0;c<COUNTOF(choices);c++)
            for(unsigned int s=0;s<COUNTOF(selected);s++) {
                menuChoices[MENU_PLAY_NEXT_LEVEL]=selected[s];
                printf("choice-y:%u,%u,%u,%d\n",menus[m],choices[c],selected[s],
                    get_menu_choice_y(menus[m],choices[c]));
            }
    const uint8_t draw_menus[]={0,1,3,9,10},counts[]={1,2,4,7},selections[]={0,2,4,7};
    for(unsigned int m=0;m<COUNTOF(draw_menus);m++)
        for(unsigned int c=0;c<COUNTOF(counts);c++)
                for(unsigned int s=0;s<COUNTOF(selections);s++) {
                    curMenu=draw_menus[m];menuChoices[curMenu]=counts[c];curSel[curMenu]=selections[s];
                    for(unsigned int i=0;i<counts[c];i++)
                        snprintf(menuInt[curMenu+1][i],sizeof menuInt[0][0],"m%uc%u",(unsigned)curMenu,i+1);
                    printf("menu-draw-case:%u,%u,%u\n",curMenu,counts[c],curSel[curMenu]);
                    JE_drawMenuChoices();
                }
    for(unsigned int menu=0;menu<14;menu++)
        for(performSave=0;performSave<=2;performSave++)
                for(unsigned int selection=2;selection<=7;selection++) {
                    curMenu=menu;curSel[MENU_DATA_CUBES]=selection;
                    for(unsigned int row=0;row<15;row++)
                        for(unsigned int column=0;column<16;column++)
                            snprintf(menuInt[row][column],sizeof menuInt[0][0],"r%uc%u",row,column);
                    for(unsigned int c=0;c<16;c++)snprintf(cube[c].header,sizeof cube[c].header,"cube%u",c);
                    printf("menu-header-case:%u,%d,%u\n",menu,performSave,selection);
                    JE_drawMenuHeader();
                }
    for(unsigned int i=0;i<34;i++)snprintf(mainMenuHelp[i],sizeof mainMenuHelp[i],"help%02u",i);
    for(unsigned int menu=0;menu<14;menu++)
        for(unsigned int column=0;column<11;column++)menuHelp[menu][column]=(menu+column)%34+1;
    const uint8_t help_counts[]={2,5,10,17},help_selections[]={2,3,5,10,12,17};
    for(unsigned int menu=0;menu<14;menu++)
        for(unsigned int c=0;c<COUNTOF(help_counts);c++)
            for(unsigned int s=0;s<COUNTOF(help_selections);s++)
                for(unsigned int power=0;power<4;power++)
                    for(int cubes=0;cubes<=1;cubes++) {
                        if(menu!=MENU_JOYSTICK_CONFIG && help_selections[s]>12)continue;
                        curMenu=menu;curSel[menu]=help_selections[s];menuChoices[menu]=help_counts[c];
                        leftPower=power&1;rightPower=power&2;cubeMax=cubes;
                        printf("help-case:%u,%u,%u,%u,%d\n",menu,curSel[menu],menuChoices[menu],power,cubes);
                        JE_drawMainMenuHelpText();
                    }
    const int32_t cash_values[]={INT32_MIN,-100,0,100,INT32_MAX};
    const uint8_t score_menus[]={0,MENU_UPGRADE_SUB,7,13};
    for(unsigned int menu=0;menu<COUNTOF(score_menus);menu++)
        for(unsigned int value=0;value<COUNTOF(cash_values);value++) {
            curMenu=score_menus[menu];cash_left_value=cash_values[value];
            printf("score-case:%u,%d\n",curMenu,cash_left_value);
            JE_drawScore();
        }
    for(int i=0;i<64000;i++)front[i]=(uint8_t)(i*29+7);
    player[0]=(WeaponViewPlayer){.x=19,.y=23,.delta_x_shot_move=31,.delta_y_shot_move=-7,
        .last_x_explosion_follow=88,.last_y_explosion_follow=91,.sidekick={{1,2},{3,4}}};
    memset(shotAvail,0xA5,sizeof shotAvail);memset(shotRepeat,0xA5,sizeof shotRepeat);
    memset(shotMultiPos,0xA5,sizeof shotMultiPos);power=42;lastPower=23;
    puts("weapon-view-case");
    JE_initWeaponView();
    uint32_t screen_hash=2166136261u;
    for(int i=0;i<64000;i++)screen_hash=(screen_hash^front[i])*16777619u;
    printf("weapon-view:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%u\n",
        player[0].sidekick[0].x,player[0].sidekick[0].y,player[0].sidekick[1].x,player[0].sidekick[1].y,
        player[0].x,player[0].y,player[0].delta_x_shot_move,player[0].delta_y_shot_move,
        player[0].last_x_explosion_follow,player[0].last_y_explosion_follow,
        power,lastPower,shotAvail[0],screen_hash);
    printf("weapon-arrays:%u,%u,%u,%u\n",
        shotAvail[MAX_PWEAPON-1],shotRepeat[0],shotRepeat[10],shotMultiPos[10]);
    const int scale_breaks[]={1,4,20,81,0};
    for(unsigned int c=0;c<COUNTOF(scale_breaks);c++) {
        for(int i=0;i<64000;i++) {
            front[i]=(uint8_t)(i*19+c*3+5);
            back[i]=(uint8_t)(i*31+c*7+13);
        }
        scale_trace=true;scale_button_after=scale_breaks[c];scale_button_calls=0;
        printf("scale-picture-case:%u,%d\n",c,scale_breaks[c]);
        JE_scaleInPicture(VGAScreen,VGAScreen2);
        scale_trace=false;
    }
    const int ship_specs_breaks[]={1,6,81,0};
    for(unsigned int c=0;c<COUNTOF(ship_specs_breaks);c++) {
        for(int i=0;i<64000;i++) {
            front[i]=(uint8_t)(i*17+c*3+1);
            back[i]=(uint8_t)(i*13+c*9+2);
            work_pixels[i]=(uint8_t)(i*7+c*5+3);
        }
        scale_trace=true;scale_button_after=ship_specs_breaks[c];scale_button_calls=0;
        printf("ship-specs-case:%u,%d\n",c,ship_specs_breaks[c]);
        JE_doShipSpecs();
        scale_trace=false;
    }
    const uint8_t routes[][5]={{21,0,0,0,0},{2,12,14,18,20}};
    const uint8_t route_sizes[]={1,5},origins[]={13,21};
    for(unsigned int id=0;id<21;id++)snprintf(pName[id],sizeof pName[id],"planet%u",id+1);
    strcpy(miscText[5],"Done");
    for(unsigned int c=0;c<COUNTOF(route_sizes);c++) {
        mapOrigin=origins[c];mapPNum=route_sizes[c];
        for(uint8_t i=0;i<mapPNum;i++){mapPlanet[i]=routes[c][i];mapSection[i]=30+c*10+i;}
        curMenu=0;newPal=0;menuChoices[MENU_PLAY_NEXT_LEVEL]=0;curSel[MENU_PLAY_NEXT_LEVEL]=0;
        printf("next-menu-case:%u,%u,%u\n",c,mapOrigin,mapPNum);
        setup_next_level_menu();
        printf("next-menu:%d,%d,%d,%d,%d,%d,%u,%u\n",curMenu,newPal,(int)navX,(int)navY,
            (int)newNavX,(int)newNavY,menuChoices[MENU_PLAY_NEXT_LEVEL],curSel[MENU_PLAY_NEXT_LEVEL]);
        for(uint8_t i=0;i<menuChoices[MENU_PLAY_NEXT_LEVEL];i++)
            printf("next-label:%u,%s\n",i,menuInt[4][i]);
        printf("next-dots:");
        for(uint8_t i=0;i<mapPNum;i++)printf("%s%u",i?",":"",planetDots[i]);
        puts("");
        jumpSection=false;mainLevel=0;start_current_level();
        printf("start-current:%d,%d\n",mainLevel,jumpSection);
        isNetworkGame=false;return_to_options_menu();printf("return-options:0,%d\n",curMenu);
        isNetworkGame=true;return_to_options_menu();printf("return-options:1,%d\n",curMenu);
    }
    const int input_device_cases[][5]={{3,-1,0,1,2},{3,1,0,1,2},{4,-1,0,2,1},{4,1,0,2,1},
        {3,-1,1,2,1},{3,1,1,2,1},{4,-1,1,1,3},{4,1,1,1,3},
        {3,-1,3,4,1},{3,1,3,4,1},{4,-1,3,2,5},{4,1,3,2,5}};
    for(unsigned int i=0;i<COUNTOF(input_device_cases);i++) {
        const int *test=input_device_cases[i];
        curSel[MENU_2_PLAYER_ARCADE]=test[0];joysticks=test[2];
        inputDevice[0]=test[3];inputDevice[1]=test[4];
        printf("input-device-case:%d,%d,%d,%d,%d\n",test[0],test[1],test[2],test[3],test[4]);
        change_two_player_input_device(test[1]);
        printf("input-device-state:%u,%u\n",inputDevice[0],inputDevice[1]);
    }
    for(int menu=0;menu<=14;menu++) {
        curMenu=menu;if(menu<14)curSel[menu]=(uint8_t)(40+menu);
        col=77;colC=9;player[0].items.ship=(uint8_t)(20+menu);
        printf("menu-dispatch-case:%d,%u,%u\n",menu,(unsigned)(90+menu),(unsigned)(40+menu));
        JE_menuFunction((uint8_t)(90+menu));
        printf("menu-dispatch-state:%d,%d,%d,%u\n",curMenu,col,colC,old_items[0].ship);
    }
    for(int weapon_selection=3;weapon_selection<=4;weapon_selection++)
        for(int submenu_selection=2;submenu_selection<=8;submenu_selection+=3)
            for(int can_change=0;can_change<=1;can_change++)
                for(int direction=-1;direction<=1;direction+=2) {
                    const int choice=submenu_selection-2;
                    int temporary_power[7];
                    for(int i=0;i<7;i++)temporary_power[i]=10+i;
                    if(weapon_selection==4&&submenu_selection==8&&can_change&&direction>0)
                        temporary_power[choice]=255;
                    curSel[MENU_UPGRADES]=(uint8_t)weapon_selection;
                    curSel[MENU_UPGRADE_SUB]=(uint8_t)submenu_selection;
                    player[0].items.weapon[weapon_selection-3].power=7;
                    printf("weapon-power-case:%d,%d,%d,%d\n",weapon_selection,submenu_selection,direction,can_change);
                    change_upgrade_weapon_power(direction,can_change,temporary_power);
                    printf("weapon-power-state:");
                    for(int i=0;i<7;i++)printf("%s%d",i?",":"",temporary_power[i]);
                    printf(",%u\n",player[0].items.weapon[weapon_selection-3].power);
                }
    const int horizontal_cases[][9]={{2,4,3,1,1,1,0,1,1},{2,4,3,-1,0,1,1,0,0},
        {2,5,3,-1,0,0,0,0,1},{11,5,3,1,0,0,0,0,1},{4,2,3,-1,1,0,0,0,0},
        {4,2,3,1,0,1,1,0,0},{4,2,4,1,0,1,0,0,0},{12,3,3,-1,0,0,0,0,0},
        {9,4,3,1,0,0,0,0,0},{0,4,3,1,0,0,0,0,0}};
    for(unsigned int i=0;i<COUNTOF(horizontal_cases);i++) {
        const int *test=horizontal_cases[i];const int menu=test[0],selection=test[1],upgrade=test[2];
        curMenu=menu;curSel[menu]=selection;curSel[MENU_UPGRADES]=upgrade;
        curSel[MENU_UPGRADE_SUB]=2;curSel[MENU_JOYSTICK_CONFIG]=selection;
        curSel[MENU_2_PLAYER_ARCADE]=selection;leftPower=test[4];rightPower=test[5];
        rightPowerAfford=test[6];music_disabled=test[7];samples_disabled=test[8];
        inputDevice[0]=1;inputDevice[1]=2;joysticks=0;tyrMusicVolume=100;fxVolume=80;
        int temporary_power[7];for(int j=0;j<7;j++)temporary_power[j]=10+j;
        player[0].items.weapon[upgrade-3].power=7;
        printf("horizontal-case:%d,%d,%d,%d,%d,%d,%d,%d,%d\n",menu,selection,upgrade,
            test[3],test[4],test[5],test[6],test[7],test[8]);
        handle_item_screen_horizontal_key(test[3],temporary_power);
        printf("horizontal-state:%u,%u,%u,%u,%u,%u,",
            music_disabled,samples_disabled,inputDevice[0],inputDevice[1],tyrMusicVolume,fxVolume);
        for(int j=0;j<7;j++)printf("%s%d",j?",":"",temporary_power[j]);
        printf(",%u\n",player[0].items.weapon[upgrade-3].power);
    }
    const int activate_cases[][5]={{0,4,3,2,2},{4,5,3,3,3},{4,6,4,4,4},
        {4,8,5,5,2},{5,7,4,6,1},{13,3,3,2,3}};
    for(unsigned int i=0;i<COUNTOF(activate_cases);i++) {
        const int *test=activate_cases[i];const int menu=test[0],selection=test[1];
        const int upgrade=test[2],item_count=test[3],initial_power=test[4];
        curMenu=menu;curSel[menu]=selection;curSel[MENU_UPGRADES]=upgrade;
        itemAvailMax[upgrade==3?1:upgrade==4?2:3]=item_count;
        player[0].items.ship=(uint8_t)(20+menu);
        player[0].items.weapon[upgrade==3?0:upgrade==4?1:0].power=initial_power+10;
        int temporary_power[7];for(int j=0;j<7;j++)temporary_power[j]=initial_power+j;
        keyboardUsed=false;col=77;colC=9;
        printf("activate-case:%d,%d,%d,%d,%d\n",menu,selection,upgrade,item_count,initial_power);
        activate_item_screen_selection(temporary_power);
        printf("activate-state:%u,",keyboardUsed);
        for(int j=0;j<7;j++)printf("%s%d",j?",":"",temporary_power[j]);
        printf(",%u,%d,%d,%d,%u\n",
            player[0].items.weapon[upgrade==3?0:upgrade==4?1:0].power,
            curMenu,col,colC,old_items[0].ship);
    }
    const int cancel_cases[][7]={{6,1,9,17,1,3,2},{4,2,8,17,0,6,3},
        {8,8,9,17,0,4,4},{5,3,9,17,0,7,2},{7,1,9,17,0,2,5},{12,3,9,17,0,5,4}};
    for(unsigned int i=0;i<COUNTOF(cancel_cases);i++) {
        const int *test=cancel_cases[i];const int menu=test[0],escape=test[1];
        curMenu=menu;oldMenu=test[2];oldPal=test[3];newPal=17;quikSave=test[4];
        curSel[MENU_UPGRADE_SUB]=test[5];lastCurSel=test[6];
        player[0].cash=123;player[0].items.ship=30;
        player[0].items.weapon[0].id=9;player[0].items.weapon[0].power=8;
        old_items[0].ship=10;old_items[0].weapon[0].id=5;old_items[0].weapon[0].power=3;
        cash_left_value=83;keyboardUsed=false;
        printf("cancel-case:%d,%d,%d,%d,%d,%d,%d\n",menu,escape,oldMenu,oldPal,
            quikSave,curSel[MENU_UPGRADE_SUB],lastCurSel);
        cancel_item_screen_selection();
        printf("cancel-state:%u,%d,%d,%u,%u,%u,%u,%u\n",keyboardUsed,curMenu,newPal,
            player[0].cash,player[0].items.ship,player[0].items.weapon[0].id,
            player[0].items.weapon[0].power,curSel[MENU_UPGRADE_SUB]);
    }
    const int help_cases[][2]={{3,0},{4,0},{8,0},{2,1}};
    for(unsigned int i=0;i<COUNTOF(help_cases);i++) {
        const int menu=help_cases[i][0],network=help_cases[i][1];
        curMenu=menu;isNetworkGame=network;newPal=9;curPal=6;backFromHelp=false;
        for(int j=0;j<64000;j++) {
            front[j]=(uint8_t)(j*29+7);
            back[j]=(uint8_t)(j*31+13);
        }
        memset(colors,0,sizeof(colors));
        for(int p=0;p<20;p++)for(int j=0;j<1024;j++)palettes[p][j]=(uint8_t)(p*13+j*7);
        printf("help-case:%d,%d\n",menu,network);
        show_item_screen_help();
        uint32_t background_hash=2166136261u,color_hash=2166136261u,screen_hash=2166136261u;
        for(int j=0;j<64000;j++) {
            background_hash=(background_hash^back[j])*16777619u;
            screen_hash=(screen_hash^front[j])*16777619u;
        }
        for(int j=0;j<1024;j++)color_hash=(color_hash^colors[j])*16777619u;
        printf("help-state:%d,%d,%d,%u,%u,%u\n",newPal,curPal,backFromHelp,
            screen_hash,color_hash,background_hash);
    }
    const int quick_save_cases[][3]={{8,1,17},{7,0,9},{6,1,3},{2,0,5}};
    for(unsigned int i=0;i<COUNTOF(quick_save_cases);i++) {
        const int menu=quick_save_cases[i][0],save=quick_save_cases[i][1];
        curMenu=menu;curPal=quick_save_cases[i][2];quikSave=false;
        saveGamePageIndex=7;performSave=!save;newPal=9;oldMenu=0;oldPal=0;
        printf("quick-save-case:%d,%d,%d\n",menu,save,curPal);
        open_quick_save_menu(save);
        printf("quick-save-state:%d,%u,%d,%u,%d,%d,%u\n",quikSave,oldMenu,curMenu,
            saveGamePageIndex,performSave,newPal,oldPal);
    }
    const int wait_cases[][19]={
        {2,1,0,0,0,0,0,0,0,0,0,0,0,0,12,14,0,31,42},
        {2,0,1,0,0,0,0,0,0,0,0,0,0,0,12,14,0,51,62},
        {2,1,0,0,1,0,0,0,0,0,0,0,0,0,12,14,0,71,82},
        {2,1,0,0,0,1,1,0,0,0,0,0,0,0,12,14,0,91,102},
        {7,1,0,0,1,1,0,0,0,0,0,0,0,0,12,14,0,111,122},
        {6,1,0,0,1,1,1,0,0,0,0,0,0,0,12,14,0,131,142},
        {8,0,1,1,0,0,0,0,0,0,0,0,0,0,12,14,0,151,162},
        {8,0,1,2,0,0,0,0,0,0,0,0,0,0,120,20,0,171,182},
        {8,0,0,0,0,0,0,0,1,1,1,0,0,0,120,20,0,191,202},
        {8,1,0,0,0,0,0,1,1,1,1,1,1,2,132,20,0,211,222},
        {8,0,0,0,0,0,0,0,0,0,0,1,0,2,12,14,1,231,242},
        {8,0,0,3,0,0,0,0,0,0,0,0,0,0,12,14,0,251,262},
        {8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,14,-1,271,282},
        {8,0,0,0,0,0,0,0,0,0,0,0,0,0,12,9,1,291,302}
    };
    for(unsigned int i=0;i<COUNTOF(wait_cases);i++) {
        const int *test=wait_cases[i];
        curMenu=test[0];newkey=test[1];mouseButton=test[2];mouseCursor=test[3];
        curPal=6;quikSave=false;saveGamePageIndex=7;performSave=false;
        newPal=9;oldMenu=9;oldPal=17;mouseX=test[17];mouseY=test[18];
        inputDetected=false;yLoc=test[14];cube[0].last_line=test[15];yChg=test[16];
        currentCube=0;ready=false;modifiers=0;memset(keysactive,0,sizeof keysactive);
        keysactive[SDL_SCANCODE_S]=test[4];keysactive[SDL_SCANCODE_L]=test[5];
        keysactive[test[6]?SDL_SCANCODE_RALT:SDL_SCANCODE_LALT]=test[4]||test[5];
        keysactive[SDL_SCANCODE_PAGEUP]=test[7];keysactive[SDL_SCANCODE_PAGEDOWN]=test[8];
        keysactive[SDL_SCANCODE_UP]=test[9];keysactive[SDL_SCANCODE_DOWN]=test[10];
        joysticks=test[13];memset(joystick,0,sizeof joystick);
        if(joysticks>0)joystick[0].direction[0]=test[11];
        if(joysticks>1)joystick[1].direction[2]=test[12];
        printf("wait-case:");
        for(int j=0;j<19;j++)printf("%s%d",j?",":"",test[j]);
        puts("");
        const bool wait_result=wait_for_item_screen_input();
        printf("wait-state:%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d,%d\n",
            wait_result,yChg,curMenu,quikSave,oldMenu,saveGamePageIndex,performSave,
            newPal,oldPal,mouseButton,mouseX,mouseY,inputDetected);
    }
    const int key_cases[][8]={
        {4,3,46,3,2,1,20,77},{4,4,45,1,2,1,20,77},{4,5,46,3,2,1,20,77},
        {2,3,46,3,2,1,20,77},{4,4,56,1,2,3,20,77},{4,3,56,1,2,1,20,77},
        {4,3,56,1,2,1,20,77},{2,2,44,1,2,1,20,77},{2,2,40,1,2,1,20,77},
        {2,2,88,1,2,1,20,77},{2,2,41,1,2,1,20,77},{2,2,58,1,2,1,20,77},
        {2,2,82,1,2,1,20,77},{2,2,81,1,2,1,20,77},{8,2,74,1,2,1,20,77},
        {8,2,77,1,2,1,20,77},{2,2,74,1,2,1,20,77},{2,2,77,1,2,1,20,77},
        {2,2,80,1,2,1,20,77},{2,2,79,1,2,1,20,77},{2,2,42,1,2,1,20,77}
    };
    for(unsigned int i=0;i<COUNTOF(key_cases);i++) {
        const int *test=key_cases[i];curMenu=test[0];curSel[MENU_UPGRADES]=test[1];
        lastkey_scan=test[2];player[0].items.weapon[0].id=1;player[0].items.weapon[0].power=test[3];
        player[0].items.weapon[1].id=2;player[0].items.weapon[1].power=test[4];
        player[0].weapon_mode=test[5];cube[0].last_line=test[6];yLoc=test[7];currentCube=0;
        weaponPort[1].opnum=3;weaponPort[2].opnum=2;
        memset(weaponPort[1].op,0,sizeof weaponPort[1].op);
        memset(weaponPort[2].op,0,sizeof weaponPort[2].op);
        weaponPort[1].op[0][2]=300;
        int temporary_power[7]={0};
        printf("key-case:%d,%d,%d,%d,%d,%d,%d,%d\n",test[0],test[1],test[2],
            test[3],test[4],test[5],test[6],test[7]);
        handle_item_screen_key(temporary_power);
        printf("key-state:%u,%d,%u,%u,%d\n",player[0].weapon_mode,
            weaponPort[1].op[0][2],weaponPort[2].op[0][1],yLoc,weaponPort[0].op[0][0]);
    }
    const int move_cases[][12]={
        {2,2,4,-1,0,2,5,6,5,0,0,0},{2,4,4,1,0,2,5,6,5,0,0,0},
        {8,2,4,-1,0,2,5,6,5,0,0,0},{4,2,5,1,3,3,5,6,5,0,0,0},
        {4,4,5,1,4,2,5,6,5,0,0,0},{12,4,7,1,0,2,5,6,5,1,0,0},
        {12,6,7,-1,0,2,5,6,5,1,0,0},{12,3,7,1,0,2,5,6,5,1,0,0},
        {12,4,7,-1,0,2,5,6,5,1,0,0},{12,4,7,1,0,2,5,6,5,0,0,0},
        {12,3,7,1,0,2,5,6,5,1,0,1},{2,2,2,-1,0,2,5,6,5,0,0,0}
    };
    for(unsigned int i=0;i<COUNTOF(move_cases);i++) {
        const int *test=move_cases[i];curMenu=test[0];curSel[curMenu]=test[1];
        menuChoices[curMenu]=test[2];curSel[MENU_UPGRADES]=test[4];
        curSel[MENU_UPGRADE_SUB]=test[5];player[0].items.weapon[0].power=test[6];
        player[0].items.weapon[1].power=test[7];player[0].weapon_mode=test[8];
        joysticks=test[9];joystick_config=test[10];memset(joystick,0,sizeof joystick);
        joystick[joystick_config].analog=test[11];keyboardUsed=false;lastDirection=99;
        int temporary_power[7]={10,11,12,13,14,15,16};
        printf("move-case:");
        for(int j=0;j<12;j++)printf("%s%d",j?",":"",test[j]);
        puts("");
        move_item_screen_selection(test[3],temporary_power);
        printf("move-state:%u,%d,%u,%u,%u,%u\n",keyboardUsed,lastDirection,
            curSel[curMenu],player[0].items.weapon[0].power,
            player[0].items.weapon[1].power,player[0].weapon_mode);
    }
    const int mouse_input_cases[][6]={
        {7,0,4,1,100,100},{7,2,4,1,180,38},{8,2,4,1,270,165},
        {2,2,7,1,225,76},{2,2,7,1,171,21},{8,2,4,1,200,100},
        {7,0,4,1,200,60},{11,2,7,1,225,70},{2,2,7,1,308,76}
    };
    for(unsigned int i=0;i<COUNTOF(mouse_input_cases);i++) {
        const int *test=mouse_input_cases[i];curMenu=test[0];cubeMax=test[1];
        menuChoices[curMenu]=test[2];mouseButton=test[3];mouseX=test[4];mouseY=test[5];
        newPal=9;lastDirection=99;
        int temporary_power[7]={0};
        printf("mouse-input-case:");
        for(int j=0;j<6;j++)printf("%s%d",j?",":"",test[j]);
        puts("");
        handle_item_screen_mouse_input(temporary_power);
        printf("mouse-input-state:%d,%d,%d,%d,%d,%d\n",
            curMenu,lastDirection,mouseButton,mouseX,mouseY,newPal);
    }
    const int mouse_y_values[]={-100,0,20,37,38,39,54,70,200,1000};
    const uint8_t mouse_choice_counts[]={0,4,255};
    for(uint8_t menu=0;menu<14;menu++) {
        if(menu==8)continue;
        curMenu=menu;
        for(unsigned int y=0;y<COUNTOF(mouse_y_values);y++)
            for(unsigned int c=0;c<COUNTOF(mouse_choice_counts);c++) {
                mouseY=mouse_y_values[y];menuChoices[menu]=mouse_choice_counts[c];
                printf("mouse-selection-case:%u,%d,%u\n",menu,mouseY,menuChoices[menu]);
                printf("mouse-selection:%d\n",get_item_screen_mouse_selection());
            }
    }
    const int selection_cases[][8]={{2,2,7,3,3,100,83,1},{2,3,7,3,3,100,83,1},
        {4,2,4,3,4,100,83,1},{4,2,4,3,3,100,83,6},{4,2,4,4,3,100,83,1},
        {4,3,4,4,3,100,83,1},{4,2,4,4,3,100,83,6},{4,2,4,3,5,100,83,1}};
    for(unsigned int i=0;i<COUNTOF(selection_cases);i++) {
        const int *test=selection_cases[i];const int menu=test[0],current=test[1];
        const int count=test[2],upgrade=test[3],selection=test[4];
        curMenu=menu;curSel[menu]=current;curSel[MENU_UPGRADES]=upgrade;
        menuChoices[menu]=count;player[0].cash=test[5];cash_left_value=test[6];
        player[0].weapon_mode=7;player[0].items.weapon[0].power=4;
        player[0].items.weapon[1].power=5;cost_multiplier=test[7];
        int temporary_power[7]={9,10,11,12,13,14,15};
        const uint8_t available_items[]={10,20,30,40};
        const int item_row=upgrade==3?1:2;
        for(int j=0;j<4;j++)itemAvail[item_row][j]=available_items[j];
        printf("selection-case:%d,%d,%d,%d,%d,%d,%d,%d\n",
            menu,current,count,upgrade,selection,test[5],test[6],test[7]);
        select_item_screen_menu_option(selection,temporary_power);
        printf("selection-state:%d,%u,%u,%u,%u,%u,",
            curMenu,curSel[menu],player[0].cash,player[0].weapon_mode,
            player[0].items.weapon[0].power,player[0].items.weapon[1].power);
        for(int j=0;j<7;j++)printf("%s%d",j?",":"",temporary_power[j]);
        puts("");
    }
    const int weapon_mouse_cases[][8]={{4,23,149,3,2,1,0,0},{4,36,168,3,2,0,0,0},
        {4,119,149,4,5,0,1,1},{4,131,168,4,5,0,1,0},
        {4,37,149,3,2,1,1,1},{2,23,149,3,2,1,1,1}};
    for(unsigned int i=0;i<COUNTOF(weapon_mouse_cases);i++) {
        const int *test=weapon_mouse_cases[i];const int menu=test[0],x=test[1],y=test[2];
        const int upgrade=test[3],submenu=test[4];
        curMenu=menu;mouseX=x;mouseY=y;curSel[MENU_UPGRADES]=upgrade;
        curSel[MENU_UPGRADE_SUB]=submenu;leftPower=test[5];rightPower=test[6];
        rightPowerAfford=test[7];player[0].items.weapon[upgrade-3].power=7;
        int temporary_power[7]={10,11,12,13,14,15,16};
        printf("weapon-mouse-case:%d,%d,%d,%d,%d,%d,%d,%d\n",
            menu,x,y,upgrade,submenu,test[5],test[6],test[7]);
        adjust_item_screen_weapon_power(temporary_power);
        printf("weapon-mouse-state:");
        for(int j=0;j<7;j++)printf("%s%d",j?",":"",temporary_power[j]);
        printf(",%u\n",player[0].items.weapon[upgrade-3].power);
    }
    const int volume_mouse_cases[][5]={{220,76,0,1,1},{221,70,1,1,1},
        {224,82,0,0,1},{305,82,0,1,0},{309,82,0,1,1},{1000,70,0,0,0},
        {221,86,1,0,1},{309,98,1,1,0},{309,85,1,1,1}};
    for(unsigned int i=0;i<COUNTOF(volume_mouse_cases);i++) {
        const int *test=volume_mouse_cases[i];
        mouseX=test[0];mouseY=test[1];curSel[MENU_OPTIONS]=test[2];
        music_disabled=test[3];samples_disabled=test[4];
        tyrMusicVolume=100;fxVolume=80;
        printf("volume-mouse-case:%d,%d,%d,%d,%d\n",
            test[0],test[1],test[2],test[3],test[4]);
        adjust_item_screen_volume();
        printf("volume-mouse-state:%u,%u,%u,%u,%u\n",curSel[MENU_OPTIONS],
            tyrMusicVolume,fxVolume,music_disabled,samples_disabled);
    }
    const int input_dispatch_cases[][2]={{0,0},{0,1},{1,0},{-1,1},{-1,0}};
    for(unsigned int i=0;i<COUNTOF(input_dispatch_cases);i++) {
        mouseButton=input_dispatch_cases[i][0];newkey=input_dispatch_cases[i][1];
        int temporary_power[7]={0};
        printf("input-dispatch-case:%d,%d\n",mouseButton,newkey);
        handle_item_screen_input(temporary_power);
    }
    const uint8_t update_selections[]={2,3,4,5};
    const float initial_nav_x[]={0.5f,111.25f,-100.75f,239.0f};
    const float initial_nav_y[]={-1.5f,79.75f,99.25f,-200.0f};
    mapOrigin=13;mapPNum=4;mapPlanet[0]=1;mapPlanet[1]=13;mapPlanet[2]=17;mapPlanet[3]=21;
    menuChoices[MENU_PLAY_NEXT_LEVEL]=6;
    for(unsigned int c=0;c<COUNTOF(update_selections);c++) {
        for(int i=0;i<64000;i++)front[i]=(uint8_t)(i*23+c*5+9);
        navX=initial_nav_x[c];navY=initial_nav_y[c];
        newNavX=40.5f+c*13.25f;newNavY=150.75f-c*11.5f;
        curSel[MENU_PLAY_NEXT_LEVEL]=update_selections[c];
        planetAni=c*4;planetAniWait=c%2?2:0;currentDotNum=c;currentDotWait=c%2?0:3;
        JE_computeDots();
        printf("nav-update-case:%u,%u,%u,%u\n",c,curSel[MENU_PLAY_NEXT_LEVEL],planetAni,planetAniWait);
        JE_updateNavScreen();
        uint32_t nav_hash=2166136261u;
        for(int i=0;i<64000;i++)nav_hash=(nav_hash^front[i])*16777619u;
        printf("nav-update-state:%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%u,%u,%u\n",
            tempNavX,tempNavY,(int)(navX*1000.0f),(int)(navY*1000.0f),
            (int)(newNavX*1000.0f),(int)(newNavY*1000.0f),
            planetAni,planetAniWait,currentDotNum,currentDotWait,mapPNum,mapOrigin,nav_hash);
    }
    uint32_t random=0x7A31C5E9;
    for(unsigned int i=0;i<8192;i++) {
        random=random*1664525u+1013904223u;
        int16_t start=(int16_t)(random>>16),finish=(int16_t)random;
        uint8_t dots=(uint8_t)(random>>8),distance=(uint8_t)random;
        printf("part-way:%d,%d,%u,%u,%d\n",start,finish,dots,distance,
                JE_partWay(start,finish,dots,distance));
    }
    for(int dark=0;dark<2;dark++) {
        printf("background-grid-case:%d\n",dark);JE_drawLines(&surfaces[0],dark);
    }
    const int16_t view_values[]={INT16_MIN,-101,-1,0,1,101,INT16_MAX};
    for(unsigned int x=0;x<COUNTOF(view_values);x++)
        for(unsigned int y=0;y<COUNTOF(view_values);y++)
                for(int dark=0;dark<2;dark++) {
                    tempNavX=view_values[x];tempNavY=view_values[y];
                    printf("viewport-grid-case:%d,%d,%d\n",tempNavX,tempNavY,dark);
                    JE_drawNavLines(dark);
                }
    const int16_t planet_views[]={-100,-20,0,80,160,240,400};
    const int planet_frames[]={0,4,14};
    for(uint8_t planet=0;planet<21;planet++)
        for(unsigned int x=0;x<COUNTOF(planet_views);x++)
            for(unsigned int y=0;y<COUNTOF(planet_views);y++)
                for(unsigned int f=0;f<COUNTOF(planet_frames);f++) {
                    tempNavX=planet_views[x];tempNavY=planet_views[y];planetAni=planet_frames[f];
                    printf("planet-case:%u,%d,%d,%d\n",planet,tempNavX,tempNavY,planetAni);
                    JE_drawPlanet(planet);
                }
    const int scale_cases[][10]={{9,8,13,9,9,15,1,1,9,6},
        {13,10,14,12,12,17,2,2,13,9},{7,9,11,9,10,13,0,0,8,7}};
    uint8_t scale_source[2048],scale_destination[2048];
    for(unsigned int c=0;c<COUNTOF(scale_cases);c++) {
        const int *v=scale_cases[c];
        const int source_width=v[0],source_height=v[1],source_pitch=v[2];
        const int destination_width=v[3],destination_height=v[4],destination_pitch=v[5];
        const int x1=v[6],y1=v[7],x2=v[8],y2=v[9];
        for(int i=0;i<source_pitch*source_height;i++)scale_source[i]=(uint8_t)(i*37+c*19+11);
        for(int i=0;i<destination_pitch*destination_height;i++)scale_destination[i]=(uint8_t)(i*53+c*7+0xA5);
        SDL_Surface source={source_pitch,source_height,scale_source,source_width};
        SDL_Surface destination={destination_pitch,destination_height,scale_destination,destination_width};
        JE_scaleBitmap(&destination,&source,x1,y1,x2,y2);
        uint32_t hash=2166136261u;
        for(int i=0;i<destination_pitch*destination_height;i++)
            hash=(hash^scale_destination[i])*16777619u;
        printf("scale:%u,%u\n",c,hash);
    }
    for(uint8_t origin=1;origin<=21;origin++)
        for(unsigned int mode=0;mode<16;mode++) {
                mapOrigin=origin;mapPNum=5;
                for(uint8_t x=0;x<mapPNum;x++)mapPlanet[x]=(x*7+mode*3)%21+1;
                JE_computeDots();
                printf("nav-dots:%u,%u",origin,mode);
                for(uint8_t x=0;x<mapPNum;x++) {
                    printf(":%u",planetDots[x]);
                    for(uint8_t y=0;y<planetDots[x];y++)printf(":%d,%d",planetDotX[x][y],planetDotY[x][y]);
                }
                puts("");
                tempNavX=origin*15-150;tempNavY=mode*17-120;
                const uint8_t selected_values[]={1,2,4,7},current_values[]={0,1,5,10};
                for(unsigned int s=0;s<COUNTOF(selected_values);s++)
                    for(unsigned int d=0;d<COUNTOF(current_values);d++) {
                        curSel[MENU_PLAY_NEXT_LEVEL]=selected_values[s];currentDotNum=current_values[d];
                        printf("nav-dot-case:%u,%u,%d,%d,%u,%u\n",origin,mode,tempNavX,tempNavY,
                            curSel[MENU_PLAY_NEXT_LEVEL],currentDotNum);
                        JE_drawDots();
                    }
        }
    return 0;
}
"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(build_source(Path(__file__).resolve().parents[2]), encoding="utf-8")


if __name__ == "__main__":
    main()
