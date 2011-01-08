﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevComponents.DotNetBar;
using DevComponents.DotNetBar.Design;
using DevComponents.Editors;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapEditor;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.Helpers;
using HaCreator.WzStructure;
using System.Collections;

namespace HaCreator.GUI
{
    public partial class Editor : Office2007RibbonForm
    {
        private bool usingMouse = true;
        private CheckBoxItem[] checkboxes;

        private int prevLayerNum = -1;
        private ItemTypes prevEditedTypes = ItemTypes.None;
        private ItemTypes prevVisibleTypes = ItemTypes.None;

        public Editor()
        {
            InitializeComponent();
            InitializeCustomComponent();
            GetSettings();
        }

        private void InitializeCustomComponent()
        {
            Tabs.Parent = this;
            if (Environment.OSVersion.Version.Major >= 6)
                FormBorderStyle = FormBorderStyle.Sizable;
            else
                FormBorderStyle = FormBorderStyle.None;
            MaximizedBounds = SystemInformation.WorkingArea;
            styleManager.ManagerStyle = UserSettings.applicationStyle;
            viewTiles.Tag = ItemTypes.Tiles;
            viewObjs.Tag = ItemTypes.Objects;
            viewMobs.Tag = ItemTypes.Mobs;
            viewNpcs.Tag = ItemTypes.NPCs;
            viewPortals.Tag = ItemTypes.Portals;
            viewReactors.Tag = ItemTypes.Reactors;
            viewRopes.Tag = ItemTypes.Ropes;
            viewFhs.Tag = ItemTypes.Footholds;
            viewTooltips.Tag = ItemTypes.ToolTips;
            viewSeats.Tag = ItemTypes.Chairs;
            viewBackgrounds.Tag = ItemTypes.Backgrounds;
            checkboxes = new CheckBoxItem[] { viewTiles, viewObjs, viewMobs, viewNpcs, viewPortals, viewReactors, viewRopes, viewFhs, viewTooltips, viewSeats, viewBackgrounds };

            cordsLabel.Visible = UserSettings.ShowMousePos;
            itemDescLabel.Text = "";
            cordsLabel.Text = "";

            List<string> sortedTileSets = new List<string>();
            foreach (DictionaryEntry tS in Program.InfoManager.TileSets)
                sortedTileSets.Add((string)tS.Key);
            sortedTileSets.Sort();
            foreach (string tS in sortedTileSets)
                tileSetList.Items.Add(tS);

            List<string> sortedObjSets = new List<string>();
            foreach (DictionaryEntry oS in Program.InfoManager.ObjectSets)
                sortedObjSets.Add((string)oS.Key);
            sortedObjSets.Sort();
            foreach (string oS in sortedObjSets)
                objSetListBox.Items.Add(oS);

            List<string> sortedBgSets = new List<string>();
            foreach (DictionaryEntry bS in Program.InfoManager.BackgroundSets)
                sortedBgSets.Add((string)bS.Key);
            sortedBgSets.Sort();
            foreach (string bS in sortedBgSets)
                bgSetListBox.Items.Add(bS);

            for (int i = 0; i < Program.InfoManager.Portals.Length; i++)
            {
                PortalInfo pInfo = PortalInfo.GetPortalInfoByType((PortalType)i);
                KoolkLVItem item = portalImageContainer.createItem(pInfo.Image, Tables.PortalTypeNames[i], true);
                item.Tag = pInfo;
                item.MouseDown += new MouseEventHandler(portal_MouseDown);
                item.MouseUp += new MouseEventHandler(item_MouseUp);
            }

            KoolkLVItem[] commonItems = new KoolkLVItem[] { 
                miscItemsContainer.createItem(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.FootholdColor)), "Foothold", true),
                miscItemsContainer.createItem(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.RopeColor)), "Rope", true),
                miscItemsContainer.createItem(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.ChairColor)), "Chair", true),
                miscItemsContainer.createItem(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.ToolTipColor)), "Tooltip", true)
            };
            foreach (KoolkLVItem item in commonItems)
            {
                item.MouseDown += new MouseEventHandler(commonItem_Click);
                item.MouseUp += new MouseEventHandler(item_MouseUp);
            }
            ReloadLifeList();
        }

        public static string CreateItemDescription(BoardItem item, string lineBreak)
        {
            switch (item.GetType().Name)
            {
                case "TileInstance":
                    return "Tile:" + lineBreak + ((TileInfo)item.BaseInfo).tS + @"\" + ((TileInfo)item.BaseInfo).u + @"\" + ((TileInfo)item.BaseInfo).no;
                case "ObjectInstance":
                    return "Object:" + lineBreak + ((ObjectInfo)item.BaseInfo).oS + @"\" + ((ObjectInfo)item.BaseInfo).l0 + @"\" + ((ObjectInfo)item.BaseInfo).l1 + @"\" + ((ObjectInfo)item.BaseInfo).l2;
                case "BackgroundInstance":
                    return "Background:" + lineBreak + ((BackgroundInfo)item.BaseInfo).bS + @"\" + (((BackgroundInfo)item.BaseInfo).ani ? "ani" : "back") + @"\" + ((BackgroundInfo)item.BaseInfo).no;
                case "PortalInstance":
                    return "Portal:" + lineBreak + "Name: " + ((PortalInstance)item).pn + lineBreak + "Type: " + Tables.PortalTypeNames[(int)((PortalInstance)item).pt];
                case "LifeInstance":
                    if (((LifeInstance)item).Type == ItemTypes.Mobs)
                        return "Mob:" + lineBreak + "Name: " + ((MobInfo)item.BaseInfo).Name + lineBreak + "ID: " + ((MobInfo)item.BaseInfo).ID;
                    else
                        return "Npc:" + lineBreak + "Name: " + ((NpcInfo)item.BaseInfo).Name + lineBreak + "ID: " + ((NpcInfo)item.BaseInfo).ID;
                case "ReactorInstance":
                    return "Reactor:" + lineBreak + "ID: " + ((ReactorInfo)item.BaseInfo).ID;
                case "FootholdAnchor":
                    return "Foothold";
                case "RopeAnchor":
                    return ((RopeAnchor)item).ParentRope.ladder ? "Ladder" : "Rope";
                case "Chair":
                    return "Chair";
                case "ToolTipDot":
                case "ToolTip":
                case "ToolTipChar":
                    return "Tooltip";
                default:
                    return "";
            }
        }

        void portal_MouseDown(object sender, MouseEventArgs e)
        {
            SetHeldInfo((PortalInfo)((KoolkLVItem)sender).Tag, ItemTypes.Portals);
        }

        void commonItem_Click(object sender, MouseEventArgs e)
        {
            KoolkLVItem item = (KoolkLVItem)sender;
            switch (item.Name)
            {
                case "Foothold":
                    SetVisibleEditedTypes(ItemTypes.Footholds);
                    multiBoard.SelectedBoard.Mouse.SetFootholdMode();
                    multiBoard.Focus();
                    multiBoard.RenderFrame();
                    break;
                case "Rope":
                    SetVisibleEditedTypes(ItemTypes.Ropes);
                    multiBoard.SelectedBoard.Mouse.SetRopeMode();
                    multiBoard.Focus();
                    multiBoard.RenderFrame();
                    break;
                case "Chair":
                    SetVisibleEditedTypes(ItemTypes.Chairs);
                    multiBoard.SelectedBoard.Mouse.SetChairMode();
                    multiBoard.Focus();
                    multiBoard.RenderFrame();
                    break;
                case "Tooltip":
                    SetVisibleEditedTypes(ItemTypes.Footholds);
                    multiBoard.SelectedBoard.Mouse.SetTooltipMode();
                    multiBoard.Focus();
                    multiBoard.RenderFrame();
                    break;
            }
            item.Selected = true;
        }

        private Bitmap CreateColoredBitmap(Color color)
        {
            int containerSize = UserSettings.dotDescriptionBoxSize;
            int DotWidth = Math.Min(UserSettings.DotWidth, containerSize);
            Bitmap result = new Bitmap(containerSize, containerSize);
            using (Graphics g = Graphics.FromImage(result))
                g.FillRectangle(new SolidBrush(color), new Rectangle((containerSize / 2) - (DotWidth / 2), (containerSize / 2) - (DotWidth / 2), DotWidth, DotWidth));
            return result;
        }

        private void Editor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to quit?", "Quit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (multiBoard.SelectedBoard.Mouse.State != MouseState.Selection)
                {
                    ApplicationSettings.visibleTypes = prevVisibleTypes;
                    ApplicationSettings.editedTypes = prevEditedTypes;
                }
                ApplicationSettings.lastDefaultLayer = multiBoard.SelectedBoard.SelectedLayerIndex;
                FormClosing -= Editor_FormClosing;
                Application.Exit();
            }
            else
                e.Cancel = true;
        }

        private void Editor_Load(object sender, EventArgs e)
        {
            if (new Load(multiBoard, Tabs, new EventHandler(mapEditInfo)).ShowDialog() == DialogResult.OK)
            {
                multiBoard.Start();
                InputHandler handler = new InputHandler(multiBoard);
            }
            else
            {
                FormClosing -= Editor_FormClosing;
                Close();
            }
            layersComboBox.SelectedIndex = 0;
        }

        private void mapEditInfo(object sender, EventArgs e)
        {
            Board selectedBoard = (Board)((ToolStripMenuItem)sender).Tag;
            new InfoEditor(selectedBoard.MapInfo).ShowDialog();
        }

        private void Tabs_CurrentPageChanged(TabPages.TabPage currentPage, TabPages.TabPage previousPage)
        {
            if (previousPage != null)
            {
                allLayersBox.Enabled = true;
                layersComboBox.Enabled = true;
                ((Board)previousPage.Tag).Mouse.SelectionMode();
                InputHandler.ClearSelectedItems((Board)previousPage.Tag);
            }
            multiBoard.SelectedBoard = (Board)currentPage.Tag;
            multiBoard.Focus();
            //ApplicationSettings.visibleTypes = prevVisibleTypes;
            //ApplicationSettings.editedTypes = prevEditedTypes;
            //ParseVisibleEditedTypes();
            if (multiBoard.SelectedBoard.SelectedLayerIndex == -1) allLayersBox.Checked = true;
            else { allLayersBox.Checked = false; layersComboBox.SelectedIndex = multiBoard.SelectedBoard.SelectedLayerIndex; }
            SetLayerTSInComboBox();
            multiBoard.RenderFrame();
        }

        private void SetLayerTSInComboBox()
        {
            for (int i = 0; i < layersComboBox.Items.Count; i++)
            {
                Layer layer = multiBoard.SelectedBoard.Layers[i];
                ((ComboItem)layersComboBox.Items[i]).Text = i.ToString() + (layer.tS != null ? (" - " + layer.tS) : "");
            }
        }

        private void GetSettings()
        {
            ParseVisibleEditedTypes();
            bool threeState = UserSettings.useThreeState;
            foreach (CheckBoxItem checkbox in checkboxes) checkbox.ThreeState = threeState;

            showMinimap.Checked = UserSettings.useMiniMap;
            showVR.Checked = UserSettings.showVR;
            snapButton.Checked = UserSettings.useSnapping;
            emulateParallax.Checked = UserSettings.emulateParallax;
            randomTiles.Checked = ApplicationSettings.randomTiles;

            Tabs.TabColor = UserSettings.TabColor;

            if (ApplicationSettings.lastDefaultLayer == -1) allLayersBox.Checked = true;
            else layersComboBox.SelectedIndex = ApplicationSettings.lastDefaultLayer;
        }

        private void ParseVisibleEditedTypes()
        {
            ItemTypes visibleTypes = ApplicationSettings.visibleTypes;
            ItemTypes editedTypes = ApplicationSettings.editedTypes;
            foreach (CheckBoxItem checkBox in checkboxes)
            {
                ItemTypes type = (ItemTypes)checkBox.Tag;
                if ((editedTypes & type) == type && (visibleTypes & type) == type) checkBox.CheckState = CheckState.Checked;
                else if ((editedTypes & type) != type && (visibleTypes & type) == type) checkBox.CheckState = CheckState.Indeterminate;
                else if ((editedTypes & type) != type && (visibleTypes & type) != type) checkBox.CheckState = CheckState.Unchecked;
                else
                {
                    MessageBox.Show("Unknown check state, using default");
                    checkBox.CheckState = CheckState.Checked;
                }
            }
        }

        private void Tabs_PageClosing(TabPages.TabPage page, ref bool cancel)
        {
            if (MessageBox.Show("Are you sure you want to close this map?", "Close", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                cancel = true;
        }

        private void Editor_Resize(object sender, EventArgs e)
        {
            RearrangeControls();
        }

        private void RearrangeControls()
        {
            Tabs.Location = new Point(ribbonControl.Location.X + Tabs.Margin.Left, ribbonControl.Height + Tabs.Margin.Top);
            Tabs.Size = new Size(ribbonControl.Width - Tabs.Margin.Left - Tabs.Margin.Right, Height - ribbonControl.Height - statusStrip.Height - Tabs.Margin.Top - Tabs.Margin.Bottom);
        }

        private void newButton_Click(object sender, EventArgs e)
        {
            new Load(multiBoard, Tabs, new EventHandler(mapEditInfo)).ShowDialog();
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void infoButton_Click(object sender, EventArgs e)
        {
            new About().ShowDialog();
        }

        private void ribbonControlChanged(object sender, EventArgs e)
        {
            RearrangeControls();
        }

        private void undoButton_Click(object sender, EventArgs e)
        {

        }

        private void redoButton_Click(object sender, EventArgs e)
        {

        }

        private void viewCheckboxes_CheckedChanging(object sender, CheckBoxChangeEventArgs e)
        {
            CheckBoxItem checkBox = (CheckBoxItem)sender;
            if (!usingMouse && checkBox.Checked && (ApplicationSettings.editedTypes & (ItemTypes)checkBox.Tag) == (ItemTypes)checkBox.Tag)
                e.Cancel = true;
        }

        private void viewCheckboxes_CheckedChanged(object sender, CheckBoxChangeEventArgs e)
        {
            ItemTypes newVisibleTypes = 0;
            ItemTypes newEditedTypes = 0;
            foreach (CheckBoxItem checkbox in checkboxes) switch (checkbox.CheckState)
                {

                    case CheckState.Checked:
                        newVisibleTypes ^= (ItemTypes)checkbox.Tag;
                        newEditedTypes ^= (ItemTypes)checkbox.Tag;
                        break;
                    case CheckState.Indeterminate:
                        newVisibleTypes ^= (ItemTypes)checkbox.Tag;
                        break;
                }
            ApplicationSettings.visibleTypes = newVisibleTypes;
            ApplicationSettings.editedTypes = newEditedTypes;
            if (multiBoard.SelectedBoard != null)
                InputHandler.ClearSelectedItems(multiBoard.SelectedBoard);
            multiBoard.RenderFrame();
        }

        private void showMinimap_CheckedChanged(object sender, EventArgs e)
        {
            UserSettings.useMiniMap = showMinimap.Checked;
            multiBoard.RenderFrame();
        }

        private void showVR_CheckChanged(object sender, EventArgs e)
        {
            UserSettings.showVR = showVR.Checked;
            multiBoard.RenderFrame();
        }

        private void snapButton_CheckChanged(object sender, EventArgs e)
        {
            UserSettings.useSnapping = snapButton.Checked;
            multiBoard.RenderFrame();
        }

        private void emulateParallax_CheckedChanged(object sender, EventArgs e)
        {
            UserSettings.emulateParallax = emulateParallax.Checked;
            multiBoard.RenderFrame();
        }

        private void multiBoard_OnUndoListChanged()
        {
            if (multiBoard.SelectedBoard.UndoRedoMan.UndoList.Count > 0)
                undoButton.Enabled = true;
            else undoButton.Enabled = false;
            multiBoard.RenderFrame();
        }

        private void multiBoard_OnRedoListChanged()
        {
            if (multiBoard.SelectedBoard.UndoRedoMan.RedoList.Count > 0)
                redoButton.Enabled = true;
            else redoButton.Enabled = false;
            multiBoard.RenderFrame();
        }

        private void showSettingsForm(object sender, EventArgs e)
        {
            new UserSettingsForm().ShowDialog();
            multiBoard.RenderFrame();
        }

        private void tileBrowse_Click(object sender, EventArgs e)
        {
            new TileSetBrowser(tileSetList).ShowDialog();
            multiBoard.RenderFrame();
        }

        private void LoadTileSetList()
        {
            if (tileSetList.SelectedItem == null) return;
            tileImagesContainer.Controls.Clear();
            WzImage tileSetImage = Program.InfoManager.TileSets[(string)tileSetList.SelectedItem];
            if (tileSetImage == null) return;
            foreach (WzSubProperty tCat in tileSetImage.WzProperties)
            {
                if (tCat.Name == "info") continue;
                if (ApplicationSettings.randomTiles)
                {
                    WzCanvasProperty canvasProp = (WzCanvasProperty)tCat["0"];
                    if (canvasProp == null) continue;
                    KoolkLVItem item = tileImagesContainer.createItem(canvasProp.PngProperty.GetPNG(false), tCat.Name, true);
                    TileInfo[] randomInfos = new TileInfo[tCat.WzProperties.Count];
                    for (int i = 0; i < randomInfos.Length; i++)
                    {
                        WzCanvasProperty tile = (WzCanvasProperty)tCat.WzProperties[i];
                        if (tile.HCTag == null)
                            tile.HCTag = TileInfo.Load(tile, (string)tileSetList.SelectedItem, tCat.Name, tile.Name);
                        randomInfos[i] = (TileInfo)tile.HCTag;
                    }
                    item.Tag = randomInfos;
                    item.MouseDown += new MouseEventHandler(tileItem_Click);
                    item.MouseUp += new MouseEventHandler(item_MouseUp);
                }
                else
                {
                    foreach (WzCanvasProperty tile in tCat.WzProperties)
                    {
                        if (tile.HCTag == null)
                            tile.HCTag = TileInfo.Load(tile, (string)tileSetList.SelectedItem, tCat.Name, tile.Name);
                        KoolkLVItem item = tileImagesContainer.createItem(tile.PngProperty.GetPNG(false), tCat.Name + "/" + tile.Name, true);
                        item.Tag = tile.HCTag;
                        item.MouseDown += new MouseEventHandler(tileItem_Click);
                        item.MouseUp += new MouseEventHandler(item_MouseUp);
                    }
                }
            }
        }

        void item_MouseUp(object sender, MouseEventArgs e)
        {
            ((KoolkLVItem)sender).Selected = false;
        }

        void tileItem_Click(object sender, MouseEventArgs e)
        {
            if (multiBoard.SelectedBoard.Mouse.State == MouseState.Selection)
                prevLayerNum = multiBoard.SelectedBoard.SelectedLayerIndex;
            allLayersBox.Checked = false;
            allLayersBox.Enabled = false;
            allLayersBox_CheckedChanged(allLayersBox, null);
            Layer layer = multiBoard.SelectedBoard.SelectedLayer;
            if (layer.tS != null)
            {
                TileInfo infoToAdd = null;
                if (ApplicationSettings.randomTiles)
                    infoToAdd = ((TileInfo[])((KoolkLVItem)sender).Tag)[0];
                else
                    infoToAdd = (TileInfo)((KoolkLVItem)sender).Tag;
                if (infoToAdd.tS != layer.tS)
                {
                    MessageBox.Show("Error: layer tS already set to a different tS.\r\nPlease choose a different layer.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    allLayersBox.Enabled = true;
                    if (prevLayerNum == -1) allLayersBox.Checked = true;
                    else layersComboBox.SelectedIndex = prevLayerNum;
                    return;
                }
            }
            layersComboBox.Enabled = false;
            if (multiBoard.SelectedBoard.Mouse.State == MouseState.Selection)
            {
                prevVisibleTypes = ApplicationSettings.visibleTypes;
                prevEditedTypes = ApplicationSettings.editedTypes;
            }
            ApplicationSettings.editedTypes = ItemTypes.Tiles;
            ParseVisibleEditedTypes();
            if (ApplicationSettings.randomTiles)
                multiBoard.SelectedBoard.Mouse.SetRandomTilesMode((TileInfo[])((KoolkLVItem)sender).Tag);
            else
                multiBoard.SelectedBoard.Mouse.SetHeldInfo((TileInfo)((KoolkLVItem)sender).Tag);
            multiBoard.Focus();
            multiBoard.RenderFrame();
            ((KoolkLVItem)sender).Selected = true;
        }

        private void tileSetList_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadTileSetList();
            multiBoard.RenderFrame();
        }

        private void allLayersBox_CheckedChanged(object sender, CheckBoxChangeEventArgs e)
        {
            if (multiBoard.SelectedBoard == null) return;
            layersComboBox.Enabled = !allLayersBox.Checked;
            SetLayer();
            InputHandler.ClearSelectedItems(multiBoard.SelectedBoard);
            multiBoard.RenderFrame();
        }

        private bool LayeredItemsSelected(out int layer)
        {
            foreach (BoardItem item in multiBoard.SelectedBoard.SelectedItems)
                if (item is LayeredItem)
                {
                    layer = ((LayeredItem)item).Layer.LayerNumber;
                    return true;
                }
            layer = 0;
            return false;
        }

        private bool LayerCapableOfHoldingSelectedItems(Layer layer)
        {
            if (layer.tS == null) return true;
            foreach (BoardItem item in multiBoard.SelectedBoard.SelectedItems)
                if (item is TileInstance && ((TileInfo)item.BaseInfo).tS != layer.tS) return false;
            return true;
        }

        private void layersComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (multiBoard.SelectedBoard == null) return;
            int oldLayer;
            if (multiBoard.SelectedBoard.SelectedItems.Count > 0 && LayeredItemsSelected(out oldLayer))
            {
                if (UserSettings.suppressWarnings || MessageBox.Show("Are you sure you want to move these items from layer " + oldLayer.ToString() + " to " + layersComboBox.SelectedIndex.ToString() + "?\r\n", "Cross-Layer Operation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) != System.Windows.Forms.DialogResult.Yes)
                {
                    InputHandler.ClearSelectedItems(multiBoard.SelectedBoard);
                    SetLayer();
                    return;
                }
                Layer targetLayer = multiBoard.SelectedBoard.Layers[layersComboBox.SelectedIndex];
                if (!LayerCapableOfHoldingSelectedItems(targetLayer))
                {
                    MessageBox.Show("Error: Target layer cannot hold the selected items because they contain tiles with a tS different from the layer's", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                List<IContainsLayerInfo> items = new List<IContainsLayerInfo>();
                foreach (BoardItem item in multiBoard.SelectedBoard.SelectedItems)
                {
                    if (!(item is IContainsLayerInfo)) continue;
                    ((IContainsLayerInfo)item).LayerNumber = targetLayer.LayerNumber;
                    items.Add((IContainsLayerInfo)item);
                }
                if (items.Count > 0)
                    multiBoard.SelectedBoard.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction>() { UndoRedoManager.ItemsLayerChanged(items, oldLayer, targetLayer.LayerNumber) });
                multiBoard.SelectedBoard.Layers[oldLayer].RecheckTileSet();
                targetLayer.RecheckTileSet();
            }
            SetLayer();
            multiBoard.RenderFrame();
        }

        private void SetLayer()
        {
            if (allLayersBox.Checked)
                multiBoard.SelectedBoard.SelectedLayerIndex = -1;
            else multiBoard.SelectedBoard.SelectedLayerIndex = layersComboBox.SelectedIndex;
        }

        private void multiBoard_ReturnToSelectionState()
        {
            allLayersBox.Enabled = true;
            layersComboBox.Enabled = true;
            multiBoard.SelectedBoard.Mouse.SelectionMode();
            if (prevLayerNum == -1) allLayersBox.Checked = true;
            else layersComboBox.SelectedIndex = prevLayerNum;
            ApplicationSettings.visibleTypes = prevVisibleTypes;
            ApplicationSettings.editedTypes = prevEditedTypes;
            ParseVisibleEditedTypes();
            multiBoard.RenderFrame();
        }

        private void multiBoard_OnLayerTSChanged(Layer layer)
        {
            ((ComboItem)layersComboBox.Items[layer.LayerNumber]).Text = layer.LayerNumber.ToString() + (layer.tS != null ? " - " + layer.tS : "");
        }

        private void randomTiles_CheckedChanged(object sender, EventArgs e)
        {
            ApplicationSettings.randomTiles = randomTiles.Checked;
            LoadTileSetList();
        }

        private void multiBoard_OnEditInstanceClicked(BoardItem item)
        {
            InputHandler.ClearBoundItems(multiBoard.SelectedBoard);
            switch (item.GetType().Name)
            {
                case "ObjectInstance":
                    new InstanceEditor.ObjectInstanceEditor((ObjectInstance)item).ShowDialog();
                    multiBoard.RenderFrame();
                    break;
                case "TileInstance":
                case "Chair":
                    new InstanceEditor.GeneralInstanceEditor(item).ShowDialog();
                    multiBoard.RenderFrame();
                    break;
                case "FootholdAnchor":
                    FootholdLine[] selectedFootholds = FootholdLine.GetSelectedFootholds(item.Board);
                    if (selectedFootholds.Length > 0)
                    {
                        new InstanceEditor.FootholdEditor(selectedFootholds).ShowDialog();
                    }
                    else
                        new InstanceEditor.GeneralInstanceEditor(item).ShowDialog();
                    multiBoard.RenderFrame();
                    break;
                case "RopeAnchor":
                    new InstanceEditor.RopeInstanceEditor((RopeAnchor)item).ShowDialog();
                    multiBoard.RenderFrame();
                    break;
                case "LifeInstance":
                    multiBoard.RenderFrame();
                    break;
                case "ReactorInstance":
                    new InstanceEditor.ReactorInstanceEditor((ReactorInstance)item).ShowDialog();
                    multiBoard.RenderFrame();
                    break;
                case "BackgroundInstance":
                    multiBoard.RenderFrame();
                    break;
                case "PortalInstance":
                    new InstanceEditor.PortalInstanceEditor((PortalInstance)item).ShowDialog();
                    multiBoard.RenderFrame();
                    break;
                case "ToolTip":

                    break;
                default:
                    break;
            }
        }

        private void multiBoard_OnEditBaseClicked(BoardItem item)
        {

        }

        private void objSetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void objL0ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void objL1ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void objItem_Click(object sender, MouseEventArgs e)
        {
            SetHeldInfo((ObjectInfo)((KoolkLVItem)sender).Tag, ItemTypes.Objects);
            ((KoolkLVItem)sender).Selected = true;
        }

        private void multiBoard_OnSendToBackClicked(BoardItem boardRefItem)
        {
            foreach (BoardItem item in boardRefItem.Board.SelectedItems)
            {
                if (item.Z > 0)
                {
                    item.Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemZChanged(item, item.Z, 0) });
                    item.Z = 0;
                }
            }
            boardRefItem.Board.BoardItems.Sort();
            multiBoard.RenderFrame();
        }

        private void multiBoard_OnBringToFrontClicked(BoardItem boardRefItem)
        {
            foreach (BoardItem item in boardRefItem.Board.SelectedItems)
            {
                int oldZ = item.Z;
                if (item is BackgroundInstance)
                {
                    IList list = ((BackgroundInstance)item).front ? multiBoard.SelectedBoard.BoardItems.FrontBackgrounds : multiBoard.SelectedBoard.BoardItems.BackBackgrounds;
                    int highestZ = 0;
                    foreach (BackgroundInstance bg in list)
                        if (bg.Z > highestZ)
                            highestZ = bg.Z;
                    item.Z = highestZ + 1;
                }
                else
                {
                    int highestZ = 0;
                    foreach (LayeredItem layeredItem in multiBoard.SelectedBoard.BoardItems.TileObjs)
                        if (layeredItem.Z > highestZ) highestZ = layeredItem.Z;
                    item.Z = highestZ + 1;
                }
                if (item.Z != oldZ)
                    item.Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemZChanged(item, oldZ, item.Z) });
            }
            boardRefItem.Board.BoardItems.Sort();
            multiBoard.RenderFrame();
        }

        private void bgSetListBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        void bgItem_Click(object sender, MouseEventArgs e)
        {
            SetHeldInfo((BackgroundInfo)((KoolkLVItem)sender).Tag, ItemTypes.Backgrounds);
            ((KoolkLVItem)sender).Selected = true;
        }

        private void Editor_Paint(object sender, PaintEventArgs e)
        {
            multiBoard.RenderFrame();
        }

        private void lifeModeChanged(object sender, EventArgs e)
        {
            ReloadLifeList();
        }

        private void ReloadLifeList()
        {
            string searchText = lifeSearchBox.Text.ToLower();
            lifeListBox.Items.Clear();
            List<string> items = new List<string>();
            if (reactorRButton.Checked)
            {
                if (lifeSearchBox.Text == "")
                    foreach (DictionaryEntry entry in Program.InfoManager.Reactors)
                    {
                        string id = ((ReactorInfo)entry.Value).ID;
                        items.Add(id);
                    }
                else foreach (DictionaryEntry entry in Program.InfoManager.Reactors)
                    {
                        string id = ((ReactorInfo)entry.Value).ID;
                        if (id.Contains(searchText))
                            items.Add(id);
                    }
            }
            else if (npcRButton.Checked)
            {
                if (lifeSearchBox.Text == "")
                    foreach (DictionaryEntry entry in Program.InfoManager.NPCs)
                        items.Add((string)entry.Key + " - " + (string)entry.Value);
                else foreach (DictionaryEntry entry in Program.InfoManager.NPCs)
                    {
                        string name = (string)entry.Key + " - " + (string)entry.Value;
                        if (name.ToLower().Contains(searchText))
                            items.Add((string)entry.Key + " - " + (string)entry.Value);
                    }
            }
            else
            {
                if (lifeSearchBox.Text == "")
                    foreach (DictionaryEntry entry in Program.InfoManager.Mobs)
                        items.Add((string)entry.Key + " - " + (string)entry.Value);
                else foreach (DictionaryEntry entry in Program.InfoManager.Mobs)
                    {
                        string name = (string)entry.Key + " - " + (string)entry.Value;
                        if (name.ToLower().Contains(searchText))
                            items.Add((string)entry.Key + " - " + (string)entry.Value);
                    }
            }
            items.Sort();
            foreach (string item in items) lifeListBox.Items.Add(item);
        }

        private void mapSimulatorButton_Click(object sender, EventArgs e)
        {
            multiBoard.DeviceReady = false;
            MapSimulator.MapSimulator.CreateMapSimulator(multiBoard.SelectedBoard).ShowDialog();
            multiBoard.DeviceReady = true;
            multiBoard.RenderFrame();
        }

        private void lifeListBox_SelectedValueChanged(object sender, EventArgs e)
        {

        }

        private void SetHeldInfo(MapleDrawableInfo info, ItemTypes editedTypes)
        {
            SetVisibleEditedTypes(editedTypes);
            multiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
            multiBoard.Focus();
            multiBoard.RenderFrame();
        }

        private void SetVisibleEditedTypes(ItemTypes newEditedTypes)
        {
            if (multiBoard.SelectedBoard.Mouse.State == MouseState.Selection)
                prevLayerNum = multiBoard.SelectedBoard.SelectedLayerIndex;
            allLayersBox.Checked = false;
            allLayersBox.Enabled = false;
            allLayersBox_CheckedChanged(allLayersBox, null);
            layersComboBox.Enabled = false;
            if (multiBoard.SelectedBoard.Mouse.State == MouseState.Selection)
            {
                prevVisibleTypes = ApplicationSettings.visibleTypes;
                prevEditedTypes = ApplicationSettings.editedTypes;
            }
            ApplicationSettings.editedTypes = newEditedTypes;
            ParseVisibleEditedTypes();
        }

        private void multiBoard_MouseMoved(Board selectedBoard, Microsoft.Xna.Framework.Point oldPos, Microsoft.Xna.Framework.Point newPos, Microsoft.Xna.Framework.Point currPhysicalPos)
        {
            cordsLabel.Text = "Virtual: " + newPos.X + "," + newPos.Y + " Physical: " + currPhysicalPos.X + "," + currPhysicalPos.Y;
        }

        private void multiBoard_SelectedItemChanged(BoardItem selectedItem)
        {
            if (selectedItem == null) itemDescLabel.Text = "";
            else itemDescLabel.Text = CreateItemDescription(selectedItem, " ");
        }

        private void regenMinimap_Click(object sender, EventArgs e)
        {
            if (multiBoard.SelectedBoard.RegenerateMinimap())
                MessageBox.Show("Minimap regenerated successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
            {
                MessageBox.Show("An error occured during minimap regeneration. The error has been logged. If possible, save the map and send it to haha01haha01@gmail.com", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ErrorLogger.Log(ErrorLevel.Critical, "error regenning minimap for map " + multiBoard.SelectedBoard.MapInfo.id.ToString());
            }
        }

        private void buttonItem2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            //int a = Environment.TickCount;
            MapSaver.ConvertToMapleFootholds2(ref multiBoard.SelectedBoard.BoardItems.FootholdLines, ref multiBoard.SelectedBoard.BoardItems.FHAnchors);
            sw.Stop();
            MessageBox.Show(sw.Elapsed.ToString());
            //MessageBox.Show((Environment.TickCount - a).ToString());
        }

        private void btnHaRepacker_Click(object sender, EventArgs e)
        {
            try
            {
                HaRepacker.Program.WzMan = new HaRepackerLib.WzFileManager();
                HaRepacker.Program.PrepareApplication();
                HaRepacker.GUI.MainForm mf = new HaRepacker.GUI.MainForm(null, false);
                mf.unloadAllToolStripMenuItem.Visible = false;
                mf.reloadAllToolStripMenuItem.Visible = false;
                foreach (DictionaryEntry entry in Program.WzManager.wzFiles)
                    mf.Interop_AddLoadedWzFileToManager((WzFile)entry.Value);
                mf.ShowDialog();
                HaRepacker.Program.EndApplication(false);
            }
            catch (Exception ex)
            {
                HaRepackerLib.Warning.Error("Exception while running HaRepacker: " + ex.Message);
            }
        }

        private void ribbonControl_SizeChanged(object sender, EventArgs e)
        {
            RearrangeControls();
        }
    }
}