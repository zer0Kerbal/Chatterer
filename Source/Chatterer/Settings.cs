///////////////////////////////////////////////////////////////////////////////
//
//    Chatterer a plugin for Kerbal Space Program from SQUAD
//    (https://www.kerbalspaceprogram.com/)
//    Copyright (C) 2020 LisiasT 
//    Copyright (C) 2014 Athlonic 
//    Copyright (C) 2013 Iannic-ann-od
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
///////////////////////////////////////////////////////////////////////////////

using System;
using UnityEngine;

using Asset = KSPe.IO.Asset<Chatterer.Startup>;
using Data =  KSPe.IO.Data<Chatterer.Startup>;

namespace Chatterer
{
    public partial class chatterer
    {
        // Lots and lots of extra debugging stuff
        internal bool debugging = KSPe.Globals<Startup>.DebugMode;

        //Plugin settings
        //private bool power_available = true;
        private bool quindar_toggle = true;
        private bool disable_beeps_during_chatter = false;
        //private bool remotetech_toggle = false;
        private bool sstv_on_science_toggle = true;
        //private bool disable_power_usage = false;
        private bool show_tooltips = true;
        private bool http_update_check = false;
        private bool use_vessel_settings = false;
        private bool prev_use_vessel_settings = false;
        private string menu = "chatter";    //default to chatter menu because it has to have something

        //AAE settings
        private bool aae_backgrounds_onlyinIVA = false;

        //Sliders
        private float chatter_freq_slider = 3f;
        private int chatter_freq = 3;
        private int prev_chatter_freq = 3;
        private float chatter_vol_slider = 0.5f;
        private float prev_chatter_vol_slider = 0.5f;

        private float quindar_vol_slider = 0.5f;
        private float prev_quindar_vol_slider = 0.5f;

        private float sstv_freq_slider = 0;
        private int sstv_freq = 0;
        private int prev_sstv_freq = 0;
        private float sstv_vol_slider = 0.15f;
        private float prev_sstv_vol_slider = 0.15f;

        //Insta-chatter key
        private KeyCode insta_chatter_key = KeyCode.None;
        private bool set_insta_chatter_key = false;
        private bool insta_chatter_key_just_changed = false;

        //Insta-SSTV key
        private KeyCode insta_sstv_key = KeyCode.None;
        private bool set_insta_sstv_key = false;
        private bool insta_sstv_key_just_changed = false;

        private bool mute_all = false;
        private bool all_muted = false;

        private bool show_advanced_options = false;
        private bool show_chatter_sets = false;

        //Clipboards
        private ConfigNode beepsource_clipboard;

        //Settings nodes
        
        private readonly Data.ConfigNode plugin_settings = Data.ConfigNode.For("SETTINGS", "settings.cfg");
        private readonly Data.ConfigNode vessel_settings = Data.ConfigNode.For("FLIGHTS", "vessel_settings.cfg");

        //Save/Load settings
        private void save_plugin_settings()
        {
            //these values are not saved to vessel.cfg ever and are considered global
            //Log.dbg("adding plugin settings to ConfigNode for write");
            ConfigNode plugin_settings_node = plugin_settings.Node;

            save_settings(plugin_settings_node);

            //also save values that are shared between the two configs
            save_shared_settings(plugin_settings_node);

            plugin_settings.Save();

            Log.dbg("plugin settings saved to disk");

            //update vessel_settings.cfg
            if (use_vessel_settings)
            {
                write_vessel_settings();
                Log.dbg("this vessel settings saved to disk");
            }
        }

        private void load_plugin_settings()
        {
            Log.dbg("load_plugin_settings() START");

            destroy_all_beep_players();
            chatter_array.Clear();
            beepsource_list.Clear();

            // Force the creation of the file if no existent
            if (plugin_settings.IsLoadable)
                plugin_settings.Load();
            else
            {
                Log.dbg("plugin_settings does not exists. Creating one from defaults.");
                plugin_settings.Clear();
                load_plugin_defaults();
                save_plugin_settings();
            }

            Log.dbg("Load settings specific to plugin_settings.cfg");
            load_settings(plugin_settings.Node);
            load_shared_settings(plugin_settings.Node); //load settings shared between both configs

            //if (chatter_exists && chatter_array.Count == 0)
            if (chatter_array.Count == 0)
            {
                Log.dbg("No audiosets found in config, adding defaults");
                add_default_audiosets();
                load_chatter_audio();   //load audio in case there is none
            }

            if (beeps_exists && beepsource_list.Count == 0)
            {
                Log.warn("beepsource_list.Count == 0, adding default 3");
                add_default_beepsources();
            }

            if (aae_backgrounds_exist && backgroundsource_list.Count == 0)
            {
                Log.warn("backgroundsource_list.Count == 0, adding default 2");
                add_default_backgroundsources();
            }

            Log.dbg("load_plugin_settings() END");
        }

