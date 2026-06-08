using System;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;

namespace StwamRainWorldMod
{
    [BepInPlugin("stwam.starter.venomous.abilitypatch", "The Venomous Ability Patch", "0.1.0")]
    [BepInDependency("stwam.starter", BepInDependency.DependencyFlags.HardDependency)]
    public class VenomousAbilityPatchPlugin : BaseUnityPlugin
    {
        private const string VenomousSlugcat = "Venomous";
        private const int EmergencyFoodCost = 2;
        private const int RequiredTaps = 3;
        private const int TapWindowTicks = 45;
        private const int EffectTicks = 80;
        private const int PredatorStunTicks = 120;
        private const float EmergencyPoisonDamage = 2f;
        private const float CamouflageVisualMultiplier = 0.56f;
        private const float VenomousSpearPoisonHue = 0.34f;
        private const float SpentPoisonThreshold = 0.005f;
        private static readonly Color VenomColor = new Color(0.38f, 0.95f, 0.08f);
        private static readonly Color SpentVenomSpearColor = new Color(0.02f, 0.025f, 0.02f);
        private static readonly Color VenomousBodyColor = new Color(0.43f, 0.45f, 0.42f);
        private static readonly Color VenomousEyeColor = new Color(0.02f, 0.025f, 0.02f);
        private static readonly Color VenomousSpotColor = new Color(0.48f, 0.95f, 0.08f);

        private static readonly Dictionary<Player, EscapeTapState> escapeStates =
            new Dictionary<Player, EscapeTapState>();
        private static readonly Dictionary<Player, int> camouflageTimers =
            new Dictionary<Player, int>();
        private static readonly Dictionary<Player, int> adrenalineTimers =
            new Dictionary<Player, int>();
        private static int suppressBaseVisualReduction;
        private static int spearmasterGraphicsDepth;

        public void OnEnable()
        {
            On.SlugcatStats.ctor += SlugcatStats_ctor;
            On.Player.Update += Player_Update;
            On.ArtificialIntelligence.VisualScore += ArtificialIntelligence_VisualScore;
            On.LizardAI.VisualScore += LizardAI_VisualScore;
            On.PlayerGraphics.ctor += PlayerGraphics_ctor;
            On.PlayerGraphics.Update += PlayerGraphics_Update;
            On.PlayerGraphics.MSCUpdate += PlayerGraphics_MSCUpdate;
            On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;
            On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
            On.PlayerGraphics.SlugcatColor += PlayerGraphics_SlugcatColor;
            On.PlayerGraphics.DefaultSlugcatColor += PlayerGraphics_DefaultSlugcatColor;
            On.PlayerGraphics.DefaultBodyPartColorHex += PlayerGraphics_DefaultBodyPartColorHex;
            On.PlayerGraphics.ColoredBodyPartList += PlayerGraphics_ColoredBodyPartList;
            On.PlayerGraphics.JollyBodyColorMenu += PlayerGraphics_JollyBodyColorMenu;
            On.PlayerGraphics.JollyFaceColorMenu += PlayerGraphics_JollyFaceColorMenu;
            On.PlayerGraphics.JollyUniqueColorMenu += PlayerGraphics_JollyUniqueColorMenu;
            On.PlayerGraphics.JollyColor += PlayerGraphics_JollyColor;
            On.Spear.DrawSprites += Spear_DrawSprites;
            Debug.Log("[The Venomous] Ability patch enabled.");
        }

