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
using EDDiscovery.Controls;
using EliteDangerousCore.DB;
using EliteDangerousCore;
using EliteDangerousCore.EDSM;
using EliteDangerousCore.EDDN;
using EDDiscovery.Export;

namespace EDDiscovery.UserControls
{
    public partial class UserControlStarList : UserControlCommonBase, UserControlCursorType
    {
        #region Public IF

        public HistoryEntry GetCurrentHistoryEntry { get { return dataGridViewStarList.CurrentCell != null ?
                    (dataGridViewStarList.Rows[dataGridViewStarList.CurrentCell.RowIndex].Tag as List<HistoryEntry>)[0] : null; } }

        #endregion

        #region Events

        // implement UserControlCursorType fields
        public event ChangedSelection OnChangedSelection;   // After a change of selection by the user, or after a OnHistoryChanged, or after a sort.
        public event ChangedSelectionHE OnTravelSelectionChanged;   // as above, different format, for certain older controls

        #endregion

        #region Init

        private class StarHistoryColumns
        {
            public const int LastVisit = 0;
            public const int StarName = 1;
            public const int NoVisits = 2;
            public const int OtherInformation = 3;
        }

        private const int DefaultRowHeight = 26;

        private static EDDiscoveryForm discoveryform;
        private int displaynumber;

        private string DbColumnSave { get { return "StarListControl" + ((displaynumber > 0) ? displaynumber.ToString() : "") + "DGVCol"; } }
        private string DbHistorySave { get { return "StarListControlEDUIHistory" + ((displaynumber > 0) ? displaynumber.ToString() : ""); } }
        private string DbAutoTop { get { return "StarListControlAutoTop" + ((displaynumber > 0) ? displaynumber.ToString() : ""); } }
        private string DbEDSM { get { return "StarListControlEDSM" + ((displaynumber > 0) ? displaynumber.ToString() : ""); } }

        private Dictionary<string, List<HistoryEntry>> systemsentered = new Dictionary<string, List<HistoryEntry>>();
        private Dictionary<long, DataGridViewRow> rowsbyjournalid = new Dictionary<long, DataGridViewRow>();
        private HistoryList current_historylist;

        public UserControlStarList()
        {
            InitializeComponent();
        }

        public override void Init(EDDiscoveryForm ed, UserControlCursorType tg, int vn) // TG is not used.
        {
            discoveryform = ed;
            displaynumber = vn;
            TravelHistoryFilter.InitaliseComboBox(comboBoxHistoryWindow, DbHistorySave);

            checkBoxMoveToTop.Checked = SQLiteConnectionUser.GetSettingBool(DbAutoTop, true);

            discoveryform.OnHistoryChange += HistoryChanged;
            discoveryform.OnNewEntry += AddNewEntry;

            dataGridViewStarList.MakeDoubleBuffered();
            dataGridViewStarList.RowTemplate.Height = DefaultRowHeight;
            dataGridViewStarList.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewStarList.RowTemplate.Height = 26;
            dataGridViewStarList.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;     // NEW! appears to work https://msdn.microsoft.com/en-us/library/74b2wakt(v=vs.110).aspx

            checkBoxEDSM.Checked = SQLiteDBClass.GetSettingBool(DbEDSM, false);
        }

        public override void LoadLayout()
        {
          //  DGVLoadColumnLayout(dataGridViewStarList, DbColumnSave);
        }

        public override void Closing()
        {
            DGVSaveColumnLayout(dataGridViewStarList, DbColumnSave);
            discoveryform.OnHistoryChange -= HistoryChanged;
            discoveryform.OnNewEntry -= AddNewEntry;
            SQLiteConnectionUser.PutSettingBool(DbAutoTop, checkBoxMoveToTop.Checked);
        }

        #endregion

        public override void Display(HistoryEntry he, HistoryList hl)       // initial caller..
        {
            HistoryChanged(hl);
        }

