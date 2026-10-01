// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Foundation;

namespace iNKORE.UI.WPF.Modern.Native;

internal partial class NativeMethods
{
    internal static unsafe HWND FindWindow(string lpClassName, string lpWindowName)
    {
        fixed (char* lpWindowNameLocal = lpWindowName)
        {
            fixed (char* lpClassNameLocal = lpClassName)
            {
                HWND __result = User32.FindWindow(lpClassNameLocal, lpWindowNameLocal);
                return __result;
            }
        }
    }

}
