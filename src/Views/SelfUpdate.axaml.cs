using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class SelfUpdate : ChromelessWindow
    {
        public SelfUpdate()
        {
            InitializeComponent();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void GotoDownload(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser("http://192.168.16.51:9999/sourcegit");
            e.Handled = true;
        }

        private void IgnoreThisVersion(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Version ver })
                ViewModels.Preference.Instance.IgnoreUpdateTag = ver.TagName;

            Close();
            e.Handled = true;
        }
    }
}
