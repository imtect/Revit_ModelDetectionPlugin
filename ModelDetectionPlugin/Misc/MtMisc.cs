using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace ModelDetectionPlugin {
    public class MtMisc : IExternalEventHandler {
        UIApplication m_uiApp;
        UIDocument m_uIDocument;
        MtGlobals.MiscMethods m_selMethod;
        public MtGlobals.MiscMethods SelMethod {
            set { m_selMethod = value; }
        }

        private string m_sqliteFilePath;
        public string SqliteFilePath {
            get { return m_sqliteFilePath; }
            set { m_sqliteFilePath = value; }
        }

        private string m_tableName;
        public string TableName {
            get { return m_tableName; }
            set { m_tableName = value; }
        }

        private string m_columnName;
        public string ColumnName {
            get { return m_columnName; }
            set { m_columnName = value; }
        }

        MtSQLite m_sqlite;

        Dictionary<Element, string> m_switchDic;
        Dictionary<Element, string> m_lightDic;
        Dictionary<Element, string> m_spoolDic;
        Dictionary<string, Dictionary<string, string>> m_switchLightRelationDic;

        public MtMisc() {
            m_switchDic = new Dictionary<Element, string>();
            m_lightDic = new Dictionary<Element, string>();
            m_spoolDic = new Dictionary<Element, string>();
            m_switchLightRelationDic = new Dictionary<string, Dictionary<string, string>>();
        }

        public void Execute(UIApplication uiapp) {
            m_uiApp = uiapp;
            m_uIDocument = m_uiApp.ActiveUIDocument;

            Transaction trans = new Transaction(m_uIDocument.Document, "Level");
            trans.Start();

            switch (m_selMethod) {
                case MtGlobals.MiscMethods.None:
                    break;
                case MtGlobals.MiscMethods.GetSwitchLightRelation:
                    GetSwitchLightRelation();
                    break;
                default:
                    break;
            }
            trans.Commit();
        }

        public string GetName() {
            return "Misc";
        }

        void GetSwitchLightRelation() {
            TravelSwitchLight();
            SaveRelationData();
            ClearMiscDic();
        }


        void TravelSwitchLight() {
            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);
            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            foreach (var ele in collector) {
                if (ele.Category.Name.Contains("灯具")) {
                    string label = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Note));
                    if (!string.IsNullOrEmpty(label))
                        m_switchDic.Add(ele, label);
                }
                if (ele.Category.Name.Contains("照明设备")) {
                    string label = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Note));
                    if (!string.IsNullOrEmpty(label))
                        m_lightDic.Add(ele, label);
                }

                if (ele.Category.Name.Contains("线管") || ele.Category.Name.Contains("线管配件")) {
                    string label = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Note));
                    if (!string.IsNullOrEmpty(label))
                        m_spoolDic.Add(ele, label);
                }
            }

            foreach (var item in m_switchDic) {
                string switchId = RenameEleId(item.Key);
                if (!m_switchLightRelationDic.ContainsKey(switchId))
                    m_switchLightRelationDic.Add(switchId, new Dictionary<string, string>());

                var ligths = from data in m_lightDic
                             where data.Value == item.Value
                             select data.Key;

                foreach (var light in ligths) {
                    string lightId = RenameEleId(light);
                    if (!m_switchLightRelationDic[switchId].ContainsKey(lightId))
                        m_switchLightRelationDic[switchId].Add(lightId, "0");
                }

                var spools = from data in m_spoolDic
                             where data.Value == item.Value
                             select data.Key;

                foreach (var spool in spools) {
                    string spoolId = RenameEleId(spool);
                    if (!m_switchLightRelationDic[switchId].ContainsKey(spoolId))
                        m_switchLightRelationDic[switchId].Add(spoolId, "1");
                }
            }
        }

        string RenameEleId(Element ele) {
            if (ele == null) return string.Empty;

            string district = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Distribute));
            string building = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Building));
            string level = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.MtLevel));

            if (!string.IsNullOrEmpty(district) && !string.IsNullOrEmpty(building) && !string.IsNullOrEmpty(level)) {
                return district + "-" + building + "-" + level + "_PD_" + ele.Id;
            } else {
                TaskDialog.Show("Error", "Basic info is empty : " + ele.Id);
                return string.Empty;
            }
        }

        void SaveRelationData() {
            m_sqlite = new MtSQLite(m_sqliteFilePath);

            string[] _columnName = m_columnName.Split(';');
            List<string[]> listColumnValues = new List<string[]>();

            foreach (var _switch in m_switchLightRelationDic) {
                foreach (var ctrlObj in _switch.Value) {
                    string switchCode = "'" + _switch.Key + "'";
                    string ctrlObjCode = "'" + ctrlObj.Key + "'";
                    string typeCode = "'" + ctrlObj.Value + "'";
                    listColumnValues.Add(new string[] { switchCode, ctrlObjCode, typeCode });
                }
            }
            m_sqlite.InsertIntoList(m_tableName, _columnName, listColumnValues);
        }

        void ClearMiscDic() {
            m_switchDic.Clear();
            m_lightDic.Clear();
            m_switchLightRelationDic.Clear();
        }
    }
}
