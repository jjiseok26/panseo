using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Panseo.Helpers;

namespace Panseo.Views
{
    public sealed class UpdateGuideWindow : Window
    {
        private readonly TextBlock _status;
        private readonly Button _update;
        public event Action UpdateRequested;

        public UpdateGuideWindow(string status)
        {
            Title = "Panseo 업데이트 및 그림 안내";
            Width = Math.Min(780, SystemParameters.WorkArea.Width - 30);
            Height = Math.Min(800, SystemParameters.WorkArea.Height - 30);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 252));
            var root = new DockPanel { Margin = new Thickness(16) };
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock { Text = "업데이트와 기능 안내", FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 0, 6, 8) });
            heading.Children.Add(new TextBlock { Text = "현재 버전 " + typeof(UpdateGuideWindow).Assembly.GetName().Version.ToString(3), Margin = new Thickness(6, 0, 6, 8) });
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, Padding = new Thickness(12),
                Background = Brushes.White, Foreground = Brushes.DarkSlateBlue, Margin = new Thickness(6, 0, 6, 12) };
            heading.Children.Add(_status);
            DockPanel.SetDock(heading, Dock.Top);
            root.Children.Add(heading);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _update = new Button { Content = "업데이트", IsEnabled = false, Padding = new Thickness(20, 6, 20, 6), Margin = new Thickness(6, 10, 6, 0) };
            _update.Click += (s, e) => { if (UpdateRequested != null) UpdateRequested(); };
            actions.Children.Add(_update);
            var close = new Button { Content = "닫기", Padding = new Thickness(20, 6, 20, 6), Margin = new Thickness(6, 10, 6, 0) };
            close.Click += (s, e) => Close();
            actions.Children.Add(close);
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);
            root.Children.Add(new ScrollViewer { Content = CreateIllustratedGuide(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = root;
            SetStatus(status);
        }

        public void SetStatus(string status)
        {
            _status.Text = status;
            _update.IsEnabled = status != null && status.StartsWith("새 버전", StringComparison.Ordinal);
        }

        internal static StackPanel CreateIllustratedGuide()
        {
            var panel = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(245, 247, 252)) };
            panel.Children.Add(new TextBlock { Text = "그림으로 보는 현재 버전의 기능", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 8, 6, 6) });
            var cards = new UniformGrid { Columns = 2 };
            var zoom = Picture();
            Box(zoom, 12, 32, 58, 40, "100%", Brushes.LightSteelBlue);
            Label(zoom, 84, 40, "휠 →", 16);
            Box(zoom, 150, 12, 98, 78, "✏ 200%", Brushes.LightSkyBlue);
            cards.Children.Add(Card("Ctrl+Q · 화면 확대/축소", zoom,
                "Ctrl+Q로 들어가 휠로 확대/축소합니다. 펜·도형·글자·번호 도구로 확대 화면에 그릴 수 있습니다. 그리면서 Ctrl+드래그로 이동하고, 저장·고정 후 Esc로 닫으세요. 진입 순간의 정지 화면입니다."));
            var numbers = Picture();
            Label(numbers, 12, 2, "시작번호", 12);
            Box(numbers, 82, 0, 52, 25, "5", Brushes.AliceBlue);
            Box(numbers, 142, 0, 60, 25, "적용", Brushes.Lavender);
            foreach (int number in new[] { 5, 6, 7 })
                numbers.Children.Add(DrawingHelper.CreateNumberStamp(number, new Point(40 + (number - 5) * 90, 66), Color.FromRgb(79, 70, 229), 5));
            cards.Children.Add(Card("Ctrl+N · 원하는 번호부터 시작", numbers,
                "시작번호에 숫자를 입력하고 ‘적용’ 또는 Enter를 누르세요. 5를 입력하면 다음 클릭부터 5, 6, 7…로 이어집니다. 기존 표시는 그대로 남습니다."));
            var pin = Picture();
            Box(pin, 18, 8, 224, 84, "", Brushes.AliceBlue);
            Label(pin, 35, 18, "고정 캡처", 13);
            Label(pin, 35, 48, "휠 ↕", 16);
            Label(pin, 112, 50, "Ctrl + 드래그", 12);
            cards.Children.Add(Card("고정 캡처도 확대/축소", pin,
                "Ctrl+F11로 영역을 고정합니다. 이미지 위에서 휠로 확대하고 Ctrl+드래그로 안쪽을 이동합니다. 우클릭하면 저장하기·닫기가 나옵니다."));
            var menu = Picture();
            Box(menu, 6, 30, 74, 36, "SHA-256", Brushes.LightSteelBlue);
            Label(menu, 88, 37, "→", 20);
            Box(menu, 124, 14, 128, 70, "업데이트 설치 창", Brushes.AliceBlue);
            cards.Children.Add(Card("일반 실행 파일로 업데이트", menu,
                "새 버전이 준비되면 아래 업데이트 버튼을 누르세요. 검증한 실행 파일로 설치하고 프로그램을 다시 엽니다. 보안 제품 설정은 바꾸지 않습니다."));
            panel.Children.Add(cards);
            panel.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(16), Margin = new Thickness(6),
                Child = new TextBlock { Text = "업데이트 적용 순서\n업데이트 확인 → 자동 다운로드·검증 → 아래 ‘업데이트’ 버튼 → 설치 후 자동 재실행\n\n그리는 중인 내용은 업데이트 전에 저장해 주세요. 최신 버전이면 업데이트 버튼이 비활성화됩니다.",
                    TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 23 } });
            return panel;
        }

        private static Canvas Picture() { return new Canvas { Width = 260, Height = 100 }; }

        private static Border Card(string title, UIElement picture, string description)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            content.Children.Add(new Viewbox { Child = picture, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(0, 2, 0, 8) });
            content.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 22 });
            return new Border { Child = content, Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Margin = new Thickness(6) };
        }

        private static void Box(Canvas canvas, double x, double y, double width, double height, string text, Brush background)
        {
            var box = new Border { Width = width, Height = height, Background = background, BorderBrush = Brushes.SlateGray,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Child = new TextBlock { Text = text, FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            Canvas.SetLeft(box, x); Canvas.SetTop(box, y); canvas.Children.Add(box);
        }

        private static void Label(Canvas canvas, double x, double y, string text, double size)
        {
            var label = new TextBlock { Text = text, FontSize = size, Foreground = Brushes.DarkSlateBlue };
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
        }
    }
}