        private void load_plugin_defaults()
        {
            Log.dbg("load_plugin_defaults() START");

            destroy_all_beep_players();
            chatter_array.Clear();
            beepsource_list.Clear();

            Asset.ConfigNode defaults = Asset.ConfigNode.For("DEFAULT_SETTINGS", "plugin_defaults.cfg");
            if (defaults.IsLoadable)
                defaults.Load();
            else
            {
                Log.error("plugin_defaults.cfg missing or unreadable!");
                return;
            }

            {
                Log.dbg("plugin_defaults != null");
                //Load settings specific to plugin.cfg
                if (defaults.Node.HasValue("hide_all_windows")) hide_all_windows = Boolean.Parse(defaults.Node.GetValue("hide_all_windows"));
                if (defaults.Node.HasValue("use_vessel_settings")) use_vessel_settings = Boolean.Parse(defaults.Node.GetValue("use_vessel_settings"));
                if (defaults.Node.HasValue("useBlizzy78Toolbar")) useBlizzy78Toolbar = Boolean.Parse(defaults.Node.GetValue("useBlizzy78Toolbar"));
                if (defaults.Node.HasValue("http_update_check")) http_update_check = Boolean.Parse(defaults.Node.GetValue("http_update_check"));
                if (defaults.Node.HasValue("disable_beeps_during_chatter")) disable_beeps_during_chatter = Boolean.Parse(defaults.Node.GetValue("disable_beeps_during_chatter"));
                if (defaults.Node.HasValue("insta_chatter_key")) insta_chatter_key = (KeyCode)Enum.Parse(typeof(KeyCode), defaults.Node.GetValue("insta_chatter_key"));
                if (defaults.Node.HasValue("insta_sstv_key")) insta_sstv_key = (KeyCode)Enum.Parse(typeof(KeyCode), defaults.Node.GetValue("insta_sstv_key"));
                if (defaults.Node.HasValue("show_advanced_options")) show_advanced_options = Boolean.Parse(defaults.Node.GetValue("show_advanced_options"));
                if (defaults.Node.HasValue("aae_backgrounds_onlyinIVA")) aae_backgrounds_onlyinIVA = Boolean.Parse(defaults.Node.GetValue("aae_backgrounds_onlyinIVA"));

                load_shared_settings(defaults.Node); //load settings shared between both configs
            }

            //if (chatter_exists && chatter_array.Count == 0)
            if (chatter_array.Count == 0)
            {
                Log.dbg("No audiosets found in config, adding defaults");
                add_default_audiosets();
                load_chatter_audio();   //load audio in case there is none
            }

            if (beeps_exists && beepsource_list.Count == 0)
            {
                Log.warn("beepsource_list.Count == 0, adding default 3");
                add_default_beepsources();
            }

            if (aae_backgrounds_exist && backgroundsource_list.Count == 0)
            {
                Log.warn("backgroundsource_list.Count == 0, adding default 2");
                add_default_backgroundsources();
            }

            Log.dbg("load_plugin_defaults() END");
        }

        private void save_settings(ConfigNode node)
        {
            node.SetValue("hide_all_windows", hide_all_windows, true);
            node.SetValue("use_vessel_settings", use_vessel_settings, true);
            node.SetValue("useBlizzy78Toolbar", useBlizzy78Toolbar, true);
            node.SetValue("http_update_check", http_update_check, true);
            node.SetValue("disable_beeps_during_chatter", disable_beeps_during_chatter, true);

            // Unity **SUCKS**
                if (node.HasValue("insta_chatter_key")) node.RemoveValue("insta_chatter_key");
                node.AddValue("insta_chatter_key", insta_chatter_key);

                if (node.HasValue("insta_sstv_key")) node.RemoveValue("insta_sstv_key");
                node.AddValue("insta_sstv_key", insta_sstv_key);

            node.SetValue("show_advanced_options", show_advanced_options, true);
            node.SetValue("aae_backgrounds_onlyinIVA", aae_backgrounds_onlyinIVA, true);
        }

