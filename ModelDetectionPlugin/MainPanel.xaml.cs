using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ModelDetectionPlugin {
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class MainPanel : Page, IDockablePaneProvider {
        UIApplication m_uiApp;
        UIDocument m_uidoc;

        MtSpuriousConnection m_spuriousConnection;
        MtLevel m_level;
        MtPipeRelation m_pipeRelation;
        MtBasicInfo m_basicInfo;
        MtMisc m_misc;

        ExternalEvent m_spuriousConnectionEventHandler;
        ExternalEvent m_levelEventHandler;
        ExternalEvent m_pipeRelationEventHandler;
        ExternalEvent m_basicInfoEventHandler;
        ExternalEvent m_miscEventHandler;

        public MainPanel() {
            InitializeComponent();
        }



        public void Init(UIApplication uiapp) {
            m_uiApp = uiapp;
            m_uidoc = m_uiApp.ActiveUIDocument;

            m_spuriousConnection = new MtSpuriousConnection();
            m_spuriousConnectionEventHandler = ExternalEvent.Create(m_spuriousConnection);

            m_level = new MtLevel();
            m_levelEventHandler = ExternalEvent.Create(m_level);

            m_pipeRelation = new MtPipeRelation();
            m_pipeRelationEventHandler = ExternalEvent.Create(m_pipeRelation);

            m_basicInfo = new MtBasicInfo();
            m_basicInfoEventHandler = ExternalEvent.Create(m_basicInfo);

            m_misc = new MtMisc();
            m_miscEventHandler = ExternalEvent.Create(m_misc);

        }

        public void SetupDockablePane(DockablePaneProviderData data) {
            data.FrameworkElement = this as FrameworkElement;
            data.InitialState = new DockablePaneState();
            data.InitialState.DockPosition = DockPosition.Bottom;
        }

        public void ModelDetectionTabCtrl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.Source is System.Windows.Controls.TabControl) {
                TabItem selectItem = ModelDetectionTabCtrl.SelectedItem as TabItem;
                MtLog.message = selectItem.Header.ToString();
                MessageContent.Content = MtLog.message;
                ChangeModule(selectItem);
            }
        }

        public void ChangeModule(TabItem selectItem) {
            string detectionModule = selectItem.Name.Substring(0, selectItem.Name.LastIndexOf("TabItem"));
            MtGlobals.DetectionModule module = (MtGlobals.DetectionModule)Enum.Parse(typeof(MtGlobals.DetectionModule), detectionModule);
            switch (module) {
                case MtGlobals.DetectionModule.SpuriousConnection:
                    //InitSpuriousConnect();
                    break;
                case MtGlobals.DetectionModule.Level:
                    InitLevel();
                    break;
                case MtGlobals.DetectionModule.PipeRelation:
                    InitPipeRelation();
                    break;
                case MtGlobals.DetectionModule.BasicInfo:
                    InitBasicInfo();
                    break;
                case MtGlobals.DetectionModule.Misc:
                    InitMisc();
                    break;
                default:
                    break;
            }
        }

        #region SpuriousConnect
        private void InitSpuriousConnect() {
            m_spuriousConnection.IsRemoveFan = IsRemoveFan.IsChecked == true ? true : false;
            m_spuriousConnection.IsRemoveCondensorPipe = IsRemoveCondemserPipe.IsChecked == true ? true : false;
            m_spuriousConnection.IsRemoveAirDust = IsRemoveAirDuct.IsChecked == true ? true : false;
        }

        private void IsRemoveFan_Click(object sender, RoutedEventArgs e) {
            if (m_spuriousConnection != null) {
                if (IsRemoveFan.IsChecked == true)
                    m_spuriousConnection.IsRemoveFan = true;
                else
                    m_spuriousConnection.IsRemoveFan = false;
            }
        }

        private void IsRemoveCondemserPipe_Click(object sender, RoutedEventArgs e) {
            if (m_spuriousConnection != null) {
                if (IsRemoveCondemserPipe.IsChecked == true)
                    m_spuriousConnection.IsRemoveCondensorPipe = true;
                else
                    m_spuriousConnection.IsRemoveCondensorPipe = false;
            }
        }

        private void IsRemoveAirDuct_Click(object sender, RoutedEventArgs e) {
            if (m_spuriousConnection != null) {
                if (IsRemoveAirDuct.IsChecked == true)
                    m_spuriousConnection.IsRemoveAirDust = true;
                else
                    m_spuriousConnection.IsRemoveAirDust = false;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            FilterConditonForm filterConditonForm = new FilterConditonForm();
            filterConditonForm.Show();
        }

        private void SpuriousConnectionBtn_Click(object sender, RoutedEventArgs e) {
            InitSpuriousConnect();
            m_spuriousConnection.SelMethod = MtGlobals.SpuriousConnectionMethods.TestSpuriousConnection;
            ExternalEventRequest externalEventRequest = m_spuriousConnectionEventHandler.Raise();
        }

        private void SpuriousConnectionListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.Source is System.Windows.Controls.ListView) {

                if (SpuriousConnectionListView.SelectedItems.Count == 1) {
                    SpuriousConnectionError error = SpuriousConnectionListView.SelectedItem as SpuriousConnectionError;

                    if (error != null && error is SpuriousConnectionError) {
                        string id = error.ID;
                        Element element = MtCommon.GetElementById(m_uidoc.Document, id);
                        MtCommon.ElementCenterDisplay(m_uidoc, element);

                        IList<ElementId> list = new List<ElementId>();
                        list.Add(element.Id);
                        m_uidoc.Selection.SetElementIds(list);
                    }
                } else if (SpuriousConnectionListView.SelectedItems.Count > 1) {

                }
            }
        }
        Dictionary<string, SpuriousConnectionError> removeEleDic = new Dictionary<string, SpuriousConnectionError>();
        private void ExcludingItemClicked(object sender, RoutedEventArgs e) {

            if (SpuriousConnectionListView.SelectedItems.Count != 0) {
                foreach (var item in SpuriousConnectionListView.SelectedItems) {
                    SpuriousConnectionError error = item as SpuriousConnectionError;
                    removeEleDic.Add(error.ID, error);


                }
            }
            TaskDialog.Show("msg", "Item Click");
        }

        #endregion

        #region Level
        private void LevelListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.Source is System.Windows.Controls.ListView) {
                LevelError error = LevelListView.SelectedItem as LevelError;

                if (error != null && error is LevelError) {
                    string id = error.ID;
                    Element element = MtCommon.GetElementById(m_uidoc.Document, id);
                    MtCommon.ElementCenterDisplay(m_uidoc, element);

                    IList<ElementId> list = new List<ElementId>();
                    list.Add(element.Id);
                    m_uidoc.Selection.SetElementIds(list);
                }
            }
        }

        private void MarkVerticaPipe_Click(object sender, RoutedEventArgs e) {
            m_level.SelMethod = MtGlobals.LevelMethods.MarkVerticalPipe;
            m_levelEventHandler.Raise();
        }

        private void LevelDetectionBtn_Click(object sender, RoutedEventArgs e) {
            m_level.SelMethod = MtGlobals.LevelMethods.CheckLevel;
            m_levelEventHandler.Raise();
        }

        private void AutoAdjustLevelBtn_Click(object sender, RoutedEventArgs e) {
            m_level.SelMethod = MtGlobals.LevelMethods.AutoAdjustLevel;
            m_levelEventHandler.Raise();
        }


        List<System.Windows.Controls.CheckBox> m_ltCurSystemNames;
        private void InitLevel() {
            m_ltCurSystemNames = new List<System.Windows.Controls.CheckBox>();

            SystemComboBox.ItemsSource = MtCommon.GetEnumAttributeNames(typeof(MtGlobals.SystemName));
            SystemComboBox.SelectedIndex = 0;

            m_ltCurSystemNames = CreateCheckBox(MtCommon.GetEnumAttributeNames(typeof(MtGlobals.ACSubSystem)));
            StandardSysList.ItemsSource = m_ltCurSystemNames;
        }


        private List<System.Windows.Controls.CheckBox> CreateCheckBox(List<string> subsystems) {
            List<System.Windows.Controls.CheckBox> lists = new List<System.Windows.Controls.CheckBox>();
            if (subsystems != null && subsystems.Count != 0) {
                foreach (var item in subsystems) {
                    System.Windows.Controls.CheckBox checkBox = CreateOneCheckBox(item);
                    lists.Add(checkBox);
                }
            }
            return lists;
        }

        private System.Windows.Controls.CheckBox CreateOneCheckBox(string name) {
            System.Windows.Controls.CheckBox checkBox = new System.Windows.Controls.CheckBox();
            checkBox.Content = name;
            checkBox.IsChecked = true;
            return checkBox;
        }


        private void SystemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            string selSysName = SystemComboBox.SelectedItem.ToString();
            MtGlobals.SystemName selectSys = MtCommon.GetEnumValueByString<MtGlobals.SystemName>(selSysName);

            switch (selectSys) {
                case MtGlobals.SystemName.AC:
                    m_ltCurSystemNames = CreateCheckBox(MtCommon.GetEnumAttributeNames(typeof(MtGlobals.ACSubSystem)));
                    StandardSysList.ItemsSource = m_ltCurSystemNames;
                    break;
                case MtGlobals.SystemName.WSAD:
                    m_ltCurSystemNames = CreateCheckBox(MtCommon.GetEnumAttributeNames(typeof(MtGlobals.WSADSubSystem)));
                    StandardSysList.ItemsSource = m_ltCurSystemNames;
                    break;
                case MtGlobals.SystemName.MG:
                    m_ltCurSystemNames = CreateCheckBox(MtCommon.GetEnumAttributeNames(typeof(MtGlobals.MGSubSystem)));
                    StandardSysList.ItemsSource = m_ltCurSystemNames;
                    break;
                case MtGlobals.SystemName.Steam:
                    m_ltCurSystemNames = CreateCheckBox(MtCommon.GetEnumAttributeNames(typeof(MtGlobals.SteamSubSystem)));
                    StandardSysList.ItemsSource = m_ltCurSystemNames;
                    break;
                default:
                    break;
            }
        }

        private void AddSystemBtn_Click(object sender, RoutedEventArgs e) {
            SystemNameForm systemNameForm = new SystemNameForm();
            systemNameForm.ShowDialog();
            System.Windows.Controls.CheckBox systemItem = CreateOneCheckBox(systemNameForm.SystemName);
            //m_ltCurSystemNames.Add(systemItem);
            // StandardSysList.ItemsSource = m_ltCurSystemNames;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) {
            foreach (System.Windows.Controls.CheckBox item in StandardSysList.ItemsSource) {
                item.IsChecked = true;
            }
        }

        private void CheckSystemNameBtn_Click(object sender, RoutedEventArgs e) {
            m_level.SelMethod = MtGlobals.LevelMethods.CheckNoStandardSystemName;
            m_levelEventHandler.Raise();
        }

        private void InConsistentSysNameBtn_Click(object sender, RoutedEventArgs e) {
            m_level.SelMethod = MtGlobals.LevelMethods.CheckInConsistentSystemName;
            m_levelEventHandler.Raise();
        }

        public List<string> GetSelectedStandardItems() {
            List<string> subSystems = new List<string>();
            foreach (System.Windows.Controls.CheckBox item in StandardSysList.Items) {
                if (item.IsChecked == true) {
                    subSystems.Add(item.Name);
                }
            }
            return subSystems;
        }

        #endregion

        #region PipeRelation
        private void InitPipeRelation() {
            SubSystemNameComBox.ItemsSource = MtCommon.GetEnumAttributeNames(typeof(MtGlobals.EPSystem));
            m_pipeRelation.SystemName = SystemNameComBox.Text;
            m_pipeRelation.SubSystemName = SubSystemNameComBox.Text;

            Tunnel.ItemsSource = MtCommon.GetEnumAttributeNames(typeof(MtGlobals.Tunnel));
            m_pipeRelation.TunnelName = Tunnel.Text;

            if (null != db_path.Text)
                m_pipeRelation.DBFilePath = db_path.Text;
            if (null != TableName.Text)
                m_pipeRelation.TableName = TableName.Text;
            if (null != ColumnName.Text)
                m_pipeRelation.ColumnName = ColumnName.Text;
            if (MultiSystem != null && null != MultiSystem.Text)
                m_pipeRelation.MultiSystem = MultiSystem.Text;

            m_pipeRelation.IsPositiveDir = IsPositiveDir.IsChecked == true ? true : false;
            m_pipeRelation.IsWaterReturnPipe = IsWaterReturn.IsChecked == true ? true : false;
            m_pipeRelation.IsSameSystem = SameSystemCheck.IsChecked == true ? true : false;
            m_pipeRelation.IsIsolatedElements = IsIsolatedElemtns.IsChecked == true ? true : false;
        }

        private void OpenDBFile_Btn_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = true;
            fileDialog.Title = "请选择DB文件";
            fileDialog.Filter = "db文件|*.db";
            if (fileDialog.ShowDialog() == DialogResult.OK) {
                string file = fileDialog.FileName;
                db_path.Text = file;
                m_pipeRelation.DBFilePath = file;
            }
        }

        private void SystemNameComBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (SystemNameComBox.Items != null && SystemNameComBox.Items.Count > 0 && SystemNameComBox.SelectedItem != null) {
                if (m_pipeRelation != null)
                    m_pipeRelation.SystemName = SystemNameComBox.SelectedItem.ToString();
            }
        }

        private void SubSystemNameComBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (SubSystemNameComBox.Items != null && SubSystemNameComBox.Items.Count > 0 && SubSystemNameComBox.SelectedItem != null) {
                if (m_pipeRelation != null)
                    m_pipeRelation.SubSystemName = SubSystemNameComBox.SelectedItem.ToString();
            }
        }

        private void TunnelName_TextChanged(object sender, TextChangedEventArgs e) {
            if (Tunnel.Items != null && Tunnel.Items.Count > 0 && Tunnel.SelectedItem != null) {
                if (m_pipeRelation != null)
                    m_pipeRelation.TunnelName = Tunnel.SelectedItem.ToString();
            }
        }

        private void TableName_TextChanged(object sender, TextChangedEventArgs e) {
            if (null != TableName.Text && m_pipeRelation != null) {
                try {
                    m_pipeRelation.TableName = TableName.Text.ToString();
                } catch (Exception) {
                    TaskDialog.Show("Revit", "Please input tableName.");
                }
            }
        }

        private void ColumnName_TextChanged(object sender, TextChangedEventArgs e) {
            if (null != ColumnName.Text && m_pipeRelation != null) {
                try {
                    m_pipeRelation.ColumnName = ColumnName.Text.ToString();
                } catch (Exception) {
                    TaskDialog.Show("Revit", "Please input ColumnName.");
                }
            }
        }

        private void IsPositiveDir_Click(object sender, RoutedEventArgs e) {
            if (m_pipeRelation != null) {
                if (IsPositiveDir.IsChecked == true) {
                    m_pipeRelation.IsPositiveDir = true;
                } else {
                    m_pipeRelation.IsPositiveDir = false;
                }
            }
        }

        private void IsWaterReturn_Click(object sender, RoutedEventArgs e) {
            if (m_pipeRelation != null) {
                if (IsWaterReturn.IsChecked == true) {
                    m_pipeRelation.IsWaterReturnPipe = true;
                } else {
                    m_pipeRelation.IsWaterReturnPipe = false;
                }
            }
        }

        private void SameSystemCheck_Click(object sender, RoutedEventArgs e) {

            if (m_pipeRelation != null) {
                if (SameSystemCheck.IsChecked == true) {
                    m_pipeRelation.IsSameSystem = true;
                } else {
                    m_pipeRelation.IsSameSystem = false;
                }
            }
        }

        private void IsIsolatedElemtns_Click(object sender, RoutedEventArgs e) {
            if (m_pipeRelation != null) {
                if (IsIsolatedElemtns.IsChecked == true) {
                    m_pipeRelation.IsIsolatedElements = true;
                } else {
                    m_pipeRelation.IsIsolatedElements = false;
                }
            }
        }

        private void AdvanceBtn_Click(object sender, RoutedEventArgs e) {
            ComplexPipeRelationPanel complexPipeRelationPanel = new ComplexPipeRelationPanel();
            complexPipeRelationPanel.Show();

            //if (complexPipeRelationPanel.ShowDialog() == true) {

            //}
        }

        private void CheckPipeRelation_Click(object sender, RoutedEventArgs e) {
            InitPipeRelation();
            m_pipeRelation.SelMethod = MtGlobals.PipeRelationMethods.CheckPipeRelation;
            m_pipeRelationEventHandler.Raise();
        }

        private void PipeRelationData_Btn_Click(object sender, RoutedEventArgs e) {
            InitPipeRelation();
            m_pipeRelation.SelMethod = MtGlobals.PipeRelationMethods.GetPipeRelation;
            m_pipeRelationEventHandler.Raise();
            //ResetUIState();
        }

        private void ResetUIState() {
            IsPositiveDir.IsChecked = true;
            IsWaterReturn.IsChecked = false;
            SameSystemCheck.IsChecked = true;
        }

        private void PipeRelationView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.Source is System.Windows.Controls.ListView) {
                PipeRelationError error = PipeRelationView.SelectedItem as PipeRelationError;

                if (error != null && error is PipeRelationError) {
                    string id = error.ID;
                    Element element = MtCommon.GetElementById(m_uidoc.Document, id);
                    MtCommon.ElementCenterDisplay(m_uidoc, element);

                    IList<ElementId> list = new List<ElementId>();
                    list.Add(element.Id);
                    m_uidoc.Selection.SetElementIds(list);
                }
            }
        }


        #endregion

        #region BasicInfo
        private void InitBasicInfo() {
            if (DistrictTextBox.Text != null)
                m_basicInfo.District = DistrictTextBox.Text;
            if (BuildingTextBox.Text != null)
                m_basicInfo.Building = BuildingTextBox.Text;
            m_basicInfo.IsMarkPipeInfos = MarkPipeInfoCheckBox.IsChecked == true ? true : false;

            if (DBFilePath.Text != null)
                m_basicInfo.m_sqliteFilePath = DBFilePath.Text;
            if (TableName_TextBox.Text != null)
                m_basicInfo.m_tableName = TableName_TextBox.Text.ToString();

            m_basicInfo.m_isClassifyColorByDep = DepColorCheckBox.IsChecked == true ? true : false;

            m_basicInfo.m_floorColor = MtCommon.TransToRevitColor(FloorColor_Btn.Background);
            m_basicInfo.m_corridorColor = MtCommon.TransToRevitColor(CorridorColor_Btn.Background);
        }

        private void DistrictTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (null != DistrictTextBox.Text) {
                try {
                    m_basicInfo.District = DistrictTextBox.Text;
                } catch (Exception) {
                    TaskDialog.Show("Revit", "Please input District name.");
                }
            }
        }

        private void BuildingTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (null != BuildingTextBox.Text) {
                try {
                    m_basicInfo.Building = BuildingTextBox.Text;
                } catch (Exception) {
                    TaskDialog.Show("Revit", "Please input Building name.");
                }
            }
        }

        private void MarkPipeInfoCheckBox_Click(object sender, RoutedEventArgs e) {
            if (MarkPipeInfoCheckBox.IsChecked == true) {
                m_basicInfo.IsMarkPipeInfos = true;
            } else {
                m_basicInfo.IsMarkPipeInfos = false;
            }
        }

        private void BasicInfo_Btn_Click(object sender, RoutedEventArgs e) {

            // m_basicInfo.SetBasicInfos();

            m_basicInfo.SelMethod = MtGlobals.BasicInfoMethods.MarkBasicInfo;
            m_basicInfoEventHandler.Raise();

            MtLog.message = "Mark BasicInfo Finished!";
            MessageContent.Content = MtLog.message;
        }

        private void BasicInfoListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.Source is System.Windows.Controls.ListView) {
                BasicInfoError error = BasicInfoListView.SelectedItem as BasicInfoError;

                if (error != null && error is BasicInfoError) {
                    string id = error.ID;
                    Element element = MtCommon.GetElementById(m_uidoc.Document, id);
                    MtCommon.ElementCenterDisplay(m_uidoc, element);

                    IList<ElementId> list = new List<ElementId>();
                    list.Add(element.Id);
                    m_uidoc.Selection.SetElementIds(list);
                }
            }
        }

        #region FloorTag
        private void OpenDB_Btn_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = true;
            fileDialog.Title = "请选择DB文件";
            fileDialog.Filter = "db文件|*.db";
            if (fileDialog.ShowDialog() == DialogResult.OK) {
                string file = fileDialog.FileName;
                DBFilePath.Text = file;
                m_basicInfo.m_sqliteFilePath = file;
            }
        }

        private void TableName_TextBox_TextChanged(object sender, TextChangedEventArgs e) {
            m_basicInfo.m_tableName = TableName_TextBox.Text.ToString();
        }

        private void WriteFloorData_Click(object sender, RoutedEventArgs e) {
            //m_basicInfo.SetData(m_uidoc.Document);
            InitBasicInfo();
            m_basicInfo.SelMethod = MtGlobals.BasicInfoMethods.WriteFloorInfo;
            m_basicInfoEventHandler.Raise();
        }

        private void MarkFloorData_Click(object sender, RoutedEventArgs e) {
            // m_basicInfo.MarkFloorInfo(m_uidoc.Document);
            InitBasicInfo();
            m_basicInfo.SelMethod = MtGlobals.BasicInfoMethods.MarkFloorInfo;
            m_basicInfoEventHandler.Raise();
        }

        private void FloorColor_Btn_Click(object sender, RoutedEventArgs e) {

            ColorDialog colorDialog = new ColorDialog();
            if (DialogResult.OK == colorDialog.ShowDialog()) {
                SolidBrush sb = new SolidBrush(colorDialog.Color);
                SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(sb.Color.A, sb.Color.R, sb.Color.G, sb.Color.B));
                FloorColor_Btn.Background = solidColorBrush;
            }
        }

        private void DepColorSelectBtn_Click(object sender, RoutedEventArgs e) {
            DepColorSettingPanel depColorSettingPanel = new DepColorSettingPanel();
            depColorSettingPanel.Show();
        }

        private void CorridorColor_Btn_Click(object sender, RoutedEventArgs e) {
            ColorDialog colorDialog = new ColorDialog();
            if (DialogResult.OK == colorDialog.ShowDialog()) {
                SolidBrush sb = new SolidBrush(colorDialog.Color);
                SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(sb.Color.A, sb.Color.R, sb.Color.G, sb.Color.B));
                CorridorColor_Btn.Background = solidColorBrush;
            }
        }

        private void SetFloorColor_Btn_Click(object sender, RoutedEventArgs e) {
            //m_basicInfo.SetFloorColor(m_uidoc.Document);
            InitBasicInfo();
            m_basicInfo.SelMethod = MtGlobals.BasicInfoMethods.SetDepColor;
            m_basicInfoEventHandler.Raise();
        }

        private void DepColorCheckBox_Click(object sender, RoutedEventArgs e) {
            if (DepColorCheckBox.IsChecked == true) {
                SetFloorColor_Btn.Content = "按科室配置颜色";
                m_basicInfo.m_isClassifyColorByDep = true;
            } else {
                SetFloorColor_Btn.Content = "设置楼板颜色";
                m_basicInfo.m_isClassifyColorByDep = false;
            }
        }

        private void MarkFloorBtn_Click(object sender, RoutedEventArgs e) {
            //m_basicInfo.CreateLevelFloorTags(m_uidoc.Document);
            InitBasicInfo();
            m_basicInfo.SelMethod = MtGlobals.BasicInfoMethods.MarkFloorTag;
            m_basicInfoEventHandler.Raise();
        }

        private void AddFloorTagBtn_Click(object sender, RoutedEventArgs e) {
            //m_basicInfo.AddFloorTags(m_uidoc.Document);
            InitBasicInfo();
            m_basicInfo.SelMethod = MtGlobals.BasicInfoMethods.AddFloorTag;
            m_basicInfoEventHandler.Raise();
        }

        #endregion

        #endregion

        #region Misc

        private void InitMisc() {
            if (tabelNameText.Text != null)
                m_misc.TableName = tabelNameText.Text.ToString();
            if (columnNameText.Text != null)
                m_misc.ColumnName = columnNameText.Text.ToString();
        }

        private void OpenDB_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = true;
            fileDialog.Title = "请选择DB文件";
            fileDialog.Filter = "db文件|*.db";
            if (fileDialog.ShowDialog() == DialogResult.OK) {
                string file = fileDialog.FileName;
                MiscDBFilePath.Text = file;
                m_misc.SqliteFilePath = file;
            }
        }

        #endregion

        private void SaveDataBtn_Click(object sender, RoutedEventArgs e) {
            m_misc.SelMethod = MtGlobals.MiscMethods.GetSwitchLightRelation;
            m_miscEventHandler.Raise();
        }

        private void tabelNameText_TextChanged(object sender, TextChangedEventArgs e) {
            if (tabelNameText.Text != null && m_misc != null)
                m_misc.TableName = tabelNameText.Text.ToString();
        }

        private void columnNameText_TextChanged(object sender, TextChangedEventArgs e) {
            if (columnNameText.Text != null && m_misc != null)
                m_misc.ColumnName = columnNameText.Text.ToString();
        }

        private void MultiSystem_TextChanged(object sender, TextChangedEventArgs e) {
            if (MultiSystem.Text != null && m_pipeRelation != null)
                m_pipeRelation.MultiSystem = MultiSystem.Text.ToString();
        }

        private void SameSystemCheck_Checked(object sender, RoutedEventArgs e) {

        }
    }
}
