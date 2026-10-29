using Microsoft.Xna.Framework.Input;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Simitone.Mac
{
    /// <summary>
    /// MonoGame only polls the mouse once per frame. A trackpad tap (or a fast click) sends button down and up
    /// between two polls, so the game never sees the button pressed and the click is lost.
    /// This watches SDL's button-down events as they arrive and reports each one as pressed for at least one poll.
    /// </summary>
    public static unsafe class MacMouseLatch
    {
        private const uint SDL_MOUSEBUTTONDOWN = 0x401;
        private const int SDL_BUTTON_LEFT = 1;
        private const int SDL_BUTTON_MIDDLE = 2;
        private const int SDL_BUTTON_RIGHT = 3;

        //set from the SDL event watch, cleared once a poll has reported the press.
        //SDL runs event watches inside SDL_PollEvent, which MonoGame calls on the game thread before Update.
        private static bool LeftLatched;
        private static bool MiddleLatched;
        private static bool RightLatched;

        /// <summary>
        /// Hooks SDL. Returns false (and leaves input as it was) if SDL can't be found.
        /// </summary>
        public static bool Install()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "runtimes", "osx", "native", "libSDL2-2.0.0.dylib");
            IntPtr lib;
            if (!NativeLibrary.TryLoad(path, out lib) && !NativeLibrary.TryLoad("libSDL2-2.0.0.dylib", out lib)) return false;

            IntPtr addWatch;
            if (!NativeLibrary.TryGetExport(lib, "SDL_AddEventWatch", out addWatch)) return false;

            var add = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>, IntPtr, void>)addWatch;
            add(&OnEvent, IntPtr.Zero);
            return true;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int OnEvent(IntPtr userdata, IntPtr sdlEvent)
        {
            //SDL_MouseButtonEvent: uint type, uint timestamp, uint windowID, uint which, byte button, ...
            if ((uint)Marshal.ReadInt32(sdlEvent) == SDL_MOUSEBUTTONDOWN)
            {
                switch (Marshal.ReadByte(sdlEvent, 16))
                {
                    case SDL_BUTTON_LEFT: LeftLatched = true; break;
                    case SDL_BUTTON_MIDDLE: MiddleLatched = true; break;
                    case SDL_BUTTON_RIGHT: RightLatched = true; break;
                }
            }
            return 1;
        }

        /// <summary>
        /// GameScreen.MouseStateFilter: report a latched press as held for this poll. The release then shows up
        /// on the next poll, so the UI sees a complete click.
        /// </summary>
        public static MouseState Filter(MouseState state)
        {
            var left = Apply(ref LeftLatched, state.LeftButton);
            var middle = Apply(ref MiddleLatched, state.MiddleButton);
            var right = Apply(ref RightLatched, state.RightButton);
            if (left == state.LeftButton && middle == state.MiddleButton && right == state.RightButton) return state;
            return new MouseState(state.X, state.Y, state.ScrollWheelValue, left, middle, right,
                state.XButton1, state.XButton2, state.HorizontalScrollWheelValue);
        }

        private static ButtonState Apply(ref bool latched, ButtonState polled)
        {
            if (!latched) return polled;
            latched = false;
            return ButtonState.Pressed;
        }
    }
}
