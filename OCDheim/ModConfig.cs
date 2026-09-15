using BepInEx.Configuration;
using Jotunn.Extensions;

namespace OCDheim
{
    // Per Jotunn's config-sync tutorial: BindConfig's `synced` flag makes an entry admin-only and
    // server-authoritative once connected - "Local settings will be overriden by the server's
    // values as long as the client is connected to that server." It never gates the connection
    // itself (unsynced/unmodded clients still connect fine, they just don't get the feature), so
    // this is safe for the two toggles that add pieces/prefabs (which Jotunn's ModCompatibility
    // check hashes between server and client) without affecting who can join.
    public static class ModConfig
    {
        public static ConfigEntry<bool> EnableWorldGridMode { get; private set; }
        public static ConfigEntry<bool> EnablePrecisionMode { get; private set; }
        public static ConfigEntry<bool> EnableTerrainModification { get; private set; }
        public static ConfigEntry<bool> EnableVerticalStacking { get; private set; }
        public static ConfigEntry<bool> EnableAdditionalBuildPieces { get; private set; }

        public static void Bind(ConfigFile config)
        {
            EnableWorldGridMode = config.BindConfig(
                "World Grid Mode",
                "Enabled",
                true,
                "Visually impose a grid over terrain (toggle with ALT / Right Stick Button) and snap build pieces, seeds, and terrain tools to it.",
                synced: false);

            EnablePrecisionMode = config.BindConfig(
                "Precision Mode",
                "Enabled",
                true,
                "Additional snap points on Furniture/Construction Pieces (toggle density with Z / West Button), and Construction Pieces snapping to each other.",
                synced: false);

            EnableTerrainModification = config.BindConfig(
                "Terrain Modification",
                "Enabled",
                true,
                "Precise, uniform raise/level/smooth/paint terrain shaping while Grid Mode is active, the AoE overlay HUD, and the \"Remove Terrain Modifications\" Hoe piece.",
                synced: true);

            EnableVerticalStacking = config.BindConfig(
                "Vertical Stacking",
                "Enabled",
                true,
                "Allow Piles, Stacks, and Barrels to support other pieces built on top of them.",
                synced: false);

            EnableAdditionalBuildPieces = config.BindConfig(
                "Additional Build Pieces",
                "Enabled",
                true,
                "Add the Smooth Stone brick build pieces (1x1, 2x1, 1x2, 4x2, 2x2).",
                synced: true);
        }
    }
}
