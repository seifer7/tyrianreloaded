# C# conversion progress

This inventory is the baseline of **126 files tracked before this setup**:
50 `.c` files, 50 `.h` files, and 26 other tracked files. It records **707
function definitions in C files** plus **32 inline function definitions in
headers**, for **739 baseline functions**. Function names are listed once per
source file; the two function-like initializer macros in `src/episodes.c`
(`ITEM_INT` and `ITEM_REF`) are not functions and are intentionally omitted.

**Initial status: 0/126 files dispositioned; 0/739 functions converted.**
No game code has been converted. The setup documents added for this effort
are not part of the baseline inventory below. Add every new C# source, test,
project, and build file to this inventory as it is introduced.

## How to update this tracker

- Check off each baseline file only when its C# counterpart is verified, it
  has been intentionally replaced, or it has a documented and verified
  unchanged disposition. Keep notes on non-obvious retained/replaced files in
  the relevant change or documentation.
- Check off each function only after its behavior has been implemented in C#
  and relevant tests or parity checks pass. Do not check off inspection or
  mechanical translation alone.
- Update summary counts and this inventory whenever files or functions are
  added, removed, split, merged, or renamed. For a split/merge, account for
  every original function and identify its destination.
- The binary assets, copyright/license text, and external game data may be
  retained rather than translated; they still need an explicit verified
  disposition in the file checklist.

## Baseline file inventory

### Repository root

- [ ] `.editorconfig`
- [ ] `.gitignore`
- [ ] `COPYING`
- [ ] `Doxyfile`
- [ ] `Makefile`
- [ ] `NEWS`
- [ ] `README`
- [ ] `lower-script.sh`

### Documentation and tools

- [ ] `doc/files.txt`
- [ ] `doc/tools/weapon.py`
- [ ] `doc/tyrian.hdt.txt`

### Linux integration and artwork

- [ ] `linux/icons/tyrian-128.png`
- [ ] `linux/icons/tyrian-22.png`
- [ ] `linux/icons/tyrian-24.png`
- [ ] `linux/icons/tyrian-32.png`
- [ ] `linux/icons/tyrian-48.png`
- [ ] `linux/man/opentyrian.6`
- [ ] `linux/opentyrian.desktop`

### Native source

- [ ] `src/animlib.c`
- [ ] `src/animlib.h`
- [ ] `src/arg_parse.c`
- [ ] `src/arg_parse.h`
- [ ] `src/backgrnd.c`
- [ ] `src/backgrnd.h`
- [ ] `src/config.c`
- [ ] `src/config.h`
- [ ] `src/config_file.c`
- [ ] `src/config_file.h`
- [ ] `src/data_extractor.c`
- [ ] `src/destruct.c`
- [ ] `src/destruct.h`
- [ ] `src/editship.c`
- [ ] `src/editship.h`
- [ ] `src/episodes.c`
- [ ] `src/episodes.h`
- [ ] `src/file.c`
- [ ] `src/file.h`
- [ ] `src/font.c`
- [ ] `src/font.h`
- [ ] `src/fonthand.c`
- [ ] `src/fonthand.h`
- [ ] `src/game_menu.c`
- [ ] `src/game_menu.h`
- [ ] `src/helptext.c`
- [ ] `src/helptext.h`
- [ ] `src/joystick.c`
- [ ] `src/joystick.h`
- [ ] `src/jukebox.c`
- [ ] `src/jukebox.h`
- [ ] `src/keyboard.c`
- [ ] `src/keyboard.h`
- [ ] `src/lds_play.c`
- [ ] `src/lds_play.h`
- [ ] `src/loudness.c`
- [ ] `src/loudness.h`
- [ ] `src/lvllib.c`
- [ ] `src/lvllib.h`
- [ ] `src/lvlmast.c`
- [ ] `src/lvlmast.h`
- [ ] `src/mainint.c`
- [ ] `src/mainint.h`
- [ ] `src/menus.c`
- [ ] `src/menus.h`
- [ ] `src/mouse.c`
- [ ] `src/mouse.h`
- [ ] `src/mtrand.c`
- [ ] `src/mtrand.h`
- [ ] `src/musmast.c`
- [ ] `src/musmast.h`
- [ ] `src/network.c`
- [ ] `src/network.h`
- [ ] `src/nortsong.c`
- [ ] `src/nortsong.h`
- [ ] `src/nortvars.c`
- [ ] `src/nortvars.h`
- [ ] `src/opentyr.c`
- [ ] `src/opentyr.h`
- [ ] `src/opentyrian_version.h`
- [ ] `src/opl.c`
- [ ] `src/opl.h`
- [ ] `src/palette.c`
- [ ] `src/palette.h`
- [ ] `src/params.c`
- [ ] `src/params.h`
- [ ] `src/pcxload.c`
- [ ] `src/pcxload.h`
- [ ] `src/pcxmast.c`
- [ ] `src/pcxmast.h`
- [ ] `src/picload.c`
- [ ] `src/picload.h`
- [ ] `src/player.c`
- [ ] `src/player.h`
- [ ] `src/shots.c`
- [ ] `src/shots.h`
- [ ] `src/sizebuf.c`
- [ ] `src/sizebuf.h`
- [ ] `src/sndmast.c`
- [ ] `src/sndmast.h`
- [ ] `src/sprite.c`
- [ ] `src/sprite.h`
- [ ] `src/starlib.c`
- [ ] `src/starlib.h`
- [ ] `src/tinydir.h`
- [ ] `src/tyrian2.c`
- [ ] `src/tyrian2.h`
- [ ] `src/varz.c`
- [ ] `src/varz.h`
- [ ] `src/vga256d.c`
- [ ] `src/vga256d.h`
- [ ] `src/vga_palette.c`
- [ ] `src/vga_palette.h`
- [ ] `src/video.c`
- [ ] `src/video.h`
- [ ] `src/video_scale.c`
- [ ] `src/video_scale.h`
- [ ] `src/video_scale_hqNx.c`
- [ ] `src/xmas.c`
- [ ] `src/xmas.h`

