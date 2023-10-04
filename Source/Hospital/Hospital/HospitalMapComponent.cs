using System;
using System.Collections.Generic;
using System.Linq;
using Hospital.Utilities;
using RimWorld;
using Verse;

namespace Hospital
{
    public class HospitalMapComponent : MapComponent
    {
        public Dictionary<Pawn, PatientData> Patients;
        private List<Pawn> _colonistsKeysWorkingList;
        private List<PatientData> _colonistsValuesWorkingList;
        
        public bool openForBusiness = false;
        public List<bool> openingHours = new System.Collections.Generic.List<bool>
        {
            false,false,false,false,false,false,false,true, //8
            false,false,false,true, //12
            false,false,false,true,//16
            false,false,false,true,//20
            false,false,false,false
        };
        public List<RecipeDef> refusedOperations = new List<RecipeDef>();
        public int bedsReserved = 0;
        
        public HospitalMapComponent(Map map) : base(map)
        {
            Patients = new Dictionary<Pawn, PatientData>();
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            openingHours ??= new System.Collections.Generic.List<bool>
            {
                false,false,false,false,false,false,false,true, //8
                false,false,false,true, //12
                false,false,false,true,//16
                false,false,false,true,//20
                false,false,false,false
            };
            refusedOperations ??= new List<RecipeDef>();
            
            Scribe_Collections.Look(ref openingHours, "openingHours");
            Scribe_Collections.Look(ref refusedOperations, "refusedOperations");
            Scribe_Values.Look(ref openForBusiness, "openForBusiness", false);
            Scribe_Values.Look(ref bedsReserved, "bedsReserved", 0);
            Patients ??= new Dictionary<Pawn, PatientData>();
            Scribe_Collections.Look(ref Patients, "patients", LookMode.Reference, LookMode.Deep, ref _colonistsKeysWorkingList, ref _colonistsValuesWorkingList);
        }

        public bool IsOpen()
        {
            if (!openForBusiness) return false;
            return openingHours[GenLocalDate.HourOfDay(map)];
        }

        public void PatientArrived(Pawn pawn, PatientData data)
        {
            Patients.Add(pawn, data);
            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }
        
        public void PatientLeaves(Pawn pawn)
        {
            if (Patients.TryGetValue(pawn, out var patientData))
            {
                float silver = PatientUtility.CalculateSilverToReceive(pawn, patientData);
                if (silver > 0)
                {
                    if (pawn.Faction != null)
                    {
                        int goodwill = PatientUtility.CalculateGoodwillToGain(pawn, patientData);
                        Messages.Message(
                            $"{pawn.NameFullColored} leaves: +" + silver.ToStringMoney() + ", goodwill change: " +
                            goodwill + " " +
                            pawn.Faction.name, MessageTypeDefOf.NeutralEvent);
                        pawn.Faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, false);
                    }
                    else
                    {
                        Messages.Message($"{pawn.NameFullColored} leaves: +" + silver.ToStringMoney(), MessageTypeDefOf.NeutralEvent);                        
                    }
                    var silverThing = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silverThing.stackCount = (int)silver;
                    GenPlace.TryPlaceThing(silverThing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }

                RemoveFromPatientList(pawn);
            }
            else
            {
                Log.Message($"{pawn.NameFullColored} leaves but is not a patient anymore?");   
            }
        }

        public void PatientDied(Pawn pawn)
        {
            if (Patients.TryGetValue(pawn, out var patientData))
            {
                Messages.Message($"{pawn.NameFullColored} died: -10 "+pawn.Faction.name, MessageTypeDefOf.PawnDeath);
                pawn.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -10, false);
                RemoveFromPatientList(pawn);
            }
            // else - was not a patient?
        }
        
        public void DismissPatient(Pawn pawn)
        {
            if (Patients.TryGetValue(pawn, out var patientData))
            {
                Messages.Message(
                    $"{pawn.NameFullColored} dismissed.", MessageTypeDefOf.NeutralEvent);
                RemoveFromPatientList(pawn);
            }
            // else - was not a patient?
            
        }

        private void RemoveFromPatientList(Pawn pawn)
        {
            Patients.Remove(pawn);    
            pawn.playerSettings.selfTend = true; // in case the patient gets hurt while walking home
            pawn.guest.SetGuestStatus(null); // might fix the "patients stay hanging around" issue
            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }

        public bool IsSurgeryRecipeAllowed(RecipeDef recipe)
        {
            return !refusedOperations.Exists(def => def.Equals(recipe));
        }

        public void RefuseOperation(Pawn pawn, RecipeDef recipe)
        {
            if (!refusedOperations.Exists(def => def.Equals(recipe)))
            {
                refusedOperations.Add(recipe);
                Messages.Message(
                    $"{recipe.LabelCap} blacklisted.", MessageTypeDefOf.NeutralEvent); 
            }

            DismissPatient(pawn);
        }
        
        public void UnRefuseOperation(RecipeDef recipe)
        {
            if (refusedOperations.Exists(def => def.Equals(recipe)))
            {
                refusedOperations.Remove(recipe);
            }
        }

        public bool IsFull()
        {
            // check if we have enough beds left for colonists
            if (FreeMedicalBeds() <= bedsReserved) return true;
            return false;
        }

        public int FreeMedicalBeds()
        {
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Count(bed => bed.Medical 
                && !bed.ForPrisoners 
                && bed.def.building.bed_humanlike
                && bed.Spawned
                && bed.Map == this.map
                && !bed.IsBurning()) - Patients.Count;            
        }
    }

 
}