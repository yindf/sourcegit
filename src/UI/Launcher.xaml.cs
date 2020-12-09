using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Main window for this app.
    /// </summary>
    public partial class Launcher : Window {

        /// <summary>
        ///     Tab data.
        /// </summary>
        public class Tab : INotifyPropertyChanged {
            private bool isActive = false;

            public string Title { get; set; }
            public string Tooltip { get; set; }
            public Git.Repository Repo { get; set; }
            public object Page { get; set; }

            public bool IsRepo {
                get { return Repo != null; }
            }

            public int Color {
                get { return Repo == null ? 0 : Repo.Color; }
                set {
                    if (Repo == null || Repo.Color == value) return;
                    Repo.Color = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Color"));
                }
            }

            public bool IsActive {
                get { return isActive; }
                set {
                    if (isActive == value) return;
                    isActive = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsActive"));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>
        ///     Manager tab
        /// </summary>
        public class ManagerTab : Tab {
            public ManagerTab() {
                Title = "HOME";
                Tooltip = "Repositories Manager";
                IsActive = true;
                Page = new Manager();
            }
        }

        /// <summary>
        ///     Repository tab.
        /// </summary>
        public class RepoTab : Tab {
            public RepoTab(Git.Repository repo, Dashboard page) {
                Title = repo.Parent == null ? repo.Name : $"{repo.Parent.Name} : {repo.Name}";
                Tooltip = repo.Path;
                Repo = repo;
                IsActive = false;
                Page = page;
            }
        }

        /// <summary>
        ///     Alerts.
        /// </summary>
        public ObservableCollection<string> Errors { get; set; } = new ObservableCollection<string>();

        /// <summary>
        ///     Opened tabs.
        /// </summary>
        public ObservableCollection<Tab> Tabs { get; set; } = new ObservableCollection<Tab>();

        /// <summary>
        ///     Constructor
        /// </summary>
        public Launcher() {
            Tabs.Add(new ManagerTab());
            InitializeComponent();
            openedTabs.SelectedItem = Tabs[0];
            if (App.Preference.CheckUpdate) Task.Run(CheckUpdate);
        }

        /// <summary>
        ///     Open repository
        /// </summary>
        /// <param name="repo"></param>
        public void Open(Git.Repository repo) {
            for (int i = 1; i < Tabs.Count; i++) {
                var opened = Tabs[i];
                if (opened.Repo.Path == repo.Path) {
                    openedTabs.SelectedItem = opened;
                    return;
                }
            }

            repo.Open();
            var page = new Dashboard(repo);
            var tab = new RepoTab(repo, page);
            repo.SetPopupManager(page.popupManager);
            Tabs.Add(tab);
            openedTabs.SelectedItem = tab;
        }

        /// <summary>
        ///     Checking for update.
        /// </summary>
        public void CheckUpdate() {
            try {
                var web = new WebClient();
                var raw = web.DownloadString("https://gitee.com/api/v5/repos/sourcegit/SourceGit/releases/latest");
                var ver = JsonSerializer.Deserialize<Git.Version>(raw);
                var cur = Assembly.GetExecutingAssembly().GetName().Version;

                var matches = Regex.Match(ver.TagName, @"^v(\d+)\.(\d+).*");
                if (!matches.Success) return;

                var major = int.Parse(matches.Groups[1].Value);
                var minor = int.Parse(matches.Groups[2].Value);
                if (major > cur.Major || (major == cur.Major && minor > cur.Minor)) {
                    Dispatcher.Invoke(() => {
                        var dialog = new UpdateAvailable(ver);
                        dialog.Owner = this;
                        dialog.ShowDialog();
                    });
                }
            } catch {
                // IGNORE
            }
        }

        #region LAYOUT_CONTENT
        /// <summary>
        ///     Close repository.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseRepo(object sender, RoutedEventArgs e) {
            var tab = (sender as Button).DataContext as Tab;
            if (tab == null || tab.Repo == null) {
                e.Handled = true;
                return;
            }

            Tabs.Remove(tab);

            tab.Page = null;
            tab.Repo.RemovePopup();
            tab.Repo.Close();
            tab.Repo = null;
        }

        /// <summary>
        ///     Context menu for tab items.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TabsContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var tab = (sender as TabItem).DataContext as Tab;
            if (tab == null || tab.Repo == null) {
                ev.Handled = true;
                return;
            }

            var repo = tab.Repo;

            var refresh = new MenuItem();
            refresh.Header = "Refresh";
            refresh.Click += (o, e) => {
                repo.AssertCommand(null);
                e.Handled = true;
            };

            var iconBookmark = FindResource("Icon.Bookmark") as Geometry;
            var bookmark = new MenuItem();
            bookmark.Header = "Bookmark";
            for (int i = 0; i < Converters.IntToRepoColor.Colors.Length; i++) {
                var icon = new System.Windows.Shapes.Path();
                icon.Style = FindResource("Style.Icon") as Style;
                icon.Data = iconBookmark;
                icon.Fill = Converters.IntToRepoColor.Colors[i];
                icon.Width = 8;

                var mark = new MenuItem();
                mark.Icon = icon;
                mark.Header = $"{i}";

                var refIdx = i;
                mark.Click += (o, e) => {
                    tab.Color = refIdx;
                    e.Handled = true;
                };

                bookmark.Items.Add(mark);
            }

            var copyPath = new MenuItem();
            copyPath.Header = "Copy path";
            copyPath.Click += (o, e) => {
                Clipboard.SetText(repo.Path);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(refresh);
            menu.Items.Add(bookmark);
            menu.Items.Add(copyPath);
            menu.IsOpen = true;

            ev.Handled = true;
        }

        /// <summary>
        ///     Open preference dialog.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowPreference(object sender, RoutedEventArgs e) {
            var dialog = new Preference();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        /// <summary>
        ///     Open about dialog.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowAbout(object sender, RoutedEventArgs e) {
            var about = new About();
            about.Owner = this;
            about.ShowDialog();
        }

        /// <summary>
        ///     Remove an alert.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveError(object sender, RoutedEventArgs e) {
            var alert = (sender as Button).DataContext as string;
            Errors.Remove(alert);
        }
        #endregion

        #region WINDOW_COMMANDS
        /// <summary>
        ///     Minimize
        /// </summary>
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        /// <summary>
        ///     Maximize/Restore
        /// </summary>
        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        /// <summary>
        ///     Quit
        /// </summary>
        private void Quit(object sender, RoutedEventArgs e) {
            App.Current.Shutdown();
        }
        #endregion

        #region DRAG_DROP
        private void TabsMouseMove(object sender, MouseEventArgs e) {
            var item = e.Source as TabItem;
            if (item == null) return;

            var tab = item.DataContext as Tab;
            if (tab == null || tab.Repo == null) return;

            if (Mouse.LeftButton == MouseButtonState.Pressed) {
                DragDrop.DoDragDrop(item, item, DragDropEffects.All);
                e.Handled = true;
            }
        }

        private void TabsDrop(object sender, DragEventArgs e) {
            var tabItemSrc = e.Data.GetData(typeof(TabItem)) as TabItem;
            var tabItemDst = e.Source as TabItem;
            if (tabItemSrc.Equals(tabItemDst)) return;

            var tabSrc = tabItemSrc.DataContext as Tab;
            var tabDst = tabItemDst.DataContext as Tab;
            if (tabDst.Repo == null) {
                Tabs.Remove(tabSrc);
                Tabs.Insert(1, tabSrc);
            } else {
                int dstIdx = Tabs.IndexOf(tabDst);

                Tabs.Remove(tabSrc);
                Tabs.Insert(dstIdx, tabSrc);
            }
        }
        #endregion

        #region TAB_SCROLL
        private void OpenedTabsSizeChanged(object sender, SizeChangedEventArgs e) {
            if (openedTabs.ActualWidth > openedTabsColumn.ActualWidth) {
                openedTabsOpts.Visibility = Visibility.Visible;
            } else {
                openedTabsOpts.Visibility = Visibility.Collapsed;
            }
        }

        private void ScrollToLeft(object sender, RoutedEventArgs e) {
            openedTabsScroller.LineLeft();
        }

        private void ScrollToRight(object sender, RoutedEventArgs e) {
            openedTabsScroller.LineRight();
        }
        #endregion
    }
}