### Visual Studio project and resources

- [ ] `visualc/data_extractor.vcxproj`
- [ ] `visualc/opentyrian.props.template`
- [ ] `visualc/opentyrian.sln`
- [ ] `visualc/opentyrian.vcxproj`
- [ ] `visualc/resources.rc`
- [ ] `visualc/saves/.gitkeep`
- [ ] `visualc/sdl_paths.props.template`
- [ ] `visualc/tyrian.ico`

## Function inventory

### `src/animlib.c`

- [ ] `JE_loadPage`
- [ ] `JE_findPage`
- [ ] `JE_renderFrame`
- [ ] `JE_playAnim`
- [ ] `JE_loadAnim`
- [ ] `JE_closeAnim`
- [ ] `JE_playRunSkipDump`

### `src/arg_parse.c`

- [ ] `parse_args`
- [ ] `permute`
- [ ] `parse_short_opt`
- [ ] `parse_long_opt`
- [ ] `ot_strchrnul`

### `src/backgrnd.c`

- [ ] `JE_darkenBackground`
- [ ] `blit_background_row`
- [ ] `blit_background_row_blend`
- [ ] `draw_background_1`
- [ ] `draw_background_2`
- [ ] `draw_background_2_blend`
- [ ] `draw_background_3`
- [ ] `JE_filterScreen`
- [ ] `JE_checkSmoothies`
- [ ] `lava_filter`
- [ ] `water_filter`
- [ ] `iced_blur_filter`
- [ ] `blur_filter`
- [ ] `initialize_starfield`
- [ ] `update_and_draw_starfield`

### `src/config.c`

- [ ] `xor`
- [ ] `load_opentyrian_config`
- [ ] `save_opentyrian_config`
- [ ] `playeritems_to_pitems`
- [ ] `pitems_to_playeritems`
- [ ] `saveGameOperation`
- [ ] `loadGameOperation`
- [ ] `JE_saveGame`
- [ ] `read_savegame_metadata`
- [ ] `build_save_game_list`
- [ ] `JE_loadGame`
- [ ] `JE_initProcessorType`
- [ ] `JE_setNewGameSpeed`
- [ ] `JE_encryptSaveTemp`
- [ ] `JE_decryptSaveTemp`
- [ ] `get_user_directory`
- [ ] `JE_loadConfiguration`
- [ ] `JE_saveConfiguration`

### `src/config_file.c`

- [ ] `config_oom`
- [ ] `string_init_len`
- [ ] `string_deinit`
- [ ] `string_equal_len`
- [ ] `config_init`
- [ ] `config_deinit`
- [ ] `init_section`
- [ ] `deinit_section`
- [ ] `config_add_section_len`
- [ ] `config_find_sections`
- [ ] `config_find_section`
- [ ] `config_find_or_add_section`
- [ ] `init_option_value`
- [ ] `deinit_option_value`
- [ ] `init_option`
- [ ] `deinit_option`
- [ ] `append_option`
- [ ] `get_option_len`
- [ ] `config_set_option_len`
- [ ] `config_get_option`
- [ ] `config_get_or_set_option_len`
- [ ] `config_set_string_option_len`
- [ ] `config_get_string_option`
- [ ] `config_get_or_set_string_option`
- [ ] `config_set_bool_option`
- [ ] `config_get_bool_option`
- [ ] `config_get_or_set_bool_option`
- [ ] `config_set_int_option`
- [ ] `config_get_int_option`
- [ ] `config_get_or_set_int_option`
- [ ] `config_set_uint_option`
- [ ] `config_get_uint_option`
- [ ] `config_get_or_set_uint_option`
- [ ] `config_set_value_len`
- [ ] `config_add_value_len`
- [ ] `config_remove_value`
- [ ] `config_get_value`
- [ ] `is_whitespace`
- [ ] `is_end`
- [ ] `is_whitespace_or_end`
- [ ] `match_directive`
- [ ] `match_nonquote_field`
- [ ] `parse_quote_field`
- [ ] `parse_field`
- [ ] `config_parse`
- [ ] `write_field`
- [ ] `config_write`

