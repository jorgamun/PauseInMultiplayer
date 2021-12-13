
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace PauseInMultiplayer
{
    public class PauseInMultiplayer : Mod
    {

        int timeInterval = -100;
        int foodDuration = -100;
        int drinkDuration = -100;

        string pauseTime = "false";
        IDictionary<long, string> pauseTimeAll;

        string pauseCommand = "false";

        public override void Entry(IModHelper helper)
        {

            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.UpdateTicking += GameLoop_UpdateTicking;

            Helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageReceived;
            Helper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;
            Helper.Events.Multiplayer.PeerDisconnected += Multiplayer_PeerDisconnected;
        }

        private void Multiplayer_PeerDisconnected(object? sender, StardewModdingAPI.Events.PeerDisconnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
                pauseTimeAll.Remove(e.Peer.PlayerID);
        }

        private void Multiplayer_PeerConnected(object? sender, StardewModdingAPI.Events.PeerConnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
                pauseTimeAll[e.Peer.PlayerID] = "false";
        }

        private void Multiplayer_ModMessageReceived(object? sender, StardewModdingAPI.Events.ModMessageReceivedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                if (e.FromModID == this.ModManifest.UniqueID && e.Type == "pauseTime")
                {
                    pauseTimeAll[e.FromPlayerID] = e.ReadAs<string>();
                }
            }
            else
            {
                if (e.FromModID == this.ModManifest.UniqueID && e.Type == "pauseCommand")
                {
                    pauseCommand = e.ReadAs<string>();
                }
            }

        }

        private void GameLoop_SaveLoaded(object? sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            //only the main player will use this dictionary
            if (Context.IsMainPlayer)
            {
                pauseTimeAll = new Dictionary<long, string>();
                pauseTimeAll[Game1.player.UniqueMultiplayerID] = "false";
            }

        }

        private void GameLoop_UpdateTicking(object? sender, StardewModdingAPI.Events.UpdateTickingEventArgs e)
        {
            //this mod does nothing if a game isn't running
            if (!Context.IsWorldReady) return;

            //set the pause time data to whether or not time should be paused for this player
            pauseTime = (!Context.IsPlayerFree) ? "true" : "false";

            if (!Context.CanPlayerMove)
                pauseTime = "true";

            if (Game1.currentMinigame != null)
                pauseTime = "true";

            if (Context.IsMainPlayer)
                pauseTimeAll[Game1.player.UniqueMultiplayerID] = pauseTime;
            else
                this.Helper.Multiplayer.SendMessage(pauseTime, "pauseTime", modIDs: new[] { this.ModManifest.UniqueID }, playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });


            //this logic only applies for the main player to control the state of the world
            if (Context.IsMainPlayer)
            {
                if (shouldPause())
                {
                    this.Helper.Multiplayer.SendMessage("true", "pauseCommand", new[] { this.ModManifest.UniqueID });

                    //save the last time interval, if it's not already saved
                    if (Game1.gameTimeInterval >= 0) timeInterval = Game1.gameTimeInterval;

                    Game1.gameTimeInterval = -100;

                    //pause all Characters
                    foreach (GameLocation location in Game1.locations)
                    {
                        //I don't know if the game stores null locations, and at this point I'm too afraid to ask
                        if (location == null) continue;

                        //pause all NPCs, doesn't seem to work for animals or monsters
                        foreach (Character character in location.characters)
                        {
                            character.movementPause = 1;
                        }

                        //pause all farm animals
                        if (location is Farm)
                            foreach (FarmAnimal animal in (location as Farm).getAllFarmAnimals())
                                animal.pauseTimer = 100;
                        else if (location is AnimalHouse)
                            foreach (FarmAnimal animal in (location as AnimalHouse).animals.Values)
                                animal.pauseTimer = 100;
                    }

                }
                else
                {

                    //reset time interval if it hasn't been fixed from the last pause
                    if (Game1.gameTimeInterval < 0)
                    {
                        this.Helper.Multiplayer.SendMessage("false", "pauseCommand", new[] { this.ModManifest.UniqueID });

                        Game1.gameTimeInterval = timeInterval;
                        timeInterval = -100;
                    }

                }
            }

            //pause food and drink buff durations must be run for each player independently
            if (shouldPause())
            {
                //set temporary duration locks if it has just become paused
                if (foodDuration < 0 && Game1.buffsDisplay.food != null)
                    foodDuration = Game1.buffsDisplay.food.millisecondsDuration;
                if (drinkDuration < 0 && Game1.buffsDisplay.drink != null)
                    drinkDuration = Game1.buffsDisplay.drink.millisecondsDuration;

                if (Game1.buffsDisplay.food != null)
                    Game1.buffsDisplay.food.millisecondsDuration = foodDuration;
                if (Game1.buffsDisplay.drink != null)
                    Game1.buffsDisplay.drink.millisecondsDuration = drinkDuration;
            }
            else
            {
                foodDuration = -100;
                drinkDuration = -100;
            }


        }

        private bool shouldPause()
        {
            if (Context.IsMainPlayer)
            {
                foreach (string pauseTime in pauseTimeAll.Values)
                    if (pauseTime == "false") return false;

                return true;
            }
            else
            {
                return pauseCommand == "false" ? false : true;
            }

        }
    }
}
