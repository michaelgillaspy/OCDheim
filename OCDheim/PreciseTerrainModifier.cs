using System.Reflection;
using HarmonyLib;
using UnityEngine;

using static OCDheim.GroundLevelSpinner;
using static TerrainModifier;

namespace OCDheim
{
    [HarmonyPatch]
    public static class PreciseTerrainModifier
    {
        private const int AoESize = 1;
        public const int HTilesPerChunk = 64;
        private const int PTilesPerChunk = 65;
        public const float HalfPTilesPerChunk = HTilesPerChunk * 0.5f;
        public const float PTileSize = HTilesPerChunk / (float)PTilesPerChunk;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp))]
        [HarmonyPatch(nameof(TerrainComp.InternalDoOperation))]
        private static bool Prefix(Vector3 pos, TerrainOp.Settings modifier, Heightmap ___m_hmap, ref float[] ___m_levelDelta, ref float[] ___m_smoothDelta, ref Color[] ___m_paintMask, ref bool[] ___m_modifiedHeight, ref bool[] ___m_modifiedPaint)
        {
            if (!modifier.m_level && !modifier.m_raise && !modifier.m_smooth && !modifier.m_paintCleared)
            {
                RemoveTerrainModifications(pos, ___m_hmap, ref ___m_levelDelta, ref ___m_smoothDelta, ref ___m_modifiedHeight);
                RecolorTerrain(pos, PaintType.Reset, ___m_hmap, ref ___m_paintMask, ref ___m_modifiedPaint, removeColor: true);
            }
            return true;
        }

        public static void LevelTerrain(Vector3 worldPos, Heightmap hMap, TerrainComp compiler, ref float[] levelΔ, ref float[] smoothΔ, ref bool[] modifiedHeight)
        {
            Logger.Debug(() => "[INIT] Level Terrain Modification");

            hMap.WorldToVertex(worldPos, out var xPos, out var yPos);
            var referenceH = worldPos.y - compiler.transform.position.y;
            Logger.Debug(() => $"worldPos: {worldPos}, xPos: {xPos}, yPos: {yPos}, referenceH: {referenceH}");

            FindExtremums(xPos, out var xMin, out var xMax);
            FindExtremums(yPos, out var yMin, out var yMax);
            for (var x = xMin; x <= xMax; x++)
            {
                for (var y = yMin; y <= yMax; y++)
                {
                    var tileIndex = y * PTilesPerChunk + x;
                    var tileH = hMap.GetHeight(x, y);
                    var Δh = referenceH - tileH;
                    var oldLevelΔ = levelΔ[tileIndex];
                    var oldSmoothΔ = smoothΔ[tileIndex];
                    var newLevelΔ = oldLevelΔ + oldSmoothΔ + Δh;
                    var roundedNewLevelΔ = RoundToTwoDecimals(tileH, oldLevelΔ + oldSmoothΔ, newLevelΔ);
                    // Vanilla TerrainComp.LevelTerrain clamps m_levelDelta to ±8 - match it, or the
                    // owner's saved terrain and our local preview disagree at extreme heights.
                    var limitedNewLevelΔ = Mathf.Clamp(roundedNewLevelΔ, -8.0f, 8.0f);
                    levelΔ[tileIndex] = limitedNewLevelΔ;
                    smoothΔ[tileIndex] = 0f;
                    modifiedHeight[tileIndex] = true;
                    Logger.Debug(() => $"tilePos: ({x}, {y}), tileH: {tileH}, Δh: {Δh}, oldLevelΔ: {oldLevelΔ}, oldSmoothΔ: {oldSmoothΔ}, newLevelΔ: {newLevelΔ}, roundedNewLevelΔ: {roundedNewLevelΔ}, limitedNewLevelΔ: {limitedNewLevelΔ}");
                }
            }

            Logger.Debug(() => "[SUCCESS] Level Terrain Modification");
        }

        public static void SmoothenTerrain(Vector3 worldPos, Heightmap hMap, TerrainComp compiler, ref float[] smoothΔ, ref bool[] modifiedHeight)
        {
            Logger.Debug(() => "[INIT] Smooth Terrain Modification");

            hMap.WorldToVertex(worldPos, out var xPos, out var yPos);
            var referenceH = worldPos.y - compiler.transform.position.y;
            Logger.Debug(() => $"worldPos: {worldPos}, xPos: {xPos}, yPos: {yPos}, referenceH: {referenceH}");

            FindExtremums(xPos, out var xMin, out var xMax);
            FindExtremums(yPos, out var yMin, out var yMax);
            for (var x = xMin; x <= xMax; x++)
            {
                for (var y = yMin; y <= yMax; y++)
                {
                    var tileIndex = y * PTilesPerChunk + x;
                    var tileH = hMap.GetHeight(x, y);
                    var Δh = referenceH - tileH;
                    var oldΔh = smoothΔ[tileIndex];
                    var newΔh = oldΔh + Δh;
                    var roundedNewΔh = RoundToTwoDecimals(tileH, oldΔh, newΔh);
                    var limΔh = Mathf.Clamp(roundedNewΔh, -1.0f, 1.0f);
                    smoothΔ[tileIndex] = limΔh;
                    modifiedHeight[tileIndex] = true;
                    Logger.Debug(() => $"tilePos: ({x}, {y}), tileH: {tileH}, Δh: {Δh}, oldΔh: {oldΔh}, newΔh: {newΔh}, roundedNewΔh: {roundedNewΔh}, limΔh: {limΔh}");
                }
            }

            Logger.Debug(() => "[SUCCESS] Smooth Terrain Modification");
        }

        public static void RaiseTerrain(Vector3 worldPos, Heightmap hMap, TerrainComp compiler, float power, ref float[] levelΔ, ref float[] smoothΔ, ref bool[] modifiedHeight)
        {
            Logger.Debug(() => "[INIT] Raise Terrain Modification");

            hMap.WorldToVertex(worldPos, out var xPos, out var yPos);
            var referenceH = worldPos.y - compiler.transform.position.y + power;
            Logger.Debug(() => $"worldPos: {worldPos}, xPos: {xPos}, yPos: {yPos}, power: {power}, referenceH: {referenceH}");

            FindExtremums(xPos, out var xMin, out var xMax);
            FindExtremums(yPos, out var yMin, out var yMax);
            for (var x = xMin; x <= xMax; x++)
            {
                for (var y = yMin; y <= yMax; y++)
                {
                    var tileIndex = y * PTilesPerChunk + x;
                    var tileH = hMap.GetHeight(x, y);
                    var Δh = referenceH - tileH;
                    if (Δh >= 0)
                    {
                        var oldLevelΔ = levelΔ[tileIndex];
                        var oldSmoothΔ = smoothΔ[tileIndex];
                        var newLevelΔ = oldLevelΔ + oldSmoothΔ + Δh;
                        var newSmoothΔ = 0f;
                        var roundedNewLevelΔ = RoundToTwoDecimals(tileH, oldLevelΔ + oldSmoothΔ, newLevelΔ + newSmoothΔ);
                        var limitedNewLevelΔ = Mathf.Clamp(roundedNewLevelΔ, -16.0f, 16.0f);
                        levelΔ[tileIndex] = limitedNewLevelΔ;
                        smoothΔ[tileIndex] = newSmoothΔ;
                        modifiedHeight[tileIndex] = true;
                        Logger.Debug(() => $"tilePos: ({x}, {y}), tileH: {tileH}, Δh: {Δh}, oldLevelΔ: {oldLevelΔ}, oldSmoothΔ: {oldSmoothΔ}, newLevelΔ: {newLevelΔ}, newSmoothΔ: {newSmoothΔ}, roundedNewLevelΔ: {roundedNewLevelΔ}, limitedNewLevelΔ: {limitedNewLevelΔ}");
                    }
                    else
                    {
                        Logger.Debug(() => "Declined to process tile: Δh < 0!");
                        Logger.Debug(() => $"tilePos: ({x}, {y}), tileH: {tileH}, Δh: {Δh}");
                    }
                }
            }

            Logger.Debug(() => "[SUCCESS] Raise Terrain Modification");
        }

        public static void RecolorTerrain(Vector3 worldPos, PaintType paintType, Heightmap hMap, ref Color[] paintMask, ref bool[] modifiedPaint, bool removeColor = false)
        {
            Logger.Info(() => "[INIT] Color Terrain Modification");

            var tileColor = ResolveColor(paintType);
            PositionRelativeTo(hMap.transform.position, worldPos, out var xPos, out var yPos);
            Logger.Info(() => $"worldPos: {worldPos}, chunkPos: {hMap.transform.position}, relPos: ({xPos}, {yPos})");

            FindExtremums(xPos, out var xMin, out var xMax);
            FindExtremums(yPos, out var yMin, out var yMax);
            for (var x = xMin; x <= xMax; x++)
            {
                for (var y = yMin; y <= yMax; y++)
                {
                    ApplyColor(x, y, tileColor, ref paintMask, ref modifiedPaint, removeColor);
                }
            }

            Logger.Info(() => "[SUCCESS] Color Terrain Modification");
        }

        private static void RemoveTerrainModifications(Vector3 worldPos, Heightmap hMap, ref float[] levelΔ, ref float[] smoothΔ, ref bool[] modifiedHeight)
        {
            Logger.Debug(() => "[INIT] Remove Terrain Modifications");

            hMap.WorldToVertex(worldPos, out var xPos, out var yPos);
            Logger.Debug(() => $"worldPos: {worldPos}, vertexPos: ({xPos}, {yPos})");

            FindExtremums(xPos, out var xMin, out var xMax);
            FindExtremums(yPos, out var yMin, out var yMax);
            for (var x = xMin; x <= xMax; x++)
            {
                for (var y = yMin; y <= yMax; y++)
                {
                    var tileIndex = y * PTilesPerChunk + x;
                    levelΔ[tileIndex] = 0;
                    smoothΔ[tileIndex] = 0;
                    modifiedHeight[tileIndex] = false;
                    Logger.Debug(() => $"tilePos: ({x}, {y}), tileIndex: {tileIndex}");
                }
            }
            Logger.Debug(() => "[SUCCESS] Remove Terrain Modifications");
        }

        private static void FindExtremums(int val, out int minVal, out int maxVal)
        {
            minVal = Mathf.Max(0, val - AoESize);
            maxVal = Mathf.Min(val + AoESize, HTilesPerChunk);
        }

        private static float RoundToTwoDecimals(float oldH, float oldΔh, float newΔh)
        {
            var newH = oldH - oldΔh + newΔh;
            var roundedNewH = Mathf.Round(newH * 100) / 100;
            var roundedNewΔh = roundedNewH - oldH + oldΔh;
            Logger.Debug(() => $"oldH: {oldH}, oldΔH: {oldΔh}, newΔH: {newΔh}, newH: {newH}, roundedNewH: {roundedNewH}, roundedNewΔh: {roundedNewΔh}");

            return roundedNewΔh;
        }

        private static void PositionRelativeTo(Vector3 chunkMid, Vector3 worldPos, out int x, out int y)
        {
            var chunkMin = chunkMid - new Vector3(HalfPTilesPerChunk, 0.0f, HalfPTilesPerChunk);
            var relPos = worldPos - chunkMin;
            x = Mathf.FloorToInt(relPos.x / PTileSize);
            y = Mathf.FloorToInt(relPos.z / PTileSize);
        }

        private static Color ResolveColor(PaintType paintType)
        {
            switch (paintType)
            {
                case PaintType.Dirt:
                    return Color.red;
                case PaintType.Paved:
                    return Color.blue;
                case PaintType.Cultivate:
                    return Color.green;
                default:
                    return Color.black;
            }
        }

        private static void ApplyColor(int x, int y, Color tileColor, ref Color[] paintMask, ref bool[] modifiedPaint, bool removeColor = false)
        {
            var tileIndex = y * PTilesPerChunk + x;
            paintMask[tileIndex] = tileColor;
            modifiedPaint[tileIndex] = !removeColor;
            Logger.Info(() => $"tilePos: ({x}, {y}), tileIndex: {tileIndex}, tileColor: {tileColor}");
        }
    }

    [HarmonyPatch]
    public static class PreciseLevelTerrainModification
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp))]
        [HarmonyPatch(nameof(TerrainComp.LevelTerrain))]
        private static bool Prefix(Vector3 worldPos, float radius, bool square, TerrainComp __instance, Heightmap ___m_hmap, ref float[] ___m_levelDelta, ref float[] ___m_smoothDelta, ref bool[] ___m_modifiedHeight)
        {
            if (ClientSideGridModeOverride.IsGridModeEnabled(radius))
            {
                PreciseTerrainModifier.LevelTerrain(worldPos, ___m_hmap, __instance, ref ___m_levelDelta, ref ___m_smoothDelta, ref ___m_modifiedHeight);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public static class PreciseSmoothTerrainModification
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp))]
        [HarmonyPatch(nameof(TerrainComp.SmoothTerrain))]
        private static bool Prefix(Vector3 worldPos, float radius, bool square, float power, TerrainComp __instance, Heightmap ___m_hmap, ref float[] ___m_smoothDelta, ref bool[] ___m_modifiedHeight)
        {
            if (ClientSideGridModeOverride.IsGridModeEnabled(radius))
            {
                PreciseTerrainModifier.SmoothenTerrain(worldPos, ___m_hmap, __instance, ref ___m_smoothDelta, ref ___m_modifiedHeight);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public static class PreciseRaiseTerrainModification
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp))]
        [HarmonyPatch(nameof(TerrainComp.RaiseTerrain))]
        private static bool Prefix(Vector3 worldPos, float radius, float delta, bool square, float power, TerrainComp __instance, Heightmap ___m_hmap, ref float[] ___m_levelDelta, ref float[] ___m_smoothDelta, ref bool[] ___m_modifiedHeight)
        {
            if (ClientSideGridModeOverride.IsGridModeEnabled(radius))
            {
                PreciseTerrainModifier.RaiseTerrain(worldPos, ___m_hmap, __instance, delta, ref ___m_levelDelta, ref ___m_smoothDelta, ref ___m_modifiedHeight);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public static class PreciseColorTerrainModification
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp))]
        [HarmonyPatch(nameof(TerrainComp.PaintCleared))]
        private static bool Prefix(Vector3 worldPos, TerrainOp.Settings settings, Heightmap ___m_hmap, ref Color[] ___m_paintMask, ref bool[] ___m_modifiedPaint)
        {
            if (ClientSideGridModeOverride.IsGridModeEnabled(settings.m_paintRadius))
            {
                PreciseTerrainModifier.RecolorTerrain(worldPos, settings.m_paintType, ___m_hmap, ref ___m_paintMask, ref ___m_modifiedPaint);
                return false;
            }

            return true;
        }
    }

    // DIRTY HACK: bend the flow to our will with a hijacked "unused" variable. Thus is the life of the modder ;)
    //
    // Grid mode is a client-side decision, but the terrain op is applied by whoever owns the zone's
    // TerrainComp ZDO. Up to Valheim 0.2x, TerrainOp.Settings.Serialize wrote every settings field,
    // so simply mutating the placed TerrainOp's own m_settings here was enough - the mutation rode
    // along to the owner. Valheim 1.0 made Serialize write nothing but the prefab's name hash, and
    // Deserialize resolve that hash back to the *shared ObjectDB prefab's* Settings object, so a
    // per-instance mutation is discarded and every grid-mode op silently fell back to vanilla's
    // radial one (the overlay is drawn client-side, so it kept showing a square).
    //
    // Instead, append our own payload to the tail of the same ZPackage: RPC_ApplyOperation reads
    // exactly one int for the settings and ignores whatever follows, so vanilla (and any peer
    // without OCDheim) is unaffected. The receiving side rebuilds the sentinels onto a *clone* -
    // never onto the shared prefab Settings, which every player and every later op reads from.
    [HarmonyPatch]
    public static class ClientSideGridModeOverride
    {
        private const int GridPayloadMagic = 0x0CD4E100;

        private const int LevelSentinel = 1 << 0;
        private const int RaiseSentinel = 1 << 1;
        private const int SmoothSentinel = 1 << 2;
        private const int PaintSentinel = 1 << 3;
        private const int RaiseDeltaOverride = 1 << 4;

        private static bool appendGridPayload;
        private static int gridSentinels;
        private static float gridRaiseDelta;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp))]
        [HarmonyPatch(nameof(TerrainComp.ApplyOperation))]
        private static void PrepareGridPayload(TerrainOp modifier)
        {
            appendGridPayload = KeyBinder.gridModeEnabled && ModConfig.EnableTerrainModification.Value;
            if (!appendGridPayload) { return; }

            var settings = modifier.m_settings;
            gridSentinels = 0;
            gridRaiseDelta = 0f;

            if (settings.m_level)
            {
                gridSentinels |= LevelSentinel;
            }
            if (settings.m_smooth)
            {
                gridSentinels |= SmoothSentinel;
            }
            if (settings.m_paintCleared)
            {
                gridSentinels |= PaintSentinel;
            }
            if (settings.m_raise && settings.m_raiseDelta >= 0)
            {
                gridSentinels |= RaiseSentinel | RaiseDeltaOverride;
                gridRaiseDelta = RaiseGroundSpinner.value;
            }
            // Lower ground has no precise implementation (PreciseTerrainModifier.RaiseTerrain skips
            // every tile with Δh < 0), so it keeps running vanilla - only the spinner delta is sent.
            if (settings.m_raise && settings.m_raiseDelta < 0)
            {
                gridSentinels |= RaiseDeltaOverride;
                gridRaiseDelta = LowerGroundSpinner.value;
            }
        }

        // Clear the flag however ApplyOperation ends, so an aborted op can't leak grid mode into
        // the next TerrainOp.Settings.Serialize call.
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(TerrainComp))]
        [HarmonyPatch(nameof(TerrainComp.ApplyOperation))]
        private static void ClearGridPayload()
        {
            appendGridPayload = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainOp.Settings))]
        [HarmonyPatch(nameof(TerrainOp.Settings.Serialize))]
        private static void AppendGridPayload(ZPackage pkg)
        {
            if (!appendGridPayload) { return; }

            pkg.Write(GridPayloadMagic);
            pkg.Write(gridSentinels);
            pkg.Write(gridRaiseDelta);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainOp.Settings))]
        [HarmonyPatch(nameof(TerrainOp.Settings.Deserialize))]
        private static void ReadGridPayload(ZPackage pkg, ref TerrainOp.Settings __result)
        {
            if (__result == null) { return; }

            const int payloadSize = sizeof(int) + sizeof(int) + sizeof(float);
            var payloadStart = pkg.GetPos();
            if (payloadStart + payloadSize > pkg.Size()) { return; }
            if (pkg.ReadInt() != GridPayloadMagic)
            {
                pkg.SetPos(payloadStart);
                return;
            }

            var sentinels = pkg.ReadInt();
            var raiseDelta = pkg.ReadSingle();
            var settings = Clone(__result);

            if ((sentinels & LevelSentinel) != 0)
            {
                settings.m_levelRadius = float.NegativeInfinity;
            }
            if ((sentinels & RaiseSentinel) != 0)
            {
                settings.m_raiseRadius = float.NegativeInfinity;
            }
            if ((sentinels & SmoothSentinel) != 0)
            {
                settings.m_smoothRadius = float.NegativeInfinity;
            }
            if ((sentinels & PaintSentinel) != 0)
            {
                settings.m_paintRadius = float.NegativeInfinity;
            }
            if ((sentinels & RaiseDeltaOverride) != 0)
            {
                settings.m_raiseDelta = raiseDelta;
            }

            Logger.Debug(() => $"[GRID PAYLOAD] sentinels: {sentinels}, raiseDelta: {raiseDelta}");
            __result = settings;
        }

        // Field-by-field rather than a hand written copy constructor: Settings grew six new fields
        // in 1.0 alone, and a forgotten one would silently apply the wrong terrain op.
        private static TerrainOp.Settings Clone(TerrainOp.Settings source)
        {
            var clone = new TerrainOp.Settings();
            foreach (var field in typeof(TerrainOp.Settings).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                field.SetValue(clone, field.GetValue(source));
            }

            return clone;
        }

        // DIRTY HACK: This is surely how I will be remembered ;)
        public static bool IsGridModeEnabled(float radius)
        {
            return float.IsNegativeInfinity(radius);
        }
    }
}