### `src/data_extractor.c`

- [ ] `print_usage`
- [ ] `main`

### `src/destruct.c`

- [ ] `get_unit_by_name`
- [ ] `load_destruct_config`
- [ ] `JE_destructGame`
- [ ] `JE_destructMain`
- [ ] `JE_introScreen`
- [ ] `DrawModeSelectMenu`
- [ ] `JE_modeSelect`
- [ ] `JE_generateTerrain`
- [ ] `DE_generateBaseTerrain`
- [ ] `DE_drawBaseTerrain`
- [ ] `DE_generateUnits`
- [ ] `DE_generateWalls`
- [ ] `DE_generateRings`
- [ ] `aliasDirtPixel`
- [ ] `JE_aliasDirt`
- [ ] `JE_placementPosition`
- [ ] `JE_stabilityCheck`
- [ ] `JE_tempScreenChecking`
- [ ] `JE_makeExplosion`
- [ ] `JE_eSound`
- [ ] `JE_superPixel`
- [ ] `JE_helpScreen`
- [ ] `JE_pauseScreen`
- [ ] `DE_ResetUnits`
- [ ] `DE_ResetPlayers`
- [ ] `DE_ResetWeapons`
- [ ] `DE_ResetLevel`
- [ ] `DE_ResetAI`
- [ ] `DE_ResetActions`
- [ ] `DE_RunTick`
- [ ] `DE_RunTickCycleDeadUnits`
- [ ] `DE_RunTickGravity`
- [ ] `DE_GravityDrawUnit`
- [ ] `DE_GravityLowerUnit`
- [ ] `DE_GravityFlyUnit`
- [ ] `DE_RunTickAnimate`
- [ ] `DE_RunTickDrawWalls`
- [ ] `DE_RunTickExplosions`
- [ ] `DE_TestExplosionCollision`
- [ ] `DE_DestroyUnit`
- [ ] `DE_RunTickShots`
- [ ] `DE_DrawTrails`
- [ ] `DE_RunTickAI`
- [ ] `DE_RunTickDrawCrosshairs`
- [ ] `DE_RunTickDrawHUD`
- [ ] `DE_RunTickGetInput`
- [ ] `DE_ProcessInput`
- [ ] `DE_CycleWeaponUp`
- [ ] `DE_CycleWeaponDown`
- [ ] `DE_MakeShot`
- [ ] `DE_RunMagnet`
- [ ] `DE_RaiseAngle`
- [ ] `DE_LowerAngle`
- [ ] `DE_RaisePower`
- [ ] `DE_LowerPower`
- [ ] `DE_isValidUnit`
- [ ] `DE_RunTickCheckEndgame`
- [ ] `DE_RunTickPlaySounds`
- [ ] `JE_pixCool`

### `src/editship.c`

- [ ] `JE_decryptShips`
- [ ] `JE_loadExtraShapes`

### `src/episodes.c`

- [ ] `item_data_error`
- [ ] `value_in_array_je_byte`
- [ ] `add_random_shop_item`
- [ ] `item_idx_from_jebyte`
- [ ] `item_idx_from_int`
- [ ] `item_idx`
- [ ] `get_item_index_from_int`
- [ ] `get_item_index`
- [ ] `apply_item_field`
- [ ] `prepare_extracted_data_directories`
- [ ] `extract_item_data`
- [ ] `close_extracted_file`
- [ ] `open_extracted_category`
- [ ] `item_data_realloc`
- [ ] `is_item_space`
- [ ] `trim_item_text`
- [ ] `valid_item_id_span`
- [ ] `valid_item_id`
- [ ] `item_type_from_name`
- [ ] `hash_item_id`
- [ ] `find_item_slot`
- [ ] `rebuild_item_slots`
- [ ] `find_loading_item_index`
- [ ] `replace_item_record`
- [ ] `validate_item_field`
- [ ] `read_item_bundle_text`
- [ ] `load_item_bundle`
- [ ] `compare_item_paths`
- [ ] `load_additional_item_bundles`
- [ ] `load_items`
- [ ] `read_legacy_item_data`
- [ ] `JE_initEpisode`
- [ ] `JE_scanForEpisodes`
- [ ] `JE_findNextEpisode`

### `src/file.c`

