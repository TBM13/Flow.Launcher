// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Windows.Win32.Foundation;

namespace iNKORE.UI.WPF.Modern.Helpers
{
    internal static class PointUtil
    {
        internal static Rect ToRect(this RECT rc)
        {
            Rect rect = new Rect
            {
                X = rc.left,
                Y = rc.top,
                Width = rc.right - rc.left,
                Height = rc.bottom - rc.top
            };

            return rect;
        }
    }
}
