using System;
using System.Collections.Generic;

namespace Avalonia3DControl.UI
{
    /// <summary>
    /// 优化的字符渲染器，使用查找表提高七段显示字符的渲染性能
    /// </summary>
    public static class CharacterRenderer
    {
        /// <summary>
        /// 七段显示的段定义
        /// </summary>
        [Flags]
        private enum Segments : byte
        {
            None = 0,
            A = 1 << 0,  // 上
            B = 1 << 1,  // 右上
            C = 1 << 2,  // 右下
            D = 1 << 3,  // 下
            E = 1 << 4,  // 左下
            F = 1 << 5,  // 左上
            G = 1 << 6   // 中
        }

        /// <summary>
        /// 字符到段的映射查找表
        /// </summary>
        private static readonly Dictionary<char, Segments> CharacterSegments = new Dictionary<char, Segments>
        {
            ['0'] = Segments.A | Segments.B | Segments.C | Segments.D | Segments.E | Segments.F,
            ['1'] = Segments.B | Segments.C,
            ['2'] = Segments.A | Segments.B | Segments.G | Segments.E | Segments.D,
            ['3'] = Segments.A | Segments.B | Segments.G | Segments.C | Segments.D,
            ['4'] = Segments.F | Segments.G | Segments.B | Segments.C,
            ['5'] = Segments.A | Segments.F | Segments.G | Segments.C | Segments.D,
            ['6'] = Segments.A | Segments.F | Segments.G | Segments.E | Segments.C | Segments.D,
            ['7'] = Segments.A | Segments.B | Segments.C,
            ['8'] = Segments.A | Segments.B | Segments.C | Segments.D | Segments.E | Segments.F | Segments.G,
            ['9'] = Segments.A | Segments.B | Segments.C | Segments.D | Segments.F | Segments.G,
            ['-'] = Segments.G,
            [' '] = Segments.None
        };

        /// <summary>
        /// 段坐标定义（相对坐标0..1）
        /// </summary>
        private static readonly Dictionary<Segments, (float x1, float y1, float x2, float y2)> SegmentCoordinates = 
            new Dictionary<Segments, (float, float, float, float)>
        {
            [Segments.A] = (0.1f, 0.95f, 0.9f, 0.95f),   // 上
            [Segments.B] = (0.9f, 0.95f, 0.9f, 0.5f),    // 右上
            [Segments.C] = (0.9f, 0.5f, 0.9f, 0.05f),    // 右下
            [Segments.D] = (0.1f, 0.05f, 0.9f, 0.05f),   // 下
            [Segments.E] = (0.1f, 0.5f, 0.1f, 0.05f),    // 左下
            [Segments.F] = (0.1f, 0.95f, 0.1f, 0.5f),    // 左上
            [Segments.G] = (0.15f, 0.5f, 0.85f, 0.5f)    // 中
        };

        /// <summary>
        /// 添加字符的线段到顶点列表
        /// </summary>
        /// <param name="verts">顶点列表</param>
        /// <param name="c">字符</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="w">宽度</param>
        /// <param name="h">高度</param>
        public static void AddCharSegments(List<float> verts, char c, float x, float y, float w, float h)
        {
            // 处理小数点特殊情况
            if (c == '.')
            {
                // 右下角一个小点（用短线代替）
                float cx = x + w * 0.85f;
                float cy = y + h * 0.02f;
                verts.Add(cx - w * 0.03f);
                verts.Add(cy - h * 0.02f);
                verts.Add(cx + w * 0.03f);
                verts.Add(cy + h * 0.02f);
                return;
            }

            // 查找字符对应的段
            if (!CharacterSegments.TryGetValue(c, out Segments segments))
            {
                return; // 不支持的字符，跳过
            }

            // 遍历所有段，添加激活的段
            foreach (Segments segment in Enum.GetValues<Segments>())
            {
                if (segment == Segments.None || !segments.HasFlag(segment))
                    continue;

                if (SegmentCoordinates.TryGetValue(segment, out var coords))
                {
                    // 转换相对坐标到实际坐标
                    float x1 = x + w * coords.x1;
                    float y1 = y + h * coords.y1;
                    float x2 = x + w * coords.x2;
                    float y2 = y + h * coords.y2;

                    // 添加线段顶点
                    verts.Add(x1);
                    verts.Add(y1);
                    verts.Add(x2);
                    verts.Add(y2);
                }
            }
        }

        /// <summary>
        /// 添加文本的线段到顶点列表
        /// </summary>
        /// <param name="verts">顶点列表</param>
        /// <param name="text">文本</param>
        /// <param name="startX">起始X坐标</param>
        /// <param name="startY">起始Y坐标</param>
        /// <param name="charW">字符宽度</param>
        /// <param name="charH">字符高度</param>
        public static void AddTextLineSegments(List<float> verts, string text, float startX, float startY, float charW, float charH)
        {
            float x = startX;
            foreach (char c in text)
            {
                AddCharSegments(verts, c, x, startY, charW, charH);
                x += charW * 0.75f; // 字间距
            }
        }
    }
}