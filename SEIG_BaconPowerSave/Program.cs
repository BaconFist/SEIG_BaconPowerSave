using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Screens.Helpers.RadialMenuActions;
using Sandbox.Game.Weapons.Guns;
//using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ObjectBuilders.Gui;
using VRage.Scripting;
using VRageMath;
using VRageRender;

namespace SpaceEngineers
{
    internal class Program : MyGridProgram
    {
        //INGAME_SCRIPT BEGIN
        string HELP = @"Bacon Power Save - Help
 Args:
  p, plan plan_name 
    name/id of the powerplan defined in CustomData
    use * as plan_name to perform action on all plans (all activated plans + all plans configured in Custom Data)
    required
  a, activate 
    enable powerplan; turn power off
  d, deactivate
    disable powerplan; turn power on
  i, ignore blockname or type
    dont turn on or off blocks with argument in name or type 
    imnplicit: [plan_name]
  ls, list plans 
  ls -l, list plans with block ids
  help, show help
  clearcache true, clear plan cache
    


Example:
  turn off blocks but doors and jump drives:
    'p example_plan i door i jumpdrive'

plans can be stored in CustomData:
    BaconPowerSave:plan_name:arguments

Example: 
    BaconPoserSave:flight_ready:i Thruster i Gyroscope i Cockpit 
";
        const string CONFIG_PREFIX = @"BaconPowerSave";
        const string GLOBAL_TAG = "[BPS]";
        const string MATCH_ALL_PLANS = "*";

        Dictionary<string, List<string>> planCache = new Dictionary<string, List<string>>();


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            Load();
            
        }

