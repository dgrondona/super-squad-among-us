using MiraAPI.Utilities;
using TownOfUs;
using UnityEngine;

namespace SuperSquadAmongUs;

public static class SuperSquadColors
{
    // Crew Colors
    public static Color Chameleon => TownOfUsColors.UseBasic ? Palette.CrewmateBlue : new Color32(81, 180, 154, 255);
    public static Color Apparater => TownOfUsColors.UseBasic ? Palette.CrewmateBlue : new Color32(46, 204, 113, 255);
    // Neutral Colors
    public static Color Sentinel => new Color32(143, 162, 141, 255);
}