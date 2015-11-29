﻿using System.Diagnostics;
using Cairo;
using System.Collections.Generic;
using TinyIoC;

//TODO use stand-alone window to show the items
//BUG Hover state persists when move from mainRect to outside.
//BUG Abnormal representation when drag from mainRect to outside.

namespace IMGUI
{
    internal class ComboBox : Control, IRect
    {
        #region State machine constants
        static class ComboBoxState
        {
            public const string Normal = "Normal";
            public const string Hovered = "Hovered";
            public const string Active = "Active";
            public const string ShowingItems = "ShowingItems";
        }

        static class ComboBoxCommand
        {
            public const string MoveIn = "MoveIn";
            public const string MoveOut = "MoveOut";
            public const string MousePress = "MouseDown";
            public const string ShowItems = "ShowItems";
            public const string SelectItem = "SelectItem";
        }

        static readonly string[] states =
        {
            ComboBoxState.Normal, ComboBoxCommand.MoveIn, ComboBoxState.Hovered,
            ComboBoxState.Hovered, ComboBoxCommand.MoveOut, ComboBoxState.Normal,
            ComboBoxState.Hovered, ComboBoxCommand.MousePress, ComboBoxState.Active,
            ComboBoxState.Active, ComboBoxCommand.ShowItems, ComboBoxState.ShowingItems,
            ComboBoxState.ShowingItems, ComboBoxCommand.SelectItem, ComboBoxState.Normal,
        };
        #endregion

        private StateMachine stateMachine;

        public string[] Texts { get; private set; }
        public BorderlessForm ItemsContainer { get; private set; }
        public Rect Rect { get; private set; }

        private string text;
        public string Text
        {
            get { return text; }
            private set
            {
                if (Text == value)
                {
                    return;
                }

                text = value;
                NeedRepaint = true;
            }
        }

        public ITextFormat Format { get; private set; }
        public ITextLayout Layout { get; private set; }

        public int SelectedIndex { get; private set; }//TODO consider remove this property
        
        internal ComboBox(string name, BaseForm form, string[] texts, Rect rect)
            : base(name, form)
        {
            Rect = rect;
            Texts = texts;
            Text = texts[0];
            SelectedIndex = 0;
            stateMachine = new StateMachine(ComboBoxState.Normal, states);

            var font = Skin.current.Button[State].Font;
            Format = Application.IocContainer.Resolve<ITextFormat>(
                new NamedParameterOverloads
                    {
                        {"fontFamilyName", font.FontFamily},
                        {"fontWeight", font.FontWeight},
                        {"fontStyle", font.FontStyle},
                        {"fontStretch", font.FontStretch},
                        {"fontSize", (float) font.Size}
                    });
            var textStyle = Skin.current.Button[State].TextStyle;
            Format.Alignment = textStyle.TextAlignment;
            Layout = Application.IocContainer.Resolve<ITextLayout>(
                new NamedParameterOverloads
                    {
                        {"text", Text},
                        {"textFormat", Format},
                        {"maxWidth", (int)Rect.Width},
                        {"maxHeight", (int)Rect.Height}
                    });


            var screenRect = Utility.GetScreenRect(Rect, this.Form);
            ItemsContainer = new ComboxBoxItemsForm(
                screenRect,
                Texts, i =>
                {
                    SelectedIndex = i;
                    this.stateMachine.MoveNext(ComboBoxCommand.SelectItem);
                });
        }

        internal static int DoControl(Context g, Context gTop, BaseForm form, Rect rect, string[] texts, int selectedIndex, string name)
        {
            if (!form.Controls.ContainsKey(name))
            {
                var comboBox = new ComboBox(name, form, texts, rect);
                comboBox.SelectedIndex = selectedIndex;

                comboBox.OnUpdate();
                comboBox.OnRender(g);
            }
            var control = form.Controls[name] as ComboBox;
            Debug.Assert(control != null);
            control.Active = true;

            return control.SelectedIndex;
        }

        #region Overrides of Control