        public void OnDisable()
        {
            On.SlugcatStats.ctor -= SlugcatStats_ctor;
            On.Player.Update -= Player_Update;
            On.ArtificialIntelligence.VisualScore -= ArtificialIntelligence_VisualScore;
            On.LizardAI.VisualScore -= LizardAI_VisualScore;
            On.PlayerGraphics.ctor -= PlayerGraphics_ctor;
            On.PlayerGraphics.Update -= PlayerGraphics_Update;
            On.PlayerGraphics.MSCUpdate -= PlayerGraphics_MSCUpdate;
            On.PlayerGraphics.InitiateSprites -= PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.DrawSprites -= PlayerGraphics_DrawSprites;
            On.PlayerGraphics.ApplyPalette -= PlayerGraphics_ApplyPalette;
            On.PlayerGraphics.AddToContainer -= PlayerGraphics_AddToContainer;
            On.PlayerGraphics.SlugcatColor -= PlayerGraphics_SlugcatColor;
            On.PlayerGraphics.DefaultSlugcatColor -= PlayerGraphics_DefaultSlugcatColor;
            On.PlayerGraphics.DefaultBodyPartColorHex -= PlayerGraphics_DefaultBodyPartColorHex;
            On.PlayerGraphics.ColoredBodyPartList -= PlayerGraphics_ColoredBodyPartList;
            On.PlayerGraphics.JollyBodyColorMenu -= PlayerGraphics_JollyBodyColorMenu;
            On.PlayerGraphics.JollyFaceColorMenu -= PlayerGraphics_JollyFaceColorMenu;
            On.PlayerGraphics.JollyUniqueColorMenu -= PlayerGraphics_JollyUniqueColorMenu;
            On.PlayerGraphics.JollyColor -= PlayerGraphics_JollyColor;
            On.Spear.DrawSprites -= Spear_DrawSprites;
            escapeStates.Clear();
            camouflageTimers.Clear();
            adrenalineTimers.Clear();
        }

        private static void SlugcatStats_ctor(
            On.SlugcatStats.orig_ctor orig,
            SlugcatStats self,
            SlugcatStats.Name slugcat,
            bool malnourished)
        {
            orig(self, slugcat, malnourished);

            if (!IsVenomousSlugcatName(slugcat))
            {
                return;
            }

            CopySurvivorMovementStats(self, malnourished);
        }

        private static void CopySurvivorMovementStats(SlugcatStats target, bool malnourished)
        {
            SlugcatStats survivor = new SlugcatStats(SlugcatStats.Name.White, malnourished);
            target.runspeedFac = survivor.runspeedFac;
            target.bodyWeightFac = survivor.bodyWeightFac;
            target.generalVisibilityBonus = survivor.generalVisibilityBonus;
            target.visualStealthInSneakMode = survivor.visualStealthInSneakMode;
            target.loudnessFac = survivor.loudnessFac;
            target.lungsFac = survivor.lungsFac;
            target.throwingSkill = survivor.throwingSkill;
            target.poleClimbSpeedFac = survivor.poleClimbSpeedFac;
            target.corridorClimbSpeedFac = survivor.corridorClimbSpeedFac;
            target.drownThreshold = survivor.drownThreshold;
            target.swimForceFac = survivor.swimForceFac;
            target.swimBoostCost = survivor.swimBoostCost;
            target.swimBoostForce = survivor.swimBoostForce;
            target.swimBoostCooldown = survivor.swimBoostCooldown;
            target.swimBoostMinAir = survivor.swimBoostMinAir;
        }

        private static void PlayerGraphics_ctor(
            On.PlayerGraphics.orig_ctor orig,
            PlayerGraphics self,
            PhysicalObject ow)
        {
            Player player = ow as Player;
            if (!IsVenomous(player))
            {
                orig(self, ow);
                return;
            }

            RunAsSpearmasterGraphics(player, delegate
            {
                orig(self, ow);
            });
        }

        private static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            if (!IsVenomousGraphics(self))
            {
                orig(self);
                return;
            }

            RunAsSpearmasterGraphics(PlayerFromGraphics(self), delegate
            {
                orig(self);
            });
        }

        private static void PlayerGraphics_MSCUpdate(On.PlayerGraphics.orig_MSCUpdate orig, PlayerGraphics self)
        {
            if (!IsVenomousGraphics(self))
            {
                orig(self);
                return;
            }

            RunAsSpearmasterGraphics(PlayerFromGraphics(self), delegate
            {
                orig(self);
            });
        }

