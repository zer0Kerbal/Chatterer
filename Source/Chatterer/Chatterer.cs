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


/* DO ME
 * 
 * 1 - Continue to separate the code in different .cs files accordingly to their function
 * 
 * 2 - RemoteTech support :
 *   - try to add ping<>pong (accordingly with delay) Beeps beeween vessel & KSC
 *   - add a parazited noise as chatter response if offline
 *   
 * 3 - Create an API for external access (Chatter/beeps/SSTV trigger, mute, ...)
 * 
 * //
 * 
 * ADD some fillable Preset slots to store configured chatter/beeps/filters for later use (single beep, all beeps, or all audio)
 * save all clipboard nodes to disk when any are filled/changed
 * load them at start and vessel switch
 * 
 * 
 * //ADD a settings 'clipboard' to copy/paste current single beepsource settings to another beepsource
 * //ADD EVA-capsule chatter (if nearby crew > 0) and capsule-capsule chatter (if vessel crew > 1)
 *  
 */


///////////////////////////////////////////////////////////////////////////////


using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KSP.UI.Screens;

using Asset = KSPe.IO.Asset<Chatterer.Startup>;
using GDBAsset = KSPe.GameDB.Asset<Chatterer.Startup>;
using File = KSPe.IO.File<Chatterer.Startup>;

namespace Chatterer
{
    public class ChatterAudioList
    {
        //class to manage chatter clips
        public List<AudioClip> capcom;
        public List<AudioClip> capsule;
        public List<AudioClip> capsuleF;
        public string directory;
        public bool is_active;

        public ChatterAudioList()
        {
            capcom = new List<AudioClip>();
            capsule = new List<AudioClip>();
            capsuleF = new List<AudioClip>();
            directory = "dir";
            is_active = true;
        }

		internal static ChatterAudioList createFrom(ConfigNode cn)
		{
			ChatterAudioList r = new ChatterAudioList();
			if (cn.HasValue("directory")) r.directory = cn.GetValue("directory");
			if (cn.HasValue("is_active")) r.is_active = Boolean.Parse(cn.GetValue("is_active"));
			return r;
		}
	}

    public class AudioSettings
    {
        public AudioChorusFilter chorus_filter;
        public AudioDistortionFilter distortion_filter;
        public AudioEchoFilter echo_filter;
        public AudioHighPassFilter highpass_filter;
        public AudioLowPassFilter lowpass_filter;
        public AudioReverbFilter reverb_filter;
        public AudioReverbPreset reverb_preset;
        public int reverb_preset_index;
        public int sel_filter; //currently selected filter in filters window

        public AudioSettings()
        {
            sel_filter = 0;
            reverb_preset_index = 0;
        }
    }

    public class BeepSource : AudioSettings
    {
        //class to manage beeps
        public GameObject beep_player;
        public string beep_name;
        public AudioSource audiosource;
        public Rect settings_window_pos;
        public bool show_settings_window;
        public int settings_window_id;
        public bool precise;
        public float precise_freq_slider;
        public int precise_freq;
        public int prev_precise_freq;
        public float loose_freq_slider;
        public int loose_freq;
        public int prev_loose_freq;
        public int loose_timer_limit;
        public float timer;
        public string current_clip;
        public bool randomizeBeep;

        public BeepSource() : base()
        {
            settings_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
            show_settings_window = false;
            precise = true;
            precise_freq_slider = -1f;
            precise_freq = -1;
            prev_precise_freq = -1;
            loose_freq_slider = 0;
            loose_freq = 0;
            prev_loose_freq = 0;
            loose_timer_limit = 0;
            timer = 0;
            randomizeBeep = false;
        }
    }

    public class BackgroundSource
    {
        //class to manage background audiosources
        public GameObject background_player;
        public string name;
        public AudioSource audiosource;
        public string current_clip;
    }

    //public class SoundscapeSource
    //{
    //    public GameObject soundscape_player;
    //    public string name;
    //    public AudioSource audiosource;
    //    public string current_clip;
    //}

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public partial class chatterer : MonoBehaviour
    {
        //Version
        private string this_version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private string main_window_title = "Chatterer ";
        
        private static System.Random rand = new System.Random();

        private int window_base_id = -12381578;
        
        private Vessel vessel;          //is set to FlightGlobals.ActiveVessel
        private Vessel prev_vessel;     //to detect change in active vessel

        //GameObjects to hold AudioSources and AudioFilters
        //private GameObject musik_player = new GameObject();
        private GameObject chatter_player; // = new GameObject();
        private GameObject sstv_player; // = new GameObject();

        //Chatter AudioSources
        private AudioSource initial_chatter = new AudioSource();
        private AudioSource response_chatter = new AudioSource();
        private AudioSource quindar1 = new AudioSource();
        private AudioSource quindar2 = new AudioSource();
        //private AudioSource musik = new AudioSource();

        //SSTV AudioSources
        private AudioSource sstv = new AudioSource();

        //All beep objects, audiosources, and filters are managed by BeepSource class
        private readonly List<BeepSource> beepsource_list = new List<BeepSource>();     //List to hold the BeepSources
        private readonly List<BackgroundSource> backgroundsource_list = new List<BackgroundSource>();    //list to hold the BackgroundSources
        
        //Chatter, SSTV, and beep audio sample Lists and Dictionaries
        private readonly List<ChatterAudioList> chatter_array = new List<ChatterAudioList>();        //array of all chatter clips and some settings
        private readonly Dictionary<string, AudioClip> dict_probe_samples = new Dictionary<string, AudioClip>();
        private readonly Dictionary<AudioClip, string> dict_probe_samples2 = new Dictionary<AudioClip, string>();
        private readonly List<AudioClip> all_sstv_clips = new List<AudioClip>();
        private readonly Dictionary<string, AudioClip> dict_background_samples = new Dictionary<string, AudioClip>();
        private readonly Dictionary<AudioClip, string> dict_background_samples2 = new Dictionary<AudioClip, string>();
        private readonly Dictionary<string, AudioClip> dict_soundscape_samples = new Dictionary<string, AudioClip>();
        private readonly Dictionary<AudioClip, string> dict_soundscape_samples2 = new Dictionary<AudioClip, string>();

        //Chatter audio lists
        private readonly List<AudioClip> current_capcom_chatter = new List<AudioClip>();     //holds chatter of toggled sets
        private readonly List<AudioClip> current_capsule_chatter = new List<AudioClip>();    //one of these becomes initial, the other response
        private readonly List<AudioClip> current_capsuleF_chatter = new List<AudioClip>(); //Female set
        private int current_capcom_clip;
        private int current_capsule_clip;
        private int current_capsuleF_clip;

        private AudioClip quindar_01_clip;
        private AudioClip quindar_02_clip;
        private AudioClip voidnoise_clip;

        //Chatter variables
        private bool exchange_playing = false;
        private bool pod_begins_exchange = false;
        private bool was_on_EVA = false;
        private int initial_chatter_source; //whether capsule or capcom begins exchange
        private List<AudioClip> initial_chatter_set = new List<AudioClip>();    //random clip pulled from here
        private int initial_chatter_index;  //index of random clip
        private List<AudioClip> response_chatter_set = new List<AudioClip>();   //and here
        private int response_chatter_index;
        private int response_delay_secs;

        //GUI
        private bool gui_running = false;
        private int skin_index = 0;     //selected skin
        private bool gui_styles_set = false;
        private bool hide_all_windows = true;
        private string custom_dir_name = "directory name";  //default text for audioset input box
        private int active_menu = 0;    //selected main window section (sliders, sets, etc)
        private int sel_beep_src = 0;   //currently selected beep source
        private int sel_beep_page = 1;
        private int num_beep_pages;
        private int prev_num_pages;

        //integration with blizzy78's Toolbar plugin
        private IButton chatterer_toolbar_button;
        private bool useBlizzy78Toolbar = false;

        //KSP Stock application launcherButton
        private ApplicationLauncherButton launcherButton = null;
        private Texture2D chatterer_button_Texture = null;
        private Texture2D chatterer_button_TX; // = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_TX_muted; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_RX; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_RX_muted; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_SSTV; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_SSTV_muted; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_idle; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_idle_muted; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_disabled; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        private Texture2D chatterer_button_disabled_muted; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);

        //Main window
        protected Rect main_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
        
        //Probe Sample Selector window
        private Vector2 probe_sample_selector_scroll_pos = new Vector2();
        private bool show_probe_sample_selector = false;
        protected Rect probe_sample_selector_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
        private int probe_sample_selector_window_id;

        //Background AAE Selector window
        private Vector2 AAE_background_sample_selector_scroll_pos = new Vector2();
        private bool show_AAE_background_sample_selector = false;
        protected Rect AAE_background_sample_selector_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
        private int AAE_background_sample_selector_window_id;

        //The Lab window
        private bool show_lab_gui = false;
        protected Rect lab_window_pos = new Rect(Screen.width / 2f, Screen.height / 2f, 10f, 10f);
        private int lab_window_id;

        //Textures
        private Texture2D line_512x4; // = new Texture2D(512, 8, TextureFormat.ARGB32, false);

        //GUIStyles
        private GUIStyle label_txt_left;
        private GUIStyle label_txt_center;
        private GUIStyle label_txt_right;
        private GUIStyle label_txt_red_center;
        private GUIStyle button_txt_left;
        private GUIStyle button_txt_right;
        private GUIStyle button_txt_center;
        //private GUIStyle button_txt_center_green;
        private GUIStyle gs_tooltip;
        //private GUIStyle xkcd_label;
        private GUIStyle label_txt_bold;
        private GUIStyle button_txt_left_bold;
                        
        //Counters
        //private float rt_update_timer = 0;
        private float sstv_timer = 0;
        private float sstv_timer_limit = 0;
        private float secs_since_last_exchange = 0;
        private float secs_between_exchanges = 0;
        
        //RemoteTech & CommNet
        bool
            //whether the vessel has a RemoteTech SPU
            //hasRemoteTech = false,

            //whether the RemoteTech flight computer is controlling attitude
            //attitudeActive = false,

            //whether local control is active, meaning no control delays
            //localControl = false,

            //whether the vessel is in radio contact with KSC
            inRadioContact = true;

            //whether the vessel is in radio contact with a sattelite
            //inSatteliteRadioContact = false;

        //double
            ////the current signal delay (is returned as 0 if the vessel is not in contact)
            //controlDelay = 0; //delay from KSC
            ////shortestcontrolDelay = 0; // delay from nearest sattelite

        //Unsorted
        private BeepSource OTP_source = new BeepSource();
        private AudioClip OTP_stored_clip;    //holds the set probe sample while another sample plays once
        private bool OTP_playing = false;
        
        private bool chatter_exists = false;
        private bool sstv_exists = false;
        private bool science_transmitted = false;  //for SSTV on science
        private bool beeps_exists = false;
        
        private List<GUISkin> g_skin_list;

        private string yep_yep = "";
        private bool yep_yep_loaded = false;

        //AAE
        private bool aae_backgrounds_exist = false;
        private bool aae_soundscapes_exist = false;
        private bool aae_breathing_exist = false;
        private bool aae_airlock_exist = false;
        private bool aae_wind_exist = false;


        private GameObject aae_soundscape_player; // = new GameObject();
        private AudioSource aae_soundscape = new AudioSource();

        private GameObject aae_ambient_player; // = new GameObject();
        private AudioSource aae_breathing = new AudioSource();
        private AudioSource aae_airlock = new AudioSource();
        private AudioSource aae_wind = new AudioSource();
        private float aae_wind_vol_slider = 1.0f;

        private BackgroundSource sel_background_src;    //so sample selector window knows which backgroundsource we are working with


        private int aae_soundscape_freq = 0;
        private int aae_prev_soundscape_freq = 0;
        private float aae_soundscape_freq_slider = 2;
        private float aae_soundscape_timer = 0;
        private float aae_soundscape_timer_limit = 0;
        private string aae_soundscape_current_clip = "";


        private AudioSource landingsource = new AudioSource();
        private AudioSource yep_yepsource = new AudioSource();

        static readonly string[] AUDIO_FILE_EXTS = { "*.wav", "*.ogg", "*.aif", "*.aiff" };

        //////////////////////////////////////////////////
        //////////////////////////////////////////////////

        //GUI

        //integration with blizzy78's Toolbar plugin
        
        internal chatterer()
        {
            if (ToolbarManager.ToolbarAvailable)
            {
                Log.dbg("blizzy78's Toolbar plugin found ! Set toolbar button.");

                chatterer_toolbar_button = ToolbarManager.Instance.add("Chatterer", "UI");
                chatterer_toolbar_button.TexturePath = File.Asset.Solve("Textures", "chatterer_icon_toolbar");
                chatterer_toolbar_button.ToolTip = "Open/Close Chatterer UI";
                chatterer_toolbar_button.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
                chatterer_toolbar_button.OnClick += ((e) =>
                {
                    Log.dbg("Toolbar UI button clicked, when hide_all_windows = {0}", hide_all_windows);

                    if (launcherButton == null && ToolbarManager.ToolbarAvailable)
                    {
                        UIToggle();
                    }
                    else if (launcherButton != null)
                    {
                        if (hide_all_windows)
                        {
                            launcherButton.SetTrue();
                            Log.dbg("Blizzy78's Toolbar UI button clicked, launcherButton.State = {0}", launcherButton.toggleButton.CurrentState);
                        }
                        else if (!hide_all_windows)
                        {
                            launcherButton.SetFalse();
                            Log.dbg("Blizzy78's Toolbar UI button clicked, saving settings... & launcherButton.State = {0}", launcherButton.toggleButton.CurrentState);
                        }
                    }
                });
            }
        }

        private void OnGUIApplicationLauncherReady()
        {
            // Create the button in the KSP AppLauncher
            if (launcherButton == null && !useBlizzy78Toolbar)
            {
                Log.dbg("Building ApplicationLauncherButton");
                                
                launcherButton = ApplicationLauncher.Instance.AddModApplication(UIToggle, UIToggle,
                                                                            null, null,
                                                                            null, null,
                                                                            ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                                                                            chatterer_button_idle);
            }
        }

        private void launcherButtonTexture_check()
        {
        // launcherButton texture change check
             
            if (all_muted)
            {
                if (initial_chatter.isPlaying)
                {
                    if (initial_chatter_source == 0) SetAppLauncherButtonTexture(chatterer_button_RX_muted);
                    else SetAppLauncherButtonTexture(chatterer_button_TX_muted);
                }
                else if (response_chatter.isPlaying)
                {
                    if (initial_chatter_source == 1) SetAppLauncherButtonTexture(chatterer_button_RX_muted);
                    else SetAppLauncherButtonTexture(chatterer_button_TX_muted);
                }
                else if (sstv.isPlaying) SetAppLauncherButtonTexture(chatterer_button_SSTV_muted);
                else if (!inRadioContact) SetAppLauncherButtonTexture(chatterer_button_disabled_muted);
                else SetAppLauncherButtonTexture(chatterer_button_idle_muted);
             
            }
            else
            {
                if (initial_chatter.isPlaying)
                {
                    if (initial_chatter_source == 0) SetAppLauncherButtonTexture(chatterer_button_RX);
                    else SetAppLauncherButtonTexture(chatterer_button_TX);
                }
                else if (response_chatter.isPlaying)
                {
                    if (initial_chatter_source == 1) SetAppLauncherButtonTexture(chatterer_button_RX);
                    else SetAppLauncherButtonTexture(chatterer_button_TX);
                }
                else if (sstv.isPlaying) SetAppLauncherButtonTexture(chatterer_button_SSTV);
                else if (!inRadioContact) SetAppLauncherButtonTexture(chatterer_button_disabled);
                else SetAppLauncherButtonTexture(chatterer_button_idle);
            }
        }

        private void SetAppLauncherButtonTexture(Texture2D tex2d)
        {
            // Set new launcherButton texture
            if (launcherButton != null)
            {
                if (tex2d != chatterer_button_Texture)
                {
                    chatterer_button_Texture = tex2d;
                    launcherButton.SetTexture(tex2d);

                    Log.dbg("SetAppLauncherButtonTexture({0});", tex2d);
                }
            }
        }

        public void UIToggle()
        {
            if (!hide_all_windows)
            {
                hide_all_windows = true;
                save_plugin_settings();

                Log.dbg("UIToggle(OFF)");
            }
            else
            {
                hide_all_windows = !hide_all_windows;

                Log.dbg("UIToggle(ON)");
            }
        }

        public void launcherButtonRemove()
        {
            if (launcherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(launcherButton);

                Log.dbg("launcherButtonRemove");
            }

            else Log.dbg("launcherButtonRemove (useless attempt)");
        }

        public void OnSceneChangeRequest(GameScenes _scene)
        {
            launcherButtonRemove();
        }
        
        void OnCrewOnEVA(GameEvents.FromToAction<Part, Part> data)
        {
            if (aae_airlock_exist)
                aae_airlock.Play();

            was_on_EVA = false;
        }

        void OnCrewBoard(GameEvents.FromToAction<Part, Part> data)
        {
            if (aae_airlock_exist)
                aae_airlock.Play();

            was_on_EVA = true;
        }

        void OnVesselChange(Vessel data)
        {
            if (FlightGlobals.ActiveVessel != null)
            {
                vessel = FlightGlobals.ActiveVessel;
                
                if (prev_vessel != null) //prev_vessel = null on first flight load, so check this to avoid EXP throw
                {
                    //active vessel has changed
                    Log.dbg("OnVesselChange() :: prev = {0}, curr = {1}", prev_vessel.vesselName, vessel.vesselName);

                    stop_audio("all");

                    if (use_vessel_settings)
                    {
                        Log.dbg("checking each vessel_id in vessel_settings_node");

                        Log.dbg("Saving previous vessel {0}:{1}", prev_vessel.name, prev_vessel.id);
                        write_vessel_settings(prev_vessel);
                        Log.dbg("OnVesselChange() :: vessel_settings node saved to vessel_settings.cfg");

                        search_vessel_settings_node();  //search for current vessel
                    }

                    prev_vessel = vessel;
                }
                else //Sets these values on first flight load
                {
                    prev_vessel = vessel;

                    if (use_vessel_settings)
                    {
                        Log.dbg("OnVesselChange() FirstLoad :: calling load_vessel_settings_node()");
                        load_vessel_settings_node(); //load and search for settings for this vessel
                        Log.dbg("OnVesselChange() FirstLoad :: calling search_vessel_settings_node()");
                        if (!search_vessel_settings_node())
                        {
                            write_vessel_settings(vessel);
                            search_vessel_settings_node();  //search for current vessel
                        }
                    }
                }
            }
        }

        void OnVesselDestroy(Vessel vessel)
        {
            Log.dbg("OnVesselDestroy() :: {0}:{1}", vessel.vesselName, vessel.vesselName);
            remove_vessel_settings(vessel);
        }

        void OnStageSeparation(EventReport data)
        {
            Log.dbg("beginning exchange, OnStageSeparation");
            begin_exchange(0);
        }

        void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> data)
        {
            if (FlightGlobals.ActiveVessel != null && data.host.isActiveVessel)
            {
                if ((data.host.SituationString == "DOCKED" || data.host.SituationString == "SPLASHED" || data.host.SituationString == "SUB_ORBITAL" || data.host.SituationString == "ORBITING" || data.host.SituationString == "ESCAPING") && sstv.isPlaying == false)
                {
                    if (secs_since_last_exchange > 30.0f)
                    {
                        Log.dbg("beginning exchange, OnVesselSituationChange : {0}", data.host.SituationString);
                        
                        pod_begins_exchange = true;
                        begin_exchange(0);  //for delay try (rand.Next(0, 3)) for 0-2 seconds for randomness
                    }
                    else Log.dbg("prevent spam from situation change, time remaining : {0:0}s.", (30.0f - secs_since_last_exchange));
                }
            }
        }

