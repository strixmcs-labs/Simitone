using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Simitone.Mac
{
    /// <summary>
    /// Access to the SDL2 library MonoGame already loaded, for the few calls MonoGame doesn't expose.
    /// </summary>
    public static unsafe class MacSdl
    {
        private static IntPtr Lib;

        public static bool Load()
        {
            if (Lib != IntPtr.Zero) return true;
            //framework-dependent builds keep natives under runtimes/; self-contained (app bundle) builds put them next to the exe
            var dir = AppContext.BaseDirectory;
            return NativeLibrary.TryLoad(Path.Combine(dir, "runtimes", "osx", "native", "libSDL2-2.0.0.dylib"), out Lib)
                || NativeLibrary.TryLoad(Path.Combine(dir, "libSDL2-2.0.0.dylib"), out Lib);
        }

        public static IntPtr Get(string name)
        {
            IntPtr fn;
            return (Load() && NativeLibrary.TryGetExport(Lib, name, out fn)) ? fn : IntPtr.Zero;
        }

        /// <summary>Window size in points (what macOS calls the logical size).</summary>
        public static (int, int) GetWindowSize(IntPtr window)
        {
            var fn = Get("SDL_GetWindowSize");
            if (fn == IntPtr.Zero) return (0, 0);
            int w, h;
            ((delegate* unmanaged[Cdecl]<IntPtr, int*, int*, void>)fn)(window, &w, &h);
            return (w, h);
        }

        /// <summary>OpenGL drawable size in pixels. Twice the window size on a Retina display when high-DPI is on.</summary>
        public static (int, int) GetDrawableSize(IntPtr window)
        {
            var fn = Get("SDL_GL_GetDrawableSize");
            if (fn == IntPtr.Zero) return (0, 0);
            int w, h;
            ((delegate* unmanaged[Cdecl]<IntPtr, int*, int*, void>)fn)(window, &w, &h);
            return (w, h);
        }

        public static uint GetWindowFlags(IntPtr window)
        {
            var fn = Get("SDL_GetWindowFlags");
            return fn == IntPtr.Zero ? 0 : ((delegate* unmanaged[Cdecl]<IntPtr, uint>)fn)(window);
        }

        /// <summary>Desktop display size in points, and its pixel density relative to 96 dpi.</summary>
        public static (int, int, float) GetDesktopMode(int display)
        {
            var mode = Get("SDL_GetDesktopDisplayMode");
            if (mode == IntPtr.Zero) return (0, 0, 0);
            //SDL_DisplayMode: uint format, int w, int h, int refresh_rate, void* driverdata
            var buf = stackalloc byte[32];
            if (((delegate* unmanaged[Cdecl]<int, byte*, int>)mode)(display, buf) != 0) return (0, 0, 0);
            float ddpi = 0, hdpi = 0, vdpi = 0;
            var dpi = Get("SDL_GetDisplayDPI");
            if (dpi != IntPtr.Zero) ((delegate* unmanaged[Cdecl]<int, float*, float*, float*, int>)dpi)(display, &ddpi, &hdpi, &vdpi);
            return (*(int*)(buf + 4), *(int*)(buf + 8), hdpi);
        }
    }
}