- [ ] `data_dir`
- [ ] `dir_fopen`
- [ ] `dir_fopen_warn`
- [ ] `dir_fopen_die`
- [ ] `dir_file_exists`
- [ ] `ftell_eof`
- [ ] `fread_die`
- [ ] `fwrite_die`
- [ ] `write_pcx_header`

### `src/font.c`

- [ ] `draw_font_hv_shadow`
- [ ] `draw_font_hv_full_shadow`
- [ ] `draw_font_hv`
- [ ] `draw_font_hv_blend`
- [ ] `draw_font_dark`

### `src/fonthand.c`

- [ ] `JE_dString`
- [ ] `JE_fontCenter`
- [ ] `JE_textWidth`
- [ ] `JE_textShade`
- [ ] `JE_outText`
- [ ] `JE_outTextModify`
- [ ] `JE_outTextAdjust`
- [ ] `JE_outTextAndDarken`
- [ ] `JE_updateWarning`
- [ ] `JE_outTextGlow`

### `src/game_menu.c`

- [ ] `playeritem_map`
- [ ] `JE_cashLeft`
- [ ] `draw_item_screen_load_save`
- [ ] `draw_item_screen_keyboard_config`
- [ ] `draw_item_screen_joystick_config`
- [ ] `draw_item_screen_upgrade_menu`
- [ ] `draw_item_screen_status`
- [ ] `draw_item_screen_data_cubes`
- [ ] `draw_item_screen_footer`
- [ ] `prepare_item_screen_frame`
- [ ] `draw_item_screen`
- [ ] `move_item_screen_selection`
- [ ] `adjust_joystick_config_setting`
- [ ] `change_two_player_input_device`
- [ ] `change_upgrade_weapon_power`
- [ ] `handle_item_screen_horizontal_key`
- [ ] `activate_item_screen_selection`
- [ ] `cancel_item_screen_selection`
- [ ] `show_item_screen_help`
- [ ] `handle_item_screen_key`
- [ ] `update_item_screen_frame`
- [ ] `initialize_item_screen`
- [ ] `open_quick_save_menu`
- [ ] `wait_for_item_screen_input`
- [ ] `adjust_item_screen_volume`
- [ ] `get_item_screen_mouse_selection`
- [ ] `select_item_screen_menu_option`
- [ ] `adjust_item_screen_weapon_power`
- [ ] `handle_item_screen_mouse_input`
- [ ] `handle_item_screen_input`
- [ ] `JE_itemScreen`
- [ ] `draw_ship_illustration`
- [ ] `load_cubes`
- [ ] `load_cube`
- [ ] `JE_drawItem`
- [ ] `JE_drawMenuHeader`
- [ ] `get_menu_choice_y`
- [ ] `draw_menu_choice`
- [ ] `JE_drawMenuChoices`
- [ ] `JE_updateNavScreen`
- [ ] `JE_drawLines`
- [ ] `JE_drawNavLines`
- [ ] `JE_drawDots`
- [ ] `JE_drawPlanet`
- [ ] `JE_scaleBitmap`
- [ ] `JE_initWeaponView`
- [ ] `JE_computeDots`
- [ ] `JE_partWay`
- [ ] `JE_doShipSpecs`
- [ ] `JE_drawMainMenuHelpText`
- [ ] `JE_quitRequest`
- [ ] `JE_genItemMenu`
- [ ] `JE_scaleInPicture`
- [ ] `JE_drawScore`
- [ ] `setup_next_level_menu`
- [ ] `start_current_level`
- [ ] `request_menu_quit`
- [ ] `return_to_options_menu`
- [ ] `rebind_keyboard_key`
- [ ] `handle_keyboard_config_selection`
- [ ] `assign_joystick_control`
- [ ] `handle_joystick_config_selection`
- [ ] `handle_full_game_selection`
- [ ] `handle_two_player_menu_selection`
- [ ] `handle_one_player_menu_selection`
- [ ] `handle_limited_options_selection`
- [ ] `handle_super_tyrian_menu_selection`
- [ ] `handle_upgrade_menu_selection`
- [ ] `handle_options_menu_selection`
- [ ] `handle_next_level_menu_selection`
- [ ] `handle_upgrade_submenu_selection`
- [ ] `handle_load_save_menu_selection`
- [ ] `handle_data_cube_menu_selection`
- [ ] `JE_menuFunction`
- [ ] `JE_drawShipSpecs`
- [ ] `JE_weaponSimUpdate`
- [ ] `JE_weaponViewFrame`

### `src/helptext.c`

- [ ] `decrypt_string`
- [ ] `read_encrypted_pascal_string`
- [ ] `skip_pascal_string`
- [ ] `JE_helpBox`
- [ ] `JE_HBox`
- [ ] `JE_loadHelpText`

### `src/joystick.c`

