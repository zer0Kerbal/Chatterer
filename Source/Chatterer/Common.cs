using System;
namespace Chatterer
{
	public enum ChatStarter
	{
		capcom = 0,
		pod = 1
	}

	public enum PlayingState
	{
		disabled,
		disabled_muted,
		idle,
		idle_muted,
		tx,
		tx_muted,
		rx,
		rx_muted,
		sstv,
		sstv_muted,
	}
}
