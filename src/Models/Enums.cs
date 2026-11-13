using System;

namespace Panseo.Models
{
    public enum PointerShape
    {
        CircleHighlight, // 원형 하이라이터
        LaserPointer,    // 레이저 포인터
        Spotlight,       // 스포트라이트 (마우스 영역만 밝게)
        FocusRing,       // 도넛형 타겟 링
        None             // 끄기
    }

    public enum DrawingTool
    {
        None,        // 드로잉 안함 (마우스 일반 조작 모드)
        Pen,         // 일반 펜
        Highlighter, // 형광펜
        Arrow,       // 화살표
        Rectangle,   // 직사각형
        Ellipse,     // 원/타원
        Line,        // 직선
        Text,        // 텍스트 입력
        Eraser,      // 지우개
        Number       // 클릭할 때마다 순서대로 번호 넣기
    }
}
