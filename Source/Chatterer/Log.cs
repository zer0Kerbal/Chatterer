using System;
using KSPe.Util.Log;
using System.Diagnostics;

#if DEBUG
using System.Collections.Generic;
#endif

namespace Chatterer
{
	public static class Log
	{
		private static readonly Logger log = Logger.CreateForType<Startup>();

		internal static void init()
		{
			log.level =
#if DEBUG
				Level.TRACE
#else
                Level.INFO
#endif
				;
		}

		internal static void force(string msg, params object[] @params)
		{
			log.force(msg, @params);
		}

		internal static void info(string msg, params object[] @params)
		{
			log.info(msg, @params);
		}

		internal static void warn(string msg, params object[] @params)
		{
			log.warn(msg, @params);
		}

		internal static void detail(string msg, params object[] @params)
		{
			log.detail(msg, @params);
		}

		internal static void error(Exception e, object offended)
		{
			log.error(offended, e);
		}

		internal static void error(string msg, params object[] @params)
		{
			log.error(msg, @params);
		}

		[ConditionalAttribute("DEBUG")]
		internal static void dbg(string msg, params object[] @params)
		{
			log.trace(msg, @params);
		}

#if DEBUG
		private static readonly HashSet<string> DBG_SET = new HashSet<string>();
#endif

		[ConditionalAttribute("DEBUG")]
		internal static void dbgOnce(string msg, params object[] @params)
		{
			string new_msg = string.Format(msg, @params);
#if DEBUG
			if (DBG_SET.Contains(new_msg)) return;
			DBG_SET.Add(new_msg);
#endif
			log.trace(new_msg);
		}
	}
}