- [ ] `joystick_axis_threshold`
- [ ] `joystick_axis_reduce`
- [ ] `joystick_analog_angle`
- [ ] `check_assigned`
- [ ] `poll_joystick`
- [ ] `poll_joysticks`
- [ ] `push_key`
- [ ] `push_joysticks_as_keyboard`
- [ ] `init_joysticks`
- [ ] `deinit_joysticks`
- [ ] `reset_joystick_assignments`
- [ ] `load_joystick_assignments`
- [ ] `save_joystick_assignments`
- [ ] `joystick_assignments_to_string`
- [ ] `code_to_assignment`
- [ ] `assignment_to_code`
- [ ] `detect_joystick_assignment`
- [ ] `joystick_assignment_cmp`

### `src/jukebox.c`

- [ ] `jukebox`

### `src/keyboard.c`

- [ ] `flush_events_buffer`
- [ ] `wait_input`
- [ ] `wait_noinput`
- [ ] `init_keyboard`
- [ ] `mouseSetRelative`
- [ ] `JE_mousePosition`
- [ ] `mouseGetRelativePosition`
- [ ] `service_SDL_events`
- [ ] `JE_clearKeyboard`

### `src/lds_play.c`

- [ ] `lds_load`
- [ ] `lds_free`
- [ ] `lds_rewind`
- [ ] `lds_fade`
- [ ] `lds_setregs`
- [ ] `lds_setregs_adv`
- [ ] `lds_update`
- [ ] `lds_playsound`

### `src/loudness.c`

- [ ] `init_audio`
- [ ] `audioCallback`
- [ ] `deinit_audio`
- [ ] `load_music`
- [ ] `load_song`
- [ ] `play_song`
- [ ] `restart_song`
- [ ] `stop_song`
- [ ] `fade_song`
- [ ] `set_volume`
- [ ] `multiSamplePlay`

### `src/lvllib.c`

- [ ] `JE_analyzeLevel`

### `src/lvlmast.c`

- No function definitions; tracked by the file inventory.

### `src/mainint.c`

- [ ] `JE_drawTextWindow`
- [ ] `JE_outCharGlow`
- [ ] `JE_drawPortConfigButtons`
- [ ] `JE_helpSystem`
- [ ] `saveGameNamePopup`
- [ ] `helpSystemPage`
- [ ] `weapon_upgrade_cost`
- [ ] `JE_getCost`
- [ ] `saved_game_count`
- [ ] `draw_load_screen`
- [ ] `wait_for_load_screen_input`
- [ ] `read_load_screen_input`
- [ ] `handle_load_screen_action`
- [ ] `JE_loadScreen`
- [ ] `JE_totalScore`
- [ ] `JE_getValue`
- [ ] `JE_nextEpisode`
- [ ] `JE_initPlayerData`
- [ ] `JE_sortHighScores`
- [ ] `JE_highScoreScreen`
- [ ] `JE_gammaCorrect_func`
- [ ] `JE_gammaCorrect`
- [ ] `JE_gammaCheck`
- [ ] `JE_doInGameSetup`
- [ ] `draw_ingame_setup_menu`
- [ ] `wait_for_ingame_setup_input`
- [ ] `read_ingame_setup_mouse`
- [ ] `read_ingame_setup_keyboard`
- [ ] `adjust_ingame_setup_setting`
- [ ] `apply_ingame_setup_action`
- [ ] `JE_inGameSetup`
- [ ] `JE_inGameHelp`
- [ ] `JE_highScoreCheck`
- [ ] `adjust_difficulty`
- [ ] `load_next_demo`
- [ ] `replay_demo_keys`
- [ ] `JE_SFCodes`
- [ ] `JE_sort`
- [ ] `JE_playCredits`
- [ ] `JE_endLevelAni`
- [ ] `JE_drawCube`
- [ ] `JE_handleChat`
- [ ] `JE_inGameDisplays`
- [ ] `JE_mainKeyboardInput`
- [ ] `JE_pauseGame`
- [ ] `update_player_death`
- [ ] `collect_player_movement_input`
- [ ] `update_player_linking`
- [ ] `draw_player_level_end`
- [ ] `update_player_movement`
- [ ] `change_player_weapon_mode`
- [ ] `fire_main_weapons`
- [ ] `update_super_charge_weapon`
- [ ] `fire_superbomb`
- [ ] `update_sidekick_positions`
- [ ] `fire_sidekick_weapons`
- [ ] `update_player_attacks`
- [ ] `draw_player_sidekicks`
- [ ] `draw_player_ship`
- [ ] `JE_playerMovement`
- [ ] `JE_mainGamePlayerFunctions`
- [ ] `JE_getName`
- [ ] `collect_arcade_pickup`
- [ ] `collect_armor_pickup`
- [ ] `collect_bonus_level_pickup`
- [ ] `collect_score_item`
- [ ] `destroy_linked_enemies`
- [ ] `damage_enemy_on_collision`
- [ ] `JE_playerCollide`