        //Functions to handle settings shared by plugin.cfg and vessel.cfg
        private void save_shared_settings(ConfigNode node)
        {
            node.SetValue("show_tooltips", show_tooltips, true);
            node.SetValue("main_window_pos", main_window_pos.x + "," + main_window_pos.y, true);
            node.SetValue("skin_index", skin_index, true);
            node.SetValue("active_menu", active_menu, true);
            //node.SetValue("remotetech_toggle", remotetech_toggle, true;

            node.SetValue("chatter_freq", chatter_freq, true);
            node.SetValue("chatter_vol_slider", chatter_vol_slider, true);
            node.SetValue("chatter_sel_filter", chatter.sel_filter, true);
            node.SetValue("show_chatter_filter_settings", show_chatter_filter_settings, true);
            node.SetValue("show_sample_selector", show_probe_sample_selector, true);
            node.SetValue("chatter_reverb_preset_index", chatter.reverb_preset_index, true);
            node.SetValue("chatter_filter_settings_window_pos", chatter_filter_settings_window_pos.x + "," + chatter_filter_settings_window_pos.y, true);
            node.SetValue("probe_sample_selector_window_pos", probe_sample_selector_window_pos.x + "," + probe_sample_selector_window_pos.y, true);

            node.SetValue("quindar_toggle", quindar_toggle, true);
            node.SetValue("quindar_vol_slider", quindar_vol_slider, true);
            node.SetValue("sstv_freq", sstv_freq, true);
            node.SetValue("sstv_vol_slider", sstv_vol_slider, true);
            node.SetValue("sstv_on_science_toggle", sstv_on_science_toggle, true);

            node.SetValue("sel_beep_src", sel_beep_src, true);
            node.SetValue("sel_beep_page", sel_beep_page, true);
            
            //AAE
            if (aae_backgrounds_exist)
            {
                foreach (BackgroundSource src in backgroundsource_list)
                {
                    ConfigNode _background = new ConfigNode();
                    _background.AddValue("volume", src.audiosource.volume);
                    _background.AddValue("current_clip", src.current_clip);
                    node.SetNode("AAE_BACKGROUND", _background, true);
                }
            }

            if (aae_soundscapes_exist)
            {
                node.SetValue("aae_soundscape_vol", aae_soundscape.volume, true);
                node.SetValue("aae_soundscape_freq", aae_soundscape_freq, true);
            }

            if (aae_breathing_exist) node.SetValue("aae_breathing_vol", aae_breathing.volume, true);
            if (aae_wind_exist) node.SetValue("aae_wind_vol", aae_wind_vol_slider, true);
            if (aae_airlock_exist) node.SetValue("aae_airlock_vol", aae_airlock.volume, true);

            //Chatter sets
            foreach (ChatterAudioList chatter_set in chatter_array) // FIXME: eh aqui que tah dando merda no audioset.
            {
                ConfigNode _set = new ConfigNode();
                _set.AddValue("directory", chatter_set.directory);
                _set.AddValue("is_active", chatter_set.is_active);
                node.SetNode("AUDIOSET", _set, true);
            }

            save_shared_settings_filters(node, chatter);

            foreach (BeepSource source in beepsource_list)
            {
                ConfigNode beep_settings = new ConfigNode();

                save_settings(beep_settings, source);

                //filters
                save_shared_settings_filters(beep_settings, source);

                node.SetNode("BEEPSOURCE", beep_settings, true);
            }
        }

        private void load_settings(ConfigNode node, BeepSource source)
        {
            if (node.HasValue("precise")) source.precise = Boolean.Parse(node.GetValue("precise"));
            if (node.HasValue("precise_freq"))
            {
                source.precise_freq = Int32.Parse(node.GetValue("precise_freq"));
                source.precise_freq_slider = source.precise_freq;
            }
            if (node.HasValue("loose_freq"))
            {
                source.loose_freq = Int32.Parse(node.GetValue("loose_freq"));
                source.loose_freq_slider = source.loose_freq;
            }
            if (node.HasValue("volume")) source.audiosource.volume = Single.Parse(node.GetValue("volume"));
            if (node.HasValue("pitch")) source.audiosource.pitch = Single.Parse(node.GetValue("pitch"));
            if (node.HasValue("current_clip")) source.current_clip = node.GetValue("current_clip");
            if (node.HasValue("randomizeBeep")) source.randomizeBeep = Boolean.Parse(node.GetValue("randomizeBeep"));
            if (node.HasValue("sel_filter")) source.sel_filter = Int32.Parse(node.GetValue("sel_filter"));
            if (node.HasValue("show_settings_window")) source.show_settings_window = Boolean.Parse(node.GetValue("show_settings_window"));
            if (node.HasValue("reverb_preset_index")) source.reverb_preset_index = Int32.Parse(node.GetValue("reverb_preset_index"));
            if (node.HasValue("settings_window_pos_x")) source.settings_window_pos.x = Single.Parse(node.GetValue("settings_window_pos_x"));
            if (node.HasValue("settings_window_pos_y")) source.settings_window_pos.y = Single.Parse(node.GetValue("settings_window_pos_y"));
        }

        private void save_settings(ConfigNode node, BeepSource source)
        {
            node.AddValue("precise", source.precise);
            node.AddValue("precise_freq", source.precise_freq);
            node.AddValue("loose_freq", source.loose_freq);
            node.AddValue("volume", source.audiosource.volume);
            node.AddValue("pitch", source.audiosource.pitch);
            node.AddValue("current_clip", source.current_clip);
            node.AddValue("randomizeBeep", source.randomizeBeep);
            node.AddValue("sel_filter", source.sel_filter);
            node.AddValue("show_settings_window", source.show_settings_window);
            node.AddValue("show_settings_window", source.show_settings_window);
            node.AddValue("reverb_preset_index", source.reverb_preset_index);
            node.AddValue("settings_window_pos_x", source.settings_window_pos.x);
            node.AddValue("settings_window_pos_y", source.settings_window_pos.y);
        }

