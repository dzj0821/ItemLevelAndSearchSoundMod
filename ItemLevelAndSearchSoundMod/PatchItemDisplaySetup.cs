using System;
using System.Collections.Generic;
using System.Linq;
using Duckov.UI;
using Duckov.UI.Animations;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using UnityEngine.UI;

namespace ItemLevelAndSearchSoundMod
{
    [HarmonyPatch(typeof(ItemDisplay), "Setup")]
    public class PatchItemDisplaySetup
    {
        private static HashSet<ItemDisplay> updatedAnimationItemDisplays = new HashSet<ItemDisplay>();
        public static Dictionary<Item, ItemDisplay> ItemDisplayMap = new Dictionary<Item, ItemDisplay>();

        static void Postfix(ItemDisplay __instance, Item target)
        {
            if (__instance == null)
            {
                return;
            }

            if (target == null)
            {
                SetColor(__instance, Util.GetItemValueLevelColor(ItemValueLevel.White));
                return;
            }

            if (!updatedAnimationItemDisplays.Contains(__instance))
            {
                updatedAnimationItemDisplays.Add(__instance);
                var magnifier = __instance.transform.Find("InspectioningIndicator/Magnifier");
                if (magnifier != null)
                {
                    var revolver = magnifier.GetComponent<Revolver>();
                    if (revolver != null)
                    {
                        revolver.rPM = ModBehaviour.DefaultSearchAnimationValue * 0.75f;
                    }
                }
            }

            if (target.InInventory != null && target.InInventory.NeedInspection && !target.Inspected)
            {
                ItemDisplayMap[target] = __instance;
                // 物品还未搜索的情况
                SetColor(__instance, Util.GetItemValueLevelColor(ItemValueLevel.White));
                return;
            }

            ItemValueLevel level = Util.GetItemValueLevel(target);
            Color color = Util.GetItemValueLevelColor(level);
            SetColor(__instance, color);
        }

        public static void OnInspectionStateChanged(Item item)
        {
            if (!ItemDisplayMap.TryGetValue(item, out ItemDisplay itemDisplay) || itemDisplay.Target != item)
            {
                return;
            }
            if (item.Inspected)
            {
                item.onInspectionStateChanged -= OnInspectionStateChanged;
                ItemValueLevel level = Util.GetItemValueLevel(item);
                Color color = Util.GetItemValueLevelColor(level);
                SetColor(itemDisplay, color);

                ItemValueLevel playSoundLevel = level;
                if (ModBehaviour.ForceWhiteLevelTypeID.Contains(item.TypeID))
                {
                    playSoundLevel = ItemValueLevel.White;
                }
                if (ModBehaviour.ItemValueLevelSound.TryGetValue(playSoundLevel, out Sound sound))
                {
                    RESULT sfxGroupResult = FMODUnity.RuntimeManager.GetBus("bus:/Master/SFX").getChannelGroup(out ChannelGroup sfxGroup);
                    if (sfxGroupResult == RESULT.OK)
                    {
                        RESULT result = FMODUnity.RuntimeManager.CoreSystem.playSound(sound, sfxGroup, false, out Channel channel);
                        if (result != RESULT.OK)
                        {
                            ModBehaviour.ErrorMessage += $"FMOD failed to play sound: {result}\n";
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError("ItemLevelAndSearchSoundMod FMOD failed to get sfx group: " + sfxGroupResult);
                    }
                    
                }
                else
                {
                    (string eventName, float volume) = Util.GetInspectedSound(playSoundLevel);
                    EventInstance eventInstance = RuntimeManager.CreateInstance("event:/" + eventName);
                    eventInstance.setVolume(volume);
                    eventInstance.start();
                    eventInstance.release();
                }
            }
        }

        static void SetColor(ItemDisplay __instance, Color color)
        {
            try
            {
                __instance.transform.Find("BG").GetComponent<Image>().color = color;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("ItemLevelAndSearchSoundMod Patch SetColor Error: " + ex.Message);
            }
        }
    }
}