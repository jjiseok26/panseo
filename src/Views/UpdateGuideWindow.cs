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
            var close = new Button { Content = "닫기", Padding = new Thickness(20, 6, 20, 6), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(6, 10, 6, 0) };
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Bottom);
            root.Children.Add(close);
            root.Children.Add(new ScrollViewer { Content = CreateIllustratedGuide(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = root;
            SetStatus(status);
        }

        public void SetStatus(string status) { _status.Text = status; }

        internal static StackPanel CreateIllustratedGuide()
        {
            var panel = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(245, 247, 252)) };
            panel.Children.Add(new TextBlock { Text = "그림으로 보는 현재 버전의 기능", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 8, 6, 6) });
            var cards = new UniformGrid { Columns = 2 };
            var zoom = Picture();
            Box(zoom, 12, 32, 58, 40, "100%", Brushes.LightSteelBlue);
            Label(zoom, 84, 40, "휠 →", 16);
            Box(zoom, 150, 12, 98, 78, "200%", Brushes.LightSkyBlue);
            cards.Children.Add(Card("Ctrl+Q · 화면 확대/축소", zoom,
                "Ctrl+Q로 들어간 뒤 휠을 위로 굴리면 확대, 아래로 굴리면 축소합니다. 드래그로 살펴보고 Esc로 나옵니다. 진입 순간의 정지 화면입니다."));
            var numbers = Picture();
            foreach (int number in new[] { 1, 2, 3 })
                numbers.Children.Add(DrawingHelper.CreateNumberStamp(number, new Point(40 + (number - 1) * 90, 50), Color.FromRgb(79, 70, 229), 5));
            cards.Children.Add(Card("Ctrl+N · 클릭마다 번호 넣기", numbers,
                "한 번 클릭할 때마다 ①, ②, ③처럼 번호가 이어집니다. ‘번호 1부터’를 누르면 기존 표시를 남기고 다시 1부터 시작합니다."));
            var pin = Picture();
            Box(pin, 18, 8, 224, 84, "", Brushes.AliceBlue);
            Label(pin, 35, 18, "고정 캡처", 13);
            Label(pin, 35, 48, "휠 ↕", 16);
            Label(pin, 112, 50, "Ctrl + 드래그", 12);
            cards.Children.Add(Card("고정 캡처도 확대/축소", pin,
                "Ctrl+F11로 영역을 고정합니다. 이미지 위에서 휠로 확대하고 Ctrl+드래그로 안쪽을 이동합니다. 우클릭하면 저장하기·닫기가 나옵니다."));
            var menu = Picture();
            var circle = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(21), Background = Brushes.MediumSlateBlue,
                Child = new TextBlock { Text = "✏️", FontSize = 23, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            Canvas.SetLeft(circle, 6); Canvas.SetTop(circle, 28); menu.Children.Add(circle);
            Box(menu, 59, 32, 72, 30, "업데이트", Brushes.Lavender);
            Box(menu, 136, 32, 65, 30, "설명서", Brushes.Lavender);
            Box(menu, 206, 32, 34, 30, "─", Brushes.Gainsboro);
            cards.Children.Add(Card("연필 버튼과 제목표시줄", menu,
                "플로팅 동그라미는 연필 모양으로 돌아왔습니다. 업데이트·설명서 버튼은 최소화 버튼 왼쪽에서 찾을 수 있습니다."));
            panel.Children.Add(cards);
            panel.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(16), Margin = new Thickness(6),
                Child = new TextBlock { Text = "업데이트 적용 순서\n확인 → 자동 다운로드·검증 → 트레이에서 프로그램 종료 → 다시 실행\n\n메뉴를 접는 것은 종료가 아닙니다. 현재 작업은 강제로 닫지 않으며 다음 실행부터 새 버전이 적용됩니다.",
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
