using Microsoft.Xna.Framework;
using System;
using System.Runtime.InteropServices;

namespace Simitone.Mac
{
    /// <summary>
    /// MonoGame 3.8 creates its SDL window without SDL_WINDOW_ALLOW_HIGHDPI, so on a Retina display the game draws
    /// at half the screen's pixel density and macOS scales it up. This asks AppKit for a full-resolution OpenGL
    /// surface on the existing window and reports its pixel size (SDL itself only reports pixels for windows it
    /// created high-DPI, so the backing scale comes from NSWindow).
    /// </summary>
    public static unsafe class MacRetina
    {
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        private const int SDL_SYSWM_COCOA = 4;

        [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);

        private static IntPtr MsgSend;
        private static IntPtr NSWindow;
        private static IntPtr SdlWindow;

        /// <summary>
        /// Switches the window's OpenGL surface to full resolution. Call on the main thread once the window and GL
        /// context exist. Returns false (leaving the game as it was) if anything isn't available.
        /// </summary>
        public static bool Enable(IntPtr sdlWindow)
        {
            IntPtr objc;
            if (!NativeLibrary.TryLoad(ObjC, out objc) || !NativeLibrary.TryGetExport(objc, "objc_msgSend", out MsgSend)) return false;

            var nsWindow = GetNSWindow(sdlWindow);
            if (nsWindow == IntPtr.Zero) return false;

            var send = (delegate* unmanaged<IntPtr, IntPtr, IntPtr>)MsgSend;
            var sendBool = (delegate* unmanaged<IntPtr, IntPtr, byte, void>)MsgSend;

            var view = send(nsWindow, sel_registerName("contentView"));
            if (view == IntPtr.Zero) return false;
            sendBool(view, sel_registerName("setWantsBestResolutionOpenGLSurface:"), 1);

            //the GL context has to re-read its view's backing size
            var getContext = MacSdl.Get("SDL_GL_GetCurrentContext");
            if (getContext != IntPtr.Zero)
            {
                var context = ((delegate* unmanaged[Cdecl]<IntPtr>)getContext)();
                if (context != IntPtr.Zero) send(context, sel_registerName("update"));
            }

            NSWindow = nsWindow;
            SdlWindow = sdlWindow;
            return true;
        }

        /// <summary>
        /// Window size in pixels: points times the window's current backing scale (2 on Retina, 1 elsewhere).
        /// </summary>
        public static Point GetPixelSize()
        {
            if (NSWindow == IntPtr.Zero) return Point.Zero;
            var scale = ((delegate* unmanaged<IntPtr, IntPtr, double>)MsgSend)(NSWindow, sel_registerName("backingScaleFactor"));
            var (w, h) = MacSdl.GetWindowSize(SdlWindow);
            return new Point((int)Math.Round(w * scale), (int)Math.Round(h * scale));
        }

        private static IntPtr GetNSWindow(IntPtr sdlWindow)
        {
            var getVersion = MacSdl.Get("SDL_GetVersion");
            var getInfo = MacSdl.Get("SDL_GetWindowWMInfo");
            if (getVersion == IntPtr.Zero || getInfo == IntPtr.Zero) return IntPtr.Zero;

            //SDL_SysWMinfo: SDL_version (3 bytes), SDL_SYSWM_TYPE subsystem at 4, then a union whose cocoa member
            //starts with NSWindow* at 8. The union is at least 64 bytes.
            var info = stackalloc byte[256];
            ((delegate* unmanaged[Cdecl]<byte*, void>)getVersion)(info);
            if (((delegate* unmanaged[Cdecl]<IntPtr, byte*, int>)getInfo)(sdlWindow, info) == 0) return IntPtr.Zero;
            if (*(int*)(info + 4) != SDL_SYSWM_COCOA) return IntPtr.Zero;
            return *(IntPtr*)(info + 8);
        }

        /// <summary>
        /// SDL reports the mouse in points; FreeSO expects back buffer pixels and divides by DPIScaleFactor itself.
        /// </summary>
        public static Microsoft.Xna.Framework.Input.MouseState ScaleMouse(Microsoft.Xna.Framework.Input.MouseState state)
        {
            var scale = FSO.Common.FSOEnvironment.DPIScaleFactor;
            if (scale == 1) return state;
            return new Microsoft.Xna.Framework.Input.MouseState((int)(state.X * scale), (int)(state.Y * scale),
                state.ScrollWheelValue, state.LeftButton, state.MiddleButton, state.RightButton,
                state.XButton1, state.XButton2, state.HorizontalScrollWheelValue);
        }
    }
}
