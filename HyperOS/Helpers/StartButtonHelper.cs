using System;
using System.Reflection;
using Microsoft.Phone.Shell;

namespace HyperOS.Helpers
{
    /// <summary>
    /// Helper to hook into the internal Microsoft.Devices.StartButton event
    /// so the lock screen can respond cleanly when the hardware Windows/Start button is pressed.
    /// Based on Microsoft Live Lock Screen BETA implementation.
    /// </summary>
    public static class StartButtonHelper
    {
        public static void RegisterStartKey(EventHandler handler)
        {
            if (handler == null) return;
            try
            {
                Assembly assembly = ((object)PhoneApplicationService.Current).GetType().Assembly;
                Type type = assembly.GetType("Microsoft.Devices.StartButton");
                if (type == null) return;

                EventInfo evt = type.GetEvent("StartKeyPressed");
                if (evt == null) return;

                MethodInfo addMethod = evt.GetAddMethod(true);
                if (addMethod != null)
                {
                    addMethod.Invoke(AppDomain.CurrentDomain, new object[] { handler });
                }
            }
            catch { }
        }

        public static void UnregisterStartKey(EventHandler handler)
        {
            if (handler == null) return;
            try
            {
                Assembly assembly = ((object)PhoneApplicationService.Current).GetType().Assembly;
                Type type = assembly.GetType("Microsoft.Devices.StartButton");
                if (type == null) return;

                EventInfo evt = type.GetEvent("StartKeyPressed");
                if (evt == null) return;

                MethodInfo removeMethod = evt.GetRemoveMethod(true);
                if (removeMethod != null)
                {
                    removeMethod.Invoke(AppDomain.CurrentDomain, new object[] { handler });
                }
            }
            catch { }
        }
    }
}
