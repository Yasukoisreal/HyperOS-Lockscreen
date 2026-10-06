using System;
using System.Windows;
using Microsoft.Phone.Info;
using Windows.Phone.System.LockScreenExtensibility;

namespace HyperOS.Helpers
{
    /// <summary>
    /// Helper wrapper around Windows Phone 8.1 LockScreen Extensibility APIs.
    /// Based on Microsoft Live Lock Screen & Tetra Lockscreen implementation.
    /// </summary>
    public static class ExtensibilityHelper
    {
        public static bool IsRegistered()
        {
            try
            {
                return ExtensibilityApp.IsLockScreenApplicationRegistered();
            }
            catch
            {
                return false;
            }
        }

        public static void Register()
        {
            try
            {
                if (!ExtensibilityApp.IsLockScreenApplicationRegistered())
                {
                    ExtensibilityApp.RegisterLockScreenApplication();
                }
            }
            catch { }
        }

        public static void Unregister()
        {
            try
            {
                if (ExtensibilityApp.IsLockScreenApplicationRegistered())
                {
                    ExtensibilityApp.UnregisterLockScreenApplication();
                }
            }
            catch { }
        }

        public static int GetPinpadHeight()
        {
            try
            {
                return ExtensibilityApp.GetLockPinpadHeight();
            }
            catch
            {
                return 0;
            }
        }

        public static double GetScreenRatio()
        {
            try
            {
                object obj;
                if (DeviceExtendedProperties.TryGetValue("PhysicalScreenResolution", out obj) && obj is Size)
                {
                    return ((Size)obj).Width / 480.0;
                }
            }
            catch { }

            try
            {
                return (double)Application.Current.Host.Content.ScaleFactor / 100.0;
            }
            catch
            {
                return 1.0;
            }
        }

        public static double GetPinpadHeightDips()
        {
            int rawHeight = GetPinpadHeight();
            if (rawHeight <= 0) return 0;
            double ratio = GetScreenRatio();
            return (ratio > 0) ? (rawHeight / ratio) : rawHeight;
        }

        public static void BeginUnlock()
        {
            try
            {
                ExtensibilityApp.BeginUnlock();
            }
            catch { }
        }

        public static void EndUnlock()
        {
            try
            {
                ExtensibilityApp.EndUnlock();
            }
            catch { }
        }
    }
}
