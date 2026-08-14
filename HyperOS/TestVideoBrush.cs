using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyperOS.Pages
{
    public class TestVideoBrush
    {
        public static void Test()
        {
            var vb = new VideoBrush();
            vb.SourceName = "TestMediaElement";
        }
    }
}
