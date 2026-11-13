using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Panseo.Helpers
{
    public static class DrawingHelper
    {
        /// <summary>
        /// 시작점(start)에서 끝점(end)을 향하는 화살표 형태의 Path를 생성합니다.
        /// </summary>
        public static Path CreateArrowPath(Point start, Point end, Brush strokeBrush, Brush fillBrush, double strokeThickness)
        {
            var geometry = new PathGeometry();

            // 줄기 선 (Line)
            var lineFig = new PathFigure { StartPoint = start, IsClosed = false };
            lineFig.Segments.Add(new LineSegment(end, true));
            geometry.Figures.Add(lineFig);

            // 화살표 머리 (Arrow Head)
            double headLength = Math.Max(16.0, strokeThickness * 3.5);
            double headAngle = 26.0 * (Math.PI / 180.0); // 라디안 각도 (약 26도)

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length > 2)
            {
                double angle = Math.Atan2(dy, dx);

                // 화살표 날개 1
                Point p1 = new Point(
                    end.X - headLength * Math.Cos(angle - headAngle),
                    end.Y - headLength * Math.Sin(angle - headAngle)
                );

                // 화살표 날개 2
                Point p2 = new Point(
                    end.X - headLength * Math.Cos(angle + headAngle),
                    end.Y - headLength * Math.Sin(angle + headAngle)
                );

                var headFig = new PathFigure
                {
                    StartPoint = end,
                    IsClosed = true,
                    IsFilled = true
                };
                headFig.Segments.Add(new LineSegment(p1, true));
                headFig.Segments.Add(new LineSegment(p2, true));
                geometry.Figures.Add(headFig);
            }

            var path = new Path
            {
                Data = geometry,
                Stroke = strokeBrush,
                StrokeThickness = strokeThickness,
                Fill = fillBrush,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };

            return path;
        }

        /// <summary>
        /// 두 점(start, current)을 기준으로 직사각형의 위치와 크기(Rect)를 계산합니다.
        /// </summary>
        public static Rect GetBoundingRect(Point p1, Point p2)
        {
            double x = Math.Min(p1.X, p2.X);
            double y = Math.Min(p1.Y, p2.Y);
            double w = Math.Abs(p1.X - p2.X);
            double h = Math.Abs(p1.Y - p2.Y);
            return new Rect(x, y, Math.Max(1, w), Math.Max(1, h));
        }
    }
}