        public void HistoryChanged(HistoryList hl)           // on History change
        {
            if (hl == null)     // just for safety
                return;

            Tuple<long, int> pos = CurrentGridPosByJID();

            current_historylist = hl;
            rowsbyjournalid.Clear();
            systemsentered.Clear();
            dataGridViewStarList.Rows.Clear();

            var filter = (TravelHistoryFilter)comboBoxHistoryWindow.SelectedItem ?? TravelHistoryFilter.NoFilter;

            List<HistoryEntry> result = filter.Filter(hl);      // last entry, first in list
            result = HistoryList.FilterHLByTravel(result);      // keep only travel entries (location after death, FSD jumps)


            foreach (HistoryEntry he in result)        // last first..
            {
                if (!systemsentered.ContainsKey(he.System.name))
                    systemsentered[he.System.name] = new List<HistoryEntry>();

                systemsentered[he.System.name].Add(he);     // first entry is newest jump to, second is next last, etc
            }

            foreach( List<HistoryEntry> syslist in systemsentered.Values ) // will be in order of entry..
            {
                AddNewHistoryRow(false, syslist);      // add, with the properties of the first (latest) entry, giving the number of entries..
            }

            StaticFilters.FilterGridView(dataGridViewStarList, textBoxFilter.Text);

            int rowno = FindGridPosByJID(pos.Item1, true);     // find row.. must be visible..  -1 if not found/not visible

            if (rowno >= 0)
            {
                dataGridViewStarList.CurrentCell = dataGridViewStarList.Rows[rowno].Cells[pos.Item2];       // its the current cell which needs to be set, moves the row marker as well            currentGridRow = (rowno!=-1) ? 
            }
            else if (dataGridViewStarList.Rows.GetRowCount(DataGridViewElementStates.Visible) > 0)
            {
                rowno = dataGridViewStarList.Rows.GetFirstRow(DataGridViewElementStates.Visible);
                dataGridViewStarList.CurrentCell = dataGridViewStarList.Rows[rowno].Cells[StarHistoryColumns.StarName];
            }
            else
                rowno = -1;

            dataGridViewStarList.Columns[0].HeaderText = EDDiscoveryForm.EDDConfig.DisplayUTC ? "Game Time" : "Time";

            //System.Diagnostics.Debug.WriteLine("Fire HC");

            FireChangeSelection();      // and since we repainted, we should fire selection, as we in effect may have selected a new one
        }

        private void AddNewEntry(HistoryEntry he, HistoryList hl)           // on new entry from discovery system
        {
            if ( he.IsFSDJump )     // FSD jumps mean move system.. so
                HistoryChanged(hl); // just recalc all..

            if (checkBoxMoveToTop.Checked && dataGridViewStarList.DisplayedRowCount(false) > 0)   // Move focus to new row
            {
                //System.Diagnostics.Debug.WriteLine("Auto Sel");
                dataGridViewStarList.ClearSelection();
                dataGridViewStarList.CurrentCell = dataGridViewStarList.Rows[0].Cells[1];       // its the current cell which needs to be set, moves the row marker as well

                FireChangeSelection();
            }
        }

        private void AddNewHistoryRow(bool insert, List<HistoryEntry> syslist)            // second part of add history row, adds item to view.
        {
            //string debugt = item.Journalid + "  " + item.System.id_edsm + " " + item.System.GetHashCode() + " "; // add on for debug purposes to a field below

            HistoryEntry he = syslist[0];

            object[] rowobj = { EDDiscoveryForm.EDDConfig.DisplayUTC ? he.EventTimeUTC : he.EventTimeLocal, he.System.name, syslist.Count.ToStringInvariant(), Infoline(syslist) };

            int rownr;
            if (insert)
            {
                dataGridViewStarList.Rows.Insert(0, rowobj);
                rownr = 0;
            }
            else
            {
                dataGridViewStarList.Rows.Add(rowobj);
                rownr = dataGridViewStarList.Rows.Count - 1;
            }

            foreach( HistoryEntry hel in syslist )
                rowsbyjournalid[hel.Journalid] = dataGridViewStarList.Rows[rownr];      // all JIDs in this array, to this row

            dataGridViewStarList.Rows[rownr].Tag = syslist;

            dataGridViewStarList.Rows[rownr].DefaultCellStyle.ForeColor = (he.System.HasCoordinate || he.EntryType != JournalTypeEnum.FSDJump) ? discoveryform.theme.VisitedSystemColor : discoveryform.theme.NonVisitedSystemColor;

            string tip = he.EventSummary + Environment.NewLine + he.EventDescription + Environment.NewLine + he.EventDetailedInfo;

            dataGridViewStarList.Rows[rownr].Cells[0].Tag = false;  //[0] records if checked EDSm

            dataGridViewStarList.Rows[rownr].Cells[0].ToolTipText = tip;
            dataGridViewStarList.Rows[rownr].Cells[1].ToolTipText = tip;
            dataGridViewStarList.Rows[rownr].Cells[2].ToolTipText = tip;
            dataGridViewStarList.Rows[rownr].Cells[3].ToolTipText = tip;
        }

