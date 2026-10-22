using System;
using System.Collections.Generic;
using System.IO;
using GuWorld.Engine;
using GuWorld.World;
using UnityEngine;

namespace GuWorld.State
{
    /// <summary>Where the hero stands and which way they face.</summary>
    [Serializable]
    public struct Pose
    {
        public float x, y, z, yaw;
    }

    /// <summary>
    /// The saved game, format v3 (v2 as in client/state/save.ts): the format's
    /// version, the location the hero is in, the game minutes since the world
    /// began and where the hero stands and faces; the hero's look, their
    /// cultivation (aperture, Gu, bag, action bar), health and tasks; and when
    /// each moon orchid bed is in flower again. Formats 1 and 2 still load.
    ///
    /// The position is kept in the web's numbers, so a save means the same
    /// place in both games: Unity's world is the web's mirrored (X negated),
    /// so X and the yaw change sign on the way in and out (ToSaved / FromSaved).
    /// </summary>
    [Serializable]
    public class Save
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public string location;
        public double world_minutes;
        public Pose player;
        /// <summary>How the hero looks (gender, hair, beard); empty until the hero is made.</summary>
        public GuWorld.Body.Look look = new GuWorld.Body.Look();
        /// <summary>The hero as a Gu Master (aperture, Gu, slots, stones); unset in older saves.</summary>
        public GuWorld.Cultivation.CultivationSave cultivation = new GuWorld.Cultivation.CultivationSave();
        /// <summary>The hero's health; below 0 in older saves (full).</summary>
        public float health = -1;
        public GuWorld.Systems.QuestSave quests = new GuWorld.Systems.QuestSave();
        /// <summary>The game minute each moon orchid bed is in flower again, in the beds' order.</summary>
        public double[] beds = new double[0];
        /// <summary>The rest of the world the hero has changed: the wild Gu caught, the shops' stock, the wolf tides, the tamed beasts.</summary>
        public WorldSave world = new WorldSave();
        /// <summary>The body's training (strength, agility, endurance, vitality, spirit) and its stamina.</summary>
        public GuWorld.Systems.PhysiqueSave physique = new GuWorld.Systems.PhysiqueSave();
    }

    /// <summary>What of the world is kept besides the hero (a save from before it: all as new).</summary>
    [Serializable]
    public class WorldSave
    {
        /// <summary>The game minute each wild Gu's spot has a Gu again, in the spots' order.</summary>
        public double[] wild = new double[0];
        /// <summary>The day whose night's wolf tide has come already, and the day it was foretold (-1: none).</summary>
        public int tide_day = -1;
        public int tide_told = -1;
        /// <summary>The day the shops' stock was counted on, and what was sold of it ("shop/item" and lots).</summary>
        public long trade_day = long.MinValue;
        public string[] sold_keys = new string[0];
        public int[] sold_counts = new int[0];
        /// <summary>The beasts the hero has tamed.</summary>
        public PetSave[] pets = new PetSave[0];
    }

    /// <summary>A tamed beast as saved: its kind, level and health.</summary>
    [Serializable]
    public class PetSave
    {
        public string kind;
        public int level;
        public float health;
    }

    /// <summary>A game got back from a save — repaired where it had to be.</summary>
    public class RestoredGame
    {
        public string location;
        public WorldClock clock;
        public Pose player;
        /// <summary>The hero's look; not chosen yet in a new game (the hero is made first).</summary>
        public GuWorld.Body.Look look = new GuWorld.Body.Look();
        /// <summary>The hero as a Gu Master; null for a new game (the test Gu Master, Cultivator.Load).</summary>
        public GuWorld.Cultivation.CultivationSave cultivation;
        /// <summary>The hero's health (below 0: full), their tasks (null: none yet), the beds' regrowth.</summary>
        public float health = -1;
        public GuWorld.Systems.QuestSave quests;
        public double[] beds;
        /// <summary>The world as the hero left it (null: as new); the body's training (null: untrained).</summary>
        public WorldSave world;
        public GuWorld.Systems.PhysiqueSave physique;
        /// <summary>No save at all: a new game.</summary>
        public bool fresh;
        /// <summary>Why parts of the save were replaced; empty when it was sound.</summary>
        public readonly List<string> problems = new List<string>();
    }

    /// <summary>
    /// Saving and loading. The browser game kept its save on the server; here
    /// it is a JSON file in the player's data folder, written whole to a
    /// temporary file first and then moved over the old one, so a crash in the
    /// middle never leaves half a save.
    ///
    /// Restoring never trusts what comes back: a broken time becomes the first
    /// morning, a broken or out-of-bounds position the location's spawn, an
    /// unknown location or format the starting location — and each reason is
    /// kept in `problems`.
    /// </summary>
    public static class SaveGame
    {
        static readonly int[] Readable = { 1, 2, 3 };

        public static string FilePath => Path.Combine(Application.persistentDataPath, "gu_world_save.json");

        public static RestoredGame NewGame(LocationSettings start) => new RestoredGame
        {
            location = start.id,
            clock = WorldClock.NewGame(),
            player = new Pose { x = start.spawn.x, y = start.spawn.y, z = start.spawn.z, yaw = start.spawnYaw },
            fresh = true,
        };

        /// <summary>The saved game, or a new one when there is none (or it cannot be read at all).</summary>
        public static RestoredGame Load(LocationSettings start)
        {
            if (!File.Exists(FilePath))
            {
                return NewGame(start);
            }

            Save saved;

            try
            {
                saved = JsonUtility.FromJson<Save>(File.ReadAllText(FilePath));
            }
            catch (Exception error)
            {
                var broken = NewGame(start);
                broken.fresh = false;
                broken.problems.Add($"the save could not be read ({error.Message})");

                return broken;
            }

            return Restore(saved, start);
        }

        public static RestoredGame Restore(Save saved, LocationSettings start)
        {
            if (saved == null)
            {
                return NewGame(start);
            }

            var game = NewGame(start);
            game.fresh = false;

            if (Array.IndexOf(Readable, saved.version) < 0)
            {
                game.problems.Add($"unknown save format {saved.version}: a new game");

                return game;
            }

            // The hero stays who they are, wherever the save says they were.
            if (saved.look != null && saved.look.IsChosen)
            {
                game.look = saved.look;
            }

            game.cultivation = saved.cultivation;
            game.health = float.IsFinite(saved.health) ? saved.health : -1;
            game.quests = saved.quests;

            // One location at a time so far: anything else goes back to the start.
            if (saved.location != start.id)
            {
                game.problems.Add($"unknown location \"{saved.location}\": back to the start");

                return game;
            }

            game.clock = WorldClock.FromSaved(saved.world_minutes, out var repaired);
            game.beds = saved.beds;
            game.world = saved.world;
            game.physique = saved.physique;

            if (repaired)
            {
                game.problems.Add("the time was broken: the first morning");
            }

            var p = FromSaved(saved.player);
            var finite = float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z) && float.IsFinite(p.yaw);

            if (finite && start.bounds.Contains(new Vector3(p.x, p.y, p.z)))
            {
                game.player = new Pose { x = p.x, y = p.y, z = p.z, yaw = Mathf.Atan2(Mathf.Sin(p.yaw), Mathf.Cos(p.yaw)) };
            }
            else
            {
                game.problems.Add("the hero's position was broken or outside the location: the spawn");
            }

            return game;
        }

        /// <summary>A pose in Unity's world, kept in the web's numbers.</summary>
        public static Pose ToSaved(Vector3 position, float yaw) =>
            new Pose { x = -position.x, y = position.y, z = position.z, yaw = -yaw };

        /// <summary>A saved pose (the web's numbers) in Unity's world.</summary>
        public static Pose FromSaved(Pose saved) =>
            new Pose { x = -saved.x, y = saved.y, z = saved.z, yaw = -saved.yaw };

        /// <summary>Writes the save; false (and logged) when it could not be written.</summary>
        public static bool Write(Save save)
        {
            try
            {
                var temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(save, true));

                if (File.Exists(FilePath))
                {
                    File.Replace(temporary, FilePath, null);
                }
                else
                {
                    File.Move(temporary, FilePath);
                }

                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"GU World: the game could not be saved: {error}");

                return false;
            }
        }

        /// <summary>For starting over: the save is removed.</summary>
        public static void Delete()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
