using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.API;

internal class ColorHelper
{
    public static string Color(Misc.PlayerInfoColorTypes color, string innerText) => $"<color={Misc.AllowedColors[color]}>{innerText}</color>";
}
