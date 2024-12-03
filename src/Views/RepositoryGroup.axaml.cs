using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{

    public partial class RepositoryGroup : UserControl
    {
        public RepositoryGroup()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled)
            {
                if (e.Key == Key.Down && ViewModels.Welcome.Instance.Rows.Count > 0)
                {
                    TreeContainer.SelectedIndex = 0;
                    TreeContainer.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    (DataContext as ViewModels.RepositoryGroup).ClearSearchFilter();
                    e.Handled = true;
                }
            }
        }

        private void OnTreeViewKeyDown(object _, KeyEventArgs e)
        {
            if (TreeContainer.SelectedItem is ViewModels.RepositoryNode node && e.Key == Key.Enter)
            {
                if (node.IsRepository)
                {
                    var parent = this.FindAncestorOfType<Launcher>();
                    if (parent is { DataContext: ViewModels.Launcher launcher })
                        launcher.OpenRepositoryInTab(node, null);
                }
                else
                {
                    (DataContext as ViewModels.RepositoryGroup).ToggleNodeIsExpanded(node);
                }

                e.Handled = true;
            }
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var menu = (DataContext as ViewModels.RepositoryGroup).CreateContextMenu(node);
                menu.Open(menu);
                e.Handled = true;
            }
        }

        private void OnPointerPressedTreeNode(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
            {
                _pressedTreeNode = true;
                _startDragTreeNode = false;
                _pressedTreeNodePosition = e.GetPosition(sender as Grid);
            }
            else
            {
                _pressedTreeNode = false;
                _startDragTreeNode = false;
            }
        }

        private void OnPointerReleasedOnTreeNode(object sender, PointerReleasedEventArgs _2)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;

            if (sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                (DataContext as ViewModels.RepositoryGroup).OpenRepo(node);
            }
        }

        private void OnPointerMovedOverTreeNode(object sender, PointerEventArgs e)
        {
            if (_pressedTreeNode && !_startDragTreeNode &&
                sender is Grid { DataContext: ViewModels.RepositoryNode node } grid)
            {
                var delta = e.GetPosition(grid) - _pressedTreeNodePosition;
                var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
                if (sizeSquired < 64)
                    return;

                _startDragTreeNode = true;

                var data = new DataObject();
                data.Set("MovedRepositoryTreeNode", node);
                DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            }
        }

        private void OnTreeViewLostFocus(object _1, RoutedEventArgs _2)
        {
            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeView(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") || e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void DropOnTreeView(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") && e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;
                (DataContext as ViewModels.RepositoryGroup).MoveNode(moved, null);
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        OpenOrInitRepository(item.Path.LocalPath);
                        break;
                    }
                }
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void DragOverTreeNode(object sender, DragEventArgs e)
        {
            if (e.Data.Contains("MovedRepositoryTreeNode") || e.Data.Contains(DataFormats.Files))
            {
                var grid = sender as Grid;
                if (grid == null)
                    return;

                var to = grid.DataContext as ViewModels.RepositoryNode;
                if (to == null)
                    return;

                if (to.IsRepository)
                {
                    e.DragEffects = DragDropEffects.None;
                    e.Handled = true;
                }
                else
                {
                    e.DragEffects = DragDropEffects.Move;
                    e.Handled = true;
                }
            }
        }

        private void DropOnTreeNode(object sender, DragEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            var to = grid.DataContext as ViewModels.RepositoryNode;
            if (to == null || to.IsRepository)
            {
                e.Handled = true;
                return;
            }

            if (e.Data.Contains("MovedRepositoryTreeNode") &&
                e.Data.Get("MovedRepositoryTreeNode") is ViewModels.RepositoryNode moved)
            {
                e.Handled = true;

                if (to != moved)
                    (DataContext as ViewModels.RepositoryGroup).MoveNode(moved, to);
            }
            else if (e.Data.Contains(DataFormats.Files))
            {
                e.Handled = true;

                var items = e.Data.GetFiles();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        OpenOrInitRepository(item.Path.LocalPath, to);
                        break;
                    }
                }
            }

            _pressedTreeNode = false;
            _startDragTreeNode = false;
        }

        private void OnDoubleTappedTreeNode(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node })
            {
                if (node.IsRepository)
                {
                    var parent = this.FindAncestorOfType<Launcher>();
                    if (parent is { DataContext: ViewModels.Launcher launcher })
                        launcher.OpenRepositoryInTab(node, new ViewModels.LauncherPage());
                }
                else
                {
                    (DataContext as ViewModels.RepositoryGroup).ToggleNodeIsExpanded(node);
                }

                e.Handled = true;
            }
        }

        private void OpenOrInitRepository(string path, ViewModels.RepositoryNode parent = null)
        {
            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path);
                else
                    return;
            }

            var test = new Commands.QueryRepositoryRootPath(path).ReadToEnd();
            if (!test.IsSuccess || string.IsNullOrEmpty(test.StdOut))
            {
                (DataContext as ViewModels.RepositoryGroup).InitRepository(path, parent, test.StdErr);
                return;
            }

            var normalizedPath = test.StdOut.Trim().Replace("\\", "/");
            var node = ViewModels.Preference.Instance.FindOrAddNodeByRepositoryPath(normalizedPath, parent, true);
            (DataContext as ViewModels.RepositoryGroup).Refresh();

            var launcher = this.FindAncestorOfType<Launcher>()?.DataContext as ViewModels.Launcher;
            launcher?.OpenRepositoryInTab(node, launcher.ActivePage);
        }

        private void Fetch(object _, RoutedEventArgs e)
        {
            (DataContext as ViewModels.RepositoryGroup)?.Fetch();
            e.Handled = true;
        }

        private void Pull(object _, RoutedEventArgs e)
        {
            (DataContext as ViewModels.RepositoryGroup)?.Pull();
            e.Handled = true;
        }

        private void Push(object _, RoutedEventArgs e)
        {
            (DataContext as ViewModels.RepositoryGroup)?.Push();
            e.Handled = true;
        }
        private void Changes(object _, RoutedEventArgs e)
        {
            (DataContext as ViewModels.RepositoryGroup)?.Changes();
            e.Handled = true;
        }

        private bool _pressedTreeNode = false;
        private Point _pressedTreeNodePosition = new Point();
        private bool _startDragTreeNode = false;
    }
}
