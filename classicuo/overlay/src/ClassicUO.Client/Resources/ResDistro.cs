// SPDX-License-Identifier: BSD-2-Clause

using System.Globalization;
using System.Resources;

namespace ClassicUO.Resources
{
    /// <summary>
    ///     Strings only this distribution uses (ResDistro.resx). They live apart from upstream's
    ///     ResGumps, which upstream keeps adding to, so merging it never touches them.
    /// </summary>
    internal static class ResDistro
    {
        private static ResourceManager _resourceManager;

        private static ResourceManager ResourceManager =>
            _resourceManager ??= new ResourceManager("ClassicUO.Resources.ResDistro", typeof(ResDistro).Assembly);

        public static string MacroLoop => ResourceManager.GetString(nameof(MacroLoop), CultureInfo.CurrentUICulture);
    }
}
