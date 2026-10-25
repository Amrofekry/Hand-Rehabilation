using System;
using System.IO;
using System.Linq;
using GripRehab.Product;
using UnityEngine;

namespace GripRehab.Editor
{
    // Stage 6: the fabricated dashboard fixtures seed through the real encrypted Windows store
    // (Unity JSON, DPAPI and atomic file replace) in a throwaway folder, then reload with the
    // exact numbers the dashboards show. Never touches the player's records.
    public static class DashboardVerification
    {
        public static void Run()
        {
            string folder = Path.Combine(Path.GetTempPath(), "GripRehab-DashboardCheck-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repo = new ProductRepository(new WindowsProductStore(folder), new SystemProductClock());
                DashboardFixtures.Seed(repo);
                Require(repo.Current == null, "seeding leaves nobody signed in");
                var reloaded = new ProductRepository(new WindowsProductStore(folder), new SystemProductClock());
                Require(reloaded.SignIn(DashboardFixtures.Alpha, DashboardFixtures.Password), "fabricated patient signs in after reload");
                var sessions = reloaded.Sessions(reloaded.Current.id);
                var cards = ProgressSummary.Cards(sessions);
                Require(cards.Single(c => c.label == "Force").value == "55 % ref" && cards.Single(c => c.label == "Endurance").value == "22.7 s", "cards survive the encrypted store: " + string.Join(", ", cards.Select(c => c.value)));
                Require(ProgressSummary.ByModule(sessions)[0].safetyStops == 1, "the stopped attempt survives the encrypted store");
                reloaded.SignOut();
                Require(reloaded.SignIn(DashboardFixtures.Doctor, DashboardFixtures.Password), "fabricated doctor signs in after reload");
                var alpha = reloaded.Patients().Single(p => p.username == DashboardFixtures.Alpha).id;
                Require(reloaded.AuditTrail(alpha).Any(e => e.action == "added a note"), "the note's audit entry survives the encrypted store");
                Require(reloaded.MyAccess().Any(e => e.action == "signed in"), "sign-in entries survive the encrypted store");
                Debug.Log("[REHAB_DASHBOARD] PASS: fabricated fixtures seed, encrypt, reload and summarise with exact numbers in an isolated folder");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }

        static void Require(bool condition, string reason)
        {
            if (!condition) throw new Exception("Dashboards: " + reason);
        }
    }
}