        private static void PlayerGraphics_InitiateSprites(
            On.PlayerGraphics.orig_InitiateSprites orig,
            PlayerGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam)
        {
            if (!IsVenomousGraphics(self))
            {
                orig(self, sLeaser, rCam);
                return;
            }

            RunAsSpearmasterGraphics(PlayerFromGraphics(self), delegate
            {
                orig(self, sLeaser, rCam);
            });
            ApplyVenomousSpriteColors(self, sLeaser);
        }

        private static void PlayerGraphics_DrawSprites(
            On.PlayerGraphics.orig_DrawSprites orig,
            PlayerGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            float timeStacker,
            Vector2 camPos)
        {
            if (!IsVenomousGraphics(self))
            {
                orig(self, sLeaser, rCam, timeStacker, camPos);
                return;
            }

            RunAsSpearmasterGraphics(PlayerFromGraphics(self), delegate
            {
                orig(self, sLeaser, rCam, timeStacker, camPos);
            });
            ApplyVenomousSpriteColors(self, sLeaser);
        }

        private static void PlayerGraphics_ApplyPalette(
            On.PlayerGraphics.orig_ApplyPalette orig,
            PlayerGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            RoomPalette palette)
        {
            if (!IsVenomousGraphics(self))
            {
                orig(self, sLeaser, rCam, palette);
                return;
            }

            RunAsSpearmasterGraphics(PlayerFromGraphics(self), delegate
            {
                orig(self, sLeaser, rCam, palette);
            });
            ApplyVenomousSpriteColors(self, sLeaser);
        }

        private static void PlayerGraphics_AddToContainer(
            On.PlayerGraphics.orig_AddToContainer orig,
            PlayerGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            FContainer newContatiner)
        {
            if (!IsVenomousGraphics(self))
            {
                orig(self, sLeaser, rCam, newContatiner);
                return;
            }

            RunAsSpearmasterGraphics(PlayerFromGraphics(self), delegate
            {
                orig(self, sLeaser, rCam, newContatiner);
            });
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            if (!IsVenomous(self))
            {
                return;
            }

            TickEffectTimers(self);
            ApplyAdrenaline(self);
            UpdateEmergencyEscape(self);
        }

        private static float ArtificialIntelligence_VisualScore(
            On.ArtificialIntelligence.orig_VisualScore orig,
            ArtificialIntelligence self,
            Vector2 lookAtPoint,
            float bonus)
        {
            float score = orig(self, lookAtPoint, bonus);
            if (suppressBaseVisualReduction > 0)
            {
                return score;
            }

            if (HasCamouflagedVenomousNear(self, lookAtPoint))
            {
                return score * CamouflageVisualMultiplier;
            }

            return score;
        }

        private static float LizardAI_VisualScore(
            On.LizardAI.orig_VisualScore orig,
            LizardAI self,
            Vector2 lookAtPoint,
            float bonus)
        {
            float score;
            suppressBaseVisualReduction++;
            try
            {
                score = orig(self, lookAtPoint, bonus);
            }
            finally
            {
                suppressBaseVisualReduction--;
            }

            if (HasCamouflagedVenomousNear(self, lookAtPoint))
            {
                return score * CamouflageVisualMultiplier;
            }

            return score;
        }

        private static void Spear_DrawSprites(
            On.Spear.orig_DrawSprites orig,
            Spear self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            float timeStacker,
            Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (IsSpentVenomSpear(self))
            {
                RecolorAllSprites(sLeaser, SpentVenomSpearColor);
            }
        }

        private static bool IsSpentVenomSpear(Spear spear)
        {
            if (spear == null || spear.abstractSpear == null)
            {
                return false;
            }

            return spear.abstractSpear.poison <= SpentPoisonThreshold &&
                Mathf.Abs(spear.abstractSpear.poisonHue - VenomousSpearPoisonHue) < 0.02f;
        }

