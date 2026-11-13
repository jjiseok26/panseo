using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Panseo.Models;
using Panseo.Native;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Panseo.Helpers;

namespace Panseo.Views
{
    public partial class FloatingToolbarWindow : Window
    {
        private OverlayWindowManager _overlayManager;
        private bool _isMenuOpen = false;
        private bool _isCapturing;
        private ScreenZoomWindow _zoomWindow;
        private ManualWindow _manualWindow;

        public bool IsMenuOpen
        {
            get { return _isMenuOpen; }
        }

        // 동그라미 드래그 이동과 단순 클릭 구분을 위한 변수
        private Point _mouseDownScreenPos;
        private bool _isMouseDownOnCircle = false;
        private bool _hasDragged = false;

        private DrawingTool _lastActiveDrawingTool = DrawingTool.Pen;
        private PointerShape _lastActivePointerShape = PointerShape.CircleHighlight;

        private Win32.LowLevelMouseProc _keyboardProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;

        private const int HOTKEY_ID_F1 = 9000;
        private const int HOTKEY_ID_F2 = 9001;
        private const int HOTKEY_ID_F3 = 9002;
        private const int HOTKEY_ID_F4 = 9003;
        private const int HOTKEY_ID_CAPTURE = 9009;
        private const int HOTKEY_ID_PIN = 9010;
        private const int HOTKEY_ID_MOUSE = 9011;
        private const int HOTKEY_ID_ZOOM = 9012;
        private const int HOTKEY_ID_POINTER_FIRST = 9004;
        private static readonly PointerShape[] PointerShortcutShapes =
        {
            PointerShape.CircleHighlight, PointerShape.LaserPointer,
            PointerShape.Spotlight, PointerShape.FocusRing, PointerShape.None
        };
        private const uint MOD_CONTROL = 0x0002;
        private const int VK_F1 = 0x70;
        private const int VK_F2 = 0x71;
        private const int VK_F3 = 0x72;
        private const int VK_F4 = 0x73;

