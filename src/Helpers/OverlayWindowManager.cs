using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;
using Panseo.Views;

namespace Panseo.Helpers
{
    public class OverlayWindowManager
    {
        private readonly List<OverlayWindow> _overlayWindows = new List<OverlayWindow>();
        private bool _isDisposed = false;

        public OverlayWindowManager()
        {
            InitializeOverlays();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void InitializeOverlays()
        {
            // 기존 창 닫기
            CloseAllOverlays();

            // 연결된 모든 모니터(Screen.AllScreens)마다 독립된 OverlayWindow 생성
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var overlay = new OverlayWindow(screen);
                overlay.Show();
                _overlayWindows.Add(overlay);
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_isDisposed) return;

            // 모니터 연결/해제/해상도 변경 시 안전하게 재생성
            var app = System.Windows.Application.Current;
            if (app != null && app.Dispatcher != null)
            {
                app.Dispatcher.Invoke((Action)(() =>
                {
                    try
                    {
                        InitializeOverlays();
                    }
                    catch { }
                }));
            }
        }

        public void Undo()
        {
            for (int i = 0; i < _overlayWindows.Count; i++)
            {
                _overlayWindows[i].Undo();
            }
        }

        public void Redo()
        {
            for (int i = 0; i < _overlayWindows.Count; i++)
            {
                _overlayWindows[i].Redo();
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _overlayWindows.Count; i++)
            {
                _overlayWindows[i].ClearAll();
            }
        }

        private void CloseAllOverlays()
        {
            for (int i = 0; i < _overlayWindows.Count; i++)
            {
                try
                {
                    _overlayWindows[i].Close();
                }
                catch { }
            }
            _overlayWindows.Clear();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                CloseAllOverlays();
            }
        }
    }
}
