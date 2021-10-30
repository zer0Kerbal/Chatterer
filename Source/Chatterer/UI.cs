///////////////////////////////////////////////////////////////////////////////
//
//    Chatterer /L Unleashed, a plugin for Kerbal Space Program from SQUAD
//    (https://www.kerbalspaceprogram.com/)
//    Copyright (C) 2020-2021 LisiasT 
//    Copyright (C) 2014-2020 Athlonic 
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

namespace Chatterer
{
	internal static class UI
	{
		internal static class Audio
		{

		}

		internal static class Image
		{
			private const string folder = "Textures";

	        //Textures
		    private static Texture2D _line_512x4; // = new Texture2D(512, 8, TextureFormat.ARGB32, false);
			internal static Texture2D line_512x4 => _line_512x4 ?? (_line_512x4 = Asset.Texture2D.LoadFromFile(false, folder, "line_512x4"));
		}

		internal static class Icon
		{
			// FIXME : create adequate Blizzy button textures!!

			private const string folder = "Textures";

			//KSP Stock application launcherButton
			private static Texture2D _chatterer_button_Texture = null; // = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_Texture => _chatterer_button_Texture ?? (_chatterer_button_Texture = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_Texture"));

			private static Texture2D _chatterer_button_TX = null; // = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_TX => _chatterer_button_TX ?? (_chatterer_button_TX = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_TX"));

			private static Texture2D _chatterer_button_TX_muted = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_TX_muted => _chatterer_button_TX_muted ?? (_chatterer_button_TX_muted = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_TX_muted"));

			private static Texture2D _chatterer_button_RX = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_RX => _chatterer_button_RX ?? (_chatterer_button_RX = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_RX"));

			private static Texture2D _chatterer_button_RX_muted = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_RX_muted => _chatterer_button_RX_muted ?? (_chatterer_button_RX_muted = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_RX_muted"));

			private static Texture2D _chatterer_button_SSTV = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_SSTV => _chatterer_button_SSTV ?? (_chatterer_button_SSTV = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_SSTV"));

			private static Texture2D _chatterer_button_SSTV_muted = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_SSTV_muted => _chatterer_button_SSTV_muted ?? (_chatterer_button_SSTV_muted = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_SSTV_muted"));

			private static Texture2D _chatterer_button_idle = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_idle => _chatterer_button_idle ?? (_chatterer_button_idle = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_idle"));

			private static Texture2D _chatterer_button_idle_muted = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_idle_muted => _chatterer_button_idle_muted ?? (_chatterer_button_idle_muted = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_idle_muted"));

			private static Texture2D _chatterer_button_disabled = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_disabled => _chatterer_button_disabled ?? (_chatterer_button_disabled = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_disabled"));

			private static Texture2D _chatterer_button_disabled_muted = null; //  = new Texture2D(38, 38, TextureFormat.ARGB32, false);
			internal static Texture2D chatterer_button_disabled_muted => _chatterer_button_disabled_muted ?? (_chatterer_button_disabled_muted = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_button_disabled_muted"));

			// Blizzy Toolbar
			private static Texture2D _chatterer_icon_toolbar;
			internal static Texture2D chatterer_icon_toolbar => _chatterer_icon_toolbar ?? (_chatterer_icon_toolbar = Asset.Texture2D.LoadFromFile(false, folder, "chatterer_icon_toolbar"));

		}
	}
}
