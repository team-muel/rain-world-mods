using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace StwamRainWorldMod
{
    [BepInPlugin("stwam.venomous.safefoodcache", "Venomous Safe Food Cache", "1.0.0")]
    public sealed class VenomousSafeFoodCachePlugin : BaseUnityPlugin
    {
        private const string VenomousSlugcat = "Venomous";
        private const int MaxStoredCycles = 3;
        private static readonly List<StoredCorpse> trackedCorpses = new List<StoredCorpse>();
        private static readonly HashSet<AbstractCreature> trackedCreatures = new HashSet<AbstractCreature>();

        public void OnEnable()
        {
            On.Creature.InjectPoison += Creature_InjectPoison;
            On.SaveState.SessionEnded += SaveState_SessionEnded;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
        }

        private static void Creature_InjectPoison(On.Creature.orig_InjectPoison orig, Creature self, float amount, Color poisonColor)
        {
            orig(self, amount, poisonColor);

            try
            {
                if (amount <= 0f || self == null || self.abstractCreature == null || self.room == null || self.room.game == null)
                {
                    return;
                }

                if (!IsVenomousStory(self.room.game) || !LooksLikeVenomousPoison(poisonColor))
                {
                    return;
                }

                trackedCreatures.Add(self.abstractCreature);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void SaveState_SessionEnded(On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game, bool survived, bool newMalnourished)
        {
            orig(self, game, survived, newMalnourished);

            try
            {
                if (!survived || self == null || game == null || !IsVenomousSave(self))
                {
                    return;
                }

                SaveCorpses(self, game);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig(self, manager);

            try
            {
                LoadCorpses(self);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void SaveCorpses(SaveState save, RainWorldGame game)
        {
            List<StoredCorpse> next = new List<StoredCorpse>();
            string den = save.denPosition;

            foreach (StoredCorpse oldCorpse in trackedCorpses)
            {
                if (oldCorpse == null || oldCorpse.Age >= MaxStoredCycles || oldCorpse.RoomName == den)
                {
                    continue;
                }

                oldCorpse.Age++;
                next.Add(oldCorpse);
            }

            foreach (AbstractCreature crit in trackedCreatures)
            {
                StoredCorpse stored = TryMakeStoredCorpse(crit, game, den);
                if (stored != null && !ContainsSameCorpse(next, stored))
                {
                    next.Add(stored);
                }
            }

            WriteCorpses(save, next);
            trackedCorpses.Clear();
            trackedCorpses.AddRange(next);
            trackedCreatures.Clear();
        }

        private static StoredCorpse TryMakeStoredCorpse(AbstractCreature crit, RainWorldGame game, string den)
        {
            if (crit == null || crit.slatedForDeletion || crit.state == null || !crit.state.dead || crit.creatureTemplate == null)
            {
                return null;
            }

            if (!IsAllowedCreature(crit.creatureTemplate))
            {
                return null;
            }

            AbstractRoom room = game.world.GetAbstractRoom(crit.pos.room);
            if (room == null || room.name == den)
            {
                return null;
            }

            return new StoredCorpse
            {
                Age = 1,
                RoomName = room.name,
                CreatureType = crit.creatureTemplate.type.ToString(),
                X = Mathf.Max(0, crit.pos.x),
                Y = Mathf.Max(0, crit.pos.y)
            };
        }

        private static void LoadCorpses(RainWorldGame game)
        {
            trackedCorpses.Clear();
            trackedCreatures.Clear();

            if (game == null || !IsVenomousStory(game))
            {
                return;
            }

            SaveState save = game.GetStorySession.saveState;
            foreach (StoredCorpse corpse in ReadCorpses(save))
            {
                if (corpse == null || corpse.Age > MaxStoredCycles)
                {
                    continue;
                }

                AbstractRoom room = game.world.GetAbstractRoom(corpse.RoomName);
                if (room == null)
                {
                    continue;
                }

                CreatureTemplate template = StaticWorld.GetCreatureTemplate(corpse.CreatureType);
                if (template == null || !IsAllowedCreature(template))
                {
                    continue;
                }

                WorldCoordinate pos = new WorldCoordinate(room.index, Mathf.Max(0, corpse.X), Mathf.Max(0, corpse.Y), -1);
                AbstractCreature crit = new AbstractCreature(game.world, template, null, pos, game.GetNewID());
                crit.state.Die();
                crit.Die();
                crit.saveCreature = false;
                crit.ignoreCycle = true;
                room.AddEntity(crit);

                trackedCorpses.Add(corpse);
            }
        }

        private static bool ContainsSameCorpse(List<StoredCorpse> list, StoredCorpse corpse)
        {
            for (int i = 0; i < list.Count; i++)
            {
                StoredCorpse item = list[i];
                if (item.RoomName == corpse.RoomName && item.CreatureType == corpse.CreatureType && item.X == corpse.X && item.Y == corpse.Y)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllowedCreature(CreatureTemplate template)
        {
            string type = template.type.ToString();
            return type.IndexOf("Lizard", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeVenomousPoison(Color color)
        {
            return color.g >= color.r && color.g > color.b && color.g > 0.25f;
        }

        private static bool IsVenomousStory(RainWorldGame game)
        {
            return game.GetStorySession != null && game.GetStorySession.saveState != null && IsVenomousSave(game.GetStorySession.saveState);
        }

        private static bool IsVenomousSave(SaveState save)
        {
            return save.saveStateNumber != null && save.saveStateNumber.ToString() == VenomousSlugcat;
        }

        private static string SavePath(SaveState save)
        {
            return Path.Combine(Application.persistentDataPath, "VenomousSafeCorpses_" + save.saveStateNumber + ".txt");
        }

        private static List<StoredCorpse> ReadCorpses(SaveState save)
        {
            List<StoredCorpse> result = new List<StoredCorpse>();
            string path = SavePath(save);
            if (!File.Exists(path))
            {
                return result;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                StoredCorpse corpse = StoredCorpse.FromLine(lines[i]);
                if (corpse != null)
                {
                    result.Add(corpse);
                }
            }

            return result;
        }

        private static void WriteCorpses(SaveState save, List<StoredCorpse> corpses)
        {
            string path = SavePath(save);
            List<string> lines = new List<string>();
            for (int i = 0; i < corpses.Count; i++)
            {
                lines.Add(corpses[i].ToLine());
            }

            File.WriteAllLines(path, lines.ToArray());
        }

        private sealed class StoredCorpse
        {
            public int Age;
            public string RoomName;
            public string CreatureType;
            public int X;
            public int Y;

            public string ToLine()
            {
                return string.Join("|", new[]
                {
                    Age.ToString(CultureInfo.InvariantCulture),
                    Escape(RoomName),
                    Escape(CreatureType),
                    X.ToString(CultureInfo.InvariantCulture),
                    Y.ToString(CultureInfo.InvariantCulture)
                });
            }

            public static StoredCorpse FromLine(string line)
            {
                if (string.IsNullOrEmpty(line))
                {
                    return null;
                }

                string[] parts = line.Split('|');
                int age;
                int x;
                int y;
                if (parts.Length < 5 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out age) ||
                    !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                    !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                {
                    return null;
                }

                return new StoredCorpse
                {
                    Age = age,
                    RoomName = Unescape(parts[1]),
                    CreatureType = Unescape(parts[2]),
                    X = x,
                    Y = y
                };
            }

            private static string Escape(string text)
            {
                return (text ?? string.Empty).Replace("%", "%25").Replace("|", "%7C");
            }

            private static string Unescape(string text)
            {
                return (text ?? string.Empty).Replace("%7C", "|").Replace("%25", "%");
            }
        }
    }
}
