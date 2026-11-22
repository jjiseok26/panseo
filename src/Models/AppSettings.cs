using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Web.Script.Serialization;

namespace Panseo.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private static AppSettings _instance;
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AppSettings();
                }
                return _instance;
            }
        }

        // 마우스 포인터 관련 설정
        private PointerShape _pointerShape = PointerShape.CircleHighlight;
        private double _pointerSize = 65.0; // 지름
        private Color _pointerColor = Color.FromArgb(160, 255, 235, 59); // 반투명 노란색 기본
        private bool _isClickRippleEnabled = true;

        // 드로잉 관련 설정
        private DrawingTool _currentTool = DrawingTool.None;
        private Color _drawingColor = Colors.Red;
        private double _strokeThickness = 4.0;
        private double _fontSize = 22.0;
        private bool _isBlackboardActive = false;
        private bool _isTextEditing = false;
        private long _nextNumber = 1;
        private double _numberSize = 1.0;

        public double? ToolbarLeft { get; set; }
        public double? ToolbarTop { get; set; }
        public bool MenuOpen { get; set; }

        public sealed class SavedSettings
        {
            public int Schema { get; set; }
            public int PointerShape { get; set; }
            public double PointerSize { get; set; }
            public string PointerColor { get; set; }
            public bool ClickRipple { get; set; }
            public int CurrentTool { get; set; }
            public string DrawingColor { get; set; }
            public double StrokeThickness { get; set; }
            public double FontSize { get; set; }
            public bool Blackboard { get; set; }
            public double NumberSize { get; set; }
            public double? ToolbarLeft { get; set; }
            public double? ToolbarTop { get; set; }
            public bool MenuOpen { get; set; }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Panseo", "settings.json"); }
        }

        public void Load()
        {
            Load(SettingsPath);
        }

        internal void Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var saved = new JavaScriptSerializer().Deserialize<SavedSettings>(File.ReadAllText(path));
                if (saved == null || saved.Schema != 1) return;
                if (Enum.IsDefined(typeof(PointerShape), saved.PointerShape)) PointerShape = (PointerShape)saved.PointerShape;
                if (saved.PointerSize >= 20 && saved.PointerSize <= 160) PointerSize = saved.PointerSize;
                if (!string.IsNullOrEmpty(saved.PointerColor)) PointerColor = (Color)ColorConverter.ConvertFromString(saved.PointerColor);
                IsClickRippleEnabled = saved.ClickRipple;
                if (Enum.IsDefined(typeof(DrawingTool), saved.CurrentTool)) CurrentTool = (DrawingTool)saved.CurrentTool;
                if (!string.IsNullOrEmpty(saved.DrawingColor)) DrawingColor = (Color)ColorConverter.ConvertFromString(saved.DrawingColor);
                if (saved.StrokeThickness >= 2 && saved.StrokeThickness <= 24) StrokeThickness = saved.StrokeThickness;
                if (saved.FontSize >= 8 && saved.FontSize <= 100) FontSize = saved.FontSize;
                IsBlackboardActive = saved.Blackboard;
                if (saved.NumberSize >= 0.5 && saved.NumberSize <= 2) NumberSize = saved.NumberSize;
                ToolbarLeft = saved.ToolbarLeft;
                ToolbarTop = saved.ToolbarTop;
                MenuOpen = saved.MenuOpen;
            }
            catch (Exception) { /* 손상된 설정은 기본값으로 실행한다. */ }
        }

        public void Save()
        {
            Save(SettingsPath);
        }

        internal void Save(string path)
        {
            var saved = new SavedSettings
            {
                Schema = 1, PointerShape = (int)PointerShape, PointerSize = PointerSize,
                PointerColor = PointerColor.ToString(), ClickRipple = IsClickRippleEnabled,
                CurrentTool = (int)CurrentTool, DrawingColor = DrawingColor.ToString(),
                StrokeThickness = StrokeThickness, FontSize = FontSize,
                Blackboard = IsBlackboardActive, NumberSize = NumberSize,
                ToolbarLeft = ToolbarLeft, ToolbarTop = ToolbarTop, MenuOpen = MenuOpen
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(saved));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        public double NumberSize
        {
            get { return _numberSize; }
            set
            {
                double size = Math.Max(0.5, Math.Min(2.0, value));
                if (Math.Abs(_numberSize - size) < 0.01) return;
                _numberSize = size;
                OnPropertyChanged("NumberSize");
            }
        }

        public long TakeNextNumber() { return _nextNumber++; }
        public void ResetNumbering() { SetStartNumber(1); }
        public void SetStartNumber(int number)
        {
            if (number < 1) throw new ArgumentOutOfRangeException("number");
            _nextNumber = number;
        }
        public static bool TryParseStartNumber(string text, out int number)
        {
            return int.TryParse(text, out number) && number > 0;
        }

        public bool IsTextEditing
        {
            get { return _isTextEditing; }
            set
            {
                if (_isTextEditing != value)
                {
                    _isTextEditing = value;
                    OnPropertyChanged("IsTextEditing");
                }
            }
        }

        public PointerShape PointerShape
        {
            get { return _pointerShape; }
            set
            {
                if (_pointerShape != value)
                {
                    _pointerShape = value;
                    OnPropertyChanged("PointerShape");
                }
            }
        }

        public double PointerSize
        {
            get { return _pointerSize; }
            set
            {
                if (Math.Abs(_pointerSize - value) > 0.01)
                {
                    _pointerSize = value;
                    OnPropertyChanged("PointerSize");
                }
            }
        }

        public Color PointerColor
        {
            get { return _pointerColor; }
            set
            {
                if (_pointerColor != value)
                {
                    _pointerColor = value;
                    OnPropertyChanged("PointerColor");
                }
            }
        }

        public bool IsClickRippleEnabled
        {
            get { return _isClickRippleEnabled; }
            set
            {
                if (_isClickRippleEnabled != value)
                {
                    _isClickRippleEnabled = value;
                    OnPropertyChanged("IsClickRippleEnabled");
                }
            }
        }

        public DrawingTool CurrentTool
        {
            get { return _currentTool; }
            set
            {
                if (_currentTool != value)
                {
                    _currentTool = value;
                    OnPropertyChanged("CurrentTool");
                }
            }
        }

        public Color DrawingColor
        {
            get { return _drawingColor; }
            set
            {
                if (_drawingColor != value)
                {
                    _drawingColor = value;
                    OnPropertyChanged("DrawingColor");
                }
            }
        }

        public double StrokeThickness
        {
            get { return _strokeThickness; }
            set
            {
                if (Math.Abs(_strokeThickness - value) > 0.01)
                {
                    _strokeThickness = value;
                    OnPropertyChanged("StrokeThickness");
                }
            }
        }

        public double FontSize
        {
            get { return _fontSize; }
            set
            {
                if (Math.Abs(_fontSize - value) > 0.01)
                {
                    _fontSize = value;
                    OnPropertyChanged("FontSize");
                }
            }
        }

        public bool IsBlackboardActive
        {
            get { return _isBlackboardActive; }
            set
            {
                if (_isBlackboardActive != value)
                {
                    _isBlackboardActive = value;
                    OnPropertyChanged("IsBlackboardActive");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
