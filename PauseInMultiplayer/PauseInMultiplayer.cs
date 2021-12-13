
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

        public override void Entry(IModHelper helper)
        {

            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.UpdateTicking += GameLoop_UpdateTicking;
        }

        private void GameLoop_SaveLoaded(object? sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            //initialize the pause time key as false for this player
            Game1.player.modData["jorgamun/pauseTime"] = "false";
        }

        private void GameLoop_UpdateTicking(object? sender, StardewModdingAPI.Events.UpdateTickingEventArgs e)
        {
            //this mod does nothing if a game isn't running
            if (!Context.IsWorldReady) return;

            //set the pause time data to whether or not time should be paused for this player
            Game1.player.modData["jorgamun/pauseTime"] = (!Context.IsPlayerFree) ? "true" : "false";
            if (Game1.currentMinigame != null)
                Game1.player.modData["jorgamun/pauseTime"] = "true";

            //this logic only applies for the main player to control the state of the world
            if (Context.IsMainPlayer)
            {
                if(shouldPause())
                {
                    //save the last time interval, if it's not already saved
                    if (Game1.gameTimeInterval >= 0) timeInterval = Game1.gameTimeInterval;
                    
                    Game1.gameTimeInterval = -100;

                    //pause all Characters
                    foreach(GameLocation location in Game1.locations)
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
                        else if(location is AnimalHouse)
                            foreach (FarmAnimal animal in (location as AnimalHouse).animals.Values)
                                animal.pauseTimer = 100;
                    }

                }
                else
                {
                    
                    //reset time interval if it hasn't been fixed from the last pause
                    if(Game1.gameTimeInterval < 0)
                    {
                        Game1.gameTimeInterval = timeInterval;
                        timeInterval = -100;
                    }

                }
            }

            //pause food and drink buff durations must be run for each player independently
            if(shouldPause())
            {
                //set temporary duration locks if it has just become paused
                if (foodDuration < 0 && Game1.buffsDisplay.food != null)
                    foodDuration = Game1.buffsDisplay.food.millisecondsDuration;
                if (drinkDuration < 0 && Game1.buffsDisplay.drink != null)
                    drinkDuration = Game1.buffsDisplay.drink.millisecondsDuration;

                if(Game1.buffsDisplay.food != null)
                    Game1.buffsDisplay.food.millisecondsDuration = foodDuration;
                if(Game1.buffsDisplay.drink != null)
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

            //check this player first, as using getAllFarmers seems to be broken or I don't understand its purpose
            if (Game1.player.modData["jorgamun/pauseTime"] == "false")
                return false;

            //check online farmers
            foreach (Farmer f in Game1.getOnlineFarmers())
                if (f.modData["jorgamun/pauseTime"] == "false")
                    return false;

            return true;
        }
    }
}
