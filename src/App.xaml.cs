using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Panseo.Helpers;
using Panseo.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Panseo
{
    public partial class App : Application
    {
        private static Mutex _mutex = null;
        private OverlayWindowManager _overlayManager;
        private FloatingToolbarWindow _toolbarWindow;
        private NotifyIcon _trayIcon;
        private Icon _applicationIcon;
        private AutoUpdater _updater;
        private System.Windows.Threading.DispatcherTimer _updateTimer;

        public static FloatingToolbarWindow ToolbarWindow { get; private set; }

        public static void CloseToolbarMenu()
        {
            if (ToolbarWindow != null && ToolbarWindow.IsMenuOpen)
            {
                ToolbarWindow.Dispatcher.BeginInvoke((Action)(() =>
                {
                    ToolbarWindow.CloseMenu();
                }));
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                const string appName = "Panseo_ScreenAnnotator_App";
                bool createdNew;

                _mutex = new Mutex(true, appName, out createdNew);

                if (!createdNew)
                {
                    MessageBox.Show(
                        "Panseo 프로그램이 이미 실행 중입니다!\n\n화면의 [플로팅 동그라미] 또는 우측 하단 [작업표시줄 트레이 아이콘]을 확인해주세요.", 
                        "Panseo 알림", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                    Current.Shutdown();
                    return;
                }

                base.OnStartup(e);

                this.DispatcherUnhandledException += (s, args) =>
                {
                    MessageBox.Show("오류가 발생했습니다: " + args.Exception.Message, "Panseo 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Handled = true;
                };

                // 1. 멀티 모니터를 모두 지원하는 오버레이 윈도우 매니저 생성
                _overlayManager = new OverlayWindowManager();

                // 2. 플로팅 동그라미 / 메뉴 툴바 창 생성
                _toolbarWindow = new FloatingToolbarWindow(_overlayManager);
                ToolbarWindow = _toolbarWindow;
                _toolbarWindow.Show();

                // 3. 작업표시줄 시스템 트레이 아이콘 생성
                InitializeTrayIcon();
                _updater = new AutoUpdater(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                CheckForUpdates(false);
                _updateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(4) };
                _updateTimer.Tick += (s, args) => CheckForUpdates(false);
                _updateTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("초기화 중 오류가 발생했습니다: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckForUpdates(bool manual)
        {
            if (_updater == null) return;
            try
            {
                string status = await _updater.CheckAsync();
                if (status == null) return;
                if (manual) MessageBox.Show(status, "Panseo 업데이트", MessageBoxButton.OK, MessageBoxImage.Information);
                else if (status.StartsWith("새 버전") && _trayIcon != null)
                    _trayIcon.ShowBalloonTip(6000, "Panseo 자동 업데이트", status, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _updater.Log(ex);
                if (manual) MessageBox.Show("업데이트를 확인할 수 없습니다.\n" + ex.Message, "Panseo 업데이트", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _trayIcon = new NotifyIcon();
                _trayIcon.Text = "Panseo 판서 & 포인터";

                // 포터블 실행 파일에 내장한 아이콘을 사용한다.
                _applicationIcon = AppIcon.Load();
                _trayIcon.Icon = _applicationIcon;

                // 컨텍스트 메뉴 설정
                var contextMenu = new ContextMenu();

                contextMenu.MenuItems.Add(new MenuItem("✏️ 메뉴 열기 / 닫기", (s, ev) =>
                {
                    _toolbarWindow.Dispatcher.Invoke((Action)(() =>
                    {
                        _toolbarWindow.ToggleMenu();
                        _toolbarWindow.Activate();
                    }));
                }));

                contextMenu.MenuItems.Add(new MenuItem("🎯 마우스 포인터 토글 (Ctrl+F3)", (s, ev) =>
                {
                    _toolbarWindow.Dispatcher.Invoke((Action)(() => _toolbarWindow.TogglePointerEffect()));
                }));

                contextMenu.MenuItems.Add(new MenuItem("🖊️ 판서 모드 토글 (Ctrl+F2)", (s, ev) =>
                {
                    _toolbarWindow.Dispatcher.Invoke((Action)(() => _toolbarWindow.ToggleDrawingMode()));
                }));

                contextMenu.MenuItems.Add(new MenuItem("⬛ 칠판 모드 토글 (Ctrl+F4)", (s, ev) =>
                {
                    _toolbarWindow.Dispatcher.Invoke((Action)(() => _toolbarWindow.ToggleBlackboard()));
                }));

                contextMenu.MenuItems.Add(new MenuItem("확대/축소 (Ctrl+F12)", (s, ev) => _toolbarWindow.ToggleScreenZoom()));
                contextMenu.MenuItems.Add(new MenuItem("사용 설명서", (s, ev) => _toolbarWindow.ShowManual()));
                contextMenu.MenuItems.Add(new MenuItem("업데이트 확인", (s, ev) => CheckForUpdates(true)));
                contextMenu.MenuItems.Add("-");

                contextMenu.MenuItems.Add(new MenuItem("ℹ️ Panseo 정보", (s, ev) =>
                {
                    MessageBox.Show(
                        "Panseo • 멀티 모니터 완벽 지원 판서 & 포인터 도구\n\n- 기능: 듀얼 모니터 완벽 추적, 플로팅 동그라미, 화살표, 모양, 글자 입력, 포인터 하이라이트", 
                        "About Panseo", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }));

                contextMenu.MenuItems.Add(new MenuItem("❌ 프로그램 종료", (s, ev) =>
                {
                    Current.Shutdown();
                }));

                _trayIcon.ContextMenu = contextMenu;

                // 트레이 아이콘 클릭/더블클릭 시 메뉴 토글
                _trayIcon.Click += (s, ev) =>
                {
                    var mouseEvent = ev as MouseEventArgs;
                    if (mouseEvent != null && mouseEvent.Button == MouseButtons.Left)
                    {
                        _toolbarWindow.Dispatcher.Invoke((Action)(() =>
                        {
                            _toolbarWindow.ToggleMenu();
                            _toolbarWindow.Activate();
                        }));
                    }
                };

                _trayIcon.DoubleClick += (s, ev) =>
                {
                    _toolbarWindow.Dispatcher.Invoke((Action)(() =>
                    {
                        _toolbarWindow.OpenMenu();
                        _toolbarWindow.Activate();
                    }));
                };

                _trayIcon.Visible = true;
            }
            catch
            {
                // 트레이 생성 실패 시에도 메인 창은 정상 동작하도록 보호
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_applicationIcon != null) { _applicationIcon.Dispose(); _applicationIcon = null; }

            if (_overlayManager != null)
            {
                _overlayManager.Dispose();
                _overlayManager = null;
            }

            if (_toolbarWindow != null)
            {
                _toolbarWindow = null;
                ToolbarWindow = null;
            }

            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { }
                _mutex.Dispose();
                _mutex = null;
            }

            if (_updateTimer != null) _updateTimer.Stop();
            if (_updater != null) _updater.ApplyOnExit();
            base.OnExit(e);
        }
    }
}
