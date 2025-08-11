using Microsoft.Xna.Framework.Input;

namespace MapConfiguratorCivilization7
{
    public struct MouseStates
    {
        public MouseState New, Old;

        public MouseStates(MouseState New, MouseState Old)
        {
            this.New = New;
            this.Old = Old;
        }

        public bool IsMiddleButtonToggleOn()
        {
            return New.MiddleButton == ButtonState.Pressed && Old.MiddleButton == ButtonState.Released;
        }
        public bool IsMiddleButtonToggleOff()
        {
            return New.MiddleButton == ButtonState.Released && Old.MiddleButton == ButtonState.Pressed;
        }

        public bool IsLeftButtonToggleOn()
        {
            return New.LeftButton == ButtonState.Pressed && Old.LeftButton == ButtonState.Released;
        }
        public bool IsRightButtonToggleOn()
        {
            return New.RightButton == ButtonState.Pressed && Old.RightButton == ButtonState.Released;
        }
        public bool IsLeftButtonToggleOff()
        {
            return New.LeftButton == ButtonState.Released && Old.LeftButton == ButtonState.Pressed;
        }
        public bool IsRightButtonToggleOff()
        {
            return New.RightButton == ButtonState.Released && Old.RightButton == ButtonState.Pressed;
        }
    }

    public struct KeyboardStates
    {
        public KeyboardState New, Old;

        public KeyboardStates(KeyboardState New, KeyboardState Old)
        {
            this.New = New;
            this.Old = Old;
        }
        public bool IsKeyToggleDown(Keys key)
        {
            return New.IsKeyDown(key) && Old.IsKeyUp(key);
        }
        public bool IsKeyToggleUp(Keys key)
        {
            return New.IsKeyUp(key) && Old.IsKeyDown(key);
        }
    }

    public static class IO
    {
        public static KeyboardStates statesKeyboard;
        public static MouseStates statesMouse;

        public static double mouseNoMoveTime = 0;

        public static void Setup()
        {
            Update(0);
            UpdateEnd();
        }

        public static void Update(double timeMs)
        {
            statesKeyboard.New = Keyboard.GetState();
            statesMouse.New = Mouse.GetState();
            if (statesMouse.New.Position == statesMouse.Old.Position)
                mouseNoMoveTime += timeMs;
            else
                mouseNoMoveTime = 0;
        }

        public static void UpdateEnd()
        {
            statesKeyboard.Old = statesKeyboard.New;
            statesMouse.Old = statesMouse.New;
        }
    }
}
