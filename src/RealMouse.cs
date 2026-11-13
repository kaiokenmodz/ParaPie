#if PARAPIE_DEV
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ParaPie
{
    /// <summary>Playtest only: real OS-level mouse clicks inside the game window (Windows).</summary>
    internal static class RealMouse
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

        private const uint LeftDown = 0x02, LeftUp = 0x04, RightDown = 0x08, RightUp = 0x10;

        public static IntPtr Window;

        public static void Init()
        {
            Window = GetActiveWindow();
            if (Window == IntPtr.Zero) Window = GetForegroundWindow();
        }

        /// <summary>Moves the cursor to a Unity screen position (origin bottom-left).</summary>
        public static bool MoveTo(Vector2 unityScreen)
        {
            var p = new POINT { X = Mathf.RoundToInt(unityScreen.x), Y = Mathf.RoundToInt(Screen.height - unityScreen.y) };
            if (Window != IntPtr.Zero) ClientToScreen(Window, ref p);
            return SetCursorPos(p.X, p.Y);
        }

        public static void Down(bool right) { mouse_event(right ? RightDown : LeftDown, 0, 0, 0, UIntPtr.Zero); }
        public static void Up(bool right) { mouse_event(right ? RightUp : LeftUp, 0, 0, 0, UIntPtr.Zero); }
        public static bool IsForeground { get { return Window != IntPtr.Zero && GetForegroundWindow() == Window; } }
    }
}

#endif
