
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

        bool shouldPauseLast = false;

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
            var pauseTime2 = (!Context.IsPlayerFree) ? "true" : "false";

            if (!Context.CanPlayerMove)
                pauseTime2 = "true";

            //checks to see if the fishing rod has been cast. If this is true but the player is in the fishing minigame, the next if statement will pause - otherwise it won't
            if (Game1.player.CurrentItem != null && Game1.player.CurrentItem is StardewValley.Tools.FishingRod && (Game1.player.CurrentItem as StardewValley.Tools.FishingRod).isFishing)
                pauseTime2 = "false";

            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is StardewValley.Menus.BobberBar)
                pauseTime2 = "true";

            if (Game1.currentMinigame != null)
                pauseTime2 = "true";

            if (Context.IsMainPlayer)
            {
                //host
                if(pauseTime != pauseTime2)
                {
                    pauseTime = pauseTime2;
                    pauseTimeAll[Game1.player.UniqueMultiplayerID] = pauseTime;
                }
                
            }
                
            else
            {
                //client
                if(pauseTime != pauseTime2)
                {
                    pauseTime = pauseTime2;
                    this.Helper.Multiplayer.SendMessage(pauseTime, "pauseTime", modIDs: new[] { this.ModManifest.UniqueID }, playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
                }
            }

            var shouldPauseNow = shouldPause();

            //this logic only applies for the main player to control the state of the world
            if (Context.IsMainPlayer)
            {
                if (shouldPauseNow)
                {

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

                        Game1.gameTimeInterval = timeInterval;
                        timeInterval = -100;
                    }

                }

                if(shouldPauseNow != shouldPauseLast)
                    this.Helper.Multiplayer.SendMessage(shouldPauseNow ? "true" : "false", "pauseCommand", new[] { this.ModManifest.UniqueID });
                
                shouldPauseLast = shouldPauseNow;
            }

            //pause food and drink buff durations must be run for each player independently
            if (shouldPauseNow)
            {
                //set temporary duration locks if it has just become paused and/or update Duration if new food is consumed during pause
                if (Game1.buffsDisplay.food != null && Game1.buffsDisplay.food.millisecondsDuration > foodDuration)
                    foodDuration = Game1.buffsDisplay.food.millisecondsDuration;
                if (Game1.buffsDisplay.drink != null && Game1.buffsDisplay.drink.millisecondsDuration > drinkDuration)
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
                return pauseCommand == "true" ? true : false;
            }

        }
    }
}
