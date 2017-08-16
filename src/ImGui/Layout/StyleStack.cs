﻿using System;
using System.Collections.Generic;

namespace ImGui
{
    internal class StyleStack
    {
        public GUIStyle Style => GUIStyle.Default;

        Stack<StyleModifier> ModifierStack { get; } = new Stack<StyleModifier>();

        public void Push(StyleModifier modifier)
        {
            this.ModifierStack.Push(modifier);
            modifier.Modify(this.Style);
        }

        public void PushRange(StyleModifier[] modifiers)
        {
            foreach (var modifier in modifiers)
            {
                this.ModifierStack.Push(modifier);
                modifier.Modify(this.Style);
            }
        }

        public void PopStyle(int number = 1)
        {
            for (int i = 0; i < number; i++)
            {
                var modifier = this.ModifierStack.Pop();
                modifier.Restore(this.Style);
            }
        }

        #region positon, size

        #region min/max width/height

        public void PushWidth((double, double) width)
        {
            var modifier1 = new StyleModifier(GUIStyleName.MinWidth, StyleType.@double, width.Item1);
            var modifier2 = new StyleModifier(GUIStyleName.MaxWidth, StyleType.@double, width.Item2);
            Push(modifier1);
            Push(modifier2);
        }

        public void PushHeight((double, double) height)
        {
            var modifier1 = new StyleModifier(GUIStyleName.MinHeight, StyleType.@double, height.Item1);
            var modifier2 = new StyleModifier(GUIStyleName.MaxHeight, StyleType.@double, height.Item2);
            Push(modifier1);
            Push(modifier2);
        }

        #endregion

        #region stretch factor

        public void PushStretchFactor(bool isVertical, int factor)
        {
            var modifier = new StyleModifier(isVertical? GUIStyleName.VerticalStretchFactor : GUIStyleName.HorizontalStretchFactor, StyleType.@int, factor);
            Push(modifier);
        }

        #endregion

        #region cell spacing

        public void PushCellSpacing(bool isVertical, double spacing)
        {
            var modifier = new StyleModifier(isVertical ? GUIStyleName.CellingSpacingVertical : GUIStyleName.CellingSpacingHorizontal, StyleType.@double, spacing);
            Push(modifier);
        }

        #endregion

        #region alignment

        public void PushAlignment(bool isVertical, Alignment alignment)
        {
            var modifier = new StyleModifier(isVertical ? GUIStyleName.AlignmentVertical : GUIStyleName.AlignmentHorizontal, StyleType.@int, (int)alignment);
            Push(modifier);
        }

        #endregion

        #region box model

        public void PushBorder((double, double, double, double) border)
        {
            var modifier1 = new StyleModifier(GUIStyleName.BorderTop, StyleType.@double, border.Item1);
            var modifier2 = new StyleModifier(GUIStyleName.BorderRight, StyleType.@double, border.Item2);
            var modifier3 = new StyleModifier(GUIStyleName.BorderBottom, StyleType.@double, border.Item3);
            var modifier4 = new StyleModifier(GUIStyleName.BorderLeft, StyleType.@double, border.Item4);
            Push(modifier1);
            Push(modifier2);
            Push(modifier3);
            Push(modifier4);
        }

        public void PushPadding((double, double, double, double) padding)
        {
            var modifier1 = new StyleModifier(GUIStyleName.PaddingTop, StyleType.@double, padding.Item1);
            var modifier2 = new StyleModifier(GUIStyleName.PaddingRight, StyleType.@double, padding.Item2);
            var modifier3 = new StyleModifier(GUIStyleName.PaddingBottom, StyleType.@double, padding.Item3);
            var modifier4 = new StyleModifier(GUIStyleName.PaddingLeft, StyleType.@double, padding.Item4);
            Push(modifier1);
            Push(modifier2);
            Push(modifier3);
            Push(modifier4);
        }

        #endregion

        #endregion

        #region image, color

        #endregion

    }
}