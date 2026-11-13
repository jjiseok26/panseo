using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Panseo.Views
{
    public sealed class ManualWindow : Window
    {
        public ManualWindow()
        {
            Title = "판서 사용 설명서";
            Width = Math.Min(720, SystemParameters.WorkArea.Width - 30);
            Height = Math.Min(780, SystemParameters.WorkArea.Height - 30);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            var document = new FlowDocument { FontFamily = new FontFamily("Malgun Gothic, Segoe UI"), FontSize = 15,
                PagePadding = new Thickness(28), Foreground = Brushes.Black, Background = Brushes.White };
            using (var stream = typeof(ManualWindow).Assembly.GetManifestResourceStream("Panseo.Manual.txt"))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    bool title = line.StartsWith("# ");
                    bool section = line.StartsWith("## ");
                    var paragraph = new Paragraph(new Run(title ? line.Substring(2) : section ? line.Substring(3) : line));
                    paragraph.Margin = new Thickness(0, title || section ? 18 : 0, 0, 10);
                    if (title || section) { paragraph.FontSize = title ? 28 : 20; paragraph.FontWeight = FontWeights.Bold; }
                    document.Blocks.Add(paragraph);
                }
            }
            Content = new FlowDocumentScrollViewer { Document = document, IsToolBarVisible = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}
