using System.Collections.Generic;

namespace SourceGit.Models
{
    public static class Bookmarks
    {
        public static readonly Avalonia.Media.IBrush[] Brushes = [
            Avalonia.Media.Brushes.Transparent,
            Avalonia.Media.Brushes.Red,
            Avalonia.Media.Brushes.Orange,
            Avalonia.Media.Brushes.Gold,
            Avalonia.Media.Brushes.ForestGreen,
            Avalonia.Media.Brushes.DarkCyan,
            Avalonia.Media.Brushes.DeepSkyBlue,
            Avalonia.Media.Brushes.Purple,
        ];

        public static readonly List<int> Supported = new List<int>();

        static Bookmarks()
        {
            for (int i = 0; i < Brushes.Length; i++)
                Supported.Add(i);
        }

        public const int Transparent = 0;
        public const int Red = 1;
        public const int Orange = 2;
        public const int Gold = 3;
        public const int ForestGreen = 4;
        public const int DarkCyan = 5;
        public const int DeepSkyBlue = 6;
        public const int Purple = 7;
    }
}
