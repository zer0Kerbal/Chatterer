using UnityEngine;

namespace Chatterer
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class Startup : MonoBehaviour
    {
        private void Start()
        {
            Log.force("Version {0}", Version.Text);

            try
            {
                KSPe.Util.Installation.Check<Startup>();
            }
            catch (KSPe.Util.InstallmentException e)
            {
                Log.error(e, this);
                KSPe.Common.Dialogs.ShowStopperAlertBox.Show(e);
            }
        }
    }
}
