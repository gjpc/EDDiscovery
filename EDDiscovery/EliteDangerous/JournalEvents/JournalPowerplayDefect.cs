﻿using Newtonsoft.Json.Linq;
using System.Linq;

namespace EDDiscovery.EliteDangerous.JournalEvents
{
    //When written: when a player defects from one power to another
    //Parameters:
    //•	FromPower
    //•	ToPower
    public class JournalPowerplayDefect : JournalEntry
    {
        public JournalPowerplayDefect(JObject evt) : base(evt, JournalTypeEnum.PowerplayDefect)
        {
            FromPower = Tools.GetStringDef(evt["FromPower"]);
            ToPower = Tools.GetStringDef(evt["ToPower"]);

        }

        public string FromPower { get; set; }
        public string ToPower { get; set; }
    }
}