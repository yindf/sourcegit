using System;
using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConfirmCheckout : ChromelessWindow
    {
        public ConfirmCheckout()
        {
            InitializeComponent();
        }

        public Action OnConfirm;
        public Action OnCancel;

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void Confirm(object _1, RoutedEventArgs _2)
        {
            this.Close();
            OnConfirm?.Invoke();
        }

        private void Cancel(object _1, RoutedEventArgs _2)
        {
            this.Close();
            OnCancel?.Invoke();
        }
    }
}
