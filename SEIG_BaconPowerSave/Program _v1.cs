using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Screens.Helpers.RadialMenuActions;
//using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRageMath;
using VRageRender;

namespace SpaceEngineers
{
    internal class Program : MyGridProgram
    {



        //INGAME_SCRIPT BEGIN
        //
        /*
            How To Use:
            ===========
            Script is controlled by arguments.
            call `myPowerplan` everything but blocks with [BPS] or [BPS:myPowerplan] will be turned off
            call `revert:myPowerplan` and every block that was turned off for this powerplan will be turned on again

           Notes
           =====
           - Script only operates on same construct. (everyhing mechanically connected, but not connectors)
           - On the PB's display it shows you what it has done.
           - it wont turn itself off
           - it ignores CryoChambers and Solarpanels
        */
        bool autoPowerSave = false;
        float autoPowerSaveMin = 0.2f;
        float autoPowerSaveMax = 0.6f;
        string revertCmdPrefix = "revert:";
        Dictionary<string, List<long>> powerPlans = new Dictionary<string, List<long>>();
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            if (autoPowerSave)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }
        }

        public void Save()

        {

        }

        //argument: name off powersave group
        public void Main(string argument, UpdateType updateType)
        {
            
            try
            {
                if (updateType.HasFlag(UpdateType.Update100))
                {
                  //  autoPlan();
                } else if (argument.Length > 0)
                {
                    loadPlans();
                    if (argument.StartsWith(revertCmdPrefix))
                    {
                        revertPlan(argument.Substring(revertCmdPrefix.Length));
                    } else {
                        applyPlan(argument);
                    }
                    savePlans();
                }
                else
                {

                }
            } catch (Exception e)
            {
                Echo("Exception:" + e.Message);
            }
        }
        private void applyPlan(string planName)
        {
            revertActivePlans();
            if (!powerPlans.ContainsKey(planName))
            {
                powerPlans.Add(planName, new List<long>());
            }

            List<string> log = new List<string>();
            string planNameTag = @"[BPS:" + planName + @"]";
            string globalNameTag = @"[BPS]";
            List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(allBlocks);
            foreach (IMyTerminalBlock block in allBlocks)
            {
                if (
                    !block.Equals(Me)
                    && block.IsSameConstructAs(Me)
                    && block.HasAction("OnOff_Off")
                    && !block.CustomName.Contains(planNameTag)
                    && !block.CustomName.Contains(globalNameTag)
                    && block.GetValueBool("OnOff")
                    && !(block is IMyCryoChamber)
                    && !(block is IMySolarPanel)
                    && !(block is IMyBatteryBlock)
                    && !(block is IMyPowerProducer)
                )
                {
                    block.ApplyAction("OnOff_Off");
                    powerPlans[planName].Add(block.EntityId);
                    log.Add(block.CustomName);
                }
            }
            writeToLcd($"[{DateTime.Now.ToString()}] applied `{planName}` blocks set to `Off`", log);
        }

        private void revertActivePlans()
        {
            foreach(string plan in powerPlans.Keys)
            {
                revertPlan(plan);
            }
        }
        private void revertPlan(string planName)
        {
            List<string> log = new List<string>();
            if (powerPlans.ContainsKey(planName))
            {
                foreach (long id in powerPlans[planName])
                {
                    IMyTerminalBlock block = GridTerminalSystem.GetBlockWithId(id);
                    if (block != null)
                    {
                        block.ApplyAction("OnOff_On");
                        log.Add(block.CustomName);
                    }
                }
                powerPlans.Remove(planName);
                writeToLcd($"[{DateTime.Now.ToString()}] reverted `{planName}` blocks set to `On`", log);
            }
        }
   /*
        private void autoPlan()
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(blocks);
            foreach(IMyTerminalBlock block in blocks)
            {
                if(block is IMyPowerProducer && block.IsWorking)
                {

                    continue;
                }
                if(block is IMyFunctionalBlock && block.IsWorking) {
                    (block as IMyFunctionalBlock).GetValueFloat();
                    continue;
                }
            }
        }
   */
        private void loadPlans()
        {
            powerPlans.Clear();
            foreach (string line in Me.CustomData.Split('\n', '\r')) {
                if (!line.Contains(':'))
                {
                    continue;
                }
                string[] data = line.Split(':');
                string name = data[0];
                string[] blockIds = data[1].Split(';');
                if (!powerPlans.ContainsKey(name))
                {
                    powerPlans.Add(name, new List<long>());
                }
                foreach(string id in blockIds)
                {
                    long matchId = 0;
                    if(long.TryParse(id, out matchId))
                    {
                        powerPlans[name].Add(matchId);
                    }
                }
            }
        }

        private void savePlans()
        {
            StringBuilder customData = new StringBuilder();
            foreach(string name in powerPlans.Keys)
            {
                customData.AppendLine($"{name}:{string.Join(";", (powerPlans[name].ConvertAll<string>(x => x.ToString())))}");
            }
            Me.CustomData = customData.ToString();
        }

        private void writeToLcd(string line, List<string> parameters)
        {
            IMyTextSurface lcd = Me.GetSurface(0);
            string content = "";
            float width = lcd.SurfaceSize.X;
            string wipLine = "";
            foreach(string s in parameters)
            {
                string newLine = $"{wipLine} `{s}`";
                if(lcd.MeasureStringInPixels(new StringBuilder(newLine), lcd.Font, lcd.FontSize).X < width)
                {
                    wipLine = newLine;
                } else
                {
                    content += wipLine + " \\\n";
                    wipLine = "";
                }
            }
                        
            content = '\n' + line + "\\\n" + content + lcd.GetText();
            
            if(content.Length > 100000)
            {
                content = content.Substring(0, 100000);
            }
            lcd.WriteText(content);
            lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        }

   
        //INGAME_SCRIPT END
    }
}