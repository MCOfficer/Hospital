using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Hospital.MainTab
{
    public class MainTabWindow_Hospital : MainTabWindow_PawnTable
    {
        private static PawnTableDef pawnTableDef;

        public override PawnTableDef PawnTableDef => pawnTableDef ??= DefDatabase<PawnTableDef>.GetNamed("Patients");

        public override IEnumerable<Pawn> Pawns => Find.CurrentMap.GetComponent<HospitalMapComponent>().ActivePatientsList();

      /*  public override void PostOpen()
        {
            base.PostOpen();
            Find.World.renderer.wantedMode = WorldRenderMode.None;
        }
*/
    }
}
