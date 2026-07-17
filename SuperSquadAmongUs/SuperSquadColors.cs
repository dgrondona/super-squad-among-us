using MiraAPI.Utilities;
using TownOfUs;
using UnityEngine;

namespace SuperSquadAmongUs;

public static class SuperSquadColors
{
    // Crew Colors
    public static Color Chameleon => TownOfUsColors.UseBasic ? Palette.CrewmateBlue : new Color32(81, 180, 154, 255);
    public static Color Apparater => TownOfUsColors.UseBasic ? Palette.CrewmateBlue : new Color32(46, 204, 113, 255);
    public static Color InvisibleBoy => TownOfUsColors.UseBasic ? Palette.CrewmateBlue : new Color32(190, 220, 245, 255);
    // Neutral Colors
    public static Color Sentinel => new Color32(143, 162, 141, 255);
    public static Color Pelican => new Color32(106, 21, 171, 255);
    public static Color Vulture => new Color32(139, 69, 19, 255);
    // Modifier Colors
    public static Color InvisibilityCloak => new Color32(197, 213, 232, 255);
    public static Color SlideTackle => new Color32(120, 190, 90, 255);
}