﻿using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FireScript
{
    public class FireScript : BaseScript
    {
        private const int ManageFireTimeout = 50;
        private List<Fire> ActiveFires = new List<Fire>();
        private List<Tuple<CoordinateParticleEffect, Vector3>> SmokeWithoutFire = new List<Tuple<CoordinateParticleEffect, Vector3>>();
        private Dictionary<int, long> userCommands = new Dictionary<int, long>();

        public FireScript()
        {
            TriggerEvent("chat:addSuggestion", "/startfire", "Starts a fire at your location", new[]
            {
                new { name = "NumFlames", help = "The number of flames (up to 100 depending on size)" },
                new { name="Radius", help="The radius in metres (up to 30 depending on size)" },
                new { name="Explosion", help="True to create an explosion, false to not create an explosion." },
            });
            TriggerEvent("chat:addSuggestion", "/startsmoke", "Starts smoke without fire at your location", new[]
            {
                new { name = "Scale", help = "Magnitude of the smoke (recommended 0.5-5.0)" }
            });
            EventHandlers["FireScript:StartFireAtPlayer"] += new Action<int, int, int, bool>((int source, int maxFlames, int maxRange, bool explosion) =>
             {

                 // I dont feel like thinking right now, this looks horrible
                 // TODO: clean this.
                 if (userCommands.ContainsKey(source))
                 {
                     if (userCommands[source] > DateTime.Now.Ticks - 600000000)
                     {
                         sendNotification($"~r~You must wait {((600000000 - (DateTime.Now.Ticks - userCommands[source])) / 10000000):0} seconds before starting another fire!" );
                         return;
                     }
                 }
                 else
                 {
                     userCommands.Add(source, DateTime.Now.Ticks);
                 }

                 sendNotification("~g~Starting fire...");
                 updateUserCache(source, DateTime.Now.Ticks);
                 startFire(source, maxFlames, maxRange, explosion);

             });
            EventHandlers["FireScript:StopFiresAtPlayer"] += new Action<int>((int source) =>
            {
                sendNotification("~g~Stopping all fires at your location...");
                stopFires(true, Players[source].Character.Position);
            });
            EventHandlers["FireScript:StopAllFires"] += new Action<dynamic>((dynamic res) =>
            {
                sendNotification("~r~Stopping all fires...");
                stopFires(false, Vector3.Zero);
            });
            EventHandlers["FireScript:StopFireAtPosition"] += new Action<float, float, float>((float x, float y, float z) =>
            {
                sendNotification($"~r~Stopping all fires at ~h~~g~{x:0}, {y:0}, {z:0}~s~~r~...");
                stopFires(true, new Vector3(x, y, z), 3);
            });

            EventHandlers["FireScript:StartSmokeAtPlayer"] += new Action<int, float>((int source, float scale) =>
            {
                sendNotification("~g~Starting smoke...");
                startSmoke(Players[source].Character.Position, scale);
            });
            EventHandlers["FireScript:StopSmokeAtPlayer"] += new Action<int>((int source) =>
            {
                sendNotification("~g~Stopping all smoke at your location...");
                stopSmoke(true, Players[source].Character.Position);
            });

            EventHandlers["FireScript:StopAllSmoke"] += new Action<dynamic>((dynamic res) =>
            {
                sendNotification("~g~Stopping all smoke...");
                stopSmoke(false, Vector3.Zero);
            });
            Main();
        }

        private async void Main()
        {
            DateTime timekeeper = DateTime.Now;
            while (true)
            {
                await Delay(10);

                evictUserCache(); // Expire offline users from cache
                if ((System.DateTime.Now - timekeeper).TotalMilliseconds > ManageFireTimeout)
                {
                    timekeeper = DateTime.Now;
                    foreach (Fire f in ActiveFires.ToArray())
                    {

                        if (f.Active)
                        {
                            f.Manage();
                        }
                        else
                        {
                            ActiveFires.Remove(f);
                        }
                    }
                }
            }
        }

        private void stopFires(bool onlyNearbyFires, Vector3 pos, float distance = 35)
        {
            foreach (Fire f in ActiveFires.ToArray())
            {
                if (!onlyNearbyFires || Vector3.Distance(f.Position, pos) < distance)
                {
                    f.Remove(false);
                    ActiveFires.Remove(f);
                }
            }
        }

        private void startFire(int source, int maxFlames, int maxRange, bool explosion)
        {
            Vector3 Pos = Players[source].Character.Position;
            Pos.Z -= 0.87f;
            if (maxRange > 25) { maxRange = 25; }
            if (maxFlames > 25) { maxRange = 25; }
            Fire f = new Fire(Pos, maxFlames, false, maxRange, explosion);
            ActiveFires.Add(f);
            f.Start();
        }

        private async Task startSmoke(Vector3 pos, float scale)
        {
            ParticleEffectsAsset asset = new ParticleEffectsAsset("scr_agencyheistb");
            await asset.Request(1000);
            SmokeWithoutFire.Add(Tuple.Create(asset.CreateEffectAtCoord("scr_env_agency3b_smoke", pos, scale: scale, startNow: true), pos));
        }

        private void stopSmoke(bool allSmoke, Vector3 position)
        {
            foreach (Tuple<CoordinateParticleEffect, Vector3> f in SmokeWithoutFire.ToArray())
            {
                if (!allSmoke || Vector3.Distance(f.Item2, position) < 30f)
                {
                    f.Item1.RemovePTFX();
                    SmokeWithoutFire.Remove(f);
                }
            }
        }

        private void updateUserCache(int user, long time)
        {
            if (userCommands.ContainsKey(user))
            {
                userCommands[user] = time;
            }
            else
            {
                userCommands.Add(user, time);
            }
        }

        private void evictUserCache()
        {
            foreach (int user in userCommands.Keys)
            {
                if (Players[user] == null)
                {
                    userCommands.Remove(user);
                }
            }
        }

        private void sendNotification(string message)
        {
            CitizenFX.Core.UI.Screen.ShowNotification(message, false);
        }
    }
}
