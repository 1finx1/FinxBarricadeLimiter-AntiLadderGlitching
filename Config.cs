using Rocket.API;
using System.Collections.Generic;

namespace FinxBarricadeLimiter
{
    public class Config : IRocketPluginConfiguration
    {
        public List<BarricadeEntry> RestrictedBarricades { get; set; } = new List<BarricadeEntry>();

        public void LoadDefaults()
        {
            RestrictedBarricades = new List<BarricadeEntry>
            {
                new BarricadeEntry
                {
                    ID = 326, // Replace with the ID of the barricade you want to restrict
                    PlacementRadius = 10.0f, // Replace with the desired placement radius
                    MaxBarricadesAllowed = 1 // Replace with the maximum allowed barricades for this type
                },
                 new BarricadeEntry
                {
                    ID = 327, // Replace with the ID of the barricade you want to restrict
                    PlacementRadius = 10.0f, // Replace with the desired placement radius
                    MaxBarricadesAllowed = 3 // Replace with the maximum allowed barricades for this type
                },
            };
        }
    }

    public class BarricadeEntry
    {
        public ushort ID { get; set; }
        public float PlacementRadius { get; set; }
        public int MaxBarricadesAllowed { get; set; }
    }
}
