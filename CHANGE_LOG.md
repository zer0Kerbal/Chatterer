# Chatterer :: Change Log

* 2018-0312: 0.9.94 (Athlonic) for KSP 1.4.0
		- Recompiled for KSP v1.4.0.2077
		- Fixed reverb_filter deprecated function roomRolloff preventing plugin execution in KSP v1.4
* 2017-0527: 0.9.93 (Athlonic) for KSP ['1.3.1', '1.3']
		- Recompiled for KSP v1.3.0.1804
		- Fix for chatter spam on jumps (avoid exchange to trigger every time vessel leaves the ground)
		- Automatically mute all sounds when game is paused
* 2016-1231: 0.9.92 (Athlonic) for KSP ['1.2.2', '1.2.1', '1.2']
	+ Changelog:
			- Recompiled for KSP v1.2.2.1622
			- Removed Linq reference (for good)
			- Reimplemented Blizzy78's Toolbar support
* 2016-1012: 0.9.91 (Athlonic) for KSP 1.2-prerelease
		- Recompiled for KSP v1.2.0.1586
		- Removed obsolete KSPUtil reference
		- Replaced RemoteTech behaviours with stock CommNet thingy
		- Removed "using System.Linq" as advised by Squad to help performances
		- Removed RemoteTech support (for now because of Linq use)
		- Removed Blizzy78's toolbar support (for now because of Linq use)
		- Removed other useless "using ..."
* 2016-0626: 0.9.90 (Athlonic) for KSP 1.1
		- Recompiled for KSP v1.1.3.1289
* 2016-0419: 0.9.8 (Athlonic) for KSP ['1.1', '1.1-prerelease']
	+ v0.9.8 [19 Apr 2016]
		- Recompiled for KSP v1.1.0.1230
		- Fix for capsule always initiating exchange once set to true / Thanks to MoarDV
		- Implemented API for interactions with other mods / Thanks to MoarDV, again
		- Stop playing wind sounds when inside a pod/cockpit
		- Bundled MiniAVC v1.0.3.2
* 2016-0221: 0.9.7 (Athlonic) for KSP ['1.0.5', '1.0.4', '1.0.3', '1.0.2', '1.0.0']
		- Recompiled for KSP v1.0.5.1028
* 2015-0713: 0.9.6 (Athlonic) for KSP ['1.0.4', '1.0.3', '1.0.2', '1.0.0']
	+ Recompiled for KSP v1.0.4
	+ Code optimizations more GameEvents/less onUpdate loop checks
	+ Chatter gender is checked on exchange init rather than on vessel change
	+ Fixed 2 exceptions which could happen in some situations
	+ Hide GUI is now a global setting (no more per vessel based)
	+ Added SSTV when science has been transmitted to KSC
	+ Added "SSTV on science" toggle setting (under beep settings / on by
	+ default)
		- Reduced SSTV default volume from 25 to 15% to better match with other
	+ sounds
		- Fixed applauncherbutton textures handling(Now green actually means transmit
	+ and blue receive, instead of initial chatter/answer)
		- Made all audio stop when switching vessel and going to/coming from EVA
	+ (finally)
* 2015-0524: 0.9.5 (Athlonic) for KSP ['1.0.4', '1.0.3', '1.0.2', '1.0.0']
		- fixed EXP spam on probes or no crew onbard.
	+ (thanks taniwha)
* 2015-0524: 0.9.4 (Athlonic) for KSP ['1.0.2', '1.0.0']
		- Added Female audioset (Talk to me Valentina)
	+ (Female capsule chatter will play when Kerbal in command (seat[0]) or EVA
	+ Kerbal is female)
* 2015-0523: 0.9.3 (Athlonic) for KSP ['1.0.2', '1.0.0']
	+ (second update of the day)
			- Added random beeps setting
* 2015-0523: 0.9.2 (Athlonic) for KSP ['1.0.2', '1.0.0']
	+ fixed "skin index get out of range" messing up the GUI in some situations
	+ added an option to have ship background noises only when in IVA (under AAE
	+ tab)
		- set "disable beep during chatter" to false by default (for realism sake)
		- textures converted to DDS (thanks to Avera9eJoe)
	+ (be sure to remove old .png files when upgrading)
* 2015-0502: 0.9.1 (Athlonic) for KSP 1.0.0
	+ Recompiled for KSP v1.0.2
* 2015-0428: 0.9.0 (Athlonic) for KSP 1.0.0
	+ Recompiled and added compatibility for KSP 1.0 :
	+ fix for deprecated (maxAtmosphereAltitude) method
	+ fix for new applicationLauncherButton management
		- Set Insta-keys at "none" by default
		- Added a button to clear Insta-keys binding
* 2015-0116: 0.8.1 (Athlonic) for KSP 0.90
	+ Fixed RemoteTech v1.6.0 support (update RT first if you are still using
	+ Removed RT delay before capsule initiating chatter exchange (silly me)
	+ optimized code by using game events for EVA airlock sound [thanks to
	+ Davorin]
		- Fixed internal ambient sounds playing when focusing Flags [thanks to
	+ Davorin]
* 2014-1216: 0.8.0 (Athlonic) for KSP 0.90
	+ recompiled for KSP v0.90
	+ reduced pitch of EVA breathing (to sound less metallic)
	+ removed 'CSharpfirstpass.dll' reference (no more needed)
* 2014-1008: 0.7.1 (Athlonic) for KSP 0.25
	+ recompiled for KSP v0.25
	+ fixed RemoteTech v1.5.0 integration (assembly name changed)
* 2014-0906: 0.7.0 (Athlonic) for KSP 0.24.2
	+ Added RemoteTech2 integration
	+ signal delay is added to chatter response delay
	+ signal loss will disable chatter capcom's responses, beeps and SSTV transmissions
		- fixed warning msg in log about custom filters (on Awake)
* 2014-0905: 0.6.4 (Athlonic) for KSP ['0.24.2', '0.24.1']
	+ fixed Airlock sound playing when switching vessel to/from EVAed Kerbal
	+ removed warning msg in log about "soundscape" folder missing if not
	+ installed (was a safe warning anyway)
			- fixed and restored "Mute" function
			- added KSP application launcher button behaviours :
		- green = transmit (TX)
		- blue = receive (RX)
		- white = SSTV / Beep (flashing)
		- grey = idle (online)
		- grey/red = muted
		- red = disabled (offline) (for later use with RT2)
			- code cleaning and optimizations
* 2014-0901: 0.6.2 (Athlonic) for KSP 0.24.2
	+ fixed "use Blizzy78' toolbar only" setting not loading
	+ Added/tweaked GitHub and .version files for KSP-AVC "Add-on Version
	+ Checker" plugin support
		- settings are now saved when closing UI instead of every 7 sec, this should
	+ help with performances, shouldn't it ? ^^
		- fixed "Show advanced options" not showing on probes
		- fixed Skin mess up and made "none" the default,
	+ (updated blacklist for unwanted Skin)
	+ (blacklisted dupe skins as well)
		- fixed chatter menu showing in some case even if chatter button was disabled