### `src/menus.c`

- [ ] `gameplaySelect`
- [ ] `episodeSelect`
- [ ] `difficultySelect`

### `src/mouse.c`

- [ ] `JE_drawShapeTypeOne`
- [ ] `JE_grabShapeTypeOne`
- [ ] `JE_mouseStart`
- [ ] `JE_mouseStartFilter`
- [ ] `JE_mouseReplace`

### `src/mtrand.c`

- [ ] `mt_srand`
- [ ] `mt_rand`
- [ ] `mt_rand_1`
- [ ] `mt_rand_lt1`

### `src/musmast.c`

- No function definitions; tracked by the file inventory.

### `src/network.c`

- [ ] `packet_copy`
- [ ] `packets_shift_up`
- [ ] `packets_shift_down`
- [ ] `network_prepare`
- [ ] `network_send_no_ack`
- [ ] `network_send`
- [ ] `network_acknowledge`
- [ ] `network_is_alive`
- [ ] `network_check`
- [ ] `network_update`
- [ ] `network_is_sync`
- [ ] `network_state_prepare`
- [ ] `network_state_send`
- [ ] `network_state_update`
- [ ] `network_state_is_reset`
- [ ] `network_state_reset`
- [ ] `network_connect`
- [ ] `network_tyrian_halt`
- [ ] `network_init`
- [ ] `JE_clearSpecialRequests`

### `src/nortsong.c`

- [ ] `setDelay`
- [ ] `setDelay2`
- [ ] `getDelayTicks`
- [ ] `getDelayTicks2`
- [ ] `wait_delay`
- [ ] `service_wait_delay`
- [ ] `wait_delayorinput`
- [ ] `loadSndFile`
- [ ] `JE_playSampleNum`
- [ ] `setDelaySpeed`
- [ ] `JE_changeVolume`

### `src/nortvars.c`

- [ ] `JE_anyButton`
- [ ] `JE_dBar3`
- [ ] `JE_barDrawShadow`
- [ ] `JE_wipeKey`

### `src/opentyr.c`

- [ ] `str_pop_int`
- [ ] `str_pop_str`
- [ ] `getShopContentsPickerItemsCount`
- [ ] `getShopContentsPickerItem`
- [ ] `getDisplayPickerItemsCount`
- [ ] `getDisplayPickerItem`
- [ ] `getScalerPickerItemsCount`
- [ ] `getScalerPickerItem`
- [ ] `getScalingModePickerItemsCount`
- [ ] `getScalingModePickerItem`
- [ ] `draw_setup_menu`
- [ ] `wait_for_setup_input`
- [ ] `collect_setup_menu_input`
- [ ] `collect_setup_picker_input`
- [ ] `apply_setup_picker_action`
- [ ] `apply_setup_menu_activation`
- [ ] `apply_setup_menu_action`
- [ ] `setupMenu`
- [ ] `main`

### `src/opl.c`

- [ ] `operator_advance`
- [ ] `operator_advance_drums`
- [ ] `operator_output`
- [ ] `operator_off`
- [ ] `operator_sustain`
- [ ] `operator_release`
- [ ] `operator_decay`
- [ ] `operator_attack`
- [ ] `change_attackrate`

### `src/palette.c`

- [ ] `JE_loadPals`
- [ ] `set_palette`
- [ ] `set_colors`
- [ ] `init_step_fade_palette`
- [ ] `init_step_fade_solid`
- [ ] `step_fade_palette`
- [ ] `fade_palette`
- [ ] `fade_solid`
- [ ] `fade_black`
- [ ] `fade_white`
- [ ] `rgb_to_yuv`

### `src/params.c`

- [ ] `JE_paramCheck`

### `src/pcxload.c`

- [ ] `JE_loadPCX`

### `src/pcxmast.c`

- No function definitions; tracked by the file inventory.

### `src/picload.c`

- [ ] `JE_loadPic`

### `src/player.c`

- [ ] `calc_purple_balls_needed`
- [ ] `power_up_weapon`
- [ ] `handle_got_purple_ball`

### `src/shots.c`

- [ ] `simulate_player_shots`
- [ ] `player_shot_set_direction`
- [ ] `player_shot_move_and_draw`
- [ ] `player_shot_create`

### `src/sizebuf.c`

- [ ] `SZ_Init`
- [ ] `SZ_Error`
- [ ] `SZ_Memset`
- [ ] `SZ_Memcpy2`
- [ ] `SZ_Seek`
- [ ] `MSG_ReadByte`
- [ ] `MSG_ReadWord`

### `src/sndmast.c`

- No function definitions; tracked by the file inventory.

