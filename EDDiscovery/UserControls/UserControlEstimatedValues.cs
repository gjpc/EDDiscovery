﻿/*
 * Copyright © 2016 - 2017 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Threading;
using EliteDangerousCore;
using EliteDangerousCore.EDSM;
using EliteDangerousCore.DB;

namespace EDDiscovery.UserControls
{
    public partial class UserControlEstimatedValues : UserControlCommonBase
    {
        private EDDiscoveryForm _discoveryForm;
        private UserControlCursorType uctg;
        private HistoryEntry last_he = null;

        public UserControlEstimatedValues()
        {
            InitializeComponent();
        }

        public override void Init(EDDiscoveryForm ed, UserControlCursorType thc, int vn) //0=primary, 1 = first windowed version, etc
        {
            _discoveryForm = ed;
            uctg = thc;
            uctg.OnTravelSelectionChanged += Uctg_OnTravelSelectionChanged;
            ed.OnNewEntry += NewEntry;

        }

        public override void Closing()
        {
            uctg.OnTravelSelectionChanged -= Uctg_OnTravelSelectionChanged;
            _discoveryForm.OnNewEntry -= NewEntry;
        }

        public void NewEntry(HistoryEntry he, HistoryList hl)               // called when a new entry is made.. check to see if its a scan update
        {
            // if he valid, and last is null, or not he, or we have a new scan
            if (he != null && (last_he == null || he != last_he || he.EntryType == JournalTypeEnum.Scan))
            {
                last_he = he;
                DrawSystem();
            }
        }

        public override void Display(HistoryEntry he, HistoryList hl)            // when user clicks around..
        {
            if (he != null && (last_he == null || he.System != last_he.System))
            {
                last_he = he;
                DrawSystem();
            }
        }

        void DrawSystem()   // draw last_sn, last_he
        {
            dataGridViewNearest.Rows.Clear();

            if (last_he == null)
            {
                SetControlText("No Scan");
                return;
            }

            StarScan.SystemNode last_sn = _discoveryForm.history.starscan.FindSystem(last_he.System, true);

            SetControlText((last_sn == null) ? "No Scan" : ("Estimated Scan Values for " + last_sn.system.name));

            if (last_sn != null)
            {
                List<StarScan.ScanNode> all_nodes = new List<StarScan.ScanNode>();
                foreach (StarScan.ScanNode starnode in last_sn.starnodes.Values)
                {
                    all_nodes = Flatten(starnode, all_nodes);
                }

                // flatten tree of scan nodes to prepare for listing
                foreach(StarScan.ScanNode sn in all_nodes)
                {
                    // no ScanData for barycentres or belts
                    try
                    {
                        dataGridViewNearest.Rows.Add(new object[] { sn.ScanData.BodyName, sn.ScanData.EstimatedValue() });
                    }
                    catch
                    {
                        // System.Diagnostics.Debug.WriteLine("failed to add row with fullname " + sn.fullname);
                    }
                }

                dataGridViewNearest.Sort(this.EstValue, ListSortDirection.Descending);
            }
        }

        private List<StarScan.ScanNode> Flatten(StarScan.ScanNode sn, List<StarScan.ScanNode> flattened)
        {
            flattened.Add(sn);
            if (sn.children != null)
            {
                foreach (StarScan.ScanNode node in sn.children.Values)
                {
                    Flatten(node, flattened);
                }
            }
            return flattened;
        }

        private void Uctg_OnTravelSelectionChanged(HistoryEntry he, HistoryList hl)
        {
            if (he != null)
            {
                dataGridViewNearest.Rows.Clear();
            }
        }
    }
}
