using System;
using System.Windows;
using BimJsonRevitImporter.Domain.Messaging;

namespace BimJsonRevitImporters.UI.Views
{
    /// <summary>
    /// Shows or activates the main import window. Used from App so that CmdHello does not need WPF references.
    /// </summary>
    public static class MainImportWindowHelper
    {
        private static Window _window;
        private static Action _onClosed;

        public static void ShowOrActivate(IRevitEventDispatcher dispatcher, Action onClosed)
        {
            if (_window != null)
            {
                try
                {
                    _window.Activate();
                    if (_window.WindowState == WindowState.Minimized)
                        _window.WindowState = WindowState.Normal;
                }
                catch
                {
                    _window = null;
                }
            }

            if (_window == null)
            {
                _onClosed = onClosed;
                _window = new MainImportWindow(dispatcher);
                _window.Closed += (s, e) =>
                {
                    _window = null;
                    _onClosed?.Invoke();
                };
                _window.Show();
            }
        }
    }
}