        private void save_shared_settings_filters(ConfigNode node, AudioSettings audioSettings)
        {
            save_shared_settings_filter(node, audioSettings.chorus_filter);
            save_shared_settings_filter(node, audioSettings.distortion_filter);
            save_shared_settings_filter(node, audioSettings.echo_filter);
            save_shared_settings_filter(node, audioSettings.highpass_filter);
            save_shared_settings_filter(node, audioSettings.lowpass_filter);
            save_shared_settings_filter(node, audioSettings.reverb_filter);
        }

        private void load_shared_settings_filter(ConfigNode node, AudioChorusFilter chorusFilter)
        {
            if (node.HasValue("enabled")) chorusFilter.enabled = Boolean.Parse(node.GetValue("enabled"));
            if (node.HasValue("dry_mix")) chorusFilter.dryMix = Single.Parse(node.GetValue("dry_mix"));
            if (node.HasValue("wet_mix_1")) chorusFilter.wetMix1 = Single.Parse(node.GetValue("wet_mix_1"));
            if (node.HasValue("wet_mix_2")) chorusFilter.wetMix2 = Single.Parse(node.GetValue("wet_mix_2"));
            if (node.HasValue("wet_mix_3")) chorusFilter.wetMix3 = Single.Parse(node.GetValue("wet_mix_3"));
        }

        private void save_shared_settings_filter(ConfigNode node, AudioChorusFilter chorusFilter)
        {
            ConfigNode _filter = new ConfigNode();
            _filter = new ConfigNode();
            _filter.AddValue("enabled", chorusFilter.enabled);
            _filter.AddValue("dry_mix", chorusFilter.dryMix);
            _filter.AddValue("wet_mix_1", chorusFilter.wetMix1);
            _filter.AddValue("wet_mix_2", chorusFilter.wetMix2);
            _filter.AddValue("wet_mix_3", chorusFilter.wetMix3);
            _filter.AddValue("delay", chorusFilter.delay);
            _filter.AddValue("rate", chorusFilter.rate);
            _filter.AddValue("depth", chorusFilter.depth);
            node.SetNode("CHORUS", _filter, true);
        }

        private void load_shared_settings_filter(ConfigNode node, AudioDistortionFilter distortionFilter)
        {
            if (node.HasValue("enabled")) distortionFilter.enabled = Boolean.Parse(node.GetValue("enabled"));
            if (node.HasValue("distortion_level")) distortionFilter.distortionLevel = Single.Parse(node.GetValue("distortion_level"));
        }

        private void save_shared_settings_filter(ConfigNode node, AudioDistortionFilter distortionFilter)
        {
            ConfigNode _filter = new ConfigNode();
            _filter = new ConfigNode();
            _filter.AddValue("enabled", distortionFilter.enabled);
            _filter.AddValue("distortion_level", distortionFilter.distortionLevel);
            node.SetNode("DISTORTION", _filter, true);
        }

        private void load_shared_settings_filter(ConfigNode node, AudioEchoFilter echoFilter)
        {
            if (node.HasValue("enabled")) echoFilter.enabled = Boolean.Parse(node.GetValue("enabled"));
            if (node.HasValue("delay")) echoFilter.delay = Single.Parse(node.GetValue("delay"));
            if (node.HasValue("decay_ratio")) echoFilter.decayRatio = Single.Parse(node.GetValue("decay_ratio"));
            if (node.HasValue("dry_mix")) echoFilter.dryMix = Single.Parse(node.GetValue("dry_mix"));
            if (node.HasValue("wet_mix")) echoFilter.wetMix = Single.Parse(node.GetValue("wet_mix"));
        }

        private void save_shared_settings_filter(ConfigNode node, AudioEchoFilter echoFilter)
        {
            ConfigNode _filter = new ConfigNode();
            _filter.AddValue("enabled", echoFilter.enabled);
            _filter.AddValue("delay", echoFilter.delay);
            _filter.AddValue("decay_ratio", echoFilter.decayRatio);
            _filter.AddValue("dry_mix", echoFilter.dryMix);
            _filter.AddValue("wet_mix", echoFilter.wetMix);
            node.SetNode("ECHO", _filter, true);
        }

        private void load_shared_settings_filter(ConfigNode node, AudioHighPassFilter highPassFilter)
        {
            if (node.HasValue("enabled")) highPassFilter.enabled = Boolean.Parse(node.GetValue("enabled"));
            if (node.HasValue("cutoff_freq")) highPassFilter.cutoffFrequency = Single.Parse(node.GetValue("cutoff_freq"));
            if (node.HasValue("resonance_q")) highPassFilter.highpassResonanceQ = Single.Parse(node.GetValue("resonance_q"));
        }

        private void save_shared_settings_filter(ConfigNode node, AudioHighPassFilter highPassFilter)
        {
            ConfigNode _filter = new ConfigNode();
            _filter.AddValue("enabled", highPassFilter.enabled);
            _filter.AddValue("cutoff_freq", highPassFilter.cutoffFrequency);
            _filter.AddValue("resonance_q", highPassFilter.highpassResonanceQ);
            node.SetNode("HIGHPASS", _filter, true);
        }