        private static void RecolorAllSprites(RoomCamera.SpriteLeaser sLeaser, Color color)
        {
            if (sLeaser == null || sLeaser.sprites == null)
            {
                return;
            }

            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                if (sLeaser.sprites[i] != null)
                {
                    sLeaser.sprites[i].color = color;
                }
            }
        }

        private static Color PlayerGraphics_SlugcatColor(
            On.PlayerGraphics.orig_SlugcatColor orig,
            SlugcatStats.Name i)
        {
            if (UsesVenomousPalette(i))
            {
                return VenomousBodyColor;
            }

            return orig(i);
        }

        private static Color PlayerGraphics_DefaultSlugcatColor(
            On.PlayerGraphics.orig_DefaultSlugcatColor orig,
            SlugcatStats.Name i)
        {
            if (UsesVenomousPalette(i))
            {
                return VenomousBodyColor;
            }

            return orig(i);
        }

        private static List<string> PlayerGraphics_DefaultBodyPartColorHex(
            On.PlayerGraphics.orig_DefaultBodyPartColorHex orig,
            SlugcatStats.Name slugcatID)
        {
            if (UsesVenomousPalette(slugcatID))
            {
                List<string> colors = new List<string>();
                colors.Add("6E736B");
                colors.Add("050605");
                colors.Add("7AF214");
                return colors;
            }

            return orig(slugcatID);
        }

        private static List<string> PlayerGraphics_ColoredBodyPartList(
            On.PlayerGraphics.orig_ColoredBodyPartList orig,
            SlugcatStats.Name slugcatID)
        {
            if (UsesVenomousPalette(slugcatID))
            {
                List<string> parts = new List<string>();
                parts.Add("Body");
                parts.Add("Eyes");
                parts.Add("Spears");
                return parts;
            }

            return orig(slugcatID);
        }

        private static Color PlayerGraphics_JollyBodyColorMenu(
            On.PlayerGraphics.orig_JollyBodyColorMenu orig,
            SlugcatStats.Name slugName,
            SlugcatStats.Name reference)
        {
            if (UsesVenomousPalette(slugName) || UsesVenomousPalette(reference))
            {
                return VenomousBodyColor;
            }

            return orig(slugName, reference);
        }

        private static Color PlayerGraphics_JollyFaceColorMenu(
            On.PlayerGraphics.orig_JollyFaceColorMenu orig,
            SlugcatStats.Name slugName,
            SlugcatStats.Name reference,
            int playerNumber)
        {
            if (UsesVenomousPalette(slugName) || UsesVenomousPalette(reference))
            {
                return VenomousEyeColor;
            }

            return orig(slugName, reference, playerNumber);
        }

        private static Color PlayerGraphics_JollyUniqueColorMenu(
            On.PlayerGraphics.orig_JollyUniqueColorMenu orig,
            SlugcatStats.Name slugName,
            SlugcatStats.Name reference,
            int playerNumber)
        {
            if (UsesVenomousPalette(slugName) || UsesVenomousPalette(reference))
            {
                return VenomousSpotColor;
            }

            return orig(slugName, reference, playerNumber);
        }

        private static Color PlayerGraphics_JollyColor(
            On.PlayerGraphics.orig_JollyColor orig,
            int playerNumber,
            int bodyPartIndex)
        {
            if (spearmasterGraphicsDepth > 0)
            {
                if (bodyPartIndex == 0)
                {
                    return VenomousBodyColor;
                }

                if (bodyPartIndex == 1)
                {
                    return VenomousEyeColor;
                }

                if (bodyPartIndex == 2)
                {
                    return VenomousSpotColor;
                }
            }

            return orig(playerNumber, bodyPartIndex);
        }

        private static void ApplyVenomousSpriteColors(PlayerGraphics graphics, RoomCamera.SpriteLeaser sLeaser)
        {
            if (graphics == null || sLeaser == null || sLeaser.sprites == null)
            {
                return;
            }

            if (graphics.tailSpecks != null)
            {
                RecolorSpriteRange(
                    sLeaser,
                    graphics.tailSpecks.startSprite,
                    graphics.tailSpecks.numberOfSprites,
                    VenomousSpotColor);
            }

            if (graphics.bodyPearl != null)
            {
                RecolorSpriteRange(
                    sLeaser,
                    graphics.bodyPearl.startSprite,
                    graphics.bodyPearl.numberOfSprites,
                    VenomousSpotColor);
            }
        }

        private static void RecolorSpriteRange(
            RoomCamera.SpriteLeaser sLeaser,
            int startSprite,
            int count,
            Color color)
        {
            if (startSprite < 0 || count <= 0)
            {
                return;
            }

            int end = Math.Min(sLeaser.sprites.Length, startSprite + count);
            for (int i = startSprite; i < end; i++)
            {
                if (sLeaser.sprites[i] != null)
                {
                    sLeaser.sprites[i].color = color;
                }
            }
        }

        private static void RunAsSpearmasterGraphics(Player player, Action action)
        {
            if (player == null || action == null)
            {
                if (action != null)
                {
                    action();
                }
                return;
            }

            SlugcatStats.Name originalClass = player.SlugCatClass;
            spearmasterGraphicsDepth++;
            player.SlugCatClass = MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Spear;
            try
            {
                action();
            }
            finally
            {
                player.SlugCatClass = originalClass;
                spearmasterGraphicsDepth--;
            }
        }

        private static bool IsVenomousGraphics(PlayerGraphics graphics)
        {
            return IsVenomous(PlayerFromGraphics(graphics));
        }

        private static Player PlayerFromGraphics(PlayerGraphics graphics)
        {
            if (graphics == null)
            {
                return null;
            }

            return graphics.owner as Player;
        }

        private static bool UsesVenomousPalette(SlugcatStats.Name slugcatName)
        {
            if (IsVenomousSlugcatName(slugcatName))
            {
                return true;
            }

            return spearmasterGraphicsDepth > 0 &&
                slugcatName == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Spear;
        }

        private static void UpdateEmergencyEscape(Player self)
        {
            Creature.Grasp grasp = FindPredatorGrasp(self);
            bool grabbed = grasp != null && grasp.grabber != null;
            bool specDown = IsSpecialPressed(self);

            EscapeTapState state;
            if (!escapeStates.TryGetValue(self, out state))
            {
                state = new EscapeTapState();
                escapeStates[self] = state;
            }

            if (state.tapWindow > 0)
            {
                state.tapWindow--;
            }
            else
            {
                state.tapCount = 0;
            }

            if (!grabbed)
            {
                state.tapCount = 0;
                state.lastSpecial = specDown;
                return;
            }

            if (specDown && !state.lastSpecial)
            {
                state.tapCount++;
                state.tapWindow = TapWindowTicks;
            }
            state.lastSpecial = specDown;

            if (state.tapCount >= RequiredTaps && self.FoodInStomach >= EmergencyFoodCost)
            {
                TriggerEmergencyEscape(self, grasp);
                state.tapCount = 0;
                state.tapWindow = 0;
            }
        }

        private static void TriggerEmergencyEscape(Player self, Creature.Grasp grasp)
        {
            Creature predator = grasp.grabber;
            if (predator == null)
            {
                return;
            }

            self.SubtractFood(EmergencyFoodCost);

            try
            {
                grasp.Release();
            }
            catch (Exception)
            {
                try
                {
                    predator.ReleaseGrasp(grasp.graspUsed);
                }
                catch (Exception)
                {
                }
            }

            self.dangerGrasp = null;
            self.dangerGraspTime = 0;

            predator.Stun(PredatorStunTicks);
            predator.InjectPoison(EmergencyPoisonDamage, VenomColor);

            camouflageTimers[self] = EffectTicks;
            adrenalineTimers[self] = EffectTicks;
            PushAwayFromPredator(self, predator);
        }

        private static Creature.Grasp FindPredatorGrasp(Player self)
        {
            if (IsExternalGrasp(self.dangerGrasp, self))
            {
                return self.dangerGrasp;
            }

            if (self.grabbedBy == null)
            {
                return null;
            }

            for (int i = 0; i < self.grabbedBy.Count; i++)
            {
                Creature.Grasp grasp = self.grabbedBy[i];
                if (IsExternalGrasp(grasp, self))
                {
                    return grasp;
                }
            }

            return null;
        }

        private static bool IsExternalGrasp(Creature.Grasp grasp, Player self)
        {
            return grasp != null &&
                grasp.grabber != null &&
                grasp.grabber != self &&
                grasp.grabbed == self;
        }

        private static void PushAwayFromPredator(Player self, Creature predator)
        {
            if (self.mainBodyChunk == null || predator.mainBodyChunk == null)
            {
                return;
            }

            Vector2 direction = self.mainBodyChunk.pos - predator.mainBodyChunk.pos;
            if (direction.sqrMagnitude < 1f)
            {
                direction = Vector2.up;
            }
            else
            {
                direction.Normalize();
            }

            BodyChunk[] chunks = self.bodyChunks;
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] != null)
                {
                    chunks[i].vel += direction * 7.5f + Vector2.up * 2.5f;
                }
            }
        }

        private static void ApplyAdrenaline(Player self)
        {
            int timer;
            if (!adrenalineTimers.TryGetValue(self, out timer) || timer <= 0)
            {
                return;
            }

            if (self.input == null || self.input.Length == 0)
            {
                return;
            }

            Player.InputPackage input = self.input[0];
            Vector2 direction = new Vector2(input.x, input.y);
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            direction.Normalize();
            BodyChunk[] chunks = self.bodyChunks;
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] != null)
                {
                    chunks[i].vel += direction * 0.08f;
                }
            }
        }

        private static bool HasCamouflagedVenomousNear(ArtificialIntelligence ai, Vector2 lookAtPoint)
        {
            if (ai == null || ai.creature == null || ai.creature.realizedCreature == null)
            {
                return false;
            }

            Room aiRoom = ai.creature.realizedCreature.room;
            if (aiRoom == null)
            {
                return false;
            }

            foreach (KeyValuePair<Player, int> entry in camouflageTimers)
            {
                Player player = entry.Key;
                if (entry.Value <= 0 ||
                    player == null ||
                    player.room != aiRoom ||
                    player.mainBodyChunk == null ||
                    !IsVenomous(player))
                {
                    continue;
                }

                if (Vector2.Distance(lookAtPoint, player.mainBodyChunk.pos) < 420f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void TickEffectTimers(Player self)
        {
            int timer;
            if (camouflageTimers.TryGetValue(self, out timer) && timer > 0)
            {
                camouflageTimers[self] = timer - 1;
            }

            if (adrenalineTimers.TryGetValue(self, out timer) && timer > 0)
            {
                adrenalineTimers[self] = timer - 1;
            }
        }

        private static bool IsSpecialPressed(Player self)
        {
            return self.input != null &&
                self.input.Length > 0 &&
                self.input[0].spec;
        }

        private static bool IsVenomous(Player player)
        {
            return player != null && IsVenomousSlugcatName(player.SlugCatClass);
        }

        private static bool IsVenomousSlugcatName(SlugcatStats.Name slugcatName)
        {
            if (slugcatName == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(slugcatName.value) &&
                slugcatName.value.IndexOf(VenomousSlugcat, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string displayName = SlugcatStats.getSlugcatName(slugcatName);
            return !string.IsNullOrEmpty(displayName) &&
                displayName.IndexOf("Venomous", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private class EscapeTapState
        {
            public int tapCount;
            public int tapWindow;
            public bool lastSpecial;
        }
    }
}
