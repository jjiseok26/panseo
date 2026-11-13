using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Panseo.Helpers;

namespace Panseo.Views
{
    public sealed class UpdateInstallWindow : Window
    {
        private readonly TextBlock _status;
        private readonly Button _close;
        private bool _installing = true;
        private int _exitCode = 1;

        public UpdateInstallWindow(string[] args)
        {
            Title = "Panseo 업데이트 설치";
            Width = 440;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "Panseo 업데이트", FontSize = 22, FontWeight = FontWeights.Bold });
            _status = new TextBlock { Text = "프로그램 종료를 기다린 뒤 검증한 새 버전을 설치합니다…",
                FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 16) };
            panel.Children.Add(_status);
            var progress = new ProgressBar { IsIndeterminate = true, Height = 8 };
            panel.Children.Add(progress);
            _close = new Button { Content = "닫기", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(18, 5, 18, 5), Margin = new Thickness(0, 16, 0, 0) };
            _close.Click += (s, e) => Close();
            panel.Children.Add(_close);
            Content = panel;
            Closing += (s, e) => { if (_installing) e.Cancel = true; };
            Closed += (s, e) => Application.Current.Shutdown(_exitCode);
            Loaded += async (s, e) =>
            {
                try
                {
                    await Task.Run(() => AutoUpdater.ApplyUpdateFromArguments(args));
                    _status.Text = "업데이트를 완료했습니다. 다음 실행부터 새 버전이 적용됩니다.";
                    _exitCode = 0;
                }
                catch (Exception ex)
                {
                    _status.Text = "업데이트를 완료하지 못했습니다. 프로그램을 다시 실행하거나 최신 파일을 직접 받아 교체하세요.\n\n" + ex.Message;
                }
                finally
                {
                    progress.IsIndeterminate = false;
                    _installing = false;
                    _close.IsEnabled = true;
                }
                if (_exitCode == 0) { await Task.Delay(1500); Close(); }
            };
        }
    }
}
