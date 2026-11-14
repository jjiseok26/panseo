using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

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