        void Load()
        {
            string[] data = this.Storage.Split(Environment.NewLine.ToCharArray());
            Echo($"Program:{Storage}:Program");
            foreach (string s in data)
            {
                Echo($"s:{s}:s");
                using (var argv = new ArgParse(s))
                {
                    if (argv.argv.Count < 2)
                    {
                        continue;
                    }
                    string plan = argv.argv[0];
                    if (planCache.ContainsKey(plan))
                    {
                        continue;
                    }
                    planCache[plan] = new List<string>();
                    for (int i = 0; i < argv.argv.Count; i++)
                    {
                        planCache[plan].Add(argv.argv[i]);
                    }
                }
            }
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var key in planCache.Keys)
            {
                string data = $"\"{key}\" ";
                var idList = planCache[key];
                foreach(var id in idList)
                {
                    data += $" \"{id}\" ";
                }
                sb.AppendLine(data);
            }
            Echo($"SAVE:{sb.ToString()}:SAVE");
            this.Storage = sb.ToString();
        }

        //argument: name off powersave group
        public void Main(string argument, UpdateType updateType)
        {
            run(argument, updateType);
            //Echo(HELP);
        }

        
        public void run(string argument, UpdateType updateType)
        {
            var args = new ArgParse(argument);
            if (args.Contains("ls"))
            {
                PrintPlans(args);
                return;
            }
            if (args.Contains("help"))
            {
                Echo(HELP);
                return;
            }
            if(args.Contains("clearcache")) {
                string _a;
                if(args.TryGetValOfOption("clearcache", out _a) && _a == "true")
                {
                    ClearPlanCahe();
                    return;
                }
            }

            List<string> ignoreTags = new List<string>();
            string plan = "";
            if(!args.TryGetValOfOption("p", out plan) && !args.TryGetValOfOption("plan", out plan)){
                Echo("Error: no plan name. #argument: 'p' or 'plan'");
                return;
            }

            List<string> plans = new List<string>();

            if (plan == MATCH_ALL_PLANS)
            {
                plans.AddRange(findAllPlans());
            } 
            plans.AddRange(args.getAllValOfOpt("plan", new string[]{ MATCH_ALL_PLANS }));
            plans.AddRange(args.getAllValOfOpt("p", new string[] { MATCH_ALL_PLANS }));


            Echo($"used plans ({plans.Count}): {string.Join(";", plans.ToArray())};");

            bool activate = isActivate(args);
            bool deactivate = isDeactivate(args);
            Echo($"activate {activate}; deactivate: {deactivate}");

            foreach (var _plan in plans)
            {

                if (deactivate)
                {
                    deactivatePlan(_plan);
                    continue;
                }

                if (activate)
                {
                    using (ArgParse planArgs = new ArgParse(getPlanArgs(_plan)))
                    {
                        ignoreTags.AddList(args.getAllValOfOpt("i"));
                        ignoreTags.AddList(args.getAllValOfOpt("ignore"));
                        ignoreTags.AddList(planArgs.getAllValOfOpt("i"));
                        ignoreTags.AddList(planArgs.getAllValOfOpt("ignore"));
                        ignoreTags.Add($"[{_plan}]");
                        ignoreTags.Add(GLOBAL_TAG);
                    }
                    activatePlan(_plan, ignoreTags);
                    continue;
                }
            }

        }

        void ClearPlanCahe()
        {
            planCache.Clear();
            Storage = "";
            Save();
            Load();
        }

        void PrintPlans(ArgParse args)
        {
            string opt;
            args.TryGetValOfOption("ls", out opt);

            bool ll = (opt == "-l");

            foreach(var k in planCache.Keys)
            {
                Echo($"{k} {planCache[k].Count} Blocks");
                if (ll)
                {
                    foreach (var _block in planCache[k])
                    {
                        Echo($"....{_block}");
                    }
                }
            }
        }

        public List<string> findAllPlans()
        {
            List<string> buff = new List<string>();
            foreach (var key in planCache.Keys)
            {
                buff.Add(key);
            }

            string[] data = Me.CustomData.Split(Environment.NewLine.ToCharArray());
            foreach (string s in data)
            {
                string planprefix = $"{CONFIG_PREFIX}:";
                if (s.StartsWith(planprefix))
                {
                    int start = s.IndexOf(':');
                    int length = s.IndexOf(':', start+1) - start;
                    string planname = s.Substring(start, length);
                    buff.Add(planname);
                }
            }
            
            return buff;
        }

        private bool isActivate(ArgParse arg)
        {
            return (arg.Contains("a") || arg.Contains("activate"));
        }

        private bool isDeactivate(ArgParse arg)
        {
            return (arg.Contains("d") || arg.Contains("deactivate"));
        }

        public void activatePlan(string plan, List<string> ignoreTags)
        {
            if (!planCache.ContainsKey(plan))
            {
                planCache[plan] = new List<string>();
            }
            var blocks = findBlocksToTurnOff(ignoreTags.ToArray());
            foreach (var block in blocks)
            {
                string id = block.EntityId.ToString();
                block.ApplyAction("OnOff_Off");
                planCache[plan].Add(id);
            }
            Echo($"{plan}: turned off {blocks.Count} blocks");
        }

        public void deactivatePlan(string plan)
        {
            if (!planCache.ContainsKey(plan))
            {
                return;
            }
            foreach(string id in planCache[plan])
            {
                long blockId;
                if (!long.TryParse(id, out blockId)) {
                    Echo($"Error: can't parse ID <{id}>");
                }
                IMyTerminalBlock block = GridTerminalSystem.GetBlockWithId(blockId);
                block.ApplyAction("OnOff_On");
            }
            Echo($"{plan}: turned on {planCache[plan].Count} blocks");
            planCache[plan].Clear();
            planCache.Remove(plan);
        }

        public List<IMyTerminalBlock> findBlocksToTurnOff(string[] ignoreTags)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => {
                return
                    !b.Equals(Me)
                    && b.IsSameConstructAs(Me)
                    && b.IsWorking
                    && b.HasAction("OnOff_Off")
                    && b.GetValueBool("OnOff")
                    && !(b is IMyCryoChamber)
                    && !(b is IMySolarPanel)
                    && !(b is IMyBatteryBlock)
                    && !(b is IMyPowerProducer)
                    && !StringContainsAnyOf(b.CustomName, ignoreTags)
                    && !StringContainsAnyOf(b.BlockDefinition.SubtypeName, ignoreTags)
                ;
            });
            return blocks;
        }


        public bool StringContainsAnyOf(string text, string[] tags)
        {
            for(int i=0; i<tags.Length; i++)
            {
                if (text.Contains(tags[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private string getPlanArgs(string _plan)
        {
            string[] data = Me.CustomData.Split(Environment.NewLine.ToCharArray());
            foreach(string s in data)
            {
                string planprefix = $"{CONFIG_PREFIX}:{_plan}:";
                if (s.StartsWith(planprefix))
                {
                    return s.Substring(planprefix.Length);
                }
            }
            return "";
        }

        //LIB.Args
        internal class ArgParse : IDisposable
        {
            public List<string> argv;
            public ArgParse(string arguments)
            {
                argv = parse(arguments);
            }

            public bool Contains(string _arg)
            {
                return argv.Contains(_arg);
            }

            public void Dispose()
            {
                argv.Clear();
            }
            //, Array.Empty<string>()
            public List<string> getAllValOfOpt(string _option) => getAllValOfOpt(_option, Array.Empty<string>());
            public List<string> getAllValOfOpt(string _option, string[] excludeValues)
            {
                List<string> _tmp = new List<string>();
                int c = 0;
                string _value = "";
                while (TryGetValOfOption(_option, out _value, c++))
                {
                    if (!excludeValues.Contains(_value)) { 
                        _tmp.Add(_value);
                    }
                }
                return _tmp;
            }
            public bool TryGetValOfOption(string _option, out string _val, int number = 0)
            {
                _val = "";

                int index = -1;
                for (int i=0; i <= number; i++)
                {
                    index = argv.IndexOf(_option, index+1);
                    if(index == -1)
                    {
                        return false;
                    }
                }
                                
                if (index >= 0 && index+1 < argv.Count)
                {
                    _val = argv[index+1];
                    return true;
                }
                return false;
            }

            private List<string> parse(string arguments)
            {
                bool isEscaped = false;
                bool isString = false;
                char c_escape = '\\';
                char c_string = '"';
                char c_split = ' ';

                List<string> argv = new List<string>();
                string argTmp = "";

                for(int i=0;i<arguments.Length; i++)
                {
                    char glyph = arguments[i];
                    if(isEscaped)
                    {
                        isEscaped = false;
                        argTmp += glyph;
                        continue;
                    }

                    if (glyph == c_escape)
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (glyph == c_string)
                    {
                        if (isString)
                        {
                            isString = false;
                        }
                        else
                        {
                            isString = true;
                        }
                        continue;
                    }

                    if (isString)
                    {
                        argTmp += glyph;
                        continue;
                    }                   

                    if(glyph == c_split)
                    {
                        if (argTmp.Length > 0)
                        {
                            argv.Add(argTmp);
                            argTmp = "";
                        }
                        continue;
                    }

                    argTmp += glyph;
                }
                if (argTmp.Length > 0)
                {
                    argv.Add(argTmp);
                }
                return argv;
            }
        }
        //INGAME_SCRIPT END
    }
}