        string Infoline(List<HistoryEntry> syslist)
        {
            string infostr = "";

            if (syslist.Count > 1)
                infostr = "First visit " + syslist.Last().EventTimeLocal.ToShortDateString();

            HistoryEntry he = syslist[0];
            StarScan.SystemNode node = discoveryform.history.starscan?.FindSystem(he.System);

            if (node != null)
            {
                if (node.starnodes != null)
                {
                    infostr = infostr.AppendPrePad(node.starnodes.Count.ToStringInvariant() + " Star" + ((node.starnodes.Count > 1) ? "s" : ""), Environment.NewLine);

                    int total = 0;
                    foreach (StarScan.ScanNode sn in node.Bodies)
                        total++;

                    total -= node.starnodes.Count;
                    if (total > 0)
                        infostr = infostr.AppendPrePad(total.ToStringInvariant() + " Other bod" + ((total > 1) ? "ies" : "y"), ", ");
                }
            }

            return infostr;
        }

        Tuple<long, int> CurrentGridPosByJID()          // Returns JID, column index.  JID = -1 if cell is not defined
        {
            long jid = (dataGridViewStarList.CurrentCell != null) ? (dataGridViewStarList.Rows[dataGridViewStarList.CurrentCell.RowIndex].Tag as List<HistoryEntry>)[0].Journalid : -1;
            int cellno = (dataGridViewStarList.CurrentCell != null) ? dataGridViewStarList.CurrentCell.ColumnIndex : 0;
            return new Tuple<long, int>(jid, cellno);
        }

        int FindGridPosByJID(long jid, bool checkvisible)
        {
            if (rowsbyjournalid.ContainsKey(jid) && (!checkvisible || rowsbyjournalid[jid].Visible))
                return rowsbyjournalid[jid].Index;
            else
                return -1;
        }

        public void GotoPosByJID(long jid)      // uccursor requirement
        {
            int rowno = FindGridPosByJID(jid, true);
            if (rowno >= 0)
            {
                dataGridViewStarList.CurrentCell = dataGridViewStarList.Rows[rowno].Cells[StarHistoryColumns.StarName];
                dataGridViewStarList.Rows[rowno].Selected = true;
                FireChangeSelection();
            }
        }

        public void CheckEDSM()
        {
            if (dataGridViewStarList.CurrentCell != null)
            {
                DataGridViewRow row = dataGridViewStarList.CurrentRow;
                List<HistoryEntry> syslist = row.Tag as List<HistoryEntry>;

                if ((bool)row.Cells[0].Tag == false && checkBoxEDSM.Checked)
                {
                    discoveryform.history.starscan?.FindSystem(syslist[0].System, true);  // try an EDSM lookup
                    row.Cells[StarHistoryColumns.OtherInformation].Value = Infoline(syslist);
                    row.Cells[0].Tag = true;
                }
            }
        }

        private void comboBoxHistoryWindow_SelectedIndexChanged(object sender, EventArgs e)
        {
            SQLiteDBClass.PutSettingString(DbHistorySave, comboBoxHistoryWindow.Text);

            if (current_historylist != null)
                HistoryChanged(current_historylist);        // fires lots of events
        }

        private void dataGridViewTravel_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            DataGridViewSorter.DataGridSort(dataGridViewStarList, e.ColumnIndex);
            FireChangeSelection();
        }

        public void FireChangeSelection() // uccursor requirement
        {
            if (dataGridViewStarList.CurrentCell != null)
            {
                int row = dataGridViewStarList.CurrentCell.RowIndex;
                //System.Diagnostics.Debug.WriteLine("Fire Change Sel row" + row);
                if (OnChangedSelection != null)
                    OnChangedSelection(row, dataGridViewStarList.CurrentCell.ColumnIndex, false, false);
                if (OnTravelSelectionChanged != null)
                    OnTravelSelectionChanged((dataGridViewStarList.Rows[row].Tag as List<HistoryEntry>)[0], current_historylist);
            }
        }

