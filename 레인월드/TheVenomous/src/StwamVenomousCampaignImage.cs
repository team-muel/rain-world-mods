using System;
using System.Collections.Generic;
using BepInEx;
using Menu;
using UnityEngine;

namespace StwamRainWorldMod
{
    [BepInPlugin("stwam.starter.campaignimage", "The Venomous Campaign Image", "0.1.0")]
    public class CampaignImagePlugin : BaseUnityPlugin
    {
        private const string VenomousSlugcat = "Venomous";
        private const string CampaignImageName = "venomous_campaign_main";
        private static readonly Dictionary<SlugcatSelectMenu.SlugcatPageNewGame, MenuIllustration> campaignImages =
            new Dictionary<SlugcatSelectMenu.SlugcatPageNewGame, MenuIllustration>();

        public void OnEnable()
        {
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.ctor += SlugcatPageNewGame_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.GrafUpdate += SlugcatPageNewGame_GrafUpdate;
            Debug.Log("[The Venomous] Campaign image hook enabled.");
        }

        public void OnDisable()
        {
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.ctor -= SlugcatPageNewGame_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.GrafUpdate -= SlugcatPageNewGame_GrafUpdate;
            campaignImages.Clear();
        }

        private static void SlugcatPageNewGame_ctor(
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.orig_ctor orig,
            SlugcatSelectMenu.SlugcatPageNewGame self,
            Menu.Menu menu,
            MenuObject owner,
            int pageIndex,
            SlugcatStats.Name slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);

            if (!IsVenomous(slugcatNumber))
            {
                return;
            }

            try
            {
                ReplaceSlugcatSceneImage(self, menu);

                Debug.Log("[The Venomous] Campaign image applied.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void SlugcatPageNewGame_GrafUpdate(
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.orig_GrafUpdate orig,
            SlugcatSelectMenu.SlugcatPageNewGame self,
            float timeStacker)
        {
            orig(self, timeStacker);

            if (!IsVenomous(self.slugcatNumber))
            {
                return;
            }

            MenuIllustration campaignImage;
            if (!campaignImages.TryGetValue(self, out campaignImage) || campaignImage == null || campaignImage.sprite == null)
            {
                return;
            }

            campaignImage.sprite.scale = 0.42f;
            campaignImage.sprite.isVisible = self.UseAlpha(timeStacker) > 0.01f;
            MoveBehindPageText(self, campaignImage);
        }

        private static bool IsVenomous(SlugcatStats.Name slugcatNumber)
        {
            if (slugcatNumber == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(slugcatNumber.value) &&
                slugcatNumber.value.IndexOf(VenomousSlugcat, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string slugcatName = SlugcatStats.getSlugcatName(slugcatNumber);
            return !string.IsNullOrEmpty(slugcatName) &&
                slugcatName.IndexOf("Venomous", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ReplaceSlugcatSceneImage(SlugcatSelectMenu.SlugcatPageNewGame self, Menu.Menu menu)
        {
            if (self.slugcatImage == null)
            {
                return;
            }

            self.slugcatImage.hidden = false;
            ClearSceneIllustrations(self.slugcatImage);

            var campaignImage = new MenuIllustration(
                menu,
                self.slugcatImage,
                "illustrations",
                CampaignImageName,
                new Vector2(0f, 0f),
                false,
                true);

            campaignImage.sprite.scale = 0.42f;
            campaignImage.alpha = 1f;
            campaignImage.lastAlpha = 1f;
            self.slugcatImage.flatIllustrations.Add(campaignImage);
            campaignImages[self] = campaignImage;
            MoveBehindPageText(self, campaignImage);
        }

        private static void ClearSceneIllustrations(MenuScene scene)
        {
            for (int i = 0; i < scene.depthIllustrations.Count; i++)
            {
                if (scene.depthIllustrations[i] != null &&
                    scene.depthIllustrations[i].sprite != null)
                {
                    scene.depthIllustrations[i].RemoveSprites();
                }
            }

            for (int i = 0; i < scene.flatIllustrations.Count; i++)
            {
                if (scene.flatIllustrations[i] != null &&
                    scene.flatIllustrations[i].sprite != null)
                {
                    scene.flatIllustrations[i].RemoveSprites();
                }
            }

            scene.depthIllustrations.Clear();
            scene.flatIllustrations.Clear();
        }

        private static void MoveBehindPageText(SlugcatSelectMenu.SlugcatPageNewGame self, MenuIllustration campaignImage)
        {
            if (campaignImage == null || campaignImage.sprite == null)
            {
                return;
            }

            try
            {
                if (self.infoLabel != null && self.infoLabel.label != null)
                {
                    campaignImage.sprite.MoveBehindOtherNode(self.infoLabel.label);
                }
                else if (self.difficultyLabel != null && self.difficultyLabel.label != null)
                {
                    campaignImage.sprite.MoveBehindOtherNode(self.difficultyLabel.label);
                }
            }
            catch (Exception)
            {
                campaignImage.sprite.MoveToBack();
            }
        }
    }
}
