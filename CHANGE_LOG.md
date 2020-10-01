# Chatterer :: Change Log

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