        private void dataGridViewTravel_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            CheckEDSM();
            FireChangeSelection();
        }

        int keyrepeatcount = 0;     // 1 is first down, 2 is second.  on 2+ we call the check selection to update the screen.  The final key up finished the job.

        private void dataGridViewTravel_KeyDown(object sender, KeyEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("Key down " + e.KeyCode + " " + dataGridViewTravel.CurrentCell.RowIndex + ":" + dataGridViewTravel.CurrentCell.ColumnIndex);
            keyrepeatcount++;

            if (keyrepeatcount > 1)
                CheckForSelection(e.KeyCode);

            //System.Diagnostics.Debug.WriteLine("KC " + (int)e.KeyCode + " " + (int)e.KeyData + " " + e.KeyValue);
        }

        private void dataGridViewTravel_KeyPress(object sender, KeyPressEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("KP " + (int)e.KeyChar);
        }

        private void dataGridViewTravel_KeyUp(object sender, KeyEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("Key up " + e.KeyCode + " " + dataGridViewTravel.CurrentCell.RowIndex + ":" + dataGridViewTravel.CurrentCell.ColumnIndex);
            CheckForSelection(e.KeyCode);
            keyrepeatcount = 0;
        }

        void CheckForSelection(Keys code)
        {
            bool cursorkeydown = (code == Keys.Up || code == Keys.Down || code == Keys.PageDown || code == Keys.PageUp || code == Keys.Left || code == Keys.Right);

            if (cursorkeydown)
            {
                CheckEDSM();
                FireChangeSelection();
            }
        }

        private void textBoxFilter_TextChanged(object sender, EventArgs e)
        {
            Tuple<long, int> pos = CurrentGridPosByJID();

            StaticFilters.FilterGridView(dataGridViewStarList, textBoxFilter.Text);

            int rowno = FindGridPosByJID(pos.Item1, true);
            if (rowno >= 0)
                dataGridViewStarList.CurrentCell = dataGridViewStarList.Rows[rowno].Cells[pos.Item2];
        }


        #region Clicks

        HistoryEntry rightclicksystem = null;
        int rightclickrow = -1;
        HistoryEntry leftclicksystem = null;
        int leftclickrow = -1;

        private void dataGridViewTravel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)         // right click on travel map, get in before the context menu
            {
                rightclicksystem = null;
                rightclickrow = -1;
            }
            if (e.Button == MouseButtons.Left)         // right click on travel map, get in before the context menu
            {
                leftclicksystem = null;
                leftclickrow = -1;
            }

            if (dataGridViewStarList.SelectedCells.Count < 2 || dataGridViewStarList.SelectedRows.Count == 1)      // if single row completely selected, or 1 cell or less..
            {
                DataGridView.HitTestInfo hti = dataGridViewStarList.HitTest(e.X, e.Y);
                if (hti.Type == DataGridViewHitTestType.Cell)
                {
                    dataGridViewStarList.ClearSelection();                // select row under cursor.
                    dataGridViewStarList.Rows[hti.RowIndex].Selected = true;

                    if (e.Button == MouseButtons.Right)         // right click on travel map, get in before the context menu
                    {
                        rightclickrow = hti.RowIndex;
                        rightclicksystem = (dataGridViewStarList.Rows[hti.RowIndex].Tag as List<HistoryEntry>)[0];
                    }
                    if (e.Button == MouseButtons.Left)         // right click on travel map, get in before the context menu
                    {
                        leftclickrow = hti.RowIndex;
                        leftclicksystem = (dataGridViewStarList.Rows[hti.RowIndex].Tag as List<HistoryEntry>)[0];
                    }
                }
            }
        }

        private void dataGridViewTravel_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        #endregion

        #region TravelHistoryRightClick

        private void historyContextMenu_Opening(object sender, CancelEventArgs e)
        {
            if (dataGridViewStarList.SelectedCells.Count == 0)      // need something selected  stops context menu opening on nothing..
                e.Cancel = true;

            HistoryEntry prev = discoveryform.history.PreviousFrom(rightclicksystem, true);    // null can be passed in safely

            mapGotoStartoolStripMenuItem.Enabled = (rightclicksystem != null && rightclicksystem.System.HasCoordinate);
            viewOnEDSMToolStripMenuItem.Enabled = (rightclicksystem != null);
        }

        private void mapGotoStartoolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!discoveryform.Map.Is3DMapsRunning)            // if not running, click the 3dmap button
                discoveryform.Open3DMap(GetCurrentHistoryEntry);

            if (discoveryform.Map.Is3DMapsRunning)             // double check here! for paranoia.
            {
                if (discoveryform.Map.MoveToSystem(rightclicksystem.System))
                    discoveryform.Map.Show();
            }
        }

        private void viewOnEDSMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            EliteDangerousCore.EDSM.EDSMClass edsm = new EDSMClass();
            long? id_edsm = rightclicksystem.System?.id_edsm;

            if (id_edsm <= 0)
            {
                id_edsm = null;
            }

            if (!edsm.ShowSystemInEDSM(rightclicksystem.System.name, id_edsm))
                ExtendedControls.MessageBoxTheme.Show("System could not be found - has not been synched or EDSM is unavailable");

            this.Cursor = Cursors.Default;
        }

        private void setNoteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (rightclicksystem != null)
            {
                using (Forms.SetNoteForm noteform = new Forms.SetNoteForm(rightclicksystem, discoveryform))
                {
                    if (noteform.ShowDialog(this) == DialogResult.OK)
                    {
                        rightclicksystem.SetJournalSystemNoteText(noteform.NoteText, true, EDCommander.Current.SyncToEdsm);

                        discoveryform.NoteChanged(this, rightclicksystem, true);
                    }
                }
            }
        }

        #endregion

        #region Excel

        private void buttonExtExcel_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();

            dlg.Filter = "CSV export| *.csv";
            dlg.Title = "Export current History view to Excel (csv)";
                            // 0        1       2           3            4               5           6           7             8              9              10              11              12          
            string[] colh = { "Time", "System", "Visits", "Other Info" , "Visit List" , "Body", "Ship" ,  "Description", "Detailed Info", "Travel Dist", "Travel Time" , "Travel Jumps" , "Travelled MisJumps" };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                Export.ExportGrid grd = new ExportGrid();
                grd.onGetCell += delegate (int r, int c)
                {
                    if (c == -1)    // next line?
                        return r < dataGridViewStarList.Rows.Count;
                    else if (c < colh.Length && dataGridViewStarList.Rows[r].Visible)
                    {
                        List<HistoryEntry> syslist = dataGridViewStarList.Rows[r].Tag as List<HistoryEntry>;
                        HistoryEntry he = syslist[0];

                        if (c == 0)
                            return dataGridViewStarList.Rows[r].Cells[0].Value;
                        else if (c == 1)
                            return dataGridViewStarList.Rows[r].Cells[1].Value;
                        else if (c == 2)
                            return dataGridViewStarList.Rows[r].Cells[2].Value;
                        else if (c == 3)
                            return dataGridViewStarList.Rows[r].Cells[3].Value;
                        else if (c == 4)
                        {
                            string tlist = "";
                            if (syslist.Count > 1)
                            {
                                for(int i = 1; i < syslist.Count; i++)
                                    tlist = tlist.AppendPrePad(syslist[i].EventTimeLocal.ToShortDateString() + " " + syslist[i].EventTimeLocal.ToShortTimeString(), ", ");
                            }
                            return tlist;
                        }
                        else if (c == 5)
                            return he.WhereAmI;
                        else if (c == 6)
                            return he.ShipInformation != null ? he.ShipInformation.Name : "Unknown";
                        else if (c == 7)
                            return he.EventDescription;
                        else if (c == 8)
                            return he.EventDetailedInfo;
                        else if (c == 9)
                            return he.isTravelling ? he.TravelledDistance.ToString("0.0") : "";
                        else if (c == 10)
                            return he.isTravelling ? he.TravelledSeconds.ToString() : "";
                        else if (c == 11)
                            return he.isTravelling ? he.Travelledjumps.ToStringInvariant() : "";
                        else    // 12
                            return he.isTravelling ? he.TravelledMissingjump.ToStringInvariant() : "";
                    }
                    else
                        return null;
                };

                grd.onGetHeader += delegate (int c)
                {
                    return (c < colh.Length) ? colh[c] : null;
                };

                grd.Csvformat = discoveryform.ExportControl.radioButtonCustomEU.Checked ? BaseUtils.CVSWrite.CSVFormat.EU : BaseUtils.CVSWrite.CSVFormat.USA_UK;
                if (grd.ToCSV(dlg.FileName))
                    System.Diagnostics.Process.Start(dlg.FileName);
            }
        }

        private void checkBoxEDSM_CheckedChanged(object sender, EventArgs e)
        {
            SQLiteDBClass.PutSettingBool(DbEDSM, checkBoxEDSM.Checked);
            if (current_historylist != null && checkBoxEDSM.Checked)
                HistoryChanged(current_historylist);        
        }
    }

    #endregion
}
    