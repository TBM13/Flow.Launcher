using System.Runtime.InteropServices;
using Windows.Win32.Graphics.Dwm;

namespace iNKORE.UI.WPF.Modern.Native;

/// <summary>
/// Used by Desktop Window Manager (DWM)
/// </summary>
internal static class DWMAPI
{
    /// <summary>
    /// Abstraction of pointer to an object containing the attribute value to set. The type of the value set depends on the value of the dwAttribute parameter.
    /// The DWMWINDOWATTRIBUTE enumeration topic indicates, in the row for each flag, what type of value you should pass a pointer to in the pvAttribute parameter.
    /// </summary>
    public enum PvAttribute
    {
        /// <summary>
        /// Object containing the <see langowrd="false"/> attribute value to set in dwmapi.h. 
        /// </summary>
        Disable = 0x00,

        /// <summary>
        /// Object containing the <see langowrd="true"/> attribute value to set in dwmapi.h. 
        /// </summary>
        Enable = 0x01
    }

    /// <summary>
    /// Sets the value of Desktop Window Manager (DWM) non-client rendering attributes for a window.
    /// </summary>
    /// <param name="hWnd">The handle to the window for which the attribute value is to be set.</param>
    /// <param name="dwAttribute">A flag describing which value to set, specified as a value of the DWMWINDOWATTRIBUTE enumeration.</param>
    /// <param name="pvAttribute">A pointer to an object containing the attribute value to set.</param>
    /// <param name="cbAttribute">The size, in bytes, of the attribute value being set via the <c>pvAttribute</c> parameter.</param>
    /// <returns>If the function succeeds, it returns <c>S_OK</c>. Otherwise, it returns an <c>HRESULT</c> error code.</returns>
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hWnd, DWMWINDOWATTRIBUTE dwAttribute, ref int pvAttribute,
        int cbAttribute);
}