        public FloatingToolbarWindow(OverlayWindowManager overlayManager)
        {
            InitializeComponent();
            _overlayManager = overlayManager;

            // 창 아이콘 안전 로드
            try
            {
                this.Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Panseo;component/panseo.ico", UriKind.Absolute));
            }
            catch { }

            this.Loaded += OnLoaded;
            this.Closed += OnClosed;

            // 초기 위치: 화면 우측 상단 부근 (접근하기 편한 위치)
            double screenWidth = SystemParameters.WorkArea.Width;
            this.Left = Math.Max(20, screenWidth - 100);
            this.Top = 120;

            // 초기 상태: 플로팅 동그라미 버튼 모드
            ApplyViewMode();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 전역 단축키 등록 (Ctrl+F1 ~ Ctrl+F11, Ctrl+M, Ctrl+Q)
            try
            {
                var helper = new WindowInteropHelper(this);
                var source = HwndSource.FromHwnd(helper.Handle);
                if (source != null)
                {
                    source.AddHook(HwndHook);
                }

                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_F1, MOD_CONTROL, VK_F1);
                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_F2, MOD_CONTROL, VK_F2);
                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_F3, MOD_CONTROL, VK_F3);
                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_F4, MOD_CONTROL, VK_F4);
                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_CAPTURE, MOD_CONTROL | 0x4000, 0x79);
                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_PIN, MOD_CONTROL | 0x4000, 0x7A);
                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_MOUSE, MOD_CONTROL, 0x4D);
                Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_ZOOM, MOD_CONTROL | 0x4000, 0x51);
                for (int i = 0; i < PointerShortcutShapes.Length; i++)
                {
                    Win32.RegisterHotKey(helper.Handle, HOTKEY_ID_POINTER_FIRST + i, MOD_CONTROL, (uint)(0x74 + i)); // Ctrl+F5 ~ F9
                }
            }
            catch { }

            // 전역 키보드 훅 등록 (판서 도구 및 색상 단축키 감지)
            try
            {
                _keyboardProc = KeyboardHookCallback;
                using (var curProcess = Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _keyboardHookId = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, Win32.GetModuleHandle(curModule.ModuleName), 0);
                }
            }
            catch { }

            // 초기 슬라이더 수치 표시
            TxtPointerSizeVal.Text = ((int)SliderPointerSize.Value).ToString();
            TxtStrokeVal.Text = ((int)SliderStrokeThickness.Value).ToString();

            AppSettings.Instance.PropertyChanged += OnSettingsChanged;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_F1);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_F2);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_F3);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_F4);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_CAPTURE);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_PIN);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_MOUSE);
                Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_ZOOM);
                for (int i = 0; i < PointerShortcutShapes.Length; i++)
                {
                    Win32.UnregisterHotKey(helper.Handle, HOTKEY_ID_POINTER_FIRST + i);
                }
            }
            catch { }

            if (_keyboardHookId != IntPtr.Zero)
            {
                try
                {
                    Win32.UnhookWindowsHookEx(_keyboardHookId);
                    _keyboardHookId = IntPtr.Zero;
                }
                catch { }
            }

            Application.Current.Shutdown();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32.WM_HOTKEY)
            {
                if (_isCapturing) { handled = true; return IntPtr.Zero; }
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_ZOOM) { ToggleScreenZoom(); handled = true; return IntPtr.Zero; }
                if (id == HOTKEY_ID_CAPTURE || id == HOTKEY_ID_PIN)
                {
                    var editor = _zoomWindow != null ? (CaptureEditorWindow)_zoomWindow : GetActiveCaptureEditor();
                    if (editor != null)
                    {
                        if (id == HOTKEY_ID_CAPTURE) editor.SaveCapture();
                        else editor.PinCapture();
                    }
                    else StartCapture(id == HOTKEY_ID_PIN);
                    handled = true;
                }
                else if (id == HOTKEY_ID_MOUSE)
                {
                    var editor = _zoomWindow != null ? (CaptureEditorWindow)_zoomWindow : GetActiveCaptureEditor();
                    if (editor != null) editor.SelectTool(DrawingTool.None);
                    SelectTool(DrawingTool.None);
                    handled = true;
                }
                else if (id == HOTKEY_ID_F1)
                {
                    ToggleMenu();
                    handled = true;
                }
                else if (id == HOTKEY_ID_F2)
                {
                    ToggleDrawingMode();
                    handled = true;
                }
                else if (id == HOTKEY_ID_F3)
                {
                    TogglePointerEffect();
                    handled = true;
                }
                else if (id == HOTKEY_ID_F4)
                {
                    ChkBlackboard.IsChecked = !ChkBlackboard.IsChecked;
                    handled = true;
                }
                else if (id >= HOTKEY_ID_POINTER_FIRST && id < HOTKEY_ID_POINTER_FIRST + PointerShortcutShapes.Length)
                {
                    SelectPointerShape(PointerShortcutShapes[id - HOTKEY_ID_POINTER_FIRST]);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private static CaptureEditorWindow GetActiveCaptureEditor()
        {
            if (Application.Current == null) return null;
            foreach (Window window in Application.Current.Windows)
                if (window is CaptureEditorWindow && window.IsActive) return (CaptureEditorWindow)window;
            return null;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // 캡처 창의 입력은 해당 창이 처리하여 바탕화면 판서와 충돌하지 않게 한다.
            if ((IsActive && Keyboard.FocusedElement is TextBox) || _isCapturing || _zoomWindow != null || App.IsUpdateGuideActive || (_manualWindow != null && _manualWindow.IsActive) || GetActiveCaptureEditor() != null)
                return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
            if (nCode >= 0 && (wParam.ToInt32() == Win32.WM_KEYDOWN || wParam.ToInt32() == Win32.WM_SYSKEYDOWN))
            {
                var kbd = (Win32.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32.KBDLLHOOKSTRUCT));
                int vk = (int)kbd.vkCode;

                // 글자 넣기 상자에서 타이핑 중일 때는 텍스트 입력을 위해 단축키 가로채기 통과
                if (!AppSettings.Instance.IsTextEditing)
                {
                    bool isDrawingActive = AppSettings.Instance.CurrentTool != DrawingTool.None;
                    bool isMenuOpen = _isMenuOpen;

                    // 판서 모드 동작 중이거나 서브메뉴 창이 열려 있을 때 단축키 실행
                    if (isDrawingActive || isMenuOpen)
                    {
                        if (HandleDrawingShortcut(vk))
                        {
                            return new IntPtr(1); // 키 이벤트 소비
                        }
                    }
                }
            }
            return Win32.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private bool HandleDrawingShortcut(int vk)
        {
            // ESC: 판서 모드 종료 및 마우스 모드로 복귀
            if (vk == 0x1B) // VK_ESCAPE
            {
                if (AppSettings.Instance.CurrentTool != DrawingTool.None)
                {
                    Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.None)));
                    return true;
                }
                return false;
            }

            // 도구와 색상 단축키는 Ctrl 조합만 처리한다.
            if (Keyboard.Modifiers != ModifierKeys.Control) return false;

            if (vk == 0x5A) // Ctrl+Z
            {
                Dispatcher.BeginInvoke((Action)(() => _overlayManager.Undo()));
                return true;
            }
            if (vk == 0x59) // Ctrl+Y
            {
                Dispatcher.BeginInvoke((Action)(() => _overlayManager.Redo()));
                return true;
            }

            if (vk == 0x2E) // Ctrl+Delete: 전체 지우기
            {
                Dispatcher.BeginInvoke((Action)(() => BtnClearAll_Click(null, null)));
                return true;
            }

            if (vk == 0x4E) // Ctrl+N: 번호 넣기
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Number)));
                return true;
            }

            // 도구 단축키 (Ctrl + 영문 알파벳 키)
            if (vk == 0x50) // P: 일반 펜 (Pen)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Pen)));
                return true;
            }
            if (vk == 0x48) // H: 형광펜 (Highlighter)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Highlighter)));
                return true;
            }
            if (vk == 0x41) // A: 화살표 (Arrow)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Arrow)));
                return true;
            }
            if (vk == 0x52) // R: 사각형 (Rectangle)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Rectangle)));
                return true;
            }
            if (vk == 0x4F) // O: 원/타원 (Oval)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Ellipse)));
                return true;
            }
            if (vk == 0x4C) // L: 직선 (Line)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Line)));
                return true;
            }
            if (vk == 0x54) // T: 글자 넣기 (Text)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Text)));
                return true;
            }
            if (vk == 0x45) // E: 지우개 (Eraser)
            {
                Dispatcher.BeginInvoke((Action)(() => SelectTool(DrawingTool.Eraser)));
                return true;
            }

            // 색상 팔레트 단축키 (숫자 키 1 ~ 6 및 넘패드 1 ~ 6)
            // 1: 빨강
            if (vk == 0x31 || vk == 0x61)
            {
                Dispatcher.BeginInvoke((Action)(() => SetDrawingColor((Color)ColorConverter.ConvertFromString("#FFFF3B30"))));
                return true;
            }
            // 2: 노랑
            if (vk == 0x32 || vk == 0x62)
            {
                Dispatcher.BeginInvoke((Action)(() => SetDrawingColor((Color)ColorConverter.ConvertFromString("#FFFFEB3B"))));
                return true;
            }
            // 3: 초록
            if (vk == 0x33 || vk == 0x63)
            {
                Dispatcher.BeginInvoke((Action)(() => SetDrawingColor((Color)ColorConverter.ConvertFromString("#FF00E676"))));
                return true;
            }
            // 4: 파랑
            if (vk == 0x34 || vk == 0x64)
            {
                Dispatcher.BeginInvoke((Action)(() => SetDrawingColor((Color)ColorConverter.ConvertFromString("#FF29B6F6"))));
                return true;
            }
            // 5: 흰색
            if (vk == 0x35 || vk == 0x65)
            {
                Dispatcher.BeginInvoke((Action)(() => SetDrawingColor((Color)ColorConverter.ConvertFromString("#FFFFFFFF"))));
                return true;
            }
            // 6: 검정
            if (vk == 0x36 || vk == 0x66)
            {
                Dispatcher.BeginInvoke((Action)(() => SetDrawingColor((Color)ColorConverter.ConvertFromString("#FF1E1E1E"))));
                return true;
            }

            return false;
        }

        public void SelectTool(DrawingTool tool)
        {
            if (_zoomWindow != null)
            {
                _zoomWindow.SelectTool(tool);
                if (tool != DrawingTool.None) _lastActiveDrawingTool = tool;
                UpdateToolRadioButtons(tool);
                UpdateCircleVisual();
                return;
            }
            AppSettings.Instance.CurrentTool = tool;
            if (tool != DrawingTool.None)
            {
                _lastActiveDrawingTool = tool;
            }
            UpdateToolRadioButtons(tool);
        }

        public void SetDrawingColor(Color color)
        {
            AppSettings.Instance.DrawingColor = color;
            if (_zoomWindow != null) return;
            if (AppSettings.Instance.CurrentTool == DrawingTool.None)
            {
                SelectTool(_lastActiveDrawingTool != DrawingTool.None ? _lastActiveDrawingTool : DrawingTool.Pen);
            }
        }

        private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                if (e.PropertyName == "CurrentTool")
                {
                    UpdateToolRadioButtons(AppSettings.Instance.CurrentTool);
                    UpdateCircleVisual();
                }
                else if (e.PropertyName == "DrawingColor")
                {
                    UpdateCircleVisual();
                }
                else if (e.PropertyName == "PointerShape")
                {
                    UpdatePointerRadioButtons(AppSettings.Instance.PointerShape);
                }
            }));
        }

        private void UpdateCircleVisual()
        {
            if (TxtCircleToolIcon == null || FloatingCircle == null) return;

            var tool = _zoomWindow != null ? _zoomWindow.CurrentTool : AppSettings.Instance.CurrentTool;
            string icon;
            switch (tool)
            {
                case DrawingTool.Pen: icon = "✏️"; break;
                case DrawingTool.Highlighter: icon = "🖍️"; break;
                case DrawingTool.Arrow: icon = "➡️"; break;
                case DrawingTool.Rectangle: icon = "🔲"; break;
                case DrawingTool.Ellipse: icon = "⭕"; break;
                case DrawingTool.Line: icon = "➖"; break;
                case DrawingTool.Text: icon = "🔤"; break;
                case DrawingTool.Eraser: icon = "🧽"; break;
                case DrawingTool.Number: icon = "①"; break;
                default: icon = "✏️"; break;
            }
            TxtCircleToolIcon.Text = icon;

            if (tool != DrawingTool.None)
            {
                var color = AppSettings.Instance.DrawingColor;
                FloatingCircle.BorderBrush = new SolidColorBrush(color);
                FloatingCircle.BorderThickness = new Thickness(3);
                FloatingCircle.ToolTip = string.Format("판서 동작 중 ({0}) | 클릭: 메뉴 열기 (Ctrl+F1)", tool);
            }
            else
            {
                FloatingCircle.BorderBrush = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF));
                FloatingCircle.BorderThickness = new Thickness(2);
                FloatingCircle.ToolTip = "클릭: 판서 메뉴 열기 (단축키: Ctrl+F1) | 드래그: 원하는 곳으로 이동";
            }
        }

        private void UpdateToolRadioButtons(DrawingTool tool)
        {
            switch (tool)
            {
                case DrawingTool.None: RbToolMouse.IsChecked = true; break;
                case DrawingTool.Pen: RbToolPen.IsChecked = true; break;
                case DrawingTool.Highlighter: RbToolHighlighter.IsChecked = true; break;
                case DrawingTool.Arrow: RbToolArrow.IsChecked = true; break;
                case DrawingTool.Rectangle: RbToolRectangle.IsChecked = true; break;
                case DrawingTool.Ellipse: RbToolEllipse.IsChecked = true; break;
                case DrawingTool.Line: RbToolLine.IsChecked = true; break;
                case DrawingTool.Text: RbToolText.IsChecked = true; break;
                case DrawingTool.Eraser: RbToolEraser.IsChecked = true; break;
                case DrawingTool.Number: RbToolNumber.IsChecked = true; break;
            }
        }

        private void UpdatePointerRadioButtons(PointerShape shape)
        {
            switch (shape)
            {
                case PointerShape.CircleHighlight: RbPointerCircle.IsChecked = true; break;
                case PointerShape.LaserPointer: RbPointerLaser.IsChecked = true; break;
                case PointerShape.Spotlight: RbPointerSpotlight.IsChecked = true; break;
                case PointerShape.FocusRing: RbPointerRing.IsChecked = true; break;
                case PointerShape.None: RbPointerNone.IsChecked = true; break;
            }
        }

        #region 플로팅 동그라미 <-> 메뉴 카드 모드 전환

        private void ApplyViewMode()
        {
            if (_isMenuOpen)
            {
                // 메뉴 카드가 열렸을 때
                FloatingCircle.Visibility = Visibility.Collapsed;
                MenuCard.Visibility = Visibility.Visible;
                // 현재 모니터의 작업 영역에 맞춰 메뉴 전체를 표시한다.
                var helper = new WindowInteropHelper(this);
                var screen = System.Windows.Forms.Screen.FromHandle(helper.Handle);
                var source = PresentationSource.FromVisual(this);
                var transform = source != null && source.CompositionTarget != null
                    ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
                var bounds = screen.WorkingArea;
                var origin = transform.Transform(new Point(bounds.Left, bounds.Top));
                var size = transform.Transform(new Vector(bounds.Width, bounds.Height));
                MenuLayout.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double scale = Math.Min(1, Math.Min(
                    Math.Max(1, size.X - 30) / (MenuLayout.DesiredSize.Width + 3),
                    Math.Max(1, size.Y - 30) / (MenuLayout.DesiredSize.Height + 3)));
                this.Width = (MenuLayout.DesiredSize.Width + 3) * scale + 10;
                this.Height = (MenuLayout.DesiredSize.Height + 3) * scale + 10;
                this.Left = Math.Max(origin.X + 10, Math.Min(this.Left, origin.X + size.X - this.Width - 10));
                this.Top = Math.Max(origin.Y + 10, Math.Min(this.Top, origin.Y + size.Y - this.Height - 10));
            }
            else
            {
                // 닫혀서 플로팅 동그라미만 보일 때
                MenuCard.Visibility = Visibility.Collapsed;
                FloatingCircle.Visibility = Visibility.Visible;
                this.Width = 66;
                this.Height = 66;
                UpdateCircleVisual();
            }

            // 항상 최상위 Z-Order 유지 보장
            this.Topmost = false;
            this.Topmost = true;

            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    Win32.SetWindowPos(helper.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
            }
            catch { }
        }

        public void OpenMenu()
        {
            if (!_isMenuOpen)
            {
                _isMenuOpen = true;
                ApplyViewMode();
            }
        }

        public void CloseMenu()
        {
            if (_isMenuOpen)
            {
                _isMenuOpen = false;
                ApplyViewMode();
            }
        }

        public void ToggleMenu()
        {
            _isMenuOpen = !_isMenuOpen;
            ApplyViewMode();
        }

        private void BtnMinimizeToCircle_Click(object sender, RoutedEventArgs e)
        {
            CloseMenu();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseMenu();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                Win32.TryDragMove(this);
            }
        }

        #endregion

        #region 동그라미 드래그 이동 및 클릭 처리

        private void FloatingCircle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDownOnCircle = true;
            _hasDragged = false;
            Win32.POINT pt;
            Win32.GetCursorPos(out pt);
            _mouseDownScreenPos = new Point(pt.X, pt.Y);
            FloatingCircle.CaptureMouse();
        }

        private void FloatingCircle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDownOnCircle)
            {
                if (e.LeftButton != MouseButtonState.Pressed)
                {
                    FloatingCircle.ReleaseMouseCapture();
                    _isMouseDownOnCircle = false;
                    return;
                }
                Win32.POINT pt;
                Win32.GetCursorPos(out pt);
                Point curScreenPos = new Point(pt.X, pt.Y);

                double dx = curScreenPos.X - _mouseDownScreenPos.X;
                double dy = curScreenPos.Y - _mouseDownScreenPos.Y;

                // 4픽셀 이상 움직였으면 드래그로 판정하고 창 이동
                if (!_hasDragged && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
                {
                    _hasDragged = true;
                }

                if (_hasDragged)
                {
                    FloatingCircle.ReleaseMouseCapture();
                    _isMouseDownOnCircle = false;
                    Win32.TryDragMove(this);
                }
            }
        }

        private void FloatingCircle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMouseDownOnCircle)
            {
                FloatingCircle.ReleaseMouseCapture();
                _isMouseDownOnCircle = false;

                // 드래그하지 않고 제자리에서 클릭한 경우: 메뉴 창 열기!
                if (!_hasDragged)
                {
                    OpenMenu();
                }
            }
        }

        #endregion

        #region 마우스 포인터 설정 이벤트

        private void SelectPointerShape(PointerShape shape)
        {
            AppSettings.Instance.PointerShape = shape;
            if (shape != PointerShape.None) _lastActivePointerShape = shape;
            UpdatePointerRadioButtons(shape);
        }

        private void PointerShape_Click(object sender, RoutedEventArgs e)
        {
            if (RbPointerCircle.IsChecked == true) SelectPointerShape(PointerShape.CircleHighlight);
            else if (RbPointerLaser.IsChecked == true) SelectPointerShape(PointerShape.LaserPointer);
            else if (RbPointerSpotlight.IsChecked == true) SelectPointerShape(PointerShape.Spotlight);
            else if (RbPointerRing.IsChecked == true) SelectPointerShape(PointerShape.FocusRing);
            else if (RbPointerNone.IsChecked == true) SelectPointerShape(PointerShape.None);
        }

        private void SliderPointerSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPointerSizeVal != null)
            {
                TxtPointerSizeVal.Text = ((int)e.NewValue).ToString();
            }
            AppSettings.Instance.PointerSize = e.NewValue;
        }

        private void BtnPointerColor_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var hex = btn.Tag as string;
                if (!string.IsNullOrEmpty(hex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    AppSettings.Instance.PointerColor = color;
                }
            }
        }

        private void ChkClickRipple_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.IsClickRippleEnabled = ChkClickRipple.IsChecked == true;
        }

        public void TogglePointerEffect()
        {
            if (AppSettings.Instance.PointerShape != PointerShape.None)
            {
                _lastActivePointerShape = AppSettings.Instance.PointerShape;
                AppSettings.Instance.PointerShape = PointerShape.None;
                RbPointerNone.IsChecked = true;
            }
            else
            {
                AppSettings.Instance.PointerShape = _lastActivePointerShape;
                switch (_lastActivePointerShape)
                {
                    case PointerShape.CircleHighlight: RbPointerCircle.IsChecked = true; break;
                    case PointerShape.LaserPointer: RbPointerLaser.IsChecked = true; break;
                    case PointerShape.Spotlight: RbPointerSpotlight.IsChecked = true; break;
                    case PointerShape.FocusRing: RbPointerRing.IsChecked = true; break;
                }
            }
        }

        #endregion

        #region 판서 및 도형 그리기 설정 이벤트

        private void DrawingTool_Click(object sender, RoutedEventArgs e)
        {
            if (RbToolNumber.IsChecked == true) { SelectTool(DrawingTool.Number); }
            else if (RbToolMouse.IsChecked == true)
            {
                SelectTool(DrawingTool.None);
            }
            else if (RbToolPen.IsChecked == true)
            {
                SelectTool(DrawingTool.Pen);
            }
            else if (RbToolHighlighter.IsChecked == true)
            {
                SelectTool(DrawingTool.Highlighter);
            }
            else if (RbToolArrow.IsChecked == true)
            {
                SelectTool(DrawingTool.Arrow);
            }
            else if (RbToolRectangle.IsChecked == true)
            {
                SelectTool(DrawingTool.Rectangle);
            }
            else if (RbToolEllipse.IsChecked == true)
            {
                SelectTool(DrawingTool.Ellipse);
            }
            else if (RbToolLine.IsChecked == true)
            {
                SelectTool(DrawingTool.Line);
            }
            else if (RbToolText.IsChecked == true)
            {
                SelectTool(DrawingTool.Text);
            }
            else if (RbToolEraser.IsChecked == true)
            {
                SelectTool(DrawingTool.Eraser);
            }
        }

        public void ToggleDrawingMode()
        {
            if (_zoomWindow != null) { _zoomWindow.ToggleDrawingMode(); return; }
            if (AppSettings.Instance.CurrentTool != DrawingTool.None)
            {
                // 판서 해제 -> 마우스 모드로
                AppSettings.Instance.CurrentTool = DrawingTool.None;
                RbToolMouse.IsChecked = true;
            }
            else
            {
                SelectTool(_lastActiveDrawingTool != DrawingTool.None ? _lastActiveDrawingTool : DrawingTool.Pen);
            }
        }

        private void SliderStrokeThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtStrokeVal != null)
            {
                TxtStrokeVal.Text = ((int)e.NewValue).ToString();
            }
            AppSettings.Instance.StrokeThickness = e.NewValue;
        }

        private void BtnDrawingColor_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var hex = btn.Tag as string;
                if (!string.IsNullOrEmpty(hex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    SetDrawingColor(color);
                }
            }
        }

        public void ToggleBlackboard()
        {
            ChkBlackboard.IsChecked = !ChkBlackboard.IsChecked;
        }

        private void ChkBlackboard_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.IsBlackboardActive = ChkBlackboard.IsChecked == true;
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomWindow != null) _zoomWindow.Undo();
            else if (_overlayManager != null) _overlayManager.Undo();
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomWindow != null) _zoomWindow.Redo();
            else if (_overlayManager != null) _overlayManager.Redo();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomWindow != null) _zoomWindow.ClearAll();
            else if (_overlayManager != null) _overlayManager.ClearAll();
            TxtStartNumber.Text = "1";
            TxtNumberStatus.Text = "입력 후 적용 또는 Enter";
            TxtNumberStatus.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }

        private void BtnSaveScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomWindow != null) _zoomWindow.SaveCapture();
            else StartCapture(false);
        }

        private void BtnPinScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomWindow != null) _zoomWindow.PinCapture();
            else StartCapture(true);
        }

        public void ShowManual()
        {
            if (_manualWindow == null)
            {
                _manualWindow = new ManualWindow();
                _manualWindow.Closed += (s, e) => _manualWindow = null;
                _manualWindow.Show();
            }
            else { _manualWindow.WindowState = WindowState.Normal; _manualWindow.Activate(); }
        }

        private void BtnResetNumber_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.ResetNumbering();
            if (_zoomWindow != null) _zoomWindow.ResetNumbering();
            TxtStartNumber.Text = "1";
            TxtNumberStatus.Text = "다음 클릭부터 1번으로 시작합니다.";
            TxtNumberStatus.Foreground = Brushes.LightGreen;
        }

        private void BtnApplyStartNumber_Click(object sender, RoutedEventArgs e)
        {
            int number;
            if (!AppSettings.TryParseStartNumber(TxtStartNumber.Text, out number))
            {
                TxtNumberStatus.Text = "1~2147483647 사이의 정수를 입력하세요.";
                TxtNumberStatus.Foreground = Brushes.LightSalmon;
                TxtStartNumber.Focus();
                TxtStartNumber.SelectAll();
                return;
            }
            if (_zoomWindow != null) _zoomWindow.SetStartNumber(number);
            else AppSettings.Instance.SetStartNumber(number);
            SelectTool(DrawingTool.Number);
            TxtNumberStatus.Text = "다음 클릭부터 " + number + "번으로 시작합니다.";
            TxtNumberStatus.Foreground = Brushes.LightGreen;
            BtnApplyStartNumber.Focus();
        }

        private void TxtStartNumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            BtnApplyStartNumber_Click(sender, e);
            e.Handled = true;
        }
        private void BtnUpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            var app = Application.Current as App;
            if (app != null) app.CheckForUpdates(true);
        }

        private void BtnManual_Click(object sender, RoutedEventArgs e) { ShowManual(); }
        private void BtnScreenZoom_Click(object sender, RoutedEventArgs e) { ToggleScreenZoom(); }

        public async void ToggleScreenZoom()
        {
            if (_zoomWindow != null) { _zoomWindow.Close(); return; }
            if (_isCapturing) return;
            _isCapturing = true;
            bool visible = IsVisible;
            var pointer = AppSettings.Instance.PointerShape;
            try
            {
                SelectTool(DrawingTool.None);
                AppSettings.Instance.PointerShape = PointerShape.None;
                CloseMenu();
                Hide();
                await Task.Delay(350); // Allow existing click ripples to finish before freezing the screen.
                var bounds = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds;
                _zoomWindow = new ScreenZoomWindow(ScreenCapture.Take(bounds), bounds);
                _zoomWindow.Closed += (s, e) =>
                {
                    _zoomWindow = null;
                    UpdateToolRadioButtons(AppSettings.Instance.CurrentTool);
                    UpdateCircleVisual();
                };
                _zoomWindow.Show();
            }
            catch (Exception ex)
            {
                _zoomWindow = null;
                MessageBox.Show("확대 모드를 열 수 없습니다: " + ex.Message, "Panseo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (visible)
                {
                    bool activate = ShowActivated;
                    ShowActivated = false;
                    Show();
                    ShowActivated = activate;
                }
                AppSettings.Instance.PointerShape = pointer;
                _isCapturing = false;
            }
        }

        private async void StartCapture(bool pin)
        {
            if (_isCapturing) return;
            _isCapturing = true;
            bool wasVisible = IsVisible;
            var pointer = AppSettings.Instance.PointerShape;
            try
            {
                SelectTool(DrawingTool.None);
                AppSettings.Instance.PointerShape = PointerShape.None;
                CloseMenu();
                Hide();
                // 숨긴 메뉴와 포인터가 캡처에 들어가지 않도록 다음 화면 갱신을 기다린다.
                await Task.Delay(120);
                var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
                BitmapSource snapshot = ScreenCapture.Take(bounds);
                var selector = new RegionCaptureWindow(snapshot, bounds);
                if (selector.ShowDialog() == true)
                {
                    if (pin) new PinnedCaptureWindow(selector.Selection).Show();
                    else new CaptureEditorWindow(selector.Selection).Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("캡처 중 오류가 발생했습니다: " + ex.Message, "Panseo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (wasVisible)
                {
                    bool activate = ShowActivated;
                    ShowActivated = false;
                    Show();
                    ShowActivated = activate;
                }
                AppSettings.Instance.PointerShape = pointer;
                _isCapturing = false;
            }
        }

        #endregion
    }
}
