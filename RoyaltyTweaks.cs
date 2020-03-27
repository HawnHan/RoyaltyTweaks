﻿using RimWorld;
using HarmonyLib;
using Verse;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace TestMod
{
    [StaticConstructorOnStartup]
    public static class RoyaltyTweaks
    {        
        static RoyaltyTweaks() //our constructor
        {
            var harmony = new Harmony("com.mobius.royaltytweaks");
            
            harmony.PatchAll();
        }

        //public static string Validate(Room room)
        //"ThroneRoomHasMoreThan1Throne".Translate()
        [HarmonyPatch(typeof(RoomRoleWorker_ThroneRoom))]
        [HarmonyPatch(nameof(RoomRoleWorker_ThroneRoom.Validate))]
        static class RoomRoleWorker_ThroneRoom_Validate_PatchRoomRoleWorker_ThroneRoom_Validate_Patch
        {
            static bool Prefix(Room room, ref List<Building_Throne> ___tmpThrones, ref string __result)
            {
                if (!LoadedModManager.GetMod<RoyaltyTweaksMod>().GetSettings<RoyaltyTweaksSettings>().throneRoomShared)
                {
                    return true;
                }

                if (room == null || room.OutdoorsForWork)
                {
                    __result = "ThroneMustBePlacedInside".Translate();
                    return false;
                }
                List<Thing> containedAndAdjacentThings = room.ContainedAndAdjacentThings;
                string result = "No thrones";

                    for (int i = 0; i < containedAndAdjacentThings.Count; i++)
                    {
                        Thing thing = containedAndAdjacentThings[i];
                        if (thing is Building_Throne)
                        {
                        __result = null;
                        return false;
                        }
                    }                   
                    __result = result;                               

                return false;
            }
        }        

        [HarmonyPatch(typeof(Pawn_RoyaltyTracker))]
        [HarmonyPatch(nameof(Pawn_RoyaltyTracker.CanRequireThroneroom))]
        static class Pawn_RoyaltyTracker_CanRequireThroneroom_Patch
        {
            static bool Prefix(Pawn_RoyaltyTracker __instance, Pawn ___pawn,  ref bool __result)
            {
                if(!LoadedModManager.GetMod<RoyaltyTweaksMod>().GetSettings<RoyaltyTweaksSettings>().throneRoomTweaks || ___pawn.ownership.AssignedThrone != null)
                {
                    return true;
                }

                __result = ___pawn.IsFreeColonist && __instance.allowRoomRequirements && !___pawn.IsQuestLodger();
                int highestSeniority = 0;
                bool throneAssigned = false;
                if (__result)
                {
                    if(__instance != null && __instance.MostSeniorTitle != null && __instance.MostSeniorTitle.def != null)
                    {
                        List<Map> maps = Find.Maps;
                        for (int i = 0; i < maps.Count; i++)
                        {
                            if (maps[i].IsPlayerHome)
                            {
                                foreach (Pawn pawn in maps[i].mapPawns.FreeColonistsSpawned)
                                {                                    
                                    if (pawn != ___pawn && pawn.royalty != null && pawn.royalty.MostSeniorTitle != null && pawn.royalty.MostSeniorTitle.def != null)
                                    {
                                        if(pawn.royalty.MostSeniorTitle.def.seniority > highestSeniority)
                                        {
                                            highestSeniority = pawn.royalty.MostSeniorTitle.def.seniority;
                                            if (pawn.ownership.AssignedThrone != null) throneAssigned = true;
                                        }
                                    }
                                }
                            }
                        }
                        if (__instance.MostSeniorTitle.def.seniority < highestSeniority)
                        {
                            Pawn spouse = null;
                            if (LoadedModManager.GetMod<RoyaltyTweaksMod>().GetSettings<RoyaltyTweaksSettings>().spouseWantsThroneroom){
                                spouse = ___pawn.GetSpouse();
                            }                            
                            if (spouse == null || spouse.royalty == null || spouse.royalty.MostSeniorTitle == null || spouse.royalty.MostSeniorTitle.def.seniority < highestSeniority)
                            {
                                __result = false;
                            }                                                        
                        }
                        if(__instance.MostSeniorTitle.def.seniority == highestSeniority && throneAssigned)
                        {
                            __result = false;
                        }
                    }
                }               
                return false;
            }

            // 		public WorkTags CombinedDisabledWorkTags
            [HarmonyPatch(typeof(Pawn))]
            [HarmonyPatch("CombinedDisabledWorkTags", MethodType.Getter)]
            static class Pawn_CombinedDisabledWorkTags_Patch
            {
                static bool Prefix(Pawn __instance, ref WorkTags __result)
                {
                    if (LoadedModManager.GetMod<RoyaltyTweaksMod>().GetSettings<RoyaltyTweaksSettings>().willWorkPassionSkills)
                    {                       
                        WorkTags workTags = (__instance.story != null) ? __instance.story.DisabledWorkTagsBackstoryAndTraits : WorkTags.None;
                        WorkTags disabledRoyalTags = WorkTags.None;

                        if (__instance.royalty?.MostSeniorTitle?.def?.seniority > 100)
                        {
                            foreach (RoyalTitle royalTitle in __instance.royalty.AllTitlesForReading)
                            {                               
                                    if (royalTitle.conceited)
                                    {
                                        disabledRoyalTags |= royalTitle.def.disabledWorkTags;
                                    }                                                              
                            }

                           

                            var namelist = new List<string>();
                            if (LoadedModManager.GetMod<RoyaltyTweaksMod>().GetSettings<RoyaltyTweaksSettings>().willWorkOnlyMajorPassionSkills)
                            {
                                namelist = __instance.skills.skills.Where(a => a.passion == Passion.Major).Select(b => b.def.defName).ToList();
                            }
                            else
                            {
                                namelist = __instance.skills.skills.Where(a => a.passion != Passion.None).Select(b => b.def.defName).ToList();
                            }

                            var list = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                            if (namelist != null && namelist.Any())
                            {
                                var worklist = list.Where(a => a.relevantSkills != null && a.relevantSkills.Any() && namelist.Contains(a.relevantSkills.First().defName)).Select(b => b.workTags);

                                if (worklist != null && worklist.Any())
                                {
                                    foreach (var worklistItem in worklist)
                                    {
                                        disabledRoyalTags &= ~worklistItem;
                                    }

                                }
                            }


                        }
                        workTags |= disabledRoyalTags;

                        if (__instance.health != null && __instance.health.hediffSet != null)
                        {
                            foreach (Hediff hediff in __instance.health.hediffSet.hediffs)
                            {
                                HediffStage curStage = hediff.CurStage;
                                if (curStage != null)
                                {
                                    workTags |= curStage.disabledWorkTags;
                                }
                            }
                        }
                        __result = workTags;

                        return false;
                    }
                    return true;
                }
            }
        }
    }

    public class RoyaltyTweaksSettings : ModSettings
    {
        /// <summary>
        /// The three settings our mod has.
        /// </summary>
        ///
        public bool throneRoomTweaks;
        public bool throneRoomShared;
        public bool spouseWantsThroneroom;
        public bool willWorkPassionSkills;
        public bool willWorkOnlyMajorPassionSkills;
        //public float exampleFloat = 200f;
        //public List<Pawn> exampleListOfPawns = new List<Pawn>();

        /// <summary>
        /// The part that writes our settings to file. Note that saving is by ref.
        /// </summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref throneRoomTweaks, "throneRoomTweaks", true, true);
            Scribe_Values.Look(ref throneRoomShared, "throneRoomShared", true, true);            
            Scribe_Values.Look(ref spouseWantsThroneroom, "spouseWantsThroneroom", true, true);
            Scribe_Values.Look(ref willWorkPassionSkills, "willWorkPassionSkills", true, true);
            Scribe_Values.Look(ref willWorkOnlyMajorPassionSkills, "willWorkOnlyMajorPassionSkills", false, true);
            //Scribe_Values.Look(ref exampleFloat, "exampleFloat", 200f);
            //Scribe_Collections.Look(ref exampleListOfPawns, "exampleListOfPawns", LookMode.Reference);
            base.ExposeData();
        }
    }
    public class RoyaltyTweaksMod : Mod
    {
        /// <summary>
        /// A reference to our settings.
        /// </summary>
        RoyaltyTweaksSettings settings;

        /// <summary>
        /// A mandatory constructor which resolves the reference to our settings.
        /// </summary>
        /// <param name="content"></param>
        public RoyaltyTweaksMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<RoyaltyTweaksSettings>();
        }

        /// <summary>
        /// The (optional) GUI part to set your settings.
        /// </summary>
        /// <param name="inRect">A Unity Rect with the size of the settings window.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.Label("-=[ " + "ThroneRoomSettings".Translate() + " ]=-");
            listingStandard.CheckboxLabeled("ThroneRoomSharedCheckboxLabel".Translate(), ref settings.throneRoomShared, "ThroneRoomSharedCheckboxTooltip".Translate());
            listingStandard.CheckboxLabeled("ThroneRoomRequiredByHighestCheckboxLabel".Translate(), ref settings.throneRoomTweaks, "ThroneRoomRequiredByHighestCheckboxTooltip".Translate());
            if (settings.throneRoomTweaks) { 
                listingStandard.CheckboxLabeled("SpouseOptionLabel".Translate(), ref settings.spouseWantsThroneroom, "SpouseOptionTooltip".Translate());
            }
            listingStandard.Label("");
            listingStandard.Label("-=[ " + "RoyalPassionSkillSettings".Translate() + " ]=-");
            listingStandard.CheckboxLabeled("PassionSkillsCheckboxLabel".Translate(), ref settings.willWorkPassionSkills, "PassionSkillsCheckboxTooltip".Translate());
            if (settings.willWorkPassionSkills)
            {
                if (listingStandard.RadioButton("MajorAndMinorLabel".Translate(), settings.willWorkOnlyMajorPassionSkills == false, 0f, "MajorAndMinorTooltip".Translate()))
                {
                    settings.willWorkOnlyMajorPassionSkills = false;

                }
                if (listingStandard.RadioButton("OnlyMajorLabel".Translate(), settings.willWorkOnlyMajorPassionSkills == true, 0f, "OnlyMajorTooltip".Translate()))
                {
                    settings.willWorkOnlyMajorPassionSkills = true;

                }
            }            
            
            //listingStandard.Label("exampleFloatExplanation");
            //settings.exampleFloat = listingStandard.Slider(settings.exampleFloat, 100f, 300f);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Override SettingsCategory to show up in the list of settings.
        /// Using .Translate() is optional, but does allow for localisation.
        /// </summary>
        /// <returns>The (translated) mod name.</returns>
        public override string SettingsCategory()
        {
            return "RoyalTweaks".Translate();
        }
    }
}
