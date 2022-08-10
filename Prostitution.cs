using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Prostitution_Callout
{
    [CalloutProperties("Prostitution", "grumpypoo", "v1.0")]
    internal class Prostitution : Callout
    {
        public readonly Random rannum = new Random();

        public Ped trick;
        public Ped prostitute;
        public Vehicle veh;

        public PedData trickData;
        public PedData prostituteData;

        public PedHash trickHash;
        public PedHash prostituteHash;
        public VehicleHash vehicleHash;

        private Location vehicleLocation;

        public float heading;

        private struct Location
        {
            public Vector3 Coords;
            public float Header;

            public Location(Vector3 coords, float header)
            {
                Coords = coords;
                Header = header;
            }
        }

        private List<Location> possibleLocation = new List<Location>()
        {
            new Location(new Vector3(-1325.953f, -449.114f, 33.05328f), 35.87906f),
            new Location(new Vector3(-462.9382f, 772.009f, 172.1472f), 351.5161f),
            new Location(new Vector3(499.2421f, -554.4188f, 24.3578f), 265.3791f),
            new Location(new Vector3(1368.764f, -729.6934f, 66.66956f), 98.05668f),
            new Location(new Vector3(1445.44f, -1944.684f, 70.00431f), 170.1218f)

        };

        public List<VehicleHash> vHashes = new List<VehicleHash>()
        {
            VehicleHash.Emperor,
            VehicleHash.Emperor2,
            VehicleHash.Regina,
            VehicleHash.Sadler,
            VehicleHash.Burrito3
        };
        public List<PedHash> pHashes = new List<PedHash>()
        {
            PedHash.Hooker01SFY,
            PedHash.Hooker02SFY,
            PedHash.Hooker03SFY,
            PedHash.Juggalo01AFY,
            PedHash.Hippie01AFY
        };
        public List<PedHash> bHashes = new List<PedHash>()
        {
            PedHash.Hillbilly01AMM,
            PedHash.Hillbilly02AMM,
            PedHash.Hippy01AMY,
            PedHash.Hipster02AMY,
            PedHash.Hipster03AMY
        };
        
        public Prostitution()
        {
            // Pick a random location
            vehicleLocation = possibleLocation.SelectRandom();

            // Pick random hashes for our vehicles/peds
            vehicleHash = vHashes.SelectRandom();
            trickHash = bHashes.SelectRandom();
            prostituteHash = pHashes.SelectRandom();

            InitInfo(vehicleLocation.Coords);
            StartDistance = 175f;
            CalloutDescription = $"There are reports of two individuals engaged in possible sexual activities.";
            ResponseCode = 2;
            ShortName = "Prostitution";
        }

        public override Task<bool> CheckRequirements() => Task.FromResult(World.CurrentDayTime <= TimeSpan.FromHours(4) || World.CurrentDayTime >= TimeSpan.FromHours(22));

        public override async Task OnAccept()
        {
            InitBlip();
            UpdateData();
                
            await Task.FromResult(0);
        }

        public bool trickRuns()
        {
            if (rannum.Next(0, 101) <= 25)
            {
                return true;
            }
            return false;
        }

        public override async void OnStart(Ped closest)
        {
            base.OnStart(closest);

            // Spawn the vehicle
            veh = await SpawnVehicle(vehicleHash, vehicleLocation.Coords, vehicleLocation.Header);
            veh.AttachBlip();
            veh.AttachedBlip.Color = BlipColor.Red;

            // Spawn the two suspects and place them in driver/passenger seats
            trick = await SpawnPed(trickHash, veh.Position);
            trick.Task.WarpIntoVehicle(veh, VehicleSeat.Driver);
            API.SetDriverAbility(trick.Handle, 0.0f);
            trickData = await trick.GetData();

            prostitute = await SpawnPed(prostituteHash, veh.Position);
            prostitute.Task.WarpIntoVehicle(veh, VehicleSeat.Passenger);
            prostituteData = await prostitute.GetData();

            // Add our questions
            trickQuestions();
            ProstituteQuestions();

            // Set our pedData stuff (Make them the right genders)
            trickData.Gender = Gender.Male;
            prostituteData.Gender = Gender.Female;

            // Roll the dice to see if the hooker will be on coke
            if (rannum.Next(0,101) >= 50)
            {
                // Have the hooker be on Cocaine
                PedData.Drugs[] usedDrugs = new PedData.Drugs[1];
                usedDrugs[0] = PedData.Drugs.Cocaine;
                prostituteData.UsedDrugs = usedDrugs;
            }

            trick.SetData(trickData);
            prostitute.SetData(prostituteData);

            // Make the prostitute announce the cops are here
            ShowDialog("~r~Female~s~: Oh god it's the ~b~Cops~s~ put your pants back on! Drive!!!", 3000, 10f);

            // Force the vehicle to drive off, because i'm annoyed that I cant set some flag to have the car register as stopped /shrug
            await BaseScript.Delay(1000);
            API.TaskVehicleDriveWander(trick.Handle, veh.Handle, 20f, 536871351);

            // If we happen to call a random number make both suspects take off
            if (rannum.Next(0, 699) == 69)
            {
                // Stop the vehicle and clear tasks
                API.TaskVehicleDriveWander(trick.Handle, veh.Handle, 0f, 536871351);
                trick.Task.ClearAllImmediately();
                API.ClearVehicleTasks(veh.Handle);

                // Let's wrap trick in the vehicle again because without doing this trick is standing on the roof
                trick.Task.WarpIntoVehicle(veh, VehicleSeat.Driver);

                while (World.GetDistance(Game.PlayerPed.Position, veh.Position) > 15f) { await BaseScript.Delay(50); }
                ShowDialog("~r~Male~s~: GET OUT!", 2000, 8f);

                // Determine if trick hops out and shoots or if he flees via vehicle
                if (trickRuns())
                {
                    // Trick is about to shoot it out with the player
                    API.TaskLeaveVehicle(prostitute.Handle, veh.Handle, 0);
                    API.TaskLeaveVehicle(trick.Handle, veh.Handle, 0);
                    trick.Weapons.Give(WeaponHash.Pistol, 50, true, true);
                    await BaseScript.Delay(1500);
                    prostitute.Task.ReactAndFlee(closest);
                    trick.Task.ShootAt(closest);
                }
                else
                {
                    // Trick is fleeing in the vehicle
                    API.TaskLeaveVehicle(prostitute.Handle, veh.Handle, 0);
                    await BaseScript.Delay(1500);
                    prostitute.Task.ReactAndFlee(closest);
                    var flee = Pursuit.RegisterPursuit(trick);
                    flee.Init(true, 150f, 150f, false);
                    flee.ActivatePursuit();
                }
            }
            await Task.FromResult(0);
        }

        public void ProstituteQuestions()
        {
            PedQuestion q1 = new PedQuestion();
            q1.Question = "What is going on here?";
            q1.Answers = new List<string>()
            {
                "Oh nothing just talking with my friend here",
                "Well we were having some fun until you showed up...",
                "Am I being detained or am I free to go?",
                "I'm going to be honest we were having sex"
            };

            PedQuestion q2 = new PedQuestion();
            q2.Question = "How do you two know eachother?";
            q2.Answers = new List<string>()
            {
                "I went to school with them",
                "I just needed a ride back home",
                "That's my brother",
                "We don't he paid me for my services"
            };

            PedQuestion q3 = new PedQuestion();
            q3.Question = "Do you know what their name is?";
            q3.Answers = new List<string>()
            {
                "Oh that's uh... Beni",
                "His name is Dan",
                "That isn't any of your business",
                "Trick #19 of the night"
            };

            PedQuestion q4 = new PedQuestion();
            q4.Question = "It seems like you are engaged in prostitution...";
            q4.Answers = new List<string>()
            {
                "ME? HA... Honey I couldn't sell water to a dehydrated person",
                "Well you should probably get your eyes checked then",
                "Am I under arrest?",
                "Yea you caught me... I JUST NEEDED THE MONEY!"
            };

            PedQuestion[] questions = new PedQuestion[] { q1, q2, q3, q4 };
            AddPedQuestions(prostitute, questions);
        }

        public void trickQuestions()
        {
            PedQuestion q1 = new PedQuestion();
            q1.Question = "What is going on here?";
            q1.Answers = new List<string>()
            {
                "You know you stinkin LSPD are always getting on my nerves, you heathens!",
                "Oh this is my friend we were just talking",
                "~y~*wink*",
                "I was feeling a little lonely..."
            };

            PedQuestion q2 = new PedQuestion();
            q2.Question = "How do you two know eachother?";
            q2.Answers = new List<string>()
            {
                "YOU ARE A HEATHEN!",
                "Work",
                "She just needed a ride",
                "I don't she is a prostitute"
            };

            PedQuestion q3 = new PedQuestion();
            q3.Question = "Do you know what their name is?";
            q3.Answers = new List<string>()
            {
                "It's first name 'LSPD', middle name 'ARE A BUNCH OF', last name HEATHENS!",
                "Oh Donna? Her and I got WAY back",
                "That there is your momma",
                "I really don't know officer"
            };

            PedQuestion q4 = new PedQuestion();
            q4.Question = "It seems like you are engaged in prostitution...";
            q4.Answers = new List<string>()
            {
                "It seems to me you are engaged in being a heathen",
                "NO WAY YOU GOT IT ALL WRONG",
                "I go to church every Sunday, I would never",
                "You caught me, i'm really sorry"
            };

            PedQuestion[] questions = new PedQuestion[] { q1, q2, q3, q4 };
            AddPedQuestions(trick, questions);
        }
    }
}
