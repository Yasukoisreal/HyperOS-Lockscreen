using System;
using System.Windows;
using System.Windows.Controls;

namespace HyperOS.Pages
{
    public class TestBitmapCache
    {
        public static void Test()
        {
            var me = new MediaElement();
            me.CacheMode = new BitmapCache();
        }
    }
}
