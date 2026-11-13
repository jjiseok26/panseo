using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Panseo.Helpers;
using Panseo.Models;
using Panseo.Native;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace Panseo.Views
{
    public partial class OverlayWindow : Window
    {
        public Screen TargetScreen { get; private set; }

        private IntPtr _hwnd;
        private Win32.LowLevelMouseProc _mouseProc;
        private IntPtr _hookId = IntPtr.Zero;
        private DispatcherTimer _cursorTimer;

        // 드로잉 관련 상태
        private bool _isDrawing = false;
        private Point _startPoint;
        private Polyline _currentPolyline;
        private readonly Stack<UIElement> _undoStack = new Stack<UIElement>();
        private readonly Stack<UIElement> _redoStack = new Stack<UIElement>();

        // 포인터 엘리먼트 캐시
        private FrameworkElement _pointerElement;
        private Point _lastMouseScreenPos;
        private Point _textClickPos;

        public OverlayWindow(Screen screen)
        {
            InitializeComponent();
            TargetScreen = screen;

            this.Loaded += OnLoaded;
            this.Closed += OnClosed;
            AppSettings.Instance.PropertyChanged += OnSettingsChanged;
        }

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            _hwnd = helper.Handle;

            var source = System.Windows.Interop.HwndSource.FromHwnd(_hwnd);
            if (source != null)
            {
                source.AddHook(HwndHook);
            }

            // 해당 모니터의 정확한 물리 픽셀 좌표와 크기로 윈도우 배치
            ApplyMonitorBounds();

            // 전역 마우스 훅 등록 (클릭 파동 애니메이션 감지)
            _mouseProc = HookCallback;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _hookId = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, Win32.GetModuleHandle(curModule.ModuleName), 0);
            }

            // 마우스 커서 위치 추적 타이머 (부드러운 60fps 추적)
            _cursorTimer = new DispatcherTimer(DispatcherPriority.Render);
            _cursorTimer.Interval = TimeSpan.FromMilliseconds(15);
            _cursorTimer.Tick += OnCursorTimerTick;
            _cursorTimer.Start();

            // 초기 상태 적용
            UpdateClickThrough();
            UpdatePointerVisual();

            // 텍스트 상자 이벤트 등록
            InlineTextBox.KeyDown += InlineTextBox_KeyDown;
            InlineTextBox.LostFocus += InlineTextBox_LostFocus;

            // 판서 모드 단축키
            this.KeyDown += OverlayWindow_KeyDown;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 오버레이 클릭 시 활성화(포커스 강탈)를 방지하여 서브메뉴(툴바)가 뒤로 숨겨지지 않도록 보장
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        public void ApplyMonitorBounds()
        {
            if (_hwnd == IntPtr.Zero || TargetScreen == null) return;

            // Win32 SetWindowPos로 멀티 모니터 좌표에 1:1 정확히 밀착
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 
                TargetScreen.Bounds.X, TargetScreen.Bounds.Y, 
                TargetScreen.Bounds.Width, TargetScreen.Bounds.Height, 
                Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
        }

        public void UpdateTargetScreen(Screen newScreen)
        {
            TargetScreen = newScreen;
            ApplyMonitorBounds();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (_cursorTimer != null)
            {
                _cursorTimer.Stop();
            }
            if (_hookId != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                string propName = e.PropertyName;
                if (propName == "CurrentTool")
                {
                    if (AppSettings.Instance.CurrentTool == DrawingTool.None && TextEditorBox.Visibility == Visibility.Visible) CommitText();
                    UpdateClickThrough();
                }
                else if (propName == "PointerShape" || propName == "PointerSize" || propName == "PointerColor")
                {
                    UpdatePointerVisual();
                }
                else if (propName == "IsBlackboardActive")
                {
                    BlackboardLayer.Visibility = AppSettings.Instance.IsBlackboardActive ? Visibility.Visible : Visibility.Collapsed;
                }
            }));
        }

        private void UpdateClickThrough()
        {
            bool isDrawingMode = AppSettings.Instance.CurrentTool != DrawingTool.None;
            if (isDrawingMode)
            {
                Win32.SetClickThrough(_hwnd, false);
                this.IsHitTestVisible = true;
                this.Cursor = AppSettings.Instance.CurrentTool == DrawingTool.Text ? Cursors.IBeam : Cursors.Cross;
            }
            else
            {
                CloseTextEditor(false);
                Win32.SetClickThrough(_hwnd, true);
                this.IsHitTestVisible = false;
                this.Cursor = Cursors.Arrow;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && AppSettings.Instance.IsClickRippleEnabled)
            {
                int msg = wParam.ToInt32();
                if (msg == Win32.WM_LBUTTONDOWN || msg == Win32.WM_RBUTTONDOWN)
                {
                    var hookStruct = (Win32.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32.MSLLHOOKSTRUCT));
                    // 마우스 클릭 위치가 자신이 담당하는 모니터 안에 있을 때만 파동 생성
                    if (TargetScreen != null && TargetScreen.Bounds.Contains(hookStruct.pt.X, hookStruct.pt.Y))
                    {
                        Color rippleColor = msg == Win32.WM_LBUTTONDOWN ? AppSettings.Instance.PointerColor : Colors.LightSkyBlue;
                        Point pt = new Point(hookStruct.pt.X, hookStruct.pt.Y);
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            TriggerClickRipple(pt, rippleColor);
                        }));
                    }
                }
            }
            return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void OnCursorTimerTick(object sender, EventArgs e)
        {
            Win32.POINT pt;
            if (!Win32.GetCursorPos(out pt)) return;

            // 마우스 커서가 현재 모니터(TargetScreen) 내에 위치하는지 검사
            bool isInsideThisMonitor = TargetScreen != null && TargetScreen.Bounds.Contains(pt.X, pt.Y);

            if (!isInsideThisMonitor)
            {
                // 마우스가 다른 모니터에 있는 경우: 포인터 효과 숨김
                if (_pointerElement != null && _pointerElement.Visibility == Visibility.Visible)
                {
                    _pointerElement.Visibility = Visibility.Collapsed;
                }
                if (AppSettings.Instance.PointerShape == PointerShape.Spotlight)
                {
                    // 다른 모니터에 마우스가 있으면 이 모니터는 전체 어둡게 유지
                    SpotlightLayer.Data = new RectangleGeometry(new Rect(0, 0, this.ActualWidth, this.ActualHeight));
                }
                return;
            }

            // 마우스가 현재 모니터 안에 있는 경우
            _lastMouseScreenPos = new Point(pt.X, pt.Y);
            Point localPos = this.PointFromScreen(_lastMouseScreenPos);

            // 포인터 위치 업데이트 및 표시
            if (_pointerElement != null)
            {
                if (_pointerElement.Visibility != Visibility.Visible && AppSettings.Instance.PointerShape != PointerShape.None)
                {
                    _pointerElement.Visibility = Visibility.Visible;
                }
                double size = AppSettings.Instance.PointerSize;
                Canvas.SetLeft(_pointerElement, localPos.X - size / 2.0);
                Canvas.SetTop(_pointerElement, localPos.Y - size / 2.0);
            }

            // 스포트라이트 모드일 경우 마스크 갱신
            if (AppSettings.Instance.PointerShape == PointerShape.Spotlight)
            {
                UpdateSpotlightGeometry(localPos);
            }
        }

        private void UpdatePointerVisual()
        {
            PointerCanvas.Children.Clear();
            _pointerElement = null;

            var settings = AppSettings.Instance;
            double size = settings.PointerSize;
            Color baseColor = settings.PointerColor;

            if (settings.PointerShape == PointerShape.Spotlight)
            {
                SpotlightLayer.Visibility = Visibility.Visible;
                return;
            }
            else
            {
                SpotlightLayer.Visibility = Visibility.Collapsed;
            }

            if (settings.PointerShape == PointerShape.None)
            {
                return;
            }

            if (settings.PointerShape == PointerShape.CircleHighlight)
            {
                var ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(Color.FromArgb((byte)(baseColor.A * 0.45), baseColor.R, baseColor.G, baseColor.B)),
                    Stroke = new SolidColorBrush(Color.FromArgb(baseColor.A, baseColor.R, baseColor.G, baseColor.B)),
                    StrokeThickness = 2.5,
                    IsHitTestVisible = false
                };
                _pointerElement = ellipse;
            }
            else if (settings.PointerShape == PointerShape.LaserPointer)
            {
                var grid = new Grid { Width = size, Height = size, IsHitTestVisible = false };

                var glow = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new RadialGradientBrush(
                        Color.FromArgb(200, baseColor.R, baseColor.G, baseColor.B),
                        Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B))
                };
                grid.Children.Add(glow);

                double coreSize = Math.Max(6, size * 0.28);
                var core = new Ellipse
                {
                    Width = coreSize,
                    Height = coreSize,
                    Fill = Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                grid.Children.Add(core);

                _pointerElement = grid;
            }
            else if (settings.PointerShape == PointerShape.FocusRing)
            {
                var grid = new Grid { Width = size, Height = size, IsHitTestVisible = false };
                var ring = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Stroke = new SolidColorBrush(baseColor),
                    StrokeThickness = 3.0,
                    Fill = Brushes.Transparent
                };
                grid.Children.Add(ring);

                var centerDot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(baseColor),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                grid.Children.Add(centerDot);

                _pointerElement = grid;
            }

            if (_pointerElement != null)
            {
                PointerCanvas.Children.Add(_pointerElement);
            }
        }

        private void UpdateSpotlightGeometry(Point center)
        {
            double r = AppSettings.Instance.PointerSize * 1.5;
            var fullRect = new RectangleGeometry(new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            var hole = new EllipseGeometry(center, r, r);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, hole);
            SpotlightLayer.Data = combined;
        }

        private void TriggerClickRipple(Point screenPos, Color rippleColor)
        {
            Point localPos = this.PointFromScreen(screenPos);
            double initialSize = 16.0;
            double targetSize = Math.Max(70.0, AppSettings.Instance.PointerSize * 1.2);

            var ripple = new Ellipse
            {
                Width = initialSize,
                Height = initialSize,
                Stroke = new SolidColorBrush(rippleColor),
                StrokeThickness = 3.0,
                Fill = new SolidColorBrush(Color.FromArgb(40, rippleColor.R, rippleColor.G, rippleColor.B)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(ripple, localPos.X - initialSize / 2.0);
            Canvas.SetTop(ripple, localPos.Y - initialSize / 2.0);
            PointerCanvas.Children.Add(ripple);

            var duration = TimeSpan.FromMilliseconds(320);

            var widthAnim = new DoubleAnimation(initialSize, targetSize, duration);
            var heightAnim = new DoubleAnimation(initialSize, targetSize, duration);
            var opacityAnim = new DoubleAnimation(1.0, 0.0, duration);

            var leftAnim = new DoubleAnimation(localPos.X - initialSize / 2.0, localPos.X - targetSize / 2.0, duration);
            var topAnim = new DoubleAnimation(localPos.Y - initialSize / 2.0, localPos.Y - targetSize / 2.0, duration);

            opacityAnim.Completed += (s, e) =>
            {
                PointerCanvas.Children.Remove(ripple);
            };

            ripple.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            ripple.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);
            ripple.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            ripple.BeginAnimation(Canvas.LeftProperty, leftAnim);
            ripple.BeginAnimation(Canvas.TopProperty, topAnim);
        }

        #region 판서 마우스 입력 처리

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.ChangedButton != MouseButton.Left) return;
            if (AppSettings.Instance.CurrentTool == DrawingTool.None) return;

            Point pos = e.GetPosition(DrawingCanvas);

            if (AppSettings.Instance.CurrentTool == DrawingTool.Number)
            {
                StampNumber(pos);
                e.Handled = true;
                return;
            }

            if (AppSettings.Instance.CurrentTool == DrawingTool.Text)
            {
                if (TextEditorBox.Visibility == Visibility.Visible)
                {
                    CommitText();
                }
                OpenTextEditor(pos);
                return;
            }

            if (AppSettings.Instance.CurrentTool == DrawingTool.Eraser)
            {
                EraseAtPoint(pos);
                return;
            }

            _isDrawing = true;
            _startPoint = pos;
            this.CaptureMouse();

            var settings = AppSettings.Instance;
            var brush = new SolidColorBrush(settings.DrawingColor);
            brush.Freeze();

            if (settings.CurrentTool == DrawingTool.Pen || settings.CurrentTool == DrawingTool.Highlighter)
            {
                double thickness = settings.CurrentTool == DrawingTool.Highlighter ? settings.StrokeThickness * 3.5 : settings.StrokeThickness;
                double opacity = settings.CurrentTool == DrawingTool.Highlighter ? 0.38 : 1.0;

                _currentPolyline = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = thickness,
                    Opacity = opacity,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
                _currentPolyline.Points.Add(pos);
                TempCanvas.Children.Add(_currentPolyline);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isDrawing) return;

            Point currentPoint = e.GetPosition(DrawingCanvas);
            var settings = AppSettings.Instance;

            if (settings.CurrentTool == DrawingTool.Pen || settings.CurrentTool == DrawingTool.Highlighter)
            {
                if (_currentPolyline != null)
                {
                    _currentPolyline.Points.Add(currentPoint);
                }
            }
            else
            {
                TempCanvas.Children.Clear();
                var previewElement = CreateShapeElement(settings.CurrentTool, _startPoint, currentPoint, true);
                if (previewElement != null)
                {
                    TempCanvas.Children.Add(previewElement);
                }
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_isDrawing) return;

            _isDrawing = false;
            this.ReleaseMouseCapture();

            Point endPoint = e.GetPosition(DrawingCanvas);
            TempCanvas.Children.Clear();

            var settings = AppSettings.Instance;

            if (settings.CurrentTool == DrawingTool.Pen || settings.CurrentTool == DrawingTool.Highlighter)
            {
                if (_currentPolyline != null && _currentPolyline.Points.Count > 1)
                {
                    AddDrawingElement(_currentPolyline);
                }
                _currentPolyline = null;
            }
            else
            {
                double dist = (endPoint - _startPoint).Length;
                if (dist > 3.0)
                {
                    var finalElement = CreateShapeElement(settings.CurrentTool, _startPoint, endPoint, false);
                    if (finalElement != null)
                    {
                        AddDrawingElement(finalElement);
                    }
                }
            }
        }

        private UIElement CreateShapeElement(DrawingTool tool, Point start, Point end, bool isPreview)
        {
            var settings = AppSettings.Instance;
            var strokeBrush = new SolidColorBrush(settings.DrawingColor);
            var fillBrush = new SolidColorBrush(settings.DrawingColor);
            double thickness = settings.StrokeThickness;

            switch (tool)
            {
                case DrawingTool.Arrow:
                    return DrawingHelper.CreateArrowPath(start, end, strokeBrush, fillBrush, thickness);

                case DrawingTool.Rectangle:
                    var rect = DrawingHelper.GetBoundingRect(start, end);
                    var rectangle = new Rectangle
                    {
                        Width = rect.Width,
                        Height = rect.Height,
                        Stroke = strokeBrush,
                        StrokeThickness = thickness,
                        Fill = isPreview ? new SolidColorBrush(Color.FromArgb(20, settings.DrawingColor.R, settings.DrawingColor.G, settings.DrawingColor.B)) : Brushes.Transparent,
                        RadiusX = 4,
                        RadiusY = 4
                    };
                    Canvas.SetLeft(rectangle, rect.X);
                    Canvas.SetTop(rectangle, rect.Y);
                    return rectangle;

                case DrawingTool.Ellipse:
                    var ellRect = DrawingHelper.GetBoundingRect(start, end);
                    var ellipse = new Ellipse
                    {
                        Width = ellRect.Width,
                        Height = ellRect.Height,
                        Stroke = strokeBrush,
                        StrokeThickness = thickness,
                        Fill = isPreview ? new SolidColorBrush(Color.FromArgb(20, settings.DrawingColor.R, settings.DrawingColor.G, settings.DrawingColor.B)) : Brushes.Transparent
                    };
                    Canvas.SetLeft(ellipse, ellRect.X);
                    Canvas.SetTop(ellipse, ellRect.Y);
                    return ellipse;

                case DrawingTool.Line:
                    return new Line
                    {
                        X1 = start.X,
                        Y1 = start.Y,
                        X2 = end.X,
                        Y2 = end.Y,
                        Stroke = strokeBrush,
                        StrokeThickness = thickness,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
            }
            return null;
        }

        private void StampNumber(Point point)
        {
            var settings = AppSettings.Instance;
            AddDrawingElement(DrawingHelper.CreateNumberStamp(settings.TakeNextNumber(), point, settings.DrawingColor, settings.StrokeThickness));
        }

        private void AddDrawingElement(UIElement element)
        {
            DrawingCanvas.Children.Add(element);
            _undoStack.Push(element);
            _redoStack.Clear();
        }

        private void EraseAtPoint(Point pt)
        {
            for (int i = DrawingCanvas.Children.Count - 1; i >= 0; i--)
            {
                var child = DrawingCanvas.Children[i];
                var bounds = VisualTreeHelper.GetDescendantBounds(child);
                var localPt = DrawingCanvas.TranslatePoint(pt, child);
                if (bounds.Contains(localPt))
                {
                    DrawingCanvas.Children.RemoveAt(i);
                    _undoStack.Push(child);
                    break;
                }
            }
        }

        #endregion

        #region 인라인 텍스트 입력

        private void OpenTextEditor(Point pos)
        {
            _textClickPos = pos;
            Canvas.SetLeft(TextEditorBox, Math.Min(pos.X, this.ActualWidth - 250));
            Canvas.SetTop(TextEditorBox, Math.Min(pos.Y, this.ActualHeight - 120));
            TextEditorBox.Visibility = Visibility.Visible;
            AppSettings.Instance.IsTextEditing = true;

            InlineTextBox.Text = "";
            InlineTextBox.FontSize = AppSettings.Instance.FontSize;
            InlineTextBox.Foreground = new SolidColorBrush(AppSettings.Instance.DrawingColor);
            InlineTextBox.Focus();
        }

        private void InlineTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                CommitText();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseTextEditor(false);
            }
        }

        private void InlineTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitText();
        }

        private void CommitText()
        {
            string raw = InlineTextBox.Text;
            string text = raw != null ? raw.Trim() : null;
            if (!string.IsNullOrEmpty(text))
            {
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontSize = AppSettings.Instance.FontSize,
                    Foreground = new SolidColorBrush(AppSettings.Instance.DrawingColor),
                    FontFamily = new FontFamily("Malgun Gothic, Segoe UI"),
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(textBlock, _textClickPos.X);
                Canvas.SetTop(textBlock, _textClickPos.Y);
                AddDrawingElement(textBlock);
            }
            CloseTextEditor(true);
        }

        private void CloseTextEditor(bool resetText)
        {
            AppSettings.Instance.IsTextEditing = false;
            TextEditorBox.Visibility = Visibility.Collapsed;
            if (resetText) InlineTextBox.Text = "";
        }

        private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (TextEditorBox.Visibility == Visibility.Visible)
                {
                    CloseTextEditor(false);
                }
                else
                {
                    AppSettings.Instance.CurrentTool = DrawingTool.None;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Redo();
                e.Handled = true;
            }
        }

        #endregion

        #region 공개 조작 메서드

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var element = _undoStack.Pop();
                DrawingCanvas.Children.Remove(element);
                _redoStack.Push(element);
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var element = _redoStack.Pop();
                DrawingCanvas.Children.Add(element);
                _undoStack.Push(element);
            }
        }

        public void ClearAll()
        {
            DrawingCanvas.Children.Clear();
            TempCanvas.Children.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            CloseTextEditor(false);
        }

        #endregion
    }
}
