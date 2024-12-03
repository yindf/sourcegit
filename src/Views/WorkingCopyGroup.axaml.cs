using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SourceGit.Models;

namespace SourceGit.Views
{
    public partial class WorkingCopyGroup : UserControl
    {
        public WorkingCopyGroup()
        {
            InitializeComponent();

            DragDrop.SetAllowDrop(StagedChangesView, true);
            StagedChangesView.AddHandler(DragDrop.DropEvent, OnDropToStage, RoutingStrategies.Bubble | RoutingStrategies.Direct);
            DragDrop.SetAllowDrop(UnstagedChangesView, true);
            UnstagedChangesView.AddHandler(DragDrop.DropEvent, OnDropToUnstage, RoutingStrategies.Bubble | RoutingStrategies.Direct);
        }

        private void OnOpenCommitMessagePicker(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.WorkingCopyGroup vm)
            {
                var menu = vm.CreateContextMenuForCommitMessages();
                menu.Placement = PlacementMode.TopEdgeAlignedLeft;
                menu?.Open(button);
                e.Handled = true;
            }
        }

        private void OnUnstagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm && sender is Control control)
            {
                var menu = vm.CreateContextMenuForUnstagedChanges();
                menu?.Open(control);
                e.Handled = true;
            }
        }

        private void OnStagedContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm && sender is Control control)
            {
                var menu = vm.CreateContextMenuForStagedChanges();
                menu?.Open(control);
                e.Handled = true;
            }
        }

        private void OnUnstagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                vm.StageSelected(next);
                UnstagedChangesView.Focus();
                e.Handled = true;
            }
        }

        private void OnStagedChangeDoubleTapped(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.Focus();
                e.Handled = true;
            }
        }

        private void OnUnstagedKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm)
            {
                if (e.Key is Key.Space or Key.Enter)
                {
                    var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                    vm.StageSelected(next);
                    UnstagedChangesView.Focus();
                    e.Handled = true;
                    return;
                }

                if (e.Key is Key.Delete or Key.Back && vm.SelectedUnstaged is { Count: > 0 } selected)
                {
                    vm.Discard(selected);
                    e.Handled = true;
                }
            }
        }

        private void OnStagedKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm && e.Key is Key.Space or Key.Enter)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.Focus();
                e.Handled = true;
            }
        }

        private void OnStageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm)
            {
                var next = UnstagedChangesView.GetNextChangeWithoutSelection();
                vm.StageSelected(next);
                UnstagedChangesView.Focus();
            }

            e.Handled = true;
        }

        private void OnUnstageSelectedButtonClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm)
            {
                var next = StagedChangesView.GetNextChangeWithoutSelection();
                vm.UnstageSelected(next);
                StagedChangesView.Focus();
            }

            e.Handled = true;
        }

        private void OnDropToStage(object sender, DragEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm && sender is ChangeCollectionView view && e.Data.Contains("changeCollectionObject"))
            {
                vm.StageChanges(e.Data.Get("changeCollectionObject") as List<Change>, null);
            }
        }

        private void OnDropToUnstage(object sender, DragEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopyGroup vm && sender is ChangeCollectionView view && e.Data.Contains("changeCollectionObject"))
            {
                vm.UnstageChanges(e.Data.Get("changeCollectionObject") as List<Change>, null);
            }
        }
    }
}