        void OnVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> data)
        {
            if (data.host.isActiveVessel)
            {
                Log.dbg("beginning exchange, OnVesselSOIChanged : {0}", data.to.bodyName);
                begin_exchange(0);
            }
        }

        void OnScienceChanged(float sci, TransactionReasons scitxreason)
        {
            if (sstv_on_science_toggle && scitxreason == TransactionReasons.VesselRecovery || scitxreason == TransactionReasons.ScienceTransmission)
            {
                science_transmitted = true;

                Log.dbg("Event scienceTX PASS");
            }

            Log.dbg("Event scienceTX triggered, reason : {0}", scitxreason);
        }

        void OnCommHomeStatusChange(Vessel data0, bool data1)
        {
            Log.dbg("OnCommHomeStatusChange : Triggered ");

            if (HighLogic.CurrentGame.Parameters.Difficulty.EnableCommNet == true) // Check if player chose to use CommNet
            {
                if (data1 == true)
                {
                    inRadioContact = true;

                    if (!exchange_playing && !was_on_EVA && data0.isActiveVessel)
                    {
                        Log.dbg("beginning exchange, OnCommHomeStatusChange : We are online ! ");

                        pod_begins_exchange = false;
                        begin_exchange(0);
                    }
                }
                else
                {
                    inRadioContact = false;

                    if (data0.isActiveVessel)
                    {
                        Log.dbg("OnCommHomeStatusChange : We are offline zzzzzzz... ");

                        initial_chatter.PlayOneShot(voidnoise_clip);
                    }
                }
            }
            else inRadioContact = true; // If player doesn't use CommNet assume radio contact is always true

            Log.dbg("OnCommHomeStatusChange() : Vessel : {0}, inRadioContact = {1}", data0, inRadioContact);
        }

        void OnGamePause()
        {
            if (!all_muted) mute_all = true;

            Log.dbg("OnGamePause() : Mute = {0}", mute_all);
        }

        void OnGameUnpause()
        {
            if (all_muted) mute_all = false;

            Log.dbg("OnGameUnpause() : Mute = {0}", mute_all);
        }

        private bool checkChatterGender()
        {
            bool chatter_is_female = false;
			List<ProtoCrewMember> crew = vessel.GetVesselCrew();
            if (crew.Count > 0) chatter_is_female = (ProtoCrewMember.Gender.Female == crew[0].gender);

            if (debugging)
            {
                if (crew.Count == 0) Log.info("No Chatter gender check (no crew in the vicinity)");
                else Log.info("Chatter is female :{0}", chatter_is_female);
            }
            return chatter_is_female;
        }

        internal void OnDestroy() 
        {
            Log.dbg("OnDestroy() START");

            // Remove the button from the Blizzy's toolbar
            if (chatterer_toolbar_button != null)
            {
                chatterer_toolbar_button.Destroy();

                Log.dbg("OnDestroy() Blizzy78's toolbar button removed");
            }

            // Un-register the callbacks
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIApplicationLauncherReady);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneChangeRequest);
            GameEvents.onCrewOnEva.Remove(OnCrewOnEVA);
            GameEvents.onCrewBoardVessel.Remove(OnCrewBoard);
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
            GameEvents.onStageSeparation.Remove(OnStageSeparation);
            GameEvents.onVesselSituationChange.Remove(OnVesselSituationChange);
            GameEvents.onVesselSOIChanged.Remove(OnVesselSOIChanged);

            GameEvents.OnScienceChanged.Remove(OnScienceChanged);
            GameEvents.CommNet.OnCommHomeStatusChange.Remove(OnCommHomeStatusChange);
            GameEvents.onGamePause.Remove(OnGamePause);
            GameEvents.onGameUnpause.Remove(OnGameUnpause);

            // Remove the button from the KSP AppLauncher
            launcherButtonRemove();

            // Stop coroutine n' Exchange
            StopAllCoroutines();
            exchange_playing = false;

            Log.dbg("OnDestroy() END");
        }

        private void OnGUI() //start the GUI
        {
            if (debugging & !gui_running) Log.info("start_GUI()");

            draw_GUI(); 

            gui_running = true;
        }

        private void stop_GUI() //stop the GUI (virtualy, is this actually still needed ?)
        {
            if (debugging) Log.info("stop_GUI()");

            gui_running = false;
        }

        private void set_gui_styles()
        {
            label_txt_left = new GUIStyle(GUI.skin.label);
            //label_txt_left.normal.textColor = Color.white;
            label_txt_left.normal.textColor = Color.white;
            label_txt_left.alignment = TextAnchor.MiddleLeft;

            label_txt_center = new GUIStyle(GUI.skin.label);
            //label_txt_center.normal.textColor = Color.white;
            label_txt_center.normal.textColor = Color.white;
            label_txt_center.alignment = TextAnchor.MiddleCenter;

            label_txt_right = new GUIStyle(GUI.skin.label);
            label_txt_right.normal.textColor = Color.white;
            label_txt_right.alignment = TextAnchor.MiddleRight;

            label_txt_bold = new GUIStyle(GUI.skin.label);
            label_txt_bold.normal.textColor = Color.white;
            label_txt_bold.fontStyle = FontStyle.Bold;
            label_txt_bold.alignment = TextAnchor.MiddleLeft;

            label_txt_red_center = new GUIStyle(GUI.skin.label);
            label_txt_red_center.normal.textColor = Color.white;
            label_txt_red_center.alignment = TextAnchor.MiddleCenter;

            button_txt_left = new GUIStyle(GUI.skin.button);
            button_txt_left.normal.textColor = Color.white;
            button_txt_left.alignment = TextAnchor.MiddleLeft;

            button_txt_right = new GUIStyle(GUI.skin.button);
            button_txt_right.normal.textColor = Color.white;
            button_txt_right.alignment = TextAnchor.MiddleRight;

            button_txt_center = new GUIStyle(GUI.skin.button);
            button_txt_center.normal.textColor = Color.white;
            button_txt_center.alignment = TextAnchor.MiddleCenter;

            //button_txt_center_green = new GUIStyle(GUI.skin.button);
            //button_txt_center_green.normal.textColor = button_txt_center_green.hover.textColor = button_txt_center_green.active.textColor = button_txt_center_green.focused.textColor = Color.green;
            //button_txt_center_green.alignment = TextAnchor.MiddleCenter;

            gs_tooltip = new GUIStyle(GUI.skin.box);
            gs_tooltip.normal.background = GUI.skin.window.normal.background;
            gs_tooltip.normal.textColor = XKCDColors.LightGrey;
            gs_tooltip.fontSize = 11;

            button_txt_left_bold = new GUIStyle(GUI.skin.button);
            button_txt_left_bold.normal.textColor = Color.white;
            button_txt_left_bold.fontStyle = FontStyle.Bold;
            button_txt_left_bold.alignment = TextAnchor.MiddleLeft;

            //xkcd_label = new GUIStyle(GUI.skin.label);
            //xkcd_label.normal.textColor = Color.white;
            //xkcd_label.alignment = TextAnchor.MiddleLeft;


            //reset_menu_gs();
            //if (active_menu == "sliders") gs_menu_sliders = button_txt_center_green;
            //if (active_menu == "audiosets") gs_menu_audiosets = button_txt_center_green;
            //if (active_menu == "remotetech") gs_menu_remotetech = button_txt_center_green;
            //if (active_menu == "settings") gs_menu_settings = button_txt_center_green;

            //reset_beep_gs();
            //gs_beep1 = button_txt_center_green;

            gui_styles_set = true;

            Log.dbg("GUI styles set");
        }

        private void build_skin_list()
        {
            GUISkin[] skin_array = Resources.FindObjectsOfTypeAll(typeof(GUISkin)) as GUISkin[];
            g_skin_list = new List<GUISkin>();

            foreach (GUISkin _skin in skin_array)
            {
                // Some skins just don't look good here so skip them
                if (_skin.name != "PlaqueDialogSkin"
                    && _skin.name != "FlagBrowserSkin"
                    && _skin.name != "SSUITextAreaDefault"
                    && _skin.name != "ExperimentsDialogSkin"
                    && _skin.name != "ExpRecoveryDialogSkin"
                    && _skin.name != "PartTooltipSkin"
                    // Third party known skin mess up
                    && _skin.name != "UnityWKSPButtons"
                    && _skin.name != "Unity"
                    && _skin.name != "Default"
                    // Dupes
                    && _skin.name != "GameSkin"
                    && _skin.name != "GameSkin(Clone)"
                    && _skin.name != "KSP window 4"
                    && _skin.name != "KSP window 6"
                    && _skin.name != "KSP window 7"
                   )
                {
                    // Build wanted skin only list
                    g_skin_list.Add(_skin);
                }
            }

            Log.dbg("skin list built, count = {0}", g_skin_list.Count);
        }

        protected void draw_GUI()
        {
            //Apply a skin
            if (skin_index > g_skin_list.Count) skin_index = 0;
            else if (skin_index == 0) GUI.skin = null;
            else GUI.skin = g_skin_list[skin_index - 1];

            if (gui_styles_set == false) set_gui_styles();  //run this once to set a few GUIStyles

            int window_id = window_base_id;

            //main window
            if (hide_all_windows == false) main_window_pos = GUILayout.Window(window_id, main_window_pos, main_gui, main_window_title + this_version, GUILayout.Height(10f), GUILayout.Width(280f));

            //probe sample selector
            probe_sample_selector_window_id = ++window_id;
            if (hide_all_windows == false && show_probe_sample_selector) probe_sample_selector_window_pos = GUILayout.Window(probe_sample_selector_window_id, probe_sample_selector_window_pos, probe_sample_selector_gui, "Sample Selector", GUILayout.Height(350f), GUILayout.Width(280f));

            //Background sample
            AAE_background_sample_selector_window_id = ++window_id;
            if (hide_all_windows == false && show_AAE_background_sample_selector) AAE_background_sample_selector_window_pos = GUILayout.Window(AAE_background_sample_selector_window_id, AAE_background_sample_selector_window_pos, AAE_background_sample_selector_gui, "Background Sample Selector", GUILayout.Height(350f), GUILayout.Width(280f));

            //lab window
            lab_window_id = ++window_id;
            if (hide_all_windows == false && show_lab_gui) lab_window_pos = GUILayout.Window(lab_window_id, lab_window_pos, testing_gui, "The Lab", GUILayout.Height(10f), GUILayout.Width(300f));

            //chatter filters
            chatter_filter_settings_window_id = ++window_id;
            if (hide_all_windows == false && show_chatter_filter_settings)
            {
                chatter_filter_settings_window_pos = GUILayout.Window(chatter_filter_settings_window_id, chatter_filter_settings_window_pos, chatter_filter_settings_gui, "Chatter Filters", GUILayout.Height(10f), GUILayout.Width(280f));
            }

            //beep filters
            foreach (BeepSource source in beepsource_list)
            {
                source.settings_window_id = ++window_id;
                if (hide_all_windows == false && source.show_settings_window) source.settings_window_pos = GUILayout.Window(source.settings_window_id, source.settings_window_pos, beep_filter_settings_gui, "Beep " + source.beep_name + " Filters", GUILayout.Height(10f), GUILayout.Width(280f));
            }
        }

        private void main_gui(int window_id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            //Show "Chatter" menu button
            if (chatter_exists && vessel.GetCrewCount() > 0)
            {
                if (GUILayout.Button("Chatter"))
                {
                    menu = "chatter";
                }
            }

            //Show "Beeps" button
            if (beeps_exists || sstv_exists)
            {
                if (GUILayout.Button("Beeps"))
                {
                    menu = "beeps";
                }
            }

            //Show "AAE" button
            if (aae_backgrounds_exist || aae_soundscapes_exist || aae_breathing_exist || aae_airlock_exist)
            {
                if (GUILayout.Button("AAE"))
                {
                    menu = "AAE";
                }
            }

            //Show "Settings"
            if (GUILayout.Button("Settings")) menu = "settings";

            //Mute button
            string muted = "Mute";
            if (mute_all) muted = "Muted";

            if (GUILayout.Button(muted, GUILayout.ExpandWidth(false)))
            {
                mute_all = !mute_all;
                
                Log.dbg("Mute = {0}", mute_all);
            }

            string closeUI = "Close";
            if (GUILayout.Button(closeUI, GUILayout.ExpandWidth(false)))
            {
                if (launcherButton == null && ToolbarManager.ToolbarAvailable)
                {
                    UIToggle();
                }
                else if (launcherButton != null)
                {
                    launcherButton.SetFalse();
                }
            }
            
            GUILayout.EndHorizontal();

            //Separator
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            GUILayout.Label(line_512x4, GUILayout.ExpandWidth(false), GUILayout.Width(275f), GUILayout.Height(10f));
            GUILayout.EndHorizontal();

            //Display GUI accordingly
            if (menu == "chatter" && vessel.GetCrewCount() > 0) chatter_gui();
            else if (menu == "beeps") beeps_gui();
            else if (menu == "AAE") AAE_gui();
            else if (menu == "settings") settings_gui();
            else beeps_gui();

            //Tooltips
            if (show_tooltips && GUI.tooltip != "") tooltips(main_window_pos);

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void chatter_gui()
        {
            GUIContent _content = new GUIContent();

            //Chatter frequency
            chatter_freq = Convert.ToInt32(Math.Round(chatter_freq_slider));
            string chatter_freq_str = "";
            if (chatter_freq == 0) chatter_freq_str = "No chatter";
            else
            {
                if (chatter_freq == 1) chatter_freq_str = "180-300s";
                else if (chatter_freq == 2) chatter_freq_str = "90-180s";
                else if (chatter_freq == 3) chatter_freq_str = "60-90s";
                else if (chatter_freq == 4) chatter_freq_str = "30-60s";
                else if (chatter_freq == 5) chatter_freq_str = "10-30s";
            }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Chatter frequency: " + chatter_freq_str;
            _content.tooltip = "How often chatter will play";
            GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
            chatter_freq_slider = GUILayout.HorizontalSlider(chatter_freq_slider, 0, 5f, GUILayout.Width(100f));
            GUILayout.EndHorizontal();

            if (chatter_freq != prev_chatter_freq)
            {
                Log.dbg("chatter_freq has changed, setting new delay between exchanges...");
                if (chatter_freq == 0)
                {
                    exchange_playing = false;
                }
                secs_since_last_exchange = 0;
                set_new_delay_between_exchanges();
                prev_chatter_freq = chatter_freq;
            }

            //Chatter volume
            _content.text = String.Format("Chatter volume: {0:0}%", (chatter_vol_slider * 100));
            _content.tooltip = "Volume of chatter audio";
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
            chatter_vol_slider = GUILayout.HorizontalSlider(chatter_vol_slider, 0, 1f, GUILayout.Width(130f));
            GUILayout.EndHorizontal();

            if (chatter_vol_slider != prev_chatter_vol_slider)
            {
                Log.dbg("Changing chatter AudioSource volume...");
                initial_chatter.volume = chatter_vol_slider;
                response_chatter.volume = chatter_vol_slider;
                prev_chatter_vol_slider = chatter_vol_slider;
            }

            //Quindar
            _content.text = "Quindar volume: " + (quindar_vol_slider * 100).ToString("F0") + "%";
            _content.tooltip = "Volume of beeps before and after chatter";
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
            quindar_vol_slider = GUILayout.HorizontalSlider(quindar_vol_slider, 0, 1f, GUILayout.Width(130f));
            GUILayout.EndHorizontal();

            if (quindar_vol_slider != prev_quindar_vol_slider)
            {
                Log.dbg("Quindar volume has been changed...");
                quindar1.volume = quindar_vol_slider;
                quindar2.volume = quindar_vol_slider;
                prev_quindar_vol_slider = quindar_vol_slider;
            }

            if (show_advanced_options)
            {
                //Chatter sets
                _content.text = "ChatterSets";
                _content.tooltip = "Show currently loaded chatter audio";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) show_chatter_sets = !show_chatter_sets;
                GUILayout.Label("", GUILayout.ExpandWidth(true));    //spacer
                _content.text = "Filters";
                _content.tooltip = "Adjust filters for chatter audio";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    show_chatter_filter_settings = !show_chatter_filter_settings;
                }
                GUILayout.EndHorizontal();

                if (show_chatter_sets)
                {
                    for (int i = 0; i < chatter_array.Count; i++)
                    {
                        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                        ChatterAudioList cal = chatter_array[i];
                        bool temp = cal.is_active;

                        _content.text = cal.directory + " (" + (cal.capcom.Count + cal.capsule.Count + cal.capsuleF.Count).ToString() + " clips)";
                        if (cal.capsuleF.Count > 0) _content.text = _content.text + " (Female set in)";
                        _content.tooltip = "Toggle this chatter set on/off";
                        cal.is_active = GUILayout.Toggle(cal.is_active, _content, GUILayout.ExpandWidth(true));
                        _content.text = "Remove";
                        _content.tooltip = "Remove this chatter set from the list";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            //Remove this set
                            chatter_array.RemoveAt(i);
                            load_chatter_audio();
                            load_toggled_chatter_sets();    //reload toggled audio clips
                            break;
                        }

                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                    custom_dir_name = GUILayout.TextField(custom_dir_name, GUILayout.Width(150f));
                    GUILayout.Label("", GUILayout.ExpandWidth(true));   //spacer
                    _content.text = "Load";
                    _content.tooltip = "Try to load chatter set with this name";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        bool already_loaded = false;
                        foreach (ChatterAudioList r in chatter_array)
                        {
                            //check if this set is already loaded
                            already_loaded = (custom_dir_name == r.directory);
                            if (already_loaded) break;
                        }

                        if (custom_dir_name.Trim() != "" && custom_dir_name != "directory name" && !already_loaded)
                        {
							//set name isn't blank, "directory name", or already loaded.  load it.
							ChatterAudioList cal = new ChatterAudioList
							{
								directory = custom_dir_name.Trim(),
								is_active = true
							};
							chatter_array.Add(cal);

                            //reset custom_dir_name
                            custom_dir_name = "directory name";
                            //reload audio
                            load_chatter_audio();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void beeps_gui()
        {
            GUIContent _content = new GUIContent();

            //Beeps
            if (beeps_exists)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                if (show_advanced_options)
                {
                    //Decrease beepsources
                    _content.text = "Rmv";
                    _content.tooltip = "Remove the last beepsource";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        if (beepsource_list.Count > 1)
                        {
                            //remove a beepsource
                            Log.dbg("num_sources = {0}", beepsource_list.Count);

                            Destroy(beepsource_list[beepsource_list.Count - 1].beep_player);   //destroy GameObject holding Source and Filters
                            Log.dbg("beep_player destroyed");

                            Log.dbg("attempting to remove BeepSource at index {0}", (beepsource_list.Count - 1));

                            beepsource_list.RemoveAt(beepsource_list.Count - 1);  //remove the last BeepSource from the list

                            Log.dbg("BeepSource at index {0} removed from beepsource_list", beepsource_list.Count);



                            //line below is a problem
                            // sel_beep_src can only be 0-9
                            //set = 0 whenever it is lowered until a more elegant solution can be found
                            //RBRBeepSource bm = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];


                            //if (sel_beep_src == beepsource_list.Count) sel_beep_src = beepsource_list.Count - 1;    //if selected source was just removed, set it to highest available
                            sel_beep_src = 0;

                            //beepsources have decreased, check if sel_page index is out of range
                            num_beep_pages = beepsource_list.Count / 10;
                            if (beepsource_list.Count % 10 != 0) num_beep_pages++;

                            if (num_beep_pages != prev_num_pages)
                            {

                                //last page is no longer needed in the grid
                                //set sel_page to the new last page if it is out of range
                                if (sel_beep_page > num_beep_pages) sel_beep_page = num_beep_pages;
                                //set sel_source to 0
                                sel_beep_src = 0;
                                prev_num_pages = num_beep_pages;
                            }
                        }
                    }
                    
                    if (num_beep_pages > 1)
                    {
                        _content.text = "◄";
                        _content.tooltip = "Previous page";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            sel_beep_page--;

                            if (sel_beep_page < 1)
                            {
                                sel_beep_page = 1;
                                Log.dbg("this is the first page");
                            }
                            else
                            {
                                sel_beep_src = 0;
                                Log.dbg("page back");
                            }
                        }
                    }
                }

                //Beep selection grid
                List<string> sources = new List<string>();
                foreach (BeepSource b in beepsource_list)
                {

                    //when sel_page = 1, want to add 1-10
                    //when sel_page = 2, want to add 11-20

                    //min = ((sel_page - 1) * 10) + 1
                    //max <= sel_page * 10

                    int beep_num = Int32.Parse(b.beep_name);

                    if (beep_num >= ((sel_beep_page - 1) * 10) + 1 && beep_num <= sel_beep_page * 10)
                    {
                        sources.Add(b.beep_name);
                    }
                }

                //GUIContent[] _content_array = sources.ToArray();
                //
                //GUIContent[] asset_list = { new GUIContent("Wallpaper", "Change Wallpaper"), new GUIContent("Floor", "Change Floor"), new GUIContent("Light", "Switch Light") };


                string[] s = sources.ToArray();
                int sel_grid_width = 5;
                if (sources.Count < 5) sel_grid_width = sources.Count;

                sel_beep_src = GUILayout.SelectionGrid(sel_beep_src, s, sel_grid_width, GUILayout.ExpandWidth(true));
                //Log.dbg("grid OK");

                if (show_advanced_options)
                {
                    //page next
                    if (num_beep_pages > 1)
                    {
                        _content.text = "►";
                        _content.tooltip = "Next page";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            sel_beep_page++;

                            if (sel_beep_page > num_beep_pages)
                            {
                                sel_beep_page = num_beep_pages;
                                Log.dbg("this is the last page");
                            }
                            else
                            {
                                sel_beep_src = 0;
                                Log.dbg("page next");
                            }
                        }
                    }

                    //Increase beepsources
                    _content.text = "Add";
                    _content.tooltip = "Add a new beepsource";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        add_new_beepsource();
                        Log.dbg("new BeepSource added");
                        save_plugin_settings();



                        //when adding a new source that will create a new page, change to that page and set sel_beep to 0


                        //beepsources have increased, check if a new page is needed
                        num_beep_pages = beepsource_list.Count / 10;
                        if (beepsource_list.Count % 10 != 0) num_beep_pages++;

                        if (num_beep_pages != prev_num_pages)
                        {

                            //a new page is needed in the grid
                            //set sel_page to the new page
                            //sel_beep_page = num_beep_pages;
                            //set sel_source to 0
                            //sel_beep_src = 0;
                            prev_num_pages = num_beep_pages;
                        }
                    }
                }
                GUILayout.EndHorizontal();

                //Log.dbg("beepsource_list.Count = " + beepsource_list.Count);
                //Log.dbg("num_beep_pages = " + num_beep_pages);
                //Log.dbg("sel_beep_page = " + sel_beep_page);
                //Log.dbg("sel_beep_src = " + sel_beep_src);
                //Log.dbg("beepsource_list index [((sel_beep_page - 1) * 10) + sel_beep_src] = " + (((sel_beep_page - 1) * 10) + sel_beep_src));

                BeepSource bm = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];   //shortcut   0-9 only, but correspond to the correct beepsource

                //Log.dbg("shortcut OK");



                if (bm.precise)
                {
                    //show exact slider
                    bm.precise_freq = Convert.ToInt32(Math.Round(bm.precise_freq_slider));
                    string beep_freq_str = "";
                    if (bm.precise_freq == -1) beep_freq_str = "No beeps";
                    else if (bm.precise_freq == 0) beep_freq_str = "Loop";
                    else beep_freq_str = "Every " + bm.precise_freq.ToString() + "s";

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    _content.text = "Beep frequency: " + beep_freq_str;
                    _content.tooltip = "How often this beepsource will play";
                    GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                    bm.precise_freq_slider = GUILayout.HorizontalSlider(bm.precise_freq_slider, -1f, 60f, GUILayout.Width(100f));
                    GUILayout.EndHorizontal();

                    if (bm.precise_freq != bm.prev_precise_freq)
                    {
                        Log.dbg("precise_freq has changed, resetting beep_timer...");
                        bm.timer = 0;
                        bm.prev_precise_freq = bm.precise_freq;
                        if (bm.precise_freq == 0 && bm.current_clip == "Random")
                        {
                            //frequency has changed to looped mode
                            //current clip == random
                            //not allowed, too silly
                            bm.current_clip = "Default";
                        }
                    }
                }
                else
                {
                    //show loose slider
                    bm.loose_freq = Convert.ToInt32(Math.Round(bm.loose_freq_slider));
                    string beep_freq_str = "";
                    if (bm.loose_freq == 0) beep_freq_str = "No beeps";
                    else
                    {
                        if (bm.loose_freq == 1) beep_freq_str = "120-300s";
                        else if (bm.loose_freq == 2) beep_freq_str = "60-120s";
                        else if (bm.loose_freq == 3) beep_freq_str = "30-60s";
                        else if (bm.loose_freq == 4) beep_freq_str = "15-30s";
                        else if (bm.loose_freq == 5) beep_freq_str = "5-15s";
                        else if (bm.loose_freq == 6) beep_freq_str = "1-5s";
                    }

                    _content.text = "Beep frequency: " + beep_freq_str;
                    _content.tooltip = "How often this beepsource will play";
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                    bm.loose_freq_slider = GUILayout.HorizontalSlider(bm.loose_freq_slider, 0, 6f, GUILayout.Width(100f));
                    GUILayout.EndHorizontal();

                    if (bm.loose_freq != bm.prev_loose_freq)
                    {
                        Log.dbg("loose_freq has changed, resetting beep_timer...");
                        new_beep_loose_timer_limit(bm);
                        bm.timer = 0;
                        bm.prev_loose_freq = bm.loose_freq;
                    }
                }

                //Volume
                _content.text = String.Format("Beep volume: {0:0}%", (bm.audiosource.volume * 100));
                _content.tooltip = "Volume of this beepsource";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                bm.audiosource.volume = GUILayout.HorizontalSlider(bm.audiosource.volume, 0, 1f, GUILayout.Width(130f));
                GUILayout.EndHorizontal();

                //Pitch
                _content.text = "Beep pitch: " + (bm.audiosource.pitch * 100).ToString("F0") + "%";
                _content.tooltip = "Pitch of this beepsource";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                bm.audiosource.pitch = GUILayout.HorizontalSlider(bm.audiosource.pitch, 0.1f, 5f, GUILayout.Width(130f));
                GUILayout.EndHorizontal();

                //Beep timing
                string beep_timing_str = "Loose";
                if (bm.precise) beep_timing_str = "Precise";


                _content.text = beep_timing_str;
                _content.tooltip = "Switch between timing modes";
                GUILayout.BeginHorizontal();
                GUILayout.Label("Beep timing:");
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    //timing mode is being switched
                    bm.precise = !bm.precise;
                    bm.timer = 0;
                    bm.audiosource.loop = false;
                    bm.audiosource.Stop();

                    if (bm.precise)
                    {
                        Log.dbg("beep timing mode has changed to precise");
                        if (bm.current_clip == "Random" && bm.precise_freq == 0)
                        {
                            //disallow random looped clips
                            bm.current_clip = "Default";
                        }
                        set_beep_clip(bm);
                    }
                    else new_beep_loose_timer_limit(bm);   //set new loose time limit
                }
                GUILayout.Label("", GUILayout.ExpandWidth(true));
                GUILayout.Label("", GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                // Separator
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                GUILayout.Label(line_512x4, GUILayout.ExpandWidth(false), GUILayout.Width(275f), GUILayout.Height(10f));
                GUILayout.EndHorizontal();

                //Sample selector
                GUILayout.BeginHorizontal();
                _content.text = bm.current_clip;
                _content.tooltip = "Click to change the current beep sample";
                GUILayout.Label("Beep sample:", GUILayout.ExpandWidth(false));
                if (!bm.randomizeBeep)
                {
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) show_probe_sample_selector = !show_probe_sample_selector;
                }
                else GUILayout.Label(_content, GUILayout.ExpandWidth(false));

                //Toggle for Random beep setting
                _content.text = "Random";
                _content.tooltip = "Play Probe sample files randomly";
                GUILayout.Label("", GUILayout.ExpandWidth(true));
                bm.randomizeBeep = GUILayout.Toggle(bm.randomizeBeep, _content, GUILayout.ExpandWidth(false));

                GUILayout.EndHorizontal();

                if (show_advanced_options)
                {
                    //Add copy/paste single beepsource
                    //Add copy all/paste all beepsources

                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                    _content.text = "Copy";
                    _content.tooltip = "Copy beepsource to clipboard";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        //copy this beepsource values to beepsource_clipboard
                        copy_beepsource_values(bm);
                    }
                    if (beepsource_clipboard != null)
                    {
                        _content.text = "Paste";
                        _content.tooltip = "Paste beepsource from clipboard";
                        if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                        {
                            //paste beepsource_clipboard values to this beepsource
                            paste_beepsource_values(bm);
                        }
                    }

                    //Filters
                    GUILayout.Label("", GUILayout.ExpandWidth(true));    //spacer to align "Filters" to the right
                    _content.text = "Filters";
                    _content.tooltip = "Open filter settings window for this beepsource";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        bm.show_settings_window = !bm.show_settings_window;
                    }

                    GUILayout.EndHorizontal();
                }
            }

            //line to separate when both exist
            if (beeps_exists && sstv_exists)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                GUILayout.Label(line_512x4, GUILayout.ExpandWidth(false), GUILayout.Width(275f), GUILayout.Height(10f));
                GUILayout.EndHorizontal();
            }

            //SSTV
            if (sstv_exists)
            {
                sstv_freq = Convert.ToInt32(Math.Round(sstv_freq_slider));
                string sstv_freq_str = "";
                if (sstv_freq == 0) sstv_freq_str = "No SSTV";
                else
                {
                    if (sstv_freq == 1) sstv_freq_str = "1800-3600s";
                    else if (sstv_freq == 2) sstv_freq_str = "600-1800s";
                    else if (sstv_freq == 3) sstv_freq_str = "300-600s";
                    else if (sstv_freq == 4) sstv_freq_str = "120-300s";
                }

                _content.text = "SSTV frequency: " + sstv_freq_str;
                _content.tooltip = "How often SSTV will play";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                sstv_freq_slider = GUILayout.HorizontalSlider(sstv_freq_slider, 0, 4f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();

                if (sstv_freq != prev_sstv_freq)
                {
                    Log.dbg("sstv_freq has changed, setting new sstv timer limit...");
                    if (sstv_freq == 0) sstv.Stop();
                    else new_sstv_loose_timer_limit();
                    sstv_timer = 0;
                    prev_sstv_freq = sstv_freq;
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "SSTV on science transmitted";
                _content.tooltip = "Makes science yielling noises";
                sstv_on_science_toggle = GUILayout.Toggle(sstv_on_science_toggle, _content);
                GUILayout.EndHorizontal();

                _content.text = "SSTV volume: " + (sstv_vol_slider * 100).ToString("F0") + "%";
                _content.tooltip = "Volume of SSTV source";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                sstv_vol_slider = GUILayout.HorizontalSlider(sstv_vol_slider, 0, 1f, GUILayout.Width(130f));
                GUILayout.EndHorizontal();

                if (sstv_vol_slider != prev_sstv_vol_slider)
                {
                    Log.dbg("Changing SSTV AudioSource volume...");
                    sstv.volume = sstv_vol_slider;
                    prev_sstv_vol_slider = sstv_vol_slider;
                }
            }
        }

        private void AAE_gui()
        {
            GUIContent _content = new GUIContent();
            string truncated;   //truncate file names because some are stupid long

            if (aae_backgrounds_exist)
            {
                int i = 1;
                foreach (BackgroundSource src in backgroundsource_list)
                {
                    _content.text = "Background " + i + " volume: " + (src.audiosource.volume * 100).ToString("F0") + "%";
                    _content.tooltip = "Volume level for this Background audio";
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                    src.audiosource.volume = GUILayout.HorizontalSlider(src.audiosource.volume, 0, 1f, GUILayout.Width(100f));
                    GUILayout.EndHorizontal();

                    if (src.current_clip.Length > 30) truncated = src.current_clip.Substring(0, 27) + "...";
                    else truncated = src.current_clip;
                    _content.text = truncated;
                    _content.tooltip = "Click to change the selected sample";
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("Sample:");
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                    {
                        sel_background_src = src;
                        //show_sample_selector = !show_sample_selector;
                        show_AAE_background_sample_selector = !show_AAE_background_sample_selector;
                    }
                    GUILayout.EndHorizontal();
                    i++;
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Background sounds only in IVA";
                _content.tooltip = "Play only when in internal view";
                aae_backgrounds_onlyinIVA = GUILayout.Toggle(aae_backgrounds_onlyinIVA, _content);
                GUILayout.EndHorizontal();
            }

            //line to separate
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            GUILayout.Label(line_512x4, GUILayout.ExpandWidth(false), GUILayout.Width(275f), GUILayout.Height(10f));
            GUILayout.EndHorizontal();

            //EVA breathing
            if (aae_breathing_exist)
            {
                _content.text = "Breath volume: " + (aae_breathing.volume * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for EVA breathing";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_breathing.volume = GUILayout.HorizontalSlider(aae_breathing.volume, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();
            }

            //Airlock
            if (aae_airlock_exist)
            {
                _content.text = "Airlock volume: " + (aae_airlock.volume * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for Airlock";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_airlock.volume = GUILayout.HorizontalSlider(aae_airlock.volume, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();
            }

            //Wind
            if (aae_wind_exist)
            {
                _content.text = "Wind volume: " + (aae_wind_vol_slider * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for surface wind";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_wind_vol_slider = GUILayout.HorizontalSlider(aae_wind_vol_slider, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();
            }

            //Soundscape
            if (aae_soundscapes_exist)
            {
                _content.text = "Soundscape volume: " + (aae_soundscape.volume * 100).ToString("F0") + "%";
                _content.tooltip = "Volume level for Soundscapes";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, GUILayout.ExpandWidth(true));
                aae_soundscape.volume = GUILayout.HorizontalSlider(aae_soundscape.volume, 0, 1f, GUILayout.Width(100f));
                GUILayout.EndHorizontal();

                aae_soundscape_freq = Convert.ToInt32(Math.Round(aae_soundscape_freq_slider));
                string soundscape_freq_str = "";
                if (aae_soundscape_freq == 0) soundscape_freq_str = "Disabled";
                else
                {
                    if (aae_soundscape_freq == 1) soundscape_freq_str = "5-10 min";
                    else if (aae_soundscape_freq == 2) soundscape_freq_str = "2-5 min";
                    else if (aae_soundscape_freq == 3) soundscape_freq_str = "1-2 min";
                    else if (aae_soundscape_freq == 4) soundscape_freq_str = "Continuous";
                }

                _content.text = "Soundscape frequency: " + soundscape_freq_str;
                _content.tooltip = "How often soundscapes will play";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                aae_soundscape_freq_slider = GUILayout.HorizontalSlider(aae_soundscape_freq_slider, 0, 4f, GUILayout.Width(60f));
                GUILayout.EndHorizontal();

                if (aae_soundscape_freq != aae_prev_soundscape_freq)
                {
                    if (aae_soundscape_freq == 0)
                    {
                        //soundscape turned off
                        aae_soundscape.Stop();
                    }
                    if (aae_soundscape_freq == 4)
                    {
                        //if freq = 4, continuous play of soundscape

                    }
                    else
                    {
                        Log.dbg("setting new soundscape1 timer limit...");
                        new_soundscape_loose_timer_limit();
                        aae_soundscape_timer = 0;
                    }

                    aae_prev_soundscape_freq = aae_soundscape_freq;
                }

                //Curently playing soundscape
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Sample: ");
                if (aae_soundscape_current_clip.Length > 30) truncated = aae_soundscape_current_clip.Substring(0, 27) + "...";
                else truncated = aae_soundscape_current_clip;
                _content.text = truncated;
                _content.tooltip = "Click to skip to a new random soundscape";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    set_soundscape_clip();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void settings_gui()
        {
            GUIContent _content = new GUIContent();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Reset default settings";
            _content.tooltip = "Reset all chatterer settings to default";
            if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) load_plugin_defaults();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Debug Mode";
            _content.tooltip = "Spam the log with more or less usefull reports";
            debugging = GUILayout.Toggle(debugging, _content);
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Use per-vessel settings";
            _content.tooltip = "Every vessel will keep its own individual settings";
            use_vessel_settings = GUILayout.Toggle(use_vessel_settings, _content);
            GUILayout.EndHorizontal();

            if (use_vessel_settings != prev_use_vessel_settings)
            {
                //setting has just changed
                if (use_vessel_settings)
                {
                    //just toggled on, load stuff
                    Log.dbg("settings_gui() :: calling load_vessel_settings_node()");
                    load_vessel_settings_node(); //load and search for settings for this vessel
                    Log.dbg("settings_gui() :: calling search_vessel_settings_node()");
                    search_vessel_settings_node();
                }
                prev_use_vessel_settings = use_vessel_settings;
            }

            if (ToolbarManager.ToolbarAvailable)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                _content.text = "Use Blizzy78's toolbar only";
                _content.tooltip = "Hide stock Applaunch button";
                useBlizzy78Toolbar = GUILayout.Toggle(useBlizzy78Toolbar, _content);
                if (useBlizzy78Toolbar && launcherButton != null) launcherButtonRemove();
                if (!useBlizzy78Toolbar && launcherButton == null) OnGUIApplicationLauncherReady();
                GUILayout.EndHorizontal();
            }

            //_content.text = "Enable RemoteTech integration";
            //_content.tooltip = "Disable/Delay comms with KSC accordingly";
            //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //remotetech_toggle = GUILayout.Toggle(remotetech_toggle, _content);
            //GUILayout.EndHorizontal();

            //if (remotetech_toggle)
            //{
            //    GUIStyle txt_green = new GUIStyle(GUI.skin.label);
            //    txt_green.normal.textColor = txt_green.focused.textColor = Color.green;
            //    txt_green.alignment = TextAnchor.UpperLeft;
            //    GUIStyle txt_red = new GUIStyle(GUI.skin.label);
            //    txt_red.normal.textColor = txt_red.focused.textColor = Color.red;
            //    txt_red.alignment = TextAnchor.UpperLeft;

            //    string has_RT_SPU = "not found";
            //    GUIStyle has_RT_text = txt_red;
            //    if (hasRemoteTech)
            //    {
            //        has_RT_SPU = "found";
            //        has_RT_text = txt_green;
            //    }

            //    //string rt_Satteliteconnected = "Not connected to Sattelite network";
            //    //GUIStyle RT_Satteliteradio_contact_text = txt_red;
            //    //if (inSatteliteRadioContact)
            //    //{
            //    //    rt_Satteliteconnected = "Connected to Sattelite network";
            //    //    RT_Satteliteradio_contact_text = txt_green;
            //    //}

            //    string rt_connected = "Not connected to KSC";
            //    GUIStyle RT_radio_contact_text = txt_red;
            //    if (inRadioContact)
            //    {
            //        rt_connected = "Connected to KSC, delay : " + Convert.ToSingle(controlDelay) +" secs.";
            //        RT_radio_contact_text = txt_green;
            //    }

            //    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //    GUILayout.Label("RemoteTech SPU " + has_RT_SPU, has_RT_text);
            //    GUILayout.EndHorizontal();

            //    //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //    //GUILayout.Label(rt_Satteliteconnected, RT_Satteliteradio_contact_text);
            //    //GUILayout.EndHorizontal();

            //    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //    GUILayout.Label(rt_connected, RT_radio_contact_text);
            //    GUILayout.EndHorizontal();
            //}

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            _content.text = "Show tooltips";
            _content.tooltip = "It does something";
            show_tooltips = GUILayout.Toggle(show_tooltips, _content);
            GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            //_content.text = "Disable ElectricCharge usage";
            //_content.tooltip = "Plugin will not use any ElectricCharge";
            //disable_power_usage = GUILayout.Toggle(disable_power_usage, _content);
            //GUILayout.EndHorizontal();

            if (vessel.GetCrewCount() > 0)
            {
                _content.text = "Disable beeps during chatter";
                _content.tooltip = "Stop beeps from beeping while chatter is playing";
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                disable_beeps_during_chatter = GUILayout.Toggle(disable_beeps_during_chatter, _content);
                GUILayout.EndHorizontal();
                
                //The Lab
                //GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                //show_lab_gui = GUILayout.Toggle(show_lab_gui, "The Lab");
                //GUILayout.EndHorizontal();
            }

            // Allowing "advanced options" even if crew < 0
            _content.text = "Show advanced options";
            _content.tooltip = "More chatter and beep options are displayed";
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            show_advanced_options = GUILayout.Toggle(show_advanced_options, _content);
            GUILayout.EndHorizontal();

            if (vessel.GetCrewCount() > 0)
            {
                //Insta-chatter key
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (set_insta_chatter_key == false)
                {
                    _content.text = "Insta-chatter key: " + insta_chatter_key.ToString();
                    _content.tooltip = "Press this key to play chatter now";
                    GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                    _content.text = "Change";
                    _content.tooltip = "Select a new insta-chatter key";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) set_insta_chatter_key = true;

                    _content.text = "Clear";
                    _content.tooltip = "Clear insta-chatter key";
                    if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) insta_chatter_key = KeyCode.None;
                }
                GUILayout.EndHorizontal();

                if (set_insta_chatter_key)
                {
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                    GUILayout.Label("Press new Insta-chatter key...", label_txt_left);
                    GUILayout.EndHorizontal();
                }

                if (set_insta_chatter_key && Event.current.isKey)
                {
                    insta_chatter_key = Event.current.keyCode;
                    set_insta_chatter_key = false;
                    insta_chatter_key_just_changed = true;
                }
            }

            //Insta-SSTV key
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            if (set_insta_sstv_key == false)
            {
                _content.text = "Insta-SSTV key: " + insta_sstv_key.ToString();
                _content.tooltip = "Press this key to play SSTV now";
                GUILayout.Label(_content, label_txt_left, GUILayout.ExpandWidth(true));
                _content.text = "Change";
                _content.tooltip = "Select a new insta-SSTV key";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) set_insta_sstv_key = true;

                _content.text = "Clear";
                _content.tooltip = "Clear insta-SSTV key";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false))) insta_sstv_key = KeyCode.None;
            }
            GUILayout.EndHorizontal();

            if (set_insta_sstv_key)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label("Press new Insta-SSTV key...", label_txt_left);
                GUILayout.EndHorizontal();
            }

            if (set_insta_sstv_key && Event.current.isKey)
            {
                insta_sstv_key = Event.current.keyCode;
                set_insta_sstv_key = false;
                insta_sstv_key_just_changed = true;
            }

            //Skin picker
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            _content.text = "◄";
            _content.tooltip = "Select previous skin";
            if (GUILayout.Button(_content, GUILayout.ExpandWidth(true)))
            {
                skin_index--;
                if (skin_index < 0) skin_index = g_skin_list.Count;
                Log.dbg("new skin_index = {0} :: g_skin_list.Count = {1}", skin_index, g_skin_list.Count);
            }

            string skin_name = "";
            if (skin_index == 0) skin_name = "None";
            else skin_name = g_skin_list[skin_index - 1].name;
            _content.text = skin_name;
            _content.tooltip = "Current skin";
            GUILayout.Label(_content, label_txt_center, GUILayout.ExpandWidth(true));
            //Log.dbg("skin label OK :: skin_list.Count = " + skin_list.Count);
            _content.text = "►";
            _content.tooltip = "Select next skin";
            if (GUILayout.Button(_content, GUILayout.ExpandWidth(true)))
            {
                skin_index++;
                if (skin_index > g_skin_list.Count) skin_index = 0;
                Log.dbg("new skin_index = {0} :: g_skin_list.Count = {1}", skin_index, g_skin_list.Count);
            }
            
            GUILayout.EndHorizontal();
        }

        private void testing_gui(int window_id)
        {
            //EventData<Game> foo = GameEvents.onGameStateSaved;

            //if (foo == null) GUILayout.Label("EventData<Game> foo == null");
            //else GUILayout.Label("foo = " + foo.ToString());





            GUILayout.BeginVertical();

            GUILayout.Label("vessel.id.ToString() = " + vessel.id.ToString());

            GUILayout.Label("Application.platform = " + Application.platform);

            GUILayout.Label("Application.persistentDataPath = " + Application.persistentDataPath);

            GUILayout.Label("Application.unityVersion = " + Application.unityVersion);

            GUILayout.Label("Application.targetFrameRate = " + Application.targetFrameRate);

            //GUILayout.Label("AppDomain.CurrentDomain.DynamicDirectory = " + AppDomain.CurrentDomain.DynamicDirectory);

            //GUILayout.Label("AppDomain.CurrentDomain.RelativeSearchPath = " + AppDomain.CurrentDomain.RelativeSearchPath);

            GUILayout.Label("Application.absoluteURL = " + Application.absoluteURL);

            GUILayout.Label("Application.dataPath = " + Application.dataPath);

            //GUILayout.Label("Application.genuine = " + Application.genuine);

            //GUILayout.Label("Application.HasProLicense = " + Application.HasProLicense());

            GUILayout.Label("Application.internetReachability = " + Application.internetReachability);


            GUILayout.Label("str yep_yep: " + yep_yep);

            //Path.GetFileName
            //Path.GetFullPath();




            //allColors = KnownColor.DarkKhaki;


            //AudioReverbPreset arp = new AudioReverbPreset();
            //chatter.reverb_filter.reverbPreset = AudioReverbPreset.Alley;

            //AudioReverbPreset[] preset_list = Enum.GetValues(typeof(AudioReverbPreset)) as AudioReverbPreset[];

            //foreach (var val in preset_list)
            //{
            //    Log.dbg("preset val.ToString() = " +  val.ToString());
            //}

            //AssetBase ab = new AssetBase();

            //GUISkin[] jops = AssetBase.FindObjectsOfTypeIncludingAssets(typeof(GUISkin)) as GUISkin[];

            //foreach (GUISkin skin in jops)
            //{
            //    //Log.dbg("skin.name = " + skin.name);
            //    GUILayout.Label("skin.name = " + skin.name, xkcd_label);
            //}

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        
        private void probe_sample_selector_gui(int window_id)
        {
            GUIContent _content = new GUIContent();

            BeepSource source = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];   //shortcut   0-9 only, but correspond to the correct beepsource

            //GUILayout.Label("Beepsource " + source.beep_name, label_txt_center);

            probe_sample_selector_scroll_pos = GUILayout.BeginScrollView(probe_sample_selector_scroll_pos, false, true);

            //list each sample from Dict
            foreach (string key in dict_probe_samples.Keys)
            {
                AudioClip _clip = AudioClip.Create("noClip", 1, 1, 1000, true);
                GUIStyle sample_gs = label_txt_left;

                if (dict_probe_samples.TryGetValue(key, out _clip))
                {

                    //check if _clip is == source.clip
                    //if yes, bold it
                    if (_clip == source.audiosource.clip) sample_gs = label_txt_bold;
                    //else sample_gs = label_txt_left;
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                _content.text = key;
                _content.tooltip = "Probe sample file name";
                GUILayout.Label(_content, sample_gs, GUILayout.ExpandWidth(true));

                _content.text = "►";
                _content.tooltip = "Play this sample once";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    if ((exchange_playing && disable_beeps_during_chatter) || sstv.isPlaying) return;   //don't play during chatter or sstv
                    //Log.dbg("playing sample " + source.current_clip + " one time...");

                    OTP_source = source;
                    OTP_stored_clip = source.audiosource.clip;

                    //Log.dbg("OTP_stored_clip = " + OTP_stored_clip);
                    //source.current_clip = key;
                    //Log.dbg("set clip " + source.current_clip + " to play once");
                    //set_beep_clip(source);
                    //Log.dbg("source.audiosource.clip set");

                    //AudioClip _clip;
                    if (dict_probe_samples.TryGetValue(key, out _clip))
                    {
                        source.audiosource.clip = _clip;
                    }

                    OTP_playing = true;
                    source.audiosource.Play();

                }

                _content.text = "Set";
                _content.tooltip = "Set this sample to play from this beepsource";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    //sample was selected
                    source.current_clip = key;  //set current_clip
                    set_beep_clip(source);  //then assign AudioClip
                    Log.dbg("sample selector clip set :: clip = {0}", key);

                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
            {
                show_probe_sample_selector = false;
            }
            GUILayout.EndHorizontal();

            if (show_tooltips && GUI.tooltip != "") tooltips(probe_sample_selector_window_pos);

            GUI.DragWindow();
        }

        private void AAE_background_sample_selector_gui(int window_id)
        {
            GUIContent _content = new GUIContent();

            //BeepSource source = beepsource_list[(((sel_beep_page - 1) * 10) + sel_beep_src)];   //shortcut   0-9 only, but correspond to the correct beepsource

            BackgroundSource src = sel_background_src;

            AAE_background_sample_selector_scroll_pos = GUILayout.BeginScrollView(AAE_background_sample_selector_scroll_pos, false, true);

            //list each sample from Dict
            foreach (string key in dict_background_samples.Keys)
            {
                AudioClip _clip = AudioClip.Create("noClip", 1, 1, 1000, true);
                GUIStyle sample_gs = label_txt_left;

                if (dict_background_samples.TryGetValue(key, out _clip))
                {

                    //check if _clip is == source.clip
                    //if yes, bold it
                    if (_clip == src.audiosource.clip) sample_gs = label_txt_bold;
                    else sample_gs = label_txt_left;
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                _content.text = key;
                _content.tooltip = "Background sample file name";
                GUILayout.Label(_content, sample_gs, GUILayout.ExpandWidth(true));

                _content.text = "Set";
                _content.tooltip = "Set this sample to play from this backgroundsource";
                if (GUILayout.Button(_content, GUILayout.ExpandWidth(false)))
                {
                    //sample was selected
                    src.current_clip = key;  //set current_clip
                    //set_beep_clip(source);  //then assign AudioClip

                    AudioClip temp_clip = AudioClip.Create("noClip", 1, 1, 1000, true);

                    if (dict_background_samples.TryGetValue(src.current_clip, out temp_clip))
                    {
                        src.audiosource.clip = temp_clip;
                        string s = "";
                        if (dict_background_samples2.TryGetValue(src.audiosource.clip, out s))
                        {
                            src.current_clip = s;
                            Log.dbg("background AudioClip set :: current_clip = {0}", s);
                        }
                    }
                    else
                    {
                        Log.error("Could not find AudioClip for key {0} :: setting AudioClip to \"First\"", src.current_clip);
                        src.current_clip = "First";
                        set_background_clip(src);
                        //set_beep_clip(beepsource);
                    }

                    Log.dbg("sample selector clip set :: clip = {0}", key);

                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
            {
                show_AAE_background_sample_selector = false;
            }
            GUILayout.EndHorizontal();

            if (show_tooltips && GUI.tooltip != "") tooltips(probe_sample_selector_window_pos);

            GUI.DragWindow();
        }

        //Set audioclip
        private void set_beep_clip(BeepSource beepsource)
        {
            if (beepsource.current_clip == "First")
            {
                //"First" is used when creating a new beepsource
                //Log.dbg("beep AudioClip is \"First\"");
                //pick any AudioClip (when adding a new beepsource)

                //dump all values into a List
                //get a random index for that list
                //assign
                List<AudioClip> val_list = new List<AudioClip>();
                foreach (AudioClip val in dict_probe_samples.Values)
                {
                    val_list.Add(val);
                }
                beepsource.audiosource.clip = val_list[0];
                string s = "";
                if (dict_probe_samples2.TryGetValue(beepsource.audiosource.clip, out s))
                {
                    beepsource.current_clip = s;
                    Log.dbg("\"First\" AudioClip set :: current_clip = {0}", s);
                }
            }
            else if (beepsource.current_clip == "Random")
            {
                Log.dbg("setting random AudioClip...");
                List<AudioClip> clip_list = new List<AudioClip>();
                foreach (AudioClip clip in dict_probe_samples.Values)
                {
                    clip_list.Add(clip);
                }
                beepsource.audiosource.clip = clip_list[rand.Next(0, clip_list.Count)];
                string s = "";
                if (dict_probe_samples2.TryGetValue(beepsource.audiosource.clip, out s))
                {
                    beepsource.current_clip = s;
                    Log.dbg("beep AudioClip randomized :: current_clip = {0}", s);
                }
            }
            else
            {
                AudioClip temp_clip = AudioClip.Create("noClip", 1, 1, 1000, true);

                //broken here current_clip == null

                if (dict_probe_samples.TryGetValue(beepsource.current_clip, out temp_clip))
                {
                    beepsource.audiosource.clip = temp_clip;
                    string s = "";
                    if (dict_probe_samples2.TryGetValue(beepsource.audiosource.clip, out s))
                    {
                        beepsource.current_clip = s;
                        Log.dbg("beep AudioClip set :: current_clip = " + s);
                    }
                }
                else
                {
                    Log.error("Could not find AudioClip for key {0} :: setting AudioClip to \"First\"", beepsource.current_clip);
                    beepsource.current_clip = "First";
                    set_beep_clip(beepsource);
                }
            }
        }

        private void set_background_clip(BackgroundSource src)
        {
            //
            //FIX background audio clip assignment.  i think it will fuck up if there are less than 2 clips found
            //

            if (src.current_clip == "Default")
            {
                //build a list of all background clips
                List<AudioClip> val_list = new List<AudioClip>();
                foreach (AudioClip val in dict_background_samples.Values)
                {
                    val_list.Add(val);
                }

                //set current source clip from above list (first source gets first clip, second gets second, etc)
                src.audiosource.clip = val_list[backgroundsource_list.Count - 1];

                //get the file name for the clip
                string s = "";
                if (dict_background_samples2.TryGetValue(src.audiosource.clip, out s))
                {
                    src.current_clip = s;
                    Log.dbg("\"Default\" AudioClip set :: current_clip = {0}", s);
                }
            }
        }

        private void set_soundscape_clip()
        {
            //create a new List using Values from dictionary
            List<AudioClip> clips = new List<AudioClip>(dict_soundscape_samples.Values);
            aae_soundscape.clip = clips[rand.Next(0, clips.Count)];

            //get the file name for the clip
            string s = "";
            if (dict_soundscape_samples2.TryGetValue(aae_soundscape.clip, out s))
            {
                aae_soundscape_current_clip = s;
                Log.dbg("Soundscape AudioClip set :: current_clip = {0}", s);
            }
        }

        //Create/destroy sources
        private void add_new_beepsource()
        {
            BeepSource beepSource = new BeepSource();
            beepSource.beep_player = new GameObject();
            beepSource.beep_player.name = "rbr_beep_player_" + beepsource_list.Count;
            beepSource.beep_name = beepsource_list.Count.ToString();
            beepSource.audiosource = beepSource.beep_player.AddComponent<AudioSource>();
            beepSource.audiosource.volume = 0.3f;   //default 30%
            beepSource.audiosource.spatialBlend = 0.0f;
            //beepSource.audiosource.clip = all_beep_clips[0];
            beepSource.current_clip = "First";
            beepSource.chorus_filter = beepSource.beep_player.AddComponent<AudioChorusFilter>();
            beepSource.chorus_filter.enabled = false;
            beepSource.distortion_filter = beepSource.beep_player.AddComponent<AudioDistortionFilter>();
            beepSource.distortion_filter.enabled = false;
            beepSource.echo_filter = beepSource.beep_player.AddComponent<AudioEchoFilter>();
            beepSource.echo_filter.enabled = false;
            beepSource.highpass_filter = beepSource.beep_player.AddComponent<AudioHighPassFilter>();
            beepSource.highpass_filter.enabled = false;
            beepSource.lowpass_filter = beepSource.beep_player.AddComponent<AudioLowPassFilter>();
            beepSource.lowpass_filter.enabled = false;
            beepSource.reverb_filter = beepSource.beep_player.AddComponent<AudioReverbFilter>();
            beepSource.reverb_filter.enabled = false;

            if (dict_probe_samples.Count > 0)
            {
                set_beep_clip(beepSource);   //set

                if (beepSource.precise == false && beepSource.loose_freq > 0) new_beep_loose_timer_limit(beepSource);
            }

            beepsource_list.Add(beepSource);
        }

        private void add_new_backgroundsource()
        {
            BackgroundSource backgroundSource = new BackgroundSource();

            backgroundSource.background_player = new GameObject();
            backgroundSource.background_player.name = "rbr_background_player_" + backgroundsource_list.Count;
            backgroundSource.audiosource = backgroundSource.background_player.AddComponent<AudioSource>();
            backgroundSource.audiosource.volume = 0.3f;
            backgroundSource.audiosource.spatialBlend = 0.0f;
            backgroundSource.current_clip = "Default";

            backgroundsource_list.Add(backgroundSource);

            if (dict_background_samples.Count > 0)
            {
                set_background_clip(backgroundSource);  //set clip
            }
        }

        private void destroy_all_beep_players()
        {
            var allSources = FindObjectsOfType(typeof(GameObject)) as GameObject[];
            List<GameObject> temp = new List<GameObject>();
            string search_str = "rbr_beep";
            int search_str_len = search_str.Length;

            foreach (var source in allSources)
            {
                if (source.name.Length > search_str_len)
                {
                    if (source.name.Substring(0, search_str_len) == search_str)
                    {
                        Log.dbg("destroying {0}", source.name);
                        Destroy(source);
                    }
                }
            }
        }

        private void destroy_all_background_players()
        {
            var allSources = FindObjectsOfType(typeof(GameObject)) as GameObject[];
            List<GameObject> temp = new List<GameObject>();
            string search_str = "rbr_background";
            int search_str_len = search_str.Length;

            foreach (var source in allSources)
            {
                if (source.name.Length > search_str_len)
                {
                    if (source.name.Substring(0, search_str_len) == search_str)
                    {
                        Log.dbg("destroying {0}", source.name);
                        Destroy(source);
                    }
                }
            }
        }
                
        ////determine whether the vessel has a part with ModuleRemoteTechSPU and load all relevant RemoteTech variables for the vessel
        //public void updateRemoteTechData()
        //{
        //    if (RT2Hook.Instance != null)
        //    {
        //        if (hasRemoteTech == false) hasRemoteTech = true;

        //        //if (RT2Hook.Instance.HasAnyConnection(vessel.id))
        //        //{
        //        //    shortestcontrolDelay = RT2Hook.Instance.GetShortestSignalDelay(vessel.id);

        //        //    if (inSatteliteRadioContact == false)
        //        //    {
        //        //        inSatteliteRadioContact = !inSatteliteRadioContact;

        //        //        Log.dbg("Sattelite contact ! Signal delay =" + Convert.ToSingle(shortestcontrolDelay));
        //        //    }
        //        //}
        //        //else if (!RT2Hook.Instance.HasAnyConnection(vessel.id))
        //        //{
        //        //    if (inSatteliteRadioContact == true)
        //        //    {
        //        //        inSatteliteRadioContact = !inSatteliteRadioContact;

        //        //        shortestcontrolDelay = 0;
        //        //        Log.dbg("No Sattelite contact ! Satt delay set to =" + Convert.ToSingle(shortestcontrolDelay));
        //        //    }
        //        //}

        //        if (RT2Hook.Instance.HasConnectionToKSC(vessel.id))
        //        {
        //            controlDelay = RT2Hook.Instance.GetSignalDelayToKSC(vessel.id);

        //            if (inRadioContact == false)
        //            {
        //                inRadioContact = !inRadioContact;

        //                Log.dbg("Online ! Signal delay =" + Convert.ToSingle(controlDelay));
        //            }
        //        }
        //        else if (!RT2Hook.Instance.HasConnectionToKSC(vessel.id))
        //        {
        //            if (inRadioContact == true)
        //            {
        //                inRadioContact = !inRadioContact;

        //                controlDelay = 0;
        //                Log.dbg("Offline ! Delay set to =" + Convert.ToSingle(controlDelay));

        //                if (response_chatter.isPlaying == true) response_chatter.Stop();
        //                if (sstv.isPlaying == true) sstv.Stop();
        //            }
        //        }
        //    }
        //    else if (hasRemoteTech == true) hasRemoteTech = false;
        //}

        //Load audio functions
        private void load_quindar_audio()
        {
            //Create two AudioSources for quindar so PlayDelayed() can delay both beeps
            Log.dbg("loading quindar_01 clip");
            string path1 = GDBAsset.Solve("Sounds", "chatter", "quindar_01");

            if (GameDatabase.Instance.ExistsAudioClip(path1))
            {
                quindar_01_clip = GameDatabase.Instance.GetAudioClip(path1);
                Log.dbg("quindar_01 clip loaded");
            }
            else Log.warn("quindar_01 audio file missing!");

            Log.dbg("loading quindar_02 clip");
            string path2 = GDBAsset.Solve("Sounds", "chatter", "quindar_02");

            if (GameDatabase.Instance.ExistsAudioClip(path2))
            {
                quindar_02_clip = GameDatabase.Instance.GetAudioClip(path2);
                Log.dbg("quindar_02 clip loaded");
            }
            else Log.warn("quindar_02 audio file missing!");

            Log.dbg("loading voidnoise clip");
            string path3 = GDBAsset.Solve("Sounds", "chatter", "voidnoise");

            if (GameDatabase.Instance.ExistsAudioClip(path3))
            {
                voidnoise_clip = GameDatabase.Instance.GetAudioClip(path3);
                Log.dbg("voidnoise clip loaded");
            }
            else Log.warn("voidnoise audio file missing!");
        }

        private void load_beep_audio()
        {
            string probe_sounds_root = GDBAsset.SourceDir("Sounds", "beeps");

            if (Directory.Exists(probe_sounds_root))
            {
                beeps_exists = true;

                string[] st_array;
                foreach (string ext in AUDIO_FILE_EXTS)
                {
                    //Log.dbg("checking for " + ext + " files...");
                    st_array = Directory.GetFiles(probe_sounds_root, ext);
                    foreach (string file in st_array)
                    {
                        string short_file_name = Path.GetFileNameWithoutExtension(file);

                        //Log.dbg("file_name = " + file_name);

                        if (ext == "*.mp3")
                        {
                            //GameDatabase won't load MP3
                            //try old method
                            //string mp3_path = "file://" + AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/Sounds/beeps/" + short_file_name + ".mp3";
                            string mp3_path = "file://" + File.Asset.Solve("Sounds", "beeps", short_file_name + ".mp3");
                            //WWW www_chatter = new WWW(mp3_path);
                            //if (www_chatter != null)
                            //{
                            //    dict_probe_samples.Add(short_file_name, www_chatter.GetAudioClip(false));
                            //    dict_probe_samples2.Add(www_chatter.GetAudioClip(false), short_file_name);
                            //    Log.dbg("" + mp3_path + " loaded OK");
                            //}
                            //else
                            {
                                Log.warn("{0} load FAIL", mp3_path);
                            }
                        }
                        else
                        {
                            string gdb_path = GDBAsset.Solve("Sounds", "beeps", short_file_name);
                            if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                            {
                                //all_beep_clips.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                dict_probe_samples.Add(short_file_name, GameDatabase.Instance.GetAudioClip(gdb_path));
                                dict_probe_samples2.Add(GameDatabase.Instance.GetAudioClip(gdb_path), short_file_name);
                                //Log.dbg("" + gdb_path + " loaded OK");
                            }
                            else
                            {
                                //no ExistsAudioClip == false
                                Log.warn("{0} load FAIL", gdb_path);
                            }
                        }
                    }
                }
            }
            if (dict_probe_samples.Count == 0) Log.warn("No SSTV clips found");
        }

        private void load_sstv_audio()
        {
            string sstv_sounds_root = GDBAsset.SourceDir("Sounds", "sstv");

            if (Directory.Exists(sstv_sounds_root))
            {
                sstv_exists = true;

                string[] st_array;
                foreach (string ext in AUDIO_FILE_EXTS)
                {
                    Log.dbg("checking for " + ext + " files...");
                    st_array = Directory.GetFiles(sstv_sounds_root, ext);
                    foreach (string file in st_array)
                    {
                        Log.dbg("sstv file = " + file);

                        string short_file_name = Path.GetFileNameWithoutExtension(file);
                        //Log.dbg("file_name = " + file_name);

                        if (ext == "*.mp3")
                        {
                            //GameDatabase won't load MP3
                            //try old method
                            //string mp3_path = "file://" + AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/Sounds/sstv/" + short_file_name + ".mp3";
                            string mp3_path = "file://" + File.Asset.Solve("Sounds", "sstv", short_file_name + ".mp3");
                            //WWW www_chatter = new WWW(mp3_path);
                            //if (www_chatter != null)
                            //{
                            //    all_sstv_clips.Add(www_chatter.GetAudioClip(false));
                            //    Log.dbg("" + mp3_path + " loaded OK");
                            //}
                            //else
                            {
                                Log.warn("{0} load FAIL", mp3_path);
                            }
                        }
                        else
                        {
                            string gdb_path = GDBAsset.Solve("Sounds", "sstv", short_file_name);
                            if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                            {
                                all_sstv_clips.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                            }
                            else
                            {
                                //no ExistsAudioClip == false
                                Log.warn("{0} load FAIL, GameDatabase.Instance.ExistsAudioClip({1}) == false", gdb_path, short_file_name);
                            }
                        }
                    }
                }
            }
            else
            {
                Log.warn("Directory '{0}' for 'SSTV' could not be found", sstv_sounds_root);
            }

            if (all_sstv_clips.Count == 0) Log.warn("No SSTV clips found");
        }

        static readonly string[] AUDIOSET_TYPES = { "capcom", "capsule", "capsuleF" };
        private void load_chatter_audio()
        {

            //first, start a loop through all the elements in chatter_array
            //check for a capsule directory
            //if exists, run GetFiles() for each of the file extensions

            string chatter_root = GDBAsset.SourceDir("Sounds", "chatter");

            if (Directory.Exists(chatter_root))
            {
                chatter_exists = true;

                Log.dbg("loading chatter audio...");

                for (int k = 0; k < chatter_array.Count; k++)
                {
                    ChatterAudioList chatter_array_k = chatter_array[k];
                    string audioset_root = Path.Combine(chatter_root, chatter_array_k.directory);
                    if (Directory.Exists(audioset_root))
                    {
                        //audioset directory found OK
                        //Log.dbg("directory [" + chatter_array_k.directory + "] found OK");
                        foreach (string st in AUDIOSET_TYPES)
                        {
                            string audioset_type_root = Path.Combine(audioset_root, st);
                            //search through each set_type (capcom, capsule, capsuleF)
                            if (Directory.Exists(audioset_type_root))
                            {
                                Log.dbg("directory [{0}] found OK", audioset_type_root);

                                //clear any existing audio
                                if (st == "capcom") chatter_array_k.capcom.Clear();
                                else if (st == "capsule") chatter_array_k.capsule.Clear();
                                else if (st == "capsuleF") chatter_array_k.capsuleF.Clear();

                                string[] st_array;
                                foreach (string ext in AUDIO_FILE_EXTS)
                                {
                                    //Log.dbg("checking for " + ext + " files...");
                                    st_array = Directory.GetFiles(audioset_type_root, ext);
                                    foreach (string file in st_array)
                                    {
                                        string file_name = Path.GetFileNameWithoutExtension(file);
                                        //Log.dbg("file_name = " + file_name);

                                        if (ext == "*.mp3")
                                        {
                                            //try old method
                                            string mp3_path = "file://" + AssemblyLoader.loadedAssemblies.GetPathByType(typeof(chatterer)) + "/Sounds/chatter/" + chatter_array_k.directory + "/" + st + "/" + file_name + ".mp3";
                                            //WWW www_chatter = new WWW(mp3_path);
                                            //if (www_chatter != null)
                                            //{
                                            //    if (st == "capcom")
                                            //    {
                                            //        chatter_array_k.capcom.Add(www_chatter.GetAudioClip(false));
                                            //        //Log.dbg("" + mp3_path + " loaded OK");
                                            //    }
                                            //    else if (st == "capsule")
                                            //    {
                                            //        chatter_array_k.capsule.Add(www_chatter.GetAudioClip(false));
                                            //        //Log.dbg("" + mp3_path + " loaded OK");
                                            //    }
                                            //    else if (st == "capsuleF")
                                            //    {
                                            //        chatter_array_k.capsuleF.Add(www_chatter.GetAudioClip(false));
                                            //        //Log.dbg("" + mp3_path + " loaded OK");
                                            //    }
                                            //}
                                        }
                                        else
                                        {
                                            string gdb_path = GDBAsset.Solve("Sounds", "chatter", chatter_array_k.directory, st, file_name);
                                            if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                                            {
                                                if (st == "capcom")
                                                {
                                                    chatter_array_k.capcom.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                                    Log.dbg("[{0}] loaded OK", gdb_path);
                                                }
                                                else if (st == "capsule")
                                                {
                                                    chatter_array_k.capsule.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                                    Log.dbg("[{0}] loaded OK", gdb_path);
                                                }
                                                else if (st == "capsuleF")
                                                {
                                                    chatter_array_k.capsuleF.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                                                    Log.dbg("[{0}] loaded OK", gdb_path);
                                                }
                                            }
                                            else
                                            {
                                                //no audio exists at gdb_path
                                                Log.warn("{0} load FAIL", gdb_path);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Log.warn("directory [{0}/{1}] NOT found, skipping...", chatter_array_k.directory, st);
                            }
                        }
                    }
                    else
                    {
                        //audioset directory NOT found
                        Log.warn("directory [{0}] NOT found, skipping...", chatter_array_k.directory);
                    }
                }
            }
            else
            {
                Log.warn("Directory '{0}' for 'chatter' could not be found", chatter_root);
            }

            load_toggled_chatter_sets();
        }

        private void load_AAE_background_audio()
        {
            string sounds_path = GDBAsset.SourceDir("Sounds", "AAE", "background");

            if (Directory.Exists(sounds_path))
            {
                //AAE_exists = true;  //set flag to display and run AAE functions if any AAE is found

                string[] st_array;
                foreach (string ext in AUDIO_FILE_EXTS)
                {
                    //Log.dbg("checking for " + ext + " files...");
                    st_array = Directory.GetFiles(sounds_path, ext);
                    foreach (string file in st_array)
                    {
                        string file_name = Path.GetFileNameWithoutExtension(file);

                        string gdb_path = GDBAsset.Solve("Sounds", "AAE", "background", file_name);
                        if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                        {
                            aae_backgrounds_exist = true;
                            dict_background_samples.Add(file_name, GameDatabase.Instance.GetAudioClip(gdb_path));
                            dict_background_samples2.Add(GameDatabase.Instance.GetAudioClip(gdb_path), file_name);
                            //audio_list.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                            //Log.dbg("[muziker] " + gdb_path + " loaded OK");
                        }
                        else
                        {
                            //no ExistsAudioClip == false
                            Log.warn("Could not load audio {0}", gdb_path);
                        }
                    }
                }
            }
            else
            {
                Log.warn("Directory '{0}' could not be found", sounds_path);
            }
        }

        private void load_AAE_soundscape_audio()
        {
            string sounds_path = GDBAsset.SourceDir("Sounds", "AAE", "soundscape");

            if (Directory.Exists(sounds_path))
            {
                //AAE_exists = true;  //set flag to display and run AAE functions if any AAE is found

                string[] st_array;
                foreach (string ext in AUDIO_FILE_EXTS)
                {
                    //Log.dbg("checking for " + ext + " files...");
                    st_array = Directory.GetFiles(sounds_path, ext);
                    foreach (string file in st_array)
                    {
                        string file_name = Path.GetFileNameWithoutExtension(file);

                        string gdb_path = GDBAsset.Solve("Sounds", "AAE", "soundscape", file_name);
                        if (GameDatabase.Instance.ExistsAudioClip(gdb_path))
                        {
                            aae_soundscapes_exist = true;
                            dict_soundscape_samples.Add(file_name, GameDatabase.Instance.GetAudioClip(gdb_path));
                            dict_soundscape_samples2.Add(GameDatabase.Instance.GetAudioClip(gdb_path), file_name);
                            //audio_soundscape.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                            //audio_list.Add(GameDatabase.Instance.GetAudioClip(gdb_path));
                            //Log.dbg("[muziker] " + gdb_path + " loaded OK");
                        }
                        else
                        {
                            //no ExistsAudioClip == false
                            Log.warn("Could not load audio {0} : Check installation path.", gdb_path);
                        }
                    }
                }
            }
            else Log.dbg("Directory '{0}' could not be found, skipping.", sounds_path);
        }

        //Timer functions
        private void new_beep_loose_timer_limit(BeepSource bm)
        {
            if (bm.loose_freq == 1) bm.loose_timer_limit = rand.Next(120, 301);
            else if (bm.loose_freq == 2) bm.loose_timer_limit = rand.Next(60, 121);
            else if (bm.loose_freq == 3) bm.loose_timer_limit = rand.Next(30, 61);
            else if (bm.loose_freq == 4) bm.loose_timer_limit = rand.Next(15, 31);
            else if (bm.loose_freq == 5) bm.loose_timer_limit = rand.Next(5, 16);
            else if (bm.loose_freq == 6) bm.loose_timer_limit = rand.Next(1, 6);
            //Log.dbg("new beep loose timer limit set: " + bm.loose_timer_limit);
        }

        private void new_sstv_loose_timer_limit()
        {
            if (sstv_freq == 1) sstv_timer_limit = rand.Next(1800, 3601);       //30-60mins
            else if (sstv_freq == 2) sstv_timer_limit = rand.Next(900, 1801);   //15-30m
            else if (sstv_freq == 3) sstv_timer_limit = rand.Next(300, 901);    //5-15m
            else if (sstv_freq == 4) sstv_timer_limit = rand.Next(120, 301);    //2-5m
            Log.dbg("new sstv timer limit set: {0:0}", sstv_timer_limit);
        }

        private void new_soundscape_loose_timer_limit()
        {
            if (aae_soundscape_freq == 1) aae_soundscape_timer_limit = rand.Next(300, 601);   //5-10m
            if (aae_soundscape_freq == 2) aae_soundscape_timer_limit = rand.Next(120, 301);   //2-5m
            if (aae_soundscape_freq == 3) aae_soundscape_timer_limit = rand.Next(60, 121);   //1-2m
            Log.dbg("new soundscape1 timer limit set: {0:0}", aae_soundscape_timer_limit);
        }

        //Chatter functions
        private void load_toggled_chatter_sets()
        {
            //Log.dbg("loading toggled sets...");
            //load audio into current from sets that are toggled on
            current_capcom_chatter.Clear();
            current_capsule_chatter.Clear();
            current_capsuleF_chatter.Clear();

            for (int i = 0; i < chatter_array.Count; i++)
            {
                if (chatter_array[i].is_active)
                {
                    current_capcom_chatter.AddRange(chatter_array[i].capcom);
                    current_capsule_chatter.AddRange(chatter_array[i].capsule);
                    current_capsuleF_chatter.AddRange(chatter_array[i].capsuleF);
                }
            }

            Log.dbg("toggled sets loaded OK");
        }

        private void set_new_delay_between_exchanges()
        {
            if (chatter_freq == 1) secs_between_exchanges = rand.Next(180, 301);
            else if (chatter_freq == 2) secs_between_exchanges = rand.Next(90, 181);
            else if (chatter_freq == 3) secs_between_exchanges = rand.Next(60, 91);
            else if (chatter_freq == 4) secs_between_exchanges = rand.Next(30, 61);
            else if (chatter_freq == 5) secs_between_exchanges = rand.Next(10, 31);
            Log.dbg("new delay between exchanges: {0:0}", secs_between_exchanges);
        }

        private void initialize_new_exchange()
        {
            //print("initialize_new_exchange()...");
            set_new_delay_between_exchanges();
            secs_since_last_exchange = 0;
            
            bool chatter_is_female = false;
            if (FlightGlobals.ActiveVessel != null) //Avoid EXP on first load where vessel isn't loaded yet
            {
                chatter_is_female = checkChatterGender(); //Check chatter gender to play female/male voice accordingly
            }

            current_capcom_clip = rand.Next(0, current_capcom_chatter.Count); // select a new capcom clip to play
            current_capsule_clip = rand.Next(0, current_capsule_chatter.Count); // select a new capsule clip to play
            current_capsuleF_clip = rand.Next(0, current_capsuleF_chatter.Count); // select a new capsuleF clip to play

            response_delay_secs = rand.Next(2, 5);  // select another random int to set response delay time

            if (pod_begins_exchange) initial_chatter_source = 1;    //pod_begins_exchange set true OnUpdate when staging and on event change
            else initial_chatter_source = rand.Next(0, 2);   //if i_c_s == 0, con sends first message; if i_c_S == 1, pod sends first message
            pod_begins_exchange = false; // Reset so pod doesn't always being exchange.

            if (initial_chatter_source == 0)
            {
                initial_chatter_set = current_capcom_chatter;
                if (chatter_is_female) response_chatter_set = current_capsuleF_chatter;
                else response_chatter_set = current_capsule_chatter;

                initial_chatter_index = current_capcom_clip;
                if (chatter_is_female) response_chatter_index = current_capsuleF_clip;
                else response_chatter_index = current_capsule_clip;
            }
            else
            {
                if (chatter_is_female) initial_chatter_set = current_capsuleF_chatter;
                else initial_chatter_set = current_capsule_chatter;
                response_chatter_set = current_capcom_chatter;

                if (chatter_is_female) initial_chatter_index = current_capsuleF_clip;
                else initial_chatter_index = current_capsule_clip;
                response_chatter_index = current_capcom_clip;
            }
            if (initial_chatter_set.Count > 0) initial_chatter.clip = initial_chatter_set[initial_chatter_index];
            else Log.warn("Initial chatter set is empty");
            if (response_chatter_set.Count > 0) response_chatter.clip = response_chatter_set[response_chatter_index];
            else Log.warn("Response chatter set is empty");
        }

        private void load_radio()
        {
            //try to load from disk first
            string path = Application.persistentDataPath +"radio2";

            //FIX this below will never return true since path isnt correct for GameDatabase
            
            //File.Exists instead or so
            
            if (GameDatabase.Instance.ExistsAudioClip(path))
            {
                yep_yepsource.clip = GameDatabase.Instance.GetAudioClip(path);
                yep_yep_loaded = true;
            }
            //else
            //{
            //    //try www download
            //    bool radio_loaded = false;
            //    WWW www_yepyep = new WWW("http://rbri.co.nf/ksp/chatterer/radio2.ogg");

            //    while (radio_loaded == false)
            //    {
            //        if (www_yepyep.isDone)
            //        {
            //            yep_yepsource.clip = www_yepyep.GetAudioClip(false);
            //            //SavWav.Save("radio2", yep_yepsource.clip);
            //            Log.dbg("radio_yep_yep loaded OK");
            //            radio_loaded = true;
            //            yep_yep_loaded = true;
            //        }
            //    }
            //}
        }

        private void begin_exchange(float delay)
        {
            if (chatter_exists && (vessel.GetCrewCount() > 0) && exchange_playing == false)
            {
                StartCoroutine(Exchange(delay));
            }
        }

        private void stop_audio(string audio_type)
        {
            if (audio_type == "all")
            {
                foreach (BeepSource bm in beepsource_list)
                {
                    bm.audiosource.Stop();
                    bm.timer = 0;
                }
                initial_chatter.Stop();
                response_chatter.Stop();
                quindar1.Stop();
                quindar2.Stop();
                sstv.Stop();
                exchange_playing = false;
            }
            else if (audio_type == "beeps")
            {
                foreach (BeepSource bm in beepsource_list)
                {
                    bm.audiosource.loop = false;
                    bm.audiosource.Stop();
                    bm.timer = 0;
                }
            }
            else if (audio_type == "chatter")
            {
                initial_chatter.Stop();
                response_chatter.Stop();
                exchange_playing = false;
            }
        }

        //Copy/Paste beepsource
        private void copy_beepsource_values(BeepSource source)
        {
            beepsource_clipboard = new ConfigNode();

            ConfigNode _filter;

            beepsource_clipboard.AddValue("precise", source.precise);
            beepsource_clipboard.AddValue("precise_freq", source.precise_freq);
            beepsource_clipboard.AddValue("loose_freq", source.loose_freq);
            beepsource_clipboard.AddValue("volume", source.audiosource.volume);
            beepsource_clipboard.AddValue("pitch", source.audiosource.pitch);
            beepsource_clipboard.AddValue("current_clip", source.current_clip);
            beepsource_clipboard.AddValue("sel_filter", source.sel_filter);
            beepsource_clipboard.AddValue("show_settings_window", source.show_settings_window);
            beepsource_clipboard.AddValue("reverb_preset_index", source.reverb_preset_index);
            beepsource_clipboard.AddValue("settings_window_pos_x", source.settings_window_pos.x);
            beepsource_clipboard.AddValue("settings_window_pos_y", source.settings_window_pos.y);

            //filters
            //ConfigNode _filter;

            _filter = new ConfigNode();
            _filter.name = "CHORUS";
            _filter.AddValue("enabled", source.chorus_filter.enabled);
            _filter.AddValue("dry_mix", source.chorus_filter.dryMix);
            _filter.AddValue("wet_mix_1", source.chorus_filter.wetMix1);
            _filter.AddValue("wet_mix_2", source.chorus_filter.wetMix2);
            _filter.AddValue("wet_mix_3", source.chorus_filter.wetMix3);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "DISTORTION";
            _filter.AddValue("enabled", source.distortion_filter.enabled);
            _filter.AddValue("distortion_level", source.distortion_filter.distortionLevel);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "ECHO";
            _filter.AddValue("enabled", source.echo_filter.enabled);
            _filter.AddValue("delay", source.echo_filter.delay);
            _filter.AddValue("decay_ratio", source.echo_filter.decayRatio);
            _filter.AddValue("dry_mix", source.echo_filter.dryMix);
            _filter.AddValue("wet_mix", source.echo_filter.wetMix);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "HIGHPASS";
            _filter.AddValue("enabled", source.highpass_filter.enabled);
            _filter.AddValue("cutoff_freq", source.highpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", source.highpass_filter.highpassResonanceQ);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "LOWPASS";
            _filter.AddValue("enabled", source.lowpass_filter.enabled);
            _filter.AddValue("cutoff_freq", source.lowpass_filter.cutoffFrequency);
            _filter.AddValue("resonance_q", source.lowpass_filter.lowpassResonanceQ);
            beepsource_clipboard.AddNode(_filter);

            _filter = new ConfigNode();
            _filter.name = "REVERB";
            _filter.AddValue("enabled", source.reverb_filter.enabled);
            _filter.AddValue("reverb_preset", source.reverb_filter.reverbPreset);
            _filter.AddValue("dry_level", source.reverb_filter.dryLevel);
            _filter.AddValue("room", source.reverb_filter.room);
            _filter.AddValue("room_hf", source.reverb_filter.roomHF);
            _filter.AddValue("room_lf", source.reverb_filter.roomLF);
            _filter.AddValue("decay_time", source.reverb_filter.decayTime);
            _filter.AddValue("decay_hf_ratio", source.reverb_filter.decayHFRatio);
            _filter.AddValue("reflections_level", source.reverb_filter.reflectionsLevel);
            _filter.AddValue("reflections_delay", source.reverb_filter.reflectionsDelay);
            _filter.AddValue("reverb_level", source.reverb_filter.reverbLevel);
            _filter.AddValue("reverb_delay", source.reverb_filter.reverbDelay);
            _filter.AddValue("diffusion", source.reverb_filter.diffusion);
            _filter.AddValue("density", source.reverb_filter.density);
            _filter.AddValue("hf_reference", source.reverb_filter.hfReference);
            _filter.AddValue("lf_reference", source.reverb_filter.lfReference);
            beepsource_clipboard.AddNode(_filter);

            Log.dbg("single beepsource values copied to beepsource_clipboard");
        }

        private void paste_beepsource_values(BeepSource source)
        {
            if (beepsource_clipboard.HasValue("precise")) source.precise = Boolean.Parse(beepsource_clipboard.GetValue("precise"));
            if (beepsource_clipboard.HasValue("precise_freq"))
            {
                source.precise_freq = Int32.Parse(beepsource_clipboard.GetValue("precise_freq"));
                source.precise_freq_slider = source.precise_freq;
            }
            if (beepsource_clipboard.HasValue("loose_freq"))
            {
                source.loose_freq = Int32.Parse(beepsource_clipboard.GetValue("loose_freq"));
                source.loose_freq_slider = source.loose_freq;
            }
            if (beepsource_clipboard.HasValue("volume")) source.audiosource.volume = Single.Parse(beepsource_clipboard.GetValue("volume"));
            if (beepsource_clipboard.HasValue("pitch")) source.audiosource.pitch = Single.Parse(beepsource_clipboard.GetValue("pitch"));
            if (beepsource_clipboard.HasValue("current_clip")) source.current_clip = beepsource_clipboard.GetValue("current_clip");
            if (beepsource_clipboard.HasValue("sel_filter")) source.sel_filter = Int32.Parse(beepsource_clipboard.GetValue("sel_filter"));
            if (beepsource_clipboard.HasValue("show_settings_window")) source.show_settings_window = Boolean.Parse(beepsource_clipboard.GetValue("show_settings_window"));
            if (beepsource_clipboard.HasValue("reverb_preset_index")) source.reverb_preset_index = Int32.Parse(beepsource_clipboard.GetValue("reverb_preset_index"));
            if (beepsource_clipboard.HasValue("settings_window_pos_x")) source.settings_window_pos.x = Single.Parse(beepsource_clipboard.GetValue("settings_window_pos_x"));
            if (beepsource_clipboard.HasValue("settings_window_pos_y")) source.settings_window_pos.y = Single.Parse(beepsource_clipboard.GetValue("settings_window_pos_y"));

            if (dict_probe_samples.Count > 0)
            {
                set_beep_clip(source);

                if (source.precise == false && source.loose_freq > 0) new_beep_loose_timer_limit(source);
            }

            foreach (ConfigNode filter in beepsource_clipboard.nodes)
            {
                if (filter.name == "CHORUS")
                {
                    if (filter.HasValue("enabled")) source.chorus_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("dry_mix")) source.chorus_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
                    if (filter.HasValue("wet_mix_1")) source.chorus_filter.wetMix1 = Single.Parse(filter.GetValue("wet_mix_1"));
                    if (filter.HasValue("wet_mix_2")) source.chorus_filter.wetMix2 = Single.Parse(filter.GetValue("wet_mix_2"));
                    if (filter.HasValue("wet_mix_3")) source.chorus_filter.wetMix3 = Single.Parse(filter.GetValue("wet_mix_3"));
                }
                else if (filter.name == "DISTORTION")
                {
                    if (filter.HasValue("enabled")) source.distortion_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("distortion_level")) source.distortion_filter.distortionLevel = Single.Parse(filter.GetValue("distortion_level"));
                }
                else if (filter.name == "ECHO")
                {
                    if (filter.HasValue("enabled")) source.echo_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("delay")) source.echo_filter.delay = Single.Parse(filter.GetValue("delay"));
                    if (filter.HasValue("decay_ratio")) source.echo_filter.decayRatio = Single.Parse(filter.GetValue("decay_ratio"));
                    if (filter.HasValue("dry_mix")) source.echo_filter.dryMix = Single.Parse(filter.GetValue("dry_mix"));
                    if (filter.HasValue("wet_mix")) source.echo_filter.wetMix = Single.Parse(filter.GetValue("wet_mix"));
                }
                else if (filter.name == "HIGHPASS")
                {
                    if (filter.HasValue("enabled")) source.highpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("cutoff_freq")) source.highpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
                    if (filter.HasValue("resonance_q")) source.highpass_filter.highpassResonanceQ = Single.Parse(filter.GetValue("resonance_q"));
                }
                else if (filter.name == "LOWPASS")
                {
                    if (filter.HasValue("enabled")) source.lowpass_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("cutoff_freq")) source.lowpass_filter.cutoffFrequency = Single.Parse(filter.GetValue("cutoff_freq"));
                    if (filter.HasValue("resonance_q")) source.lowpass_filter.lowpassResonanceQ = Single.Parse(filter.GetValue("resonance_q"));
                }
                else if (filter.name == "REVERB")
                {
                    if (filter.HasValue("enabled")) source.reverb_filter.enabled = Boolean.Parse(filter.GetValue("enabled"));
                    if (filter.HasValue("reverb_preset")) source.reverb_filter.reverbPreset = (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), filter.GetValue("reverb_preset"));
                    if (filter.HasValue("dry_level")) source.reverb_filter.dryLevel = Single.Parse(filter.GetValue("dry_level"));
                    if (filter.HasValue("room")) source.reverb_filter.room = Single.Parse(filter.GetValue("room"));
                    if (filter.HasValue("room_hf")) source.reverb_filter.roomHF = Single.Parse(filter.GetValue("room_hf"));
                    if (filter.HasValue("room_lf")) source.reverb_filter.roomLF = Single.Parse(filter.GetValue("room_lf"));
                    if (filter.HasValue("decay_time")) source.reverb_filter.decayTime = Single.Parse(filter.GetValue("decay_time"));
                    if (filter.HasValue("decay_hf_ratio")) source.reverb_filter.decayHFRatio = Single.Parse(filter.GetValue("decay_hf_ratio"));
                    if (filter.HasValue("reflections_level")) source.reverb_filter.reflectionsLevel = Single.Parse(filter.GetValue("reflections_level"));
                    if (filter.HasValue("reflections_delay")) source.reverb_filter.reflectionsDelay = Single.Parse(filter.GetValue("reflections_delay"));
                    if (filter.HasValue("reverb_level")) source.reverb_filter.reverbLevel = Single.Parse(filter.GetValue("reverb_level"));
                    if (filter.HasValue("reverb_delay")) source.reverb_filter.reverbDelay = Single.Parse(filter.GetValue("reverb_delay"));
                    if (filter.HasValue("diffusion")) source.reverb_filter.diffusion = Single.Parse(filter.GetValue("diffusion"));
                    if (filter.HasValue("density")) source.reverb_filter.density = Single.Parse(filter.GetValue("density"));
                    if (filter.HasValue("hf_reference")) source.reverb_filter.hfReference = Single.Parse(filter.GetValue("hf_reference"));
                    if (filter.HasValue("lf_reference")) source.reverb_filter.lfReference = Single.Parse(filter.GetValue("lf_reference"));
                }
            }
            Log.dbg("single beepsource values pasted from beepsource_clipboard");
        }
        
        //Set some default stuff
        private static string[] add_default_audiosets_directories = {"apollo11", "sts1", "russian", "valentina"};
        private void add_default_audiosets()
        {
            foreach (string dir in add_default_audiosets_directories)
            {
                ChatterAudioList cal = new ChatterAudioList();
                cal.directory = dir;
                cal.is_active = true;
                chatter_array.Add(cal);
            }

            Log.dbg("audioset defaults added :: new count = {0}", chatter_array.Count);
        }

        private void add_default_beepsources()
        {
            for (int i = 0; i < 3; i++)
            {
                add_new_beepsource();
            }
        }

        private void add_default_backgroundsources()
        {
            for (int i = 0; i < 2; i++)
            {
                add_new_backgroundsource();
            }
        }

        private void mute_check()
        {
            //mute check
            if (mute_all)
            {
                //mute is on
                if (all_muted == false)
                {
                    //but things aren't muted
                    //mute them
                    if (chatter_exists)
                    {
                        initial_chatter.mute = true;
                        response_chatter.mute = true;
                        quindar1.mute = true;
                        quindar2.mute = true;
                    }

                    foreach (BackgroundSource src in backgroundsource_list)
                    {
                        src.audiosource.mute = true;
                    }

                    if (aae_breathing_exist) aae_breathing.mute = true;
                    if (aae_soundscapes_exist) aae_soundscape.mute = true;
                    if (aae_breathing_exist) aae_wind.mute = true;
                    if (aae_airlock_exist) aae_airlock.mute = true;

                    foreach (BeepSource source in beepsource_list)
                    {
                        source.audiosource.mute = true;
                    }
                    
                    if (sstv_exists) sstv.mute = true;

                    all_muted = true;   //and change flag
                }
            }
            else
            {
                //mute is off
                if (all_muted)
                {
                    //but things are muted
                    //unmute them
                    if (chatter_exists)
                    {
                        initial_chatter.mute = false;
                        response_chatter.mute = false;
                        quindar1.mute = false;
                        quindar2.mute = false;
                    }

                    foreach (BackgroundSource src in backgroundsource_list)
                    {
                        src.audiosource.mute = false;
                    }

                    if (aae_breathing_exist) aae_breathing.mute = false;
                    if (aae_soundscapes_exist) aae_soundscape.mute = false;
                    if (aae_wind_exist) aae_wind.mute = false;
                    if (aae_airlock_exist) aae_airlock.mute = false;

                    foreach (BeepSource source in beepsource_list)
                    {
                        source.audiosource.mute = false;
                    }
                    
                    if (sstv_exists) sstv.mute = false;

                    all_muted = false;   //and change flag
                }
            }
        }

        private void radio_check()
        {
            if (yep_yep == null)
            {
                Log.info("radio_check, yep_yep is null");
                yep_yep = "";
            }
            foreach (char c in Input.inputString)
            {
                //backspace char
                //if (c == "\b"[0])
                if (c == '\b')
                {
                    if (yep_yep.Length != 0)
                    {
                        yep_yep = yep_yep.Substring(0, yep_yep.Length - 1);
                    }
                }
                else
                {
                    //if (c == "\n"[0] || c == "\r"[0])
                    //{
                    //print("User entered his name: " + yep_yep);
                    //}
                    //else
                    // {
                    yep_yep += c;
                    //}
                }
            }

            if (yep_yep.Length > 5) yep_yep = yep_yep.Substring(1, 5);  //only keep 5 chars, get rid of the first

            if (http_update_check && yep_yep == "radio" && yep_yepsource.isPlaying == false)
            {
                Log.dbg("play radio");


                //need a bool that radio_yepyep is loaded ok
                if (yep_yep_loaded == false)
                {
                    load_radio();
                }

                //Play "radio"
                yep_yepsource.Play();
                yep_yep = "";
            }
        }

        //Tooltips
        private void tooltips(Rect pos)
        {
            if (show_tooltips && GUI.tooltip != "")
            {
                float w = 5.5f * GUI.tooltip.Length;
                float x = (Event.current.mousePosition.x < pos.width / 2) ? Event.current.mousePosition.x + 10 : Event.current.mousePosition.x - 10 - w;
                float h = 25f;
                float t = Event.current.mousePosition.y - (h / 4);
                GUI.Box(new Rect(x, t, w, h), GUI.tooltip, gs_tooltip);
            }
        }
        
        //Main
        public void Awake()
        {
            Log.dbg("Awake() starting...");

            chatter_player = new GameObject();
            sstv_player = new GameObject();
            aae_soundscape_player = new GameObject();
            aae_ambient_player = new GameObject();

            //Filters need to be added here BEFORE load_settings() or nullRef when trying to apply filter settings to non-existant filters
            chatter_player.name = "rbr_chatter_player";
            initial_chatter = chatter_player.AddComponent<AudioSource>();
            initial_chatter.volume = chatter_vol_slider;
            initial_chatter.spatialBlend = 0.0f;   //set as 2D audio
            response_chatter = chatter_player.AddComponent<AudioSource>();
            response_chatter.volume = chatter_vol_slider;
            response_chatter.spatialBlend = 0.0f;
            quindar1 = chatter_player.AddComponent<AudioSource>();
            quindar1.volume = quindar_vol_slider;
            quindar1.spatialBlend = 0.0f;
            quindar2 = chatter_player.AddComponent<AudioSource>();
            quindar2.volume = quindar_vol_slider;
            quindar2.spatialBlend = 0.0f;
            chatter.chorus_filter = chatter_player.AddComponent<AudioChorusFilter>();
            chatter.chorus_filter.enabled = false;
            chatter.distortion_filter = chatter_player.AddComponent<AudioDistortionFilter>();
            chatter.distortion_filter.enabled = false;
            chatter.echo_filter = chatter_player.AddComponent<AudioEchoFilter>();
            chatter.echo_filter.enabled = false;
            chatter.highpass_filter = chatter_player.AddComponent<AudioHighPassFilter>();
            chatter.highpass_filter.enabled = false;
            chatter.lowpass_filter = chatter_player.AddComponent<AudioLowPassFilter>();
            chatter.lowpass_filter.enabled = false;
            chatter.reverb_filter = chatter_player.AddComponent<AudioReverbFilter>();
            chatter.reverb_filter.enabled = false;


            //AAE
            load_AAE_background_audio();
            load_AAE_soundscape_audio();

            if (aae_soundscapes_exist)
            {
                aae_soundscape = aae_soundscape_player.AddComponent<AudioSource>();
                aae_soundscape.spatialBlend = 0.0f;
                aae_soundscape.volume = 0.3f;
                set_soundscape_clip();
                new_soundscape_loose_timer_limit();
            }

            //AAE EVA breathing
            aae_breathing = aae_ambient_player.AddComponent<AudioSource>();
            aae_breathing.spatialBlend = 0.0f;
            aae_breathing.volume = 1.0f;
            aae_breathing.loop = true;
            string breathing_path = GDBAsset.Solve("Sounds", "AAE", "effect", "breathing");
            if (GameDatabase.Instance.ExistsAudioClip(breathing_path))
            {
                aae_breathing_exist = true;
                aae_breathing.clip = GameDatabase.Instance.GetAudioClip(breathing_path);
                Log.dbg("{0} loaded OK", breathing_path);
            }
            else
            {
                Log.warn("{0} not found", breathing_path);
            }

            //AAE airlock
            aae_airlock = aae_ambient_player.AddComponent<AudioSource>();
            aae_airlock.spatialBlend = 0.0f;
            aae_airlock.volume = 1.0f;
            string airlock_path = GDBAsset.Solve("Sounds", "AAE", "effect", "airlock");
            if (GameDatabase.Instance.ExistsAudioClip(airlock_path))
            {
                aae_airlock_exist = true;
                aae_airlock.clip = GameDatabase.Instance.GetAudioClip(airlock_path);
                Log.dbg("{0} loaded OK", airlock_path);
            }
            else
            {
                Log.warn("{0} not found", airlock_path);
            }

            //AAE wind
            aae_wind = aae_ambient_player.AddComponent<AudioSource>();
            aae_wind.spatialBlend = 0.0f;
            aae_wind.volume = 1.0f;
            string wind_path = GDBAsset.Solve("Sounds", "AAE", "wind", "mario1298__weak-wind");
            if (GameDatabase.Instance.ExistsAudioClip(wind_path))
            {
                aae_wind_exist = true;
                aae_wind.clip = GameDatabase.Instance.GetAudioClip(wind_path);
                Log.dbg("{0} loaded OK", wind_path);
            }
            else
            {
                Log.warn("{0} not found", wind_path);
            }

            //yepyep
            yep_yepsource = aae_ambient_player.AddComponent<AudioSource>();
            yep_yepsource.spatialBlend = 0.0f;
            yep_yepsource.volume = 1.0f;

            //AAE landing
            landingsource = aae_ambient_player.AddComponent<AudioSource>();
            landingsource.spatialBlend = 0.0f;
            landingsource.volume = 0.5f;
            string landing_path = GDBAsset.Solve("Sounds", "AAE", "loop", "suspense1");
            if (GameDatabase.Instance.ExistsAudioClip(landing_path))
            {
                landingsource.clip = GameDatabase.Instance.GetAudioClip(landing_path);
                Log.dbg("{0} loaded OK", landing_path);
            }
            else
            {
                Log.warn("{0} not found", landing_path);
            }

            load_beep_audio();      // this must run before loading settings (else no beep clips to assign to sources))

            this.line_512x4                         = Asset.Texture2D.LoadFromFile(false, "Textures", "line_512x4");
            // initialise launcherButton textures
            this.chatterer_button_TX                = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_TX");
            this.chatterer_button_TX_muted          = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_TX_muted");
            this.chatterer_button_RX                = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_RX");
            this.chatterer_button_RX_muted          = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_RX_muted");
            this.chatterer_button_SSTV              = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_SSTV");
            this.chatterer_button_SSTV_muted        = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_SSTV_muted");
            this.chatterer_button_idle              = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_idle");
            this.chatterer_button_idle_muted        = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_idle_muted");
            this.chatterer_button_disabled          = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_disabled");
            this.chatterer_button_disabled_muted    = Asset.Texture2D.LoadFromFile(false, "Textures", "chatterer_button_disabled_muted");

            load_plugin_settings();

            load_quindar_audio();
            quindar1.clip = quindar_01_clip;
            quindar2.clip = quindar_02_clip;

            initialize_new_exchange();

            load_sstv_audio();
            sstv_player.name = "rbr_sstv_player";
            sstv = sstv_player.AddComponent<AudioSource>();
            sstv.volume = sstv_vol_slider;
            sstv.spatialBlend = 0.0f;

            new_sstv_loose_timer_limit();

            create_filter_defaults_node();

            build_skin_list();

            // Setup & callbacks
            //
            
            //for KSP Application Launcher
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIApplicationLauncherReady);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneChangeRequest);

            //to trigger Chatter
            GameEvents.onCrewOnEva.Add(OnCrewOnEVA);
            GameEvents.onCrewBoardVessel.Add(OnCrewBoard);
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            GameEvents.onStageSeparation.Add(OnStageSeparation);
            GameEvents.onVesselSituationChange.Add(OnVesselSituationChange);
            GameEvents.onVesselSOIChanged.Add(OnVesselSOIChanged);

            //to trigger SSTV on science tx
            GameEvents.OnScienceChanged.Add(OnScienceChanged);

            // for CommNet management
            GameEvents.CommNet.OnCommHomeStatusChange.Add(OnCommHomeStatusChange);

            // for whatnot
            GameEvents.onGamePause.Add(OnGamePause);
            GameEvents.onGameUnpause.Add(OnGameUnpause);

            Log.dbg("Awake() has finished...");
        }

        private void Start()
        {
            if (launcherButton == null)
            {
                OnGUIApplicationLauncherReady();
            }

            Log.dbg("Starting an exchange : Hello !");
            begin_exchange(0); // Trigger an exchange on Start to say hello
        }

        public void Update()
        {
            //Insta-... key setup
            if (insta_chatter_key_just_changed && Input.GetKeyUp(insta_chatter_key)) insta_chatter_key_just_changed = false;
            if (insta_sstv_key_just_changed && Input.GetKeyUp(insta_sstv_key)) insta_sstv_key_just_changed = false;

            mute_check();

            radio_check();

            launcherButtonTexture_check();
            
            if (FlightGlobals.ActiveVessel != null)
            {
                vessel = FlightGlobals.ActiveVessel;

                //set num_beep_pages for use in windows
                num_beep_pages = beepsource_list.Count / 10;
                if (beepsource_list.Count % 10 != 0) num_beep_pages++;
                prev_num_pages = num_beep_pages;


                //sample selector one-time play
                if (OTP_playing && OTP_source.audiosource.isPlaying == false)
                {
                    Log.dbg("one-time play has finished");
                    OTP_playing = false;
                    OTP_source.audiosource.clip = OTP_stored_clip;
                    //Log.dbg("OTP_source.current_clip = {0}", OTP_source.current_clip);
                    //set_beep_clip(OTP_source);
                }

                ////update remotetech info if needed
                //if (remotetech_toggle)
                //{
                //    rt_update_timer += Time.deltaTime;
                //    if (rt_update_timer > 2f)
                //    {
                //        updateRemoteTechData();
                //        rt_update_timer = 0;
                //    }
                //}

                ///////////////////////
                ///////////////////////
                //Do AAE

                //BACKGROUND
                if (aae_backgrounds_exist)
                {
                    //if vessel not qualified to have onboard noises, stop background audio
                    if (vessel.GetCrewCapacity() < 1 || vessel.vesselType != VesselType.Ship && vessel.vesselType != VesselType.Station && vessel.vesselType != VesselType.Base && vessel.vesselType != VesselType.Lander)
                    {
                        foreach (BackgroundSource src in backgroundsource_list)
                        {
                            if (src.audiosource.isPlaying == true)
                            {
                                src.audiosource.Stop();
                            }
                        }
                    }
                    //check if user chose to have only background when on IVA, and then check if in IVA 
                    else if ((aae_backgrounds_onlyinIVA && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA || CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)) || !aae_backgrounds_onlyinIVA)
                    {
                        foreach (BackgroundSource src in backgroundsource_list)
                        {
                            if (src.audiosource.isPlaying == false)
                            {
                                src.audiosource.loop = true;
                                src.audiosource.Play();
                            }
                        }
                    }
                    else //else stop background audio
                    {
                        foreach (BackgroundSource src in backgroundsource_list)
                        {
                            if (src.audiosource.isPlaying == true)
                            {
                                src.audiosource.Stop();
                            }
                        }
                    }
                }

                //SOUNDSCAPE
                if (aae_soundscapes_exist)
                {
                    if (aae_soundscape_freq == 0)
                    {
                        //turned off
                        aae_soundscape.Stop();
                    }
                    else if (aae_soundscape_freq == 4)
                    {
                        //don't play soundscapes when within kerbin atmo
                        if (vessel.mainBody.bodyName != "Kerbin" || (vessel.mainBody.bodyName == "Kerbin" && (vessel.situation == Vessel.Situations.ORBITING || vessel.situation == Vessel.Situations.ESCAPING)))
                        {
                            //continuous loop of clips
                            if (aae_soundscape.isPlaying == false)
                            {
                                Log.dbg("playing next soundscape clip in continuous loop...");
                                set_soundscape_clip();
                                aae_soundscape.Play();
                            }
                        }
                    }
                    else
                    {
                        //don't play soundscapes when within kerbin atmo
                        if (vessel.mainBody.bodyName != "Kerbin" || (vessel.mainBody.bodyName == "Kerbin" && (vessel.situation == Vessel.Situations.ORBITING || vessel.situation == Vessel.Situations.ESCAPING)))
                        {
                            //run timer until timer_limit is reached, then play a random clip
                            if (aae_soundscape.isPlaying == false)
                            {
                                aae_soundscape_timer += Time.deltaTime;
                                if (aae_soundscape_timer > aae_soundscape_timer_limit)
                                {
                                    Log.dbg("soundscape1 timer limit reached, playing next clip...");
                                    set_soundscape_clip();
                                    aae_soundscape.Play();
                                    aae_soundscape_timer = 0;   //reset timer
                                    new_soundscape_loose_timer_limit(); //roll new timer limit
                                }
                            }
                        }
                    }
                }

                //EVA BREATHING
                if (aae_breathing_exist)
                {
                    if (vessel.vesselType == VesselType.EVA && aae_breathing.isPlaying == false)
                    {
                        Log.dbg("breathingsource.Play() loop has started");
                        aae_breathing.Play();
                    }
                    if (vessel.vesselType != VesselType.EVA && aae_breathing.isPlaying)
                    {
                        Log.dbg("No longer EVA, breathingsource.Stop()");
                        aae_breathing.Stop();
                    }
                }

                //WIND
                if (aae_wind_exist)
                {
                    //check that body has atmosphere, vessel is within it
                    if (vessel.mainBody.atmosphere && vessel.atmDensity > 0 && (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.IVA && CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Internal))
                    {
                        //set volume according to atmosphere density
                        aae_wind.volume = aae_wind_vol_slider * Math.Min((float)vessel.atmDensity, 1);
                        //play the audio if not playing already
                        if (aae_wind.isPlaying == false)
                        {
                            Log.dbg("aae_wind.Play()");
                            aae_wind.loop = true;
                            aae_wind.Play();
                        }
                    }
                    else
                    {
                        //stop when out of atmosphere
                        if (aae_wind.isPlaying)
                        {
                            Log.dbg("aae_wind.Stop()");
                            if (aae_wind.isPlaying) aae_wind.Stop();
                        }
                    }
                }

                //END AAE
                /////////////////////////////////////////////
                /////////////////////////////////////////////
                //START SSTV


                //do SSTV
                if (sstv_exists)
                {
                    //insta-sstv activated
                    if ((inRadioContact) && (insta_sstv_key_just_changed == false && Input.GetKeyDown(insta_sstv_key) && sstv.isPlaying == false))
                    {
                        Log.dbg("beginning exchange,insta-SSTV");
                        if (exchange_playing)
                        {
                            //stop and reset any currently playing chatter
                            exchange_playing = false;
                            initial_chatter.Stop();
                            response_chatter.Stop();
                            initialize_new_exchange();
                        }
                        if (all_sstv_clips.Count > 0)
                        {
                            //get new clip, play it, set and get timers
                            sstv.clip = all_sstv_clips[rand.Next(0, all_sstv_clips.Count)];
                            sstv.Play();
                            sstv_timer = 0;
                            new_sstv_loose_timer_limit();
                        }
                        else Log.warn("No SSTV clips to play");
                    }

                    //timed/on science sstv
                    if (all_sstv_clips.Count > 0)
                    {
                        //timed : if clips exist, do things
                        if (sstv_freq > 0)
                        {
                            sstv_timer += Time.deltaTime;
                            if (sstv_timer > sstv_timer_limit)
                            {
                                sstv_timer = 0;
                                new_sstv_loose_timer_limit();
                                if (sstv.isPlaying == false && inRadioContact)
                                {

                                    //get a random one and play
                                    if (exchange_playing)
                                    {
                                        //stop and reset any currently playing chatter
                                        exchange_playing = false;
                                        initial_chatter.Stop();
                                        response_chatter.Stop();
                                        initialize_new_exchange();
                                    }
                                    sstv.clip = all_sstv_clips[rand.Next(0, all_sstv_clips.Count)];
                                    sstv.Play();
                                    //sstv_timer = 0;
                                    //new_sstv_loose_timer_limit();
                                }
                            }
                        }

                        //on science transmitted
                        if (all_sstv_clips.Count > 0 && sstv_on_science_toggle == true)
                        {
                            if (science_transmitted == true) 
                            {
                                if (sstv.isPlaying == false && (inRadioContact))
                                {
                                    //stop and reset any currently playing chatter
                                    if (exchange_playing)
                                    {
                                        exchange_playing = false;
                                        initial_chatter.Stop();
                                        response_chatter.Stop();
                                        initialize_new_exchange();
                                    }

                                    //get a random one and play
                                    sstv.clip = all_sstv_clips[rand.Next(0, all_sstv_clips.Count)];
                                    sstv.Play();

                                    Log.dbg("beginning exchange,science-SSTV...");
                                }

                                science_transmitted = false;
                            }
                        }
                    }
                }

                //END SSTV
                /////////////////////////////////////////////
                /////////////////////////////////////////////
                //START BEEPS

                //do beeps
                if (beeps_exists)
                {
                    if (dict_probe_samples.Count > 0 && OTP_playing == false && (inRadioContact))   //don't do any beeps here while OTP is playing
                    {
                        foreach (BeepSource bm in beepsource_list)
                        {
                            if (bm.precise)
                            {
                                //precise beeps
                                if (bm.precise_freq == -1)
                                {
                                    //beeps turned off at slider
                                    //bm.audiosource.Stop();    //squashed bug: this was breaking the one-time play button
                                    bm.audiosource.loop = false; ;  //instead of Stop(), just turn loop off in case it's on
                                }
                                else if (bm.precise_freq == 0)
                                {
                                    //looped beeps

                                    //disable looped sounds during chatter
                                    if ((disable_beeps_during_chatter && (initial_chatter.isPlaying || response_chatter.isPlaying)) || sstv.isPlaying)
                                    {
                                        bm.audiosource.Stop();
                                    }
                                    else
                                    {
                                        bm.audiosource.loop = true;
                                        if (bm.audiosource.isPlaying == false)
                                        {
                                            bm.audiosource.Play();
                                            SetAppLauncherButtonTexture(chatterer_button_SSTV);
                                        }
                                    }
                                }
                                else
                                {
                                    //timed beeps
                                    if (bm.audiosource.loop)
                                    {
                                        //if looping stop playing and set loop to off
                                        bm.audiosource.Stop();
                                        bm.audiosource.loop = false;
                                    }
                                    //then check the time
                                    bm.timer += Time.deltaTime;
                                    if (bm.timer > bm.precise_freq)
                                    {
                                        bm.timer = 0;
                                        //randomize beep if set to random (0)
                                        if (bm.randomizeBeep)
                                        {
                                            bm.current_clip = "Random";
                                            set_beep_clip(bm);
                                        }
                                        //play beep unless disable == true && exchange_playing == true
                                        if (sstv.isPlaying || ((initial_chatter.isPlaying || response_chatter.isPlaying) && disable_beeps_during_chatter)) return;   //no beep under these conditions
                                        else
                                        {
                                            //Log.dbg("timer limit reached, playing source {0}", bm.beep_name);
                                            bm.audiosource.Play();  //else beep
                                            SetAppLauncherButtonTexture(chatterer_button_SSTV);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                //imprecise beeps
                                //
                                //
                                //
                                //play a beep
                                //roll a new loose limit
                                if (bm.loose_freq == 0)
                                {
                                    //beeps turned off at slider
                                    //bm.audiosource.Stop();    //squashed bug: this was breaking the one-time play button
                                    bm.audiosource.loop = false; ;  //instead of Stop(), just turn loop off in case it's on
                                }
                                else
                                {
                                    bm.timer += Time.deltaTime;
                                    if (bm.timer > bm.loose_timer_limit)
                                    {
                                        bm.timer = 0;   //reset timer
                                        new_beep_loose_timer_limit(bm);    //set a new loose limit
                                        //randomize beep if set to random (0)
                                        if (bm.randomizeBeep)
                                        {
                                            bm.current_clip = "Random";
                                            set_beep_clip(bm);
                                        }
                                        if (sstv.isPlaying || ((initial_chatter.isPlaying || response_chatter.isPlaying) && disable_beeps_during_chatter)) return;   //no beep under these conditions
                                        
                                        else
                                        {
                                            bm.audiosource.Play();  //else beep
                                            SetAppLauncherButtonTexture(chatterer_button_SSTV);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //END BEEPS
                /////////////////////////////////////////////
                /////////////////////////////////////////////
                //START CHATTER

                //do insta-chatter if insta-chatter chatter key is pressed
                if (insta_chatter_key_just_changed == false && Input.GetKeyDown(insta_chatter_key))
                {
                    Log.dbg("beginning exchange,insta-chatter");

                    if (exchange_playing == true)
                    {
                        Log.dbg("insta-chatter : exchange already playing, be patient ...");
                        
                    }
                    else begin_exchange(0);
                }

                // Run timer to allow auto exchange to trigger if needed
                if (chatter_exists && vessel.GetCrewCount() > 0 && exchange_playing == false)
                {
                    //No exchange currently playing
                    secs_since_last_exchange += Time.deltaTime;

                    if (secs_since_last_exchange > secs_between_exchanges && chatter_freq > 0 && sstv.isPlaying == false)
                    {
                        Log.dbg("beginning exchange,auto");
                        begin_exchange(0);
                    }
                }
            }
            else
            {
                //FlightGlobals.ActiveVessel is null
                if (gui_running) stop_GUI();
            }
        }

        // Returns true when the pod is speaking to CapCom, or the pods is
        // transmitting SSTV data.
        public bool VesselIsTransmitting()
        {
            if (sstv.isPlaying)
            {
                return true;
            }
            else
            {
                if (exchange_playing)
                {
                    bool podInitiatedExchange = (initial_chatter_source == 1);
                    return (podInitiatedExchange) ? initial_chatter.isPlaying : response_chatter.isPlaying;
                }
                else
                {
                    return false;
                }
            }
        }

        // Returns true when CapCom is speaking to the capsule.
        public bool VesselIsReceiving()
        {
            if (exchange_playing)
            {
                bool capcomInitiatedExchange = (initial_chatter_source == 0);
                return (capcomInitiatedExchange) ? initial_chatter.isPlaying : response_chatter.isPlaying;
            }
            else
            {
                return false;
            }
        }

        // Initiate insta-chatter as if the player pressed the insta-chatter
        // button.  Treat it as always pod-initiated. like a crewmember
        // decided to talk to mission control.
        public void InitiateChatter()
        {
            if (insta_chatter_key_just_changed == false && exchange_playing == false && sstv.isPlaying == false)
            {
                //no chatter or sstv playing, play insta-chatter
                Log.dbg("beginning exchange,insta-chatter");

                pod_begins_exchange = true;
                begin_exchange(0);
            }
        }
    }
}
