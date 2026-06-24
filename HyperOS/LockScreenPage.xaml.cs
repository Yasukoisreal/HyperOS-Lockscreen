using System;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Windows.Phone.System;

namespace HyperOS
{
    public partial class LockScreenPage : PhoneApplicationPage
    {
        public LockScreenPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // If screen is locked → show lock screen
            // If user opened app normally → show settings
            try
            {
                if (SystemProtection.ScreenLocked)
                {
                    NavigationService.Navigate(
                        new Uri("/Pages/LockScreen.xaml", UriKind.Relative));
                }
                else
                {
                    NavigationService.Navigate(
                        new Uri("/Pages/MySetsPage.xaml", UriKind.Relative));
                }
            }
            catch
            {
                // Fallback to My Sets if SystemProtection fails
                NavigationService.Navigate(
                    new Uri("/Pages/MySetsPage.xaml", UriKind.Relative));
            }
        }
    }
}