        private void load_shared_settings_filter(ConfigNode node, AudioLowPassFilter lowPassFilter)
        {
            if (node.HasValue("enabled")) lowPassFilter.enabled = Boolean.Parse(node.GetValue("enabled"));
            if (node.HasValue("cutoff_freq")) lowPassFilter.cutoffFrequency = Single.Parse(node.GetValue("cutoff_freq"));
            if (node.HasValue("resonance_q")) lowPassFilter.lowpassResonanceQ = Single.Parse(node.GetValue("resonance_q"));
        }

        private void save_shared_settings_filter(ConfigNode node, AudioLowPassFilter lowPassFilter)
        {
            ConfigNode _filter = new ConfigNode();
            _filter.AddValue("enabled", lowPassFilter.enabled);
            _filter.AddValue("cutoff_freq", lowPassFilter.cutoffFrequency);
            _filter.AddValue("resonance_q", lowPassFilter.lowpassResonanceQ);
            node.SetNode("LOWPASS", _filter, true);
        }

        private void load_shared_settings_filter(ConfigNode node, AudioReverbFilter reverbFilter)
        {
            if (node.HasValue("enabled")) reverbFilter.enabled = Boolean.Parse(node.GetValue("enabled"));
            if (node.HasValue("reverb_preset")) reverbFilter.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), node.GetValue("reverb_preset"));
            if (node.HasValue("dry_level")) reverbFilter.dryLevel = Single.Parse(node.GetValue("dry_level"));
            if (node.HasValue("room")) reverbFilter.room = Single.Parse(node.GetValue("room"));
            if (node.HasValue("room_hf")) reverbFilter.roomHF = Single.Parse(node.GetValue("room_hf"));
            if (node.HasValue("room_lf")) reverbFilter.roomLF = Single.Parse(node.GetValue("room_lf"));
            if (node.HasValue("decay_time")) reverbFilter.decayTime = Single.Parse(node.GetValue("decay_time"));
            if (node.HasValue("decay_hf_ratio")) reverbFilter.decayHFRatio = Single.Parse(node.GetValue("decay_hf_ratio"));
            if (node.HasValue("reflections_level")) reverbFilter.reflectionsLevel = Single.Parse(node.GetValue("reflections_level"));
            if (node.HasValue("reflections_delay")) reverbFilter.reflectionsDelay = Single.Parse(node.GetValue("reflections_delay"));
            if (node.HasValue("reverb_level")) reverbFilter.reverbLevel = Single.Parse(node.GetValue("reverb_level"));
            if (node.HasValue("reverb_delay")) reverbFilter.reverbDelay = Single.Parse(node.GetValue("reverb_delay"));
            if (node.HasValue("diffusion")) reverbFilter.diffusion = Single.Parse(node.GetValue("diffusion"));
            if (node.HasValue("density")) reverbFilter.density = Single.Parse(node.GetValue("density"));
            if (node.HasValue("hf_reference")) reverbFilter.hfReference = Single.Parse(node.GetValue("hf_reference"));
            if (node.HasValue("lf_reference")) reverbFilter.lfReference = Single.Parse(node.GetValue("lf_reference"));
        }

        private void save_shared_settings_filter(ConfigNode node, AudioReverbFilter reverbFilter)
        {
            ConfigNode _filter = new ConfigNode();
            _filter.AddValue("enabled", reverbFilter.enabled);
            _filter.AddValue("reverb_preset", reverbFilter.reverbPreset);
            _filter.AddValue("dry_level", reverbFilter.dryLevel);
            _filter.AddValue("room", reverbFilter.room);
            _filter.AddValue("room_hf", reverbFilter.roomHF);
            _filter.AddValue("room_lf", reverbFilter.roomLF);
            _filter.AddValue("decay_time", reverbFilter.decayTime);
            _filter.AddValue("decay_hf_ratio", reverbFilter.decayHFRatio);
            _filter.AddValue("reflections_level", reverbFilter.reflectionsLevel);
            _filter.AddValue("reflections_delay", reverbFilter.reflectionsDelay);
            _filter.AddValue("reverb_level", reverbFilter.reverbLevel);
            _filter.AddValue("reverb_delay", reverbFilter.reverbDelay);
            _filter.AddValue("diffusion", reverbFilter.diffusion);
            _filter.AddValue("density", reverbFilter.density);
            _filter.AddValue("hf_reference", reverbFilter.hfReference);
            _filter.AddValue("lf_reference", reverbFilter.lfReference);
            node.SetNode("REVERB", _filter, true);
        }

        private void load_settings(ConfigNode node)
        {
            if (node.HasValue("hide_all_windows")) hide_all_windows = Boolean.Parse(node.GetValue("hide_all_windows"));
            if (node.HasValue("use_vessel_settings")) use_vessel_settings = Boolean.Parse(node.GetValue("use_vessel_settings"));
            if (node.HasValue("useBlizzy78Toolbar")) useBlizzy78Toolbar = Boolean.Parse(node.GetValue("useBlizzy78Toolbar"));
            if (node.HasValue("http_update_check")) http_update_check = Boolean.Parse(node.GetValue("http_update_check"));
            if (node.HasValue("disable_beeps_during_chatter")) disable_beeps_during_chatter = Boolean.Parse(node.GetValue("disable_beeps_during_chatter"));
            if (node.HasValue("insta_chatter_key")) insta_chatter_key = (KeyCode)Enum.Parse(typeof(KeyCode), node.GetValue("insta_chatter_key"));
            if (node.HasValue("insta_sstv_key")) insta_sstv_key = (KeyCode)Enum.Parse(typeof(KeyCode), node.GetValue("insta_sstv_key"));
            if (node.HasValue("show_advanced_options")) show_advanced_options = Boolean.Parse(node.GetValue("show_advanced_options"));
            if (node.HasValue("aae_backgrounds_onlyinIVA")) aae_backgrounds_onlyinIVA = Boolean.Parse(node.GetValue("aae_backgrounds_onlyinIVA"));
        }

        private void load_shared_settings(ConfigNode node)
        {
            Log.dbg("load_shared_settings() START");

            destroy_all_beep_players();
            beepsource_list.Clear();
            destroy_all_background_players();
            backgroundsource_list.Clear();
            chatter_array.Clear();

            if (node.HasValue("main_window_pos"))
            {
                string[] split = node.GetValue("main_window_pos").Split(Convert.ToChar(","));
                main_window_pos.x = Single.Parse(split[0]);
                main_window_pos.y = Single.Parse(split[1]);
            }

            if (node.HasValue("show_tooltips")) show_tooltips = Boolean.Parse(node.GetValue("show_tooltips"));
            if (node.HasValue("skin_index")) skin_index = Int32.Parse(node.GetValue("skin_index"));
            if (node.HasValue("active_menu")) active_menu = Int32.Parse(node.GetValue("active_menu"));
            //if (node.HasValue("remotetech_toggle")) remotetech_toggle = Boolean.Parse(node.GetValue("remotetech_toggle"));

            if (node.HasValue("chatter_freq"))
            {
                chatter_freq = Int32.Parse(node.GetValue("chatter_freq"));
                chatter_freq_slider = chatter_freq;
                prev_chatter_freq = chatter_freq;
            }
            if (node.HasValue("chatter_vol_slider"))
            {
                chatter_vol_slider = Single.Parse(node.GetValue("chatter_vol_slider"));
                initial_chatter.volume = chatter_vol_slider;
                response_chatter.volume = chatter_vol_slider;
                prev_chatter_vol_slider = chatter_vol_slider;
            }
            if (node.HasValue("chatter_sel_filter")) chatter.sel_filter = Int32.Parse(node.GetValue("chatter_sel_filter"));
            if (node.HasValue("show_chatter_filter_settings")) show_chatter_filter_settings = Boolean.Parse(node.GetValue("show_chatter_filter_settings"));
            if (node.HasValue("chatter_reverb_preset_index")) chatter.reverb_preset_index = Int32.Parse(node.GetValue("chatter_reverb_preset_index"));
            if (node.HasValue("chatter_filter_settings_window_pos"))
            {
                string[] split = node.GetValue("chatter_filter_settings_window_pos").Split(Convert.ToChar(","));
                chatter_filter_settings_window_pos.x = Single.Parse(split[0]);
                chatter_filter_settings_window_pos.y = Single.Parse(split[1]);
            }
            if (node.HasValue("show_sample_selector")) show_probe_sample_selector = Boolean.Parse(node.GetValue("show_sample_selector"));
            if (node.HasValue("probe_sample_selector_window_pos"))
            {
                string[] split = node.GetValue("probe_sample_selector_window_pos").Split(Convert.ToChar(","));
                probe_sample_selector_window_pos.x = Single.Parse(split[0]);
                probe_sample_selector_window_pos.y = Single.Parse(split[1]);
            }
            if (node.HasValue("quindar_toggle")) quindar_toggle = Boolean.Parse(node.GetValue("quindar_toggle"));
            if (node.HasValue("quindar_vol_slider"))
            {
                quindar_vol_slider = Single.Parse(node.GetValue("quindar_vol_slider"));
                prev_quindar_vol_slider = quindar_vol_slider;
            }
            if (node.HasValue("sstv_freq"))
            {
                sstv_freq = Int32.Parse(node.GetValue("sstv_freq"));
                sstv_freq_slider = sstv_freq;
                prev_sstv_freq = sstv_freq;
            }
            if (node.HasValue("sstv_vol_slider"))
            {
                sstv_vol_slider = Single.Parse(node.GetValue("sstv_vol_slider"));
                prev_sstv_vol_slider = sstv_vol_slider;
            }
            if (node.HasValue("sstv_on_science_toggle")) sstv_on_science_toggle = Boolean.Parse(node.GetValue("sstv_on_science_toggle"));

            if (node.HasValue("sel_beep_src")) sel_beep_src = Int32.Parse(node.GetValue("sel_beep_src"));
            if (sel_beep_src < 0 || sel_beep_src > 9) sel_beep_src = 0;
            if (node.HasValue("sel_beep_page")) sel_beep_page = Int32.Parse(node.GetValue("sel_beep_page"));
            
            //AAE

            if (aae_backgrounds_exist)
            {
                int i = 0;

                foreach (ConfigNode _background in node.nodes)
                {
                    if (_background.name == "AAE_BACKGROUND")
                    {
                        add_new_backgroundsource();
                        if (_background.HasValue("volume")) backgroundsource_list[i].audiosource.volume = Single.Parse(_background.GetValue("volume"));
                        if (_background.HasValue("current_clip")) backgroundsource_list[i].current_clip = _background.GetValue("current_clip");

                        if (dict_background_samples.Count > 0)
                        {
                            set_background_clip(backgroundsource_list[i]);
                        }
                        i++;
                    }
                }
            }

            if (aae_soundscapes_exist)
            {
                if (node.HasValue("aae_soundscape_vol")) aae_soundscape.volume = Single.Parse(node.GetValue("aae_soundscape_vol"));
                if (node.HasValue("aae_soundscape_freq"))
                {
                    aae_soundscape_freq = Int32.Parse(node.GetValue("aae_soundscape_freq"));
                    aae_prev_soundscape_freq = aae_soundscape_freq;
                }
            }

            if (aae_breathing_exist)
            {
                if (node.HasValue("aae_breathing_vol")) aae_breathing.volume = Single.Parse(node.GetValue("aae_breathing_vol"));
            }

            if (aae_airlock_exist)
            {
                if (node.HasValue("aae_airlock_vol")) aae_airlock.volume = Single.Parse(node.GetValue("aae_airlock_vol"));
            }

            if (aae_wind_exist)
            {
                if (node.HasValue("aae_wind_vol"))
                {
                    aae_wind_vol_slider = Single.Parse(node.GetValue("aae_wind_vol"));
                    aae_wind.volume = aae_wind_vol_slider;
                }
            }

            //Load audioset info
            foreach (ConfigNode _set in node.GetNodes("AUDIOSET"))
                chatter_array.Add(ChatterAudioList.createFrom(_set));  //create a new entry in the list for each audioset

            Log.dbg("audiosets found: {0} :: reloading chatter audio", chatter_array.Count);
            load_chatter_audio();   //reload audio

            //Chatter filters
            foreach (ConfigNode configNode in node.nodes)
            {
                load_shared_settings_filter(configNode, chatter);

                if (configNode.name == "BEEPSOURCE") //Beepsources
                {
                    Log.dbg("loading beepsource");
                    add_new_beepsource();

                    int x = beepsource_list.Count - 1;

                    load_settings(configNode, beepsource_list[x]);

                    if (dict_probe_samples.Count > 0)
                    {
                        set_beep_clip(beepsource_list[x]);

                        if (beepsource_list[x].precise == false && beepsource_list[x].loose_freq > 0) new_beep_loose_timer_limit(beepsource_list[x]);
                    }

                    foreach (ConfigNode innerConfigNode in configNode.nodes)
                        load_shared_settings_filter(innerConfigNode, beepsource_list[x]);
                }
            }
            Log.dbg("load_shared_settings() END");
        }

        private void load_shared_settings_filter(ConfigNode configNode, AudioSettings audioSettings)
		{
            if (configNode.name == "CHORUS")           load_shared_settings_filter(configNode, audioSettings.chorus_filter);
            else if (configNode.name == "DISTORTION")  load_shared_settings_filter(configNode, audioSettings.distortion_filter);
            else if (configNode.name == "ECHO")        load_shared_settings_filter(configNode, audioSettings.echo_filter);
            else if (configNode.name == "HIGHPASS")    load_shared_settings_filter(configNode, audioSettings.highpass_filter);
            else if (configNode.name == "LOWPASS")     load_shared_settings_filter(configNode, audioSettings.lowpass_filter);
            else if (configNode.name == "REVERB")      load_shared_settings_filter(configNode, audioSettings.reverb_filter);
		}

        //Functions for per-vessel settings
        private void new_vessel_node(Vessel v)
        {
            Log.dbg("new_vessel_node() START");

            ConfigNode vessel_node = new ConfigNode();

            vessel_node.AddValue("vessel_name", v.vesselName);
            vessel_node.AddValue("vessel_id", v.id.ToString());

            save_shared_settings(vessel_node);
            vessel_settings.Node.AddNode("VESSEL", vessel_node);

            Log.dbg("new_vessel_node() :: vessel_node added to vessel_settings_node");
        }

        private void load_vessel_settings_node()
        {
            Log.dbg("START load_vessel_settings_node()");

            // Force the creation of the file if needed
            if (vessel_settings.IsLoadable) vessel_settings.Load(); else vessel_settings.Clear();

            new_vessel_node(vessel);  //add current vessel to vessel_settings_node
            vessel_settings.Save();
            Log.dbg("load_vessel_settings_node() :: current vessel node saved to vessel_settings.cfg");
        }

        private void load_vessel_node(ConfigNode node)
        {
            Log.dbg("load_vessel_node() :: loading vessel settings for this vessel from node");

            //destroy_all_beep_players();
            //destroy_all_background_players();

            load_shared_settings(node);

            if (chatter_array.Count == 0)
            {
                Log.warn("No audiosets found in config, adding defaults");
                add_default_audiosets();
            }

            if (beepsource_list.Count == 0)
            {
                Log.warn("beepsource_list.Count == 0, adding default 3");
                add_default_beepsources();
            }

            if (backgroundsource_list.Count == 0)
            {
                Log.warn("backgroundsource_list.Count == 0, adding default 2");
                add_default_backgroundsources();
            }

            Log.dbg("load_vessel_node() :: vessel settings loaded OK : total beep sources = " + beepsource_list.Count);
        }

        private bool search_vessel_settings_node()
        {
            Log.dbg("START search_vessel_settings_node()");

            bool no_match = true;

            Log.dbg("active vessel id = {0}", vessel.id);

            if (vessel_settings.IsLoadable) vessel_settings.Load(); else vessel_settings.Clear();
            foreach (ConfigNode n in vessel_settings.Node.nodes)
            {
                string val = n.GetValue("vessel_id");
                Log.dbg("n.GetValue(\"vessel_id\") = {0}", n.GetValue("vessel_id"));
                if (val == vessel.id.ToString())
                {
                    Log.dbg("search_vessel_settings_node() :: vessel_id match");
                    load_vessel_node(n);    //load vals
                    no_match = false;
                    break;
                }
                else Log.dbg("no match, continuing search...");
            }
            if (no_match)
            {
                Log.dbg("finished search, no vessel_id match :: creating new node for this vessel");
                new_vessel_node(vessel);
                vessel_settings.Save();
                Log.dbg("new vessel node created and saved");
                load_chatter_audio();   //load audio in case there is none
            }
            return !no_match;
        }

        private void write_vessel_settings()
        {
            Log.dbg("Saving active vessel {0}:{1}", vessel.name, vessel.id);
            write_vessel_settings(vessel);
        }

        private void write_vessel_settings(Vessel vesselToUpdate)
        {
            Log.dbg("writing vessel_settings node to disk");

            Log.dbg("vessel = {0}:{1}", vesselToUpdate,name, vesselToUpdate.id);
            if (vessel_settings.IsLoadable) vessel_settings.Load(); else vessel_settings.Clear();

            foreach (ConfigNode cn in vessel_settings.Node.GetNodes("VESSEL"))
            {
                if (cn.HasValue("vessel_id"))
                {
                    string name = cn.GetValue("vessel_name");
                    string val = cn.GetValue("vessel_id");
                    Log.dbg("node vessel = {0}:{1}", name, val);

                    if (val == vesselToUpdate.id.ToString())
                    {
                        vessel_settings.Node.RemoveNode(cn);
                        Log.dbg("vessel node removed");
                    }
                }
            }

            new_vessel_node(vesselToUpdate);
            Log.dbg("write_vessel_settings() :: new node created using vessel {0}:{1} and added to vessel_settings node", vesselToUpdate.name, vesselToUpdate.id);
            vessel_settings.Save();
            Log.dbg("vessel_settings node saved to disk :: vessel node count = {0}", vessel_settings.Node.nodes.Count);

            Log.dbg("write_vessel_settings() END :: vessel_settings node saved to vessel_settings.cfg");
        }

        private void remove_vessel_settings(Vessel vesselToRemove)
        {
            Log.dbg("remove_vessel_settings node from disk");

            Log.dbg("vessel = {0}:{1}", vesselToRemove,name, vesselToRemove.id);
            if (vessel_settings.IsLoadable) vessel_settings.Load(); else vessel_settings.Clear();

            foreach (ConfigNode cn in vessel_settings.Node.GetNodes("VESSEL"))
            {
                if (cn.HasValue("vessel_id"))
                {
                    string name = cn.GetValue("vessel_name");
                    string val = cn.GetValue("vessel_id");
                    Log.dbg("node vessel = {0}:{1}", name, val);

                    if (val == vesselToRemove.id.ToString())
                    {
                        vessel_settings.Node.RemoveNode(cn);
                        Log.dbg("vessel node removed");
                    }
                }
            }

            vessel_settings.Save();
            Log.dbg("vessel_settings node saved to disk :: vessel node count = {0}", vessel_settings.Node.nodes.Count);

            Log.dbg("remove_vessel_settings() END :: vessel_settings node saved to vessel_settings.cfg");
        }
    }
}