### `src/sprite.c`

- [ ] `load_sprites_file`
- [ ] `load_sprites`
- [ ] `free_sprites`
- [ ] `blit_sprite`
- [ ] `blit_sprite_blend`
- [ ] `blit_sprite_hv_unsafe`
- [ ] `blit_sprite_hv`
- [ ] `blit_sprite_hv_blend`
- [ ] `blit_sprite_dark`
- [ ] `JE_loadCompShapes`
- [ ] `JE_loadCompShapesB`
- [ ] `free_sprite2s`
- [ ] `blit_sprite2`
- [ ] `blit_sprite2_clip`
- [ ] `blit_sprite2_blend`
- [ ] `blit_sprite2_darken`
- [ ] `blit_sprite2_filter`
- [ ] `blit_sprite2_filter_clip`
- [ ] `blit_sprite2x2`
- [ ] `blit_sprite2x2_clip`
- [ ] `blit_sprite2x2_blend`
- [ ] `blit_sprite2x2_darken`
- [ ] `blit_sprite2x2_filter`
- [ ] `blit_sprite2x2_filter_clip`
- [ ] `JE_loadMainShapeTables`
- [ ] `free_main_shape_tables`

### `src/starlib.c`

- [ ] `JE_starlib_main`
- [ ] `JE_wackyCol`
- [ ] `JE_starlib_init`
- [ ] `JE_resetValues`
- [ ] `JE_changeSetup`
- [ ] `JE_newStar`

### `src/tyrian2.c`

- [ ] `JE_starShowVGA`
- [ ] `blit_enemy`
- [ ] `update_enemy_acceleration`
- [ ] `animate_and_draw_enemy`
- [ ] `update_enemy_oscillation`
- [ ] `update_enemy_position`
- [ ] `calculate_enemy_aim_velocity`
- [ ] `aim_enemy_shot`
- [ ] `create_enemy_shot`
- [ ] `apply_enemy_weapon_special`
- [ ] `fire_enemy_shots`
- [ ] `process_enemy_attacks`
- [ ] `launch_enemy_from_parent`
- [ ] `JE_drawEnemy`
- [ ] `prepare_next_level`
- [ ] `draw_level_hud`
- [ ] `handle_level_input`
- [ ] `draw_level_backgrounds`
- [ ] `update_level_explosions`
- [ ] `draw_armor_warning`
- [ ] `play_level_sounds`
- [ ] `player_shot_hits_enemy`
- [ ] `mark_player_shot_linked_enemies`
- [ ] `animate_player_shot_damaged_enemies`
- [ ] `destroy_player_shot_linked_enemies`
- [ ] `process_player_shot`
- [ ] `draw_enemy_shots`
- [ ] `draw_level_debug_and_timer`
- [ ] `update_level_network_state`
- [ ] `update_level_end_state`
- [ ] `record_level_demo`
- [ ] `run_level_frame`
- [ ] `initialize_level_players_and_display`
- [ ] `initialize_level_map_and_ship_state`
- [ ] `initialize_level_flags`
- [ ] `initialize_level_status_display`
- [ ] `initialize_level_combat_state`
- [ ] `JE_main`
- [ ] `read_level_header_and_events`
- [ ] `load_level_shapes`
- [ ] `load_level_map_layer`
- [ ] `play_map_transition`
- [ ] `show_episode_completion`
- [ ] `show_episode_picture`
- [ ] `show_episode_warning`
- [ ] `read_episode_item_availability`
- [ ] `load_selected_level_map`
- [ ] `configure_episode_mode`
- [ ] `handle_episode_level_jump`
- [ ] `handle_episode_presentation`
- [ ] `JE_loadMap`
- [ ] `networkStartScreen`
- [ ] `draw_title_screen`
- [ ] `wait_for_title_screen_input`
- [ ] `read_title_screen_input`
- [ ] `dispatch_title_screen_action`
- [ ] `handle_title_screen_secret_codes`
- [ ] `titleScreen`
- [ ] `newGame`
- [ ] `newSuperArcadeGame`
- [ ] `newSuperTyrianGame`
- [ ] `intro_logos`
- [ ] `JE_readTextSync`
- [ ] `JE_displayText`
- [ ] `scale_enemy_score`
- [ ] `scale_enemy_armor`
- [ ] `initialize_enemy_score_and_armor`
- [ ] `find_enemy_sprite_sheet`
- [ ] `JE_newEnemyOld`
- [ ] `JE_newEnemy`
- [ ] `JE_makeEnemy`
- [ ] `JE_createNewEventEnemy`
- [ ] `JE_eventJump`
- [ ] `JE_searchFor`
- [ ] `resolve_event_enemy_link`
- [ ] `create_event_enemy_at_y`
- [ ] `apply_event_random_special_drop`
- [ ] `set_event_enemy_death`
- [ ] `handle_environment_event`
- [ ] `handle_enemy_global_move_event`
- [ ] `handle_enemy_global_accel_event`
- [ ] `handle_enemy_global_animation_event`
- [ ] `handle_enemy_global_damage_event`
- [ ] `handle_enemy_acceleration_reverse_event`
- [ ] `handle_enemy_fire_override_event`
- [ ] `handle_enemy_death_event`
- [ ] `handle_enemy_event`
- [ ] `handle_level_event`
- [ ] `handle_misc_event`
- [ ] `JE_eventSystem`
- [ ] `JE_whoa`
- [ ] `JE_barX`
- [ ] `draw_boss_bar`