        public override void OnUpdate()
        {
            Text = Texts[SelectedIndex];
            Layout.Text = Text;

            var oldState = State;
            bool active = stateMachine.CurrentState == ComboBoxState.Active;
            bool hover = stateMachine.CurrentState == ComboBoxState.Hovered;
            if (active)
            {
                State = "Active";
            }
            else if (hover)
            {
                State = "Hover";
            }
            else
            {
                State = "Normal";
            }

            if (State != oldState)
            {
                NeedRepaint = true;
            }

            //Execute state commands
            var containMousePosition = Rect.Contains(Utility.ScreenToClient(Input.Mouse.MousePos, Form));
            if (!Rect.Contains(Utility.ScreenToClient(Input.Mouse.LastMousePos, Form)) && containMousePosition)
            {
                stateMachine.MoveNext(ComboBoxCommand.MoveIn);
            }
            if (Rect.Contains(Utility.ScreenToClient(Input.Mouse.LastMousePos, Form)) && !containMousePosition)
            {
                stateMachine.MoveNext(ComboBoxCommand.MoveOut);
            }
            if (Input.Mouse.stateMachine.CurrentState == Input.Mouse.MouseState.Pressed && containMousePosition && Form.Focused)
            {
                if (stateMachine.MoveNext(ComboBoxCommand.MousePress))
                {
                    Input.Mouse.stateMachine.MoveNext(Input.Mouse.MouseCommand.Fetch);
                }
            }
            if(stateMachine.CurrentState == ComboBoxState.Active)//instant transition of state
            {
                var screenRect = Utility.GetScreenRect(new Rect(Rect.BottomLeft.X, Rect.BottomLeft.Y, Rect.Size), this.Form);
                ItemsContainer.Position = screenRect.TopLeft;
                Application.Forms.Add(ItemsContainer);
                ItemsContainer.Show();
                stateMachine.MoveNext(ComboBoxCommand.ShowItems);
            }

        }

        public override void OnRender(Context g)
        {
            g.DrawBoxModel(Rect, new Content(Layout), Skin.current.ComboBox[State]);
            g.LineWidth = 1;
            var trianglePoints = new Point[3];
            trianglePoints[0].X = Rect.TopRight.X - 5;
            trianglePoints[0].Y = Rect.TopRight.Y + 0.2 * Rect.Height;
            trianglePoints[1].X = trianglePoints[0].X - 0.6 * Rect.Height;
            trianglePoints[1].Y = trianglePoints[0].Y;
            trianglePoints[2].X = trianglePoints[0].X - 0.3 * Rect.Height;
            trianglePoints[2].Y = trianglePoints[0].Y + 0.6 * Rect.Height;
            g.StrokePolygon(trianglePoints, CairoEx.ColorBlack);
        }

        public override void Dispose()
        {
            Layout.Dispose();
            Format.Dispose();
        }
        
        public override void OnClear(Context g)
        {
            g.FillRectangle(Rect, CairoEx.ColorWhite);
        }


        #endregion
    }

    internal sealed class ComboxBoxItemsForm : BorderlessForm
    {
        private Rect Rect { get; set; }
        private List<string> TextList;
        private System.Action<int> CallBack { get; set; }

        public ComboxBoxItemsForm(Rect rect, string[] text, System.Action<int> callBack)
            : base((int)rect.Width, (int)rect.Height * text.Length)
        {
            Position = rect.TopLeft;
            rect.X = rect.Y = 0;
            Rect = rect;
            TextList = new List<string>(text);
            CallBack = callBack;
        }

        protected override void OnGUI(GUI gui)
        {
            gui.BeginV();
            for (int i = 0; i < TextList.Count; i++)
            {
                var itemRect = Rect;
                itemRect.Y += (i + 1) * Rect.Height;
                if(gui.Button(new Rect(Rect.Width, itemRect.Height), TextList[i], this.Name + "item" + i))
                {
                    if(CallBack!=null)
                    {
                        CallBack(i);
                    }
                    this.Hide();
                    Application.Forms.Remove(this);
                }
            }
            gui.EndV();
        }
    }
}
