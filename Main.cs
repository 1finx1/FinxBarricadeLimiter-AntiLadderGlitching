using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FinxBarricadeLimiter
{
    public class FinxBarricadeLimiter : RocketPlugin<Config>
    {
        public override TranslationList DefaultTranslations => new TranslationList
        {
            {"place_denied", "You cannot place another barricade within this radius"},
        };

        protected override void Load()
        {
            BarricadeManager.onDeployBarricadeRequested += OnDeployBarricadeRequested;
            BarricadeDrop.OnSalvageRequested_Global += OnSalvageBarricadeRequested;
        }

        protected override void Unload()
        {
            BarricadeManager.onDeployBarricadeRequested -= OnDeployBarricadeRequested;
            BarricadeDrop.OnSalvageRequested_Global -= OnSalvageBarricadeRequested;
        }

        private void OnSalvageBarricadeRequested(BarricadeDrop barricade, SteamPlayer instigatorClient, ref bool shouldAllow)
        {
            ushort barricadeID = barricade.asset.id;
            Vector3 barricadePosition = barricade.model.position;

            

            // Create an empty list of RegionCoordinate
            List<RegionCoordinate> search = new List<RegionCoordinate>();

            // Decrement the count of barricades in radius
            if (Configuration.Instance.RestrictedBarricades.Exists(entry => entry.ID == barricadeID))
            {
                int blacklistedBarricadeCount = GetBlacklistedBarricadeCount(barricadePosition, Configuration.Instance.RestrictedBarricades.Find(entry => entry.ID == barricadeID)?.PlacementRadius ?? 0f, search);

                if (blacklistedBarricadeCount > 0)
                {
                    blacklistedBarricadeCount--; // Decrement the count
                }

            }
        }

        private void OnDeployBarricadeRequested(Barricade barricade, ItemBarricadeAsset asset, Transform hit, ref Vector3 point, ref float angle_x, ref float angle_y, ref float angle_z, ref ulong owner, ref ulong group, ref bool shouldAllow)
        {
            UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(owner));

            // Check if the asset ID is in the list of restricted barricade IDs
            if (Configuration.Instance.RestrictedBarricades.Exists(entry => entry.ID == asset.id))
            {
                // Use the PlacementRadius from the configuration
                float placementRadiusroot = Configuration.Instance.RestrictedBarricades.Find(entry => entry.ID == asset.id)?.PlacementRadius ?? 0f;
                float placementRadius = placementRadiusroot * placementRadiusroot;
               
                // Calculate the region coordinates for the search based on placement radius
                List<RegionCoordinate> searchCoordinates = CalculateSearchRegionCoordinates(point, placementRadius);

                // Create a list of BarricadeDrop within the search region
                List<BarricadeDrop> searchResults = new List<BarricadeDrop>();

                // Populate searchResults with barricades within the search region
                getBarricadesInRadius(point, placementRadius, searchCoordinates, searchResults);

                // Count only barricades of the same type as the one being deployed
                int sameTypeBarricadeCountInRadius = 0;
                foreach (BarricadeDrop drop in searchResults)
                {
                    if (drop.asset.id == asset.id)
                    {
                        sameTypeBarricadeCountInRadius++;
                        float distance = Vector3.Distance(drop.model.position, point); // Calculate distance
                        Debug.Log($"Distance to another barricade: {distance} meters");
                    }
                }



                // Check if the number of barricades exceeds the allowed limit
                ushort maxAllowed = (ushort)(Configuration.Instance.RestrictedBarricades.Find(entry => entry.ID == asset.id)?.MaxBarricadesAllowed ?? 0);

                if (sameTypeBarricadeCountInRadius >= maxAllowed)
                {
                    
                    shouldAllow = false;
                    string translatedMessage = Translate("place_denied");
                    ChatManager.serverSendMessage(translatedMessage, Color.red, null, player.SteamPlayer(), EChatMode.SAY);
                }

                // Check if the new barricade is too close to existing blacklisted barricades
                float sqrPlacementRadius = placementRadius;
                foreach (BarricadeDrop drop in searchResults)
                {
                    if (drop.asset.id != asset.id)
                    {
                        float sqrDistance = (drop.model.position - point).sqrMagnitude;
                        if (sqrDistance < sqrPlacementRadius)
                        {
                            
                            shouldAllow = false;
                            string translatedMessage = Translate("place_denied");
                            ChatManager.serverSendMessage(translatedMessage, Color.red, null, player.SteamPlayer(), EChatMode.SAY);
                            break; // Stop checking once one violation is found
                        }
                    }
                }
            }
        }







        private List<RegionCoordinate> CalculateSearchRegionCoordinates(Vector3 placementPosition, float placementRadius)
        {
            List<RegionCoordinate> searchCoordinates = new List<RegionCoordinate>();

            // Calculate the minimum and maximum region coordinates based on placement radius
            byte minX, maxX, minY, maxY;
            CalculateMinMaxRegionCoordinates(placementPosition, placementRadius, out minX, out maxX, out minY, out maxY);

            

            // Add region coordinates to the search list
            for (byte x = minX; x <= maxX; x++)
            {
                for (byte y = minY; y <= maxY; y++)
                {
                    searchCoordinates.Add(new RegionCoordinate(x, y));
                }
            }

            

            return searchCoordinates;
        }

        // Function to calculate the minimum and maximum region coordinates based on placement radius
        private void CalculateMinMaxRegionCoordinates(Vector3 placementPosition, float placementRadius, out byte minX, out byte maxX, out byte minY, out byte maxY)
        {
            RegionCoord centerRegion = new RegionCoord(placementPosition);

            // Calculate the range based on the placement radius
            int range = Mathf.CeilToInt(placementRadius  / Regions.REGION_SIZE);

           

            // Clamp the region coordinates to valid bounds
            minX = (byte)Mathf.Clamp(centerRegion.x - range, 0, Regions.WORLD_SIZE - 1);
            maxX = (byte)Mathf.Clamp(centerRegion.x + range, 0, Regions.WORLD_SIZE - 1);
            minY = (byte)Mathf.Clamp(centerRegion.y - range, 0, Regions.WORLD_SIZE - 1);
            maxY = (byte)Mathf.Clamp(centerRegion.y + range, 0, Regions.WORLD_SIZE - 1);

            
        }

        public static void getBarricadesInRadius(Vector3 center, float sqrRadius, List<RegionCoordinate> search, List<BarricadeDrop> result)
        {
            foreach (RegionCoordinate regionCoordinate in search)
            {
                if (IsValidRegionCoordinate(regionCoordinate))
                {
                    if (BarricadeManager.regions[regionCoordinate.x, regionCoordinate.y] != null)
                    {
                        foreach (BarricadeDrop drop in BarricadeManager.regions[regionCoordinate.x, regionCoordinate.y].drops)
                        {
                            Transform model = drop.model;
                            if (model != null && (model.position - center).sqrMagnitude < sqrRadius)
                            {
                                result.Add(drop);
                            }
                        }
                    }
                }
            }

            
        }

        // Function to check if a region coordinate is within valid bounds
        private static bool IsValidRegionCoordinate(RegionCoordinate regionCoordinate)
        {
            return regionCoordinate.x >= 0 && regionCoordinate.x < Regions.WORLD_SIZE &&
                   regionCoordinate.y >= 0 && regionCoordinate.y < Regions.WORLD_SIZE;
        }

        private int GetBlacklistedBarricadeCount(Vector3 placementPosition, float placementRadius, List<RegionCoordinate> search)
        {
            List<BarricadeDrop> barricadesInRadius = new List<BarricadeDrop>();
            getBarricadesInRadius(placementPosition, placementRadius, search, barricadesInRadius);

            int count = 0;

            foreach (BarricadeDrop barricadeDrop in barricadesInRadius)
            {
                float distance = Vector3.Distance(barricadeDrop.model.position, placementPosition);
                if (distance < placementRadius)
                {
                    count++;
                }
            }

            

            return count;
        }



    }
}