### `src/varz.c`

- [ ] `JE_getShipInfo`
- [ ] `JE_SGr`
- [ ] `JE_drawOptions`
- [ ] `JE_drawOptionLevel`
- [ ] `JE_tyrianHalt`
- [ ] `JE_specialComplete`
- [ ] `JE_doSpecialShot`
- [ ] `JE_setupExplosion`
- [ ] `JE_setupExplosionLarge`
- [ ] `JE_wipeShieldArmorBars`
- [ ] `JE_playerDamage`
- [ ] `JE_portConfigs`
- [ ] `JE_drawShield`
- [ ] `JE_drawArmor`
- [ ] `JE_doSP`
- [ ] `JE_drawSP`

### `src/vga256d.c`

- [ ] `JE_pix`
- [ ] `JE_pix3`
- [ ] `JE_rectangle`
- [ ] `fill_rectangle_xy`
- [ ] `JE_barShade`
- [ ] `JE_barBright`
- [ ] `draw_segmented_gauge`

### `src/vga_palette.c`

- No function definitions; tracked by the file inventory.

### `src/video.c`

- [ ] `init_video`
- [ ] `deinit_video`
- [ ] `init_renderer`
- [ ] `deinit_renderer`
- [ ] `init_texture`
- [ ] `deinit_texture`
- [ ] `window_get_display_index`
- [ ] `window_center_in_display`
- [ ] `reinit_fullscreen`
- [ ] `video_on_win_resize`
- [ ] `toggle_fullscreen`
- [ ] `init_scaler`
- [ ] `set_scaling_mode_by_name`
- [ ] `JE_clr256`
- [ ] `JE_showVGA`
- [ ] `calc_dst_render_rect`
- [ ] `scale_and_flip`
- [ ] `mapScreenPointToWindow`
- [ ] `mapWindowPointToScreen`
- [ ] `scaleWindowDistanceToScreen`

### `src/video_scale.c`

- [ ] `set_scaler_by_name`
- [ ] `nn_32`
- [ ] `nn_16`
- [ ] `scale2x_32`
- [ ] `scale2x_16`
- [ ] `scale3x_32`
- [ ] `scale3x_16`

### `src/video_scale_hqNx.c`

- [ ] `interp1`
- [ ] `interp2`
- [ ] `interp3`
- [ ] `interp4`
- [ ] `interp5`
- [ ] `interp6`
- [ ] `interp7`
- [ ] `interp8`
- [ ] `interp9`
- [ ] `interp10`
- [ ] `diff`
- [ ] `hq2x_32`
- [ ] `hq3x_32`
- [ ] `hq4x_32`

### `src/xmas.c`

- [ ] `xmas_time`
- [ ] `xmas_prompt`

### Inline function definitions in headers

#### `src/config_file.h`

- [ ] `config_string_to_cstr`
- [ ] `config_add_section`
- [ ] `config_set_option`
- [ ] `config_get_or_set_option`
- [ ] `config_set_string_option`
- [ ] `config_set_value`
- [ ] `config_add_value`
- [ ] `config_is_value_list`
- [ ] `config_get_value_count`

#### `src/file.h`

- [ ] `fread_bool_die`
- [ ] `fread_u8`
- [ ] `fread_u8_die`
- [ ] `fread_s8_die`
- [ ] `fread_u16_die`
- [ ] `fread_s16_die`
- [ ] `fread_u32_die`
- [ ] `fread_s32_die`
- [ ] `fwrite_bool_die`
- [ ] `fwrite_u8`
- [ ] `fwrite_u8_die`
- [ ] `fwrite_s8_die`
- [ ] `fwrite_u16_die`
- [ ] `fwrite_s16_die`
- [ ] `fwrite_u32_die`
- [ ] `fwrite_s32_die`

#### `src/player.h`

- [ ] `all_players_dead`
- [ ] `all_players_alive`

#### `src/sprite.h`

- [ ] `sprite`
- [ ] `sprite_exists`
- [ ] `get_sprite_width`
- [ ] `get_sprite_height`

#### `src/vga256d.h`

- [ ] `fill_rectangle_wh`
