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
using static ModelDetectionPlugin.MtGlobals;
using System.Text.RegularExpressions;

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

        //public string CampusCode;
        //public string SystemCode;

        public string m_SystemCode;

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

            Transaction trans = new Transaction(m_uIDocument.Document, "Misc");
            trans.Start();

            switch (m_selMethod) {
                case MtGlobals.MiscMethods.None:
                    break;
                case MtGlobals.MiscMethods.GetSwitchLightRelation:
                    GetSwitchLightRelation();
                    break;
                case MtGlobals.MiscMethods.EncodeEquipment:
                    EncodeEquipment();
                    break;
                case MtGlobals.MiscMethods.GetPipePiameter:
                    GetPipePiameter();
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

        #region Light

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

            string district = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Campus));
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

        #endregion

        #region EncodeEquipment
        void EncodeEquipment() {
            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);
            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            foreach (var ele in collector) {
                var catogary = ele.Category.Name;
                var familyName = MtCommon.GetElementFamilyName(m_uIDocument.Document, ele);
                if (familyName.Contains("风盘") || familyName.Contains("风机盘管")) continue;
                if (catogary.Equals(MtGlobals.EquipmentCategory) || catogary.Equals(MtGlobals.ElecticCategory)) {
                    //temp
                    MtCommon.SetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.EquipmentCode), familyName.Substring(0, 5));
                }
            }
        }
        #endregion

        #region GetPipePiameter

        void GetPipePiameter() {
            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            Dictionary<string, float> m_dic = new Dictionary<string, float>();
            List<Element> eles = new List<Element>();
            float piameter = 0;

            MtCommon.WriteStringIntoText("总共：" + collector.Count().ToString());

            int index = 0;

            foreach (var ele in collector) {
                var content = MtCommon.ReadStringFromText();
                content += "\r\n" + $"索引{index}:" + ele.Id.ToString();
                MtCommon.WriteStringIntoText(content);
                index++;
                string eleID = ele.Id.ToString();

                //判断是否是设备，通过设备编码参数，判断是否为设备,设备的直径默认设置为10000
                var equipCode = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(Parameters.EquipmentCode));
                if (!string.IsNullOrEmpty(equipCode)) {
                    piameter = 10000f;
                } else {
                    //获取管道直径参数：只要包含“直径”二字的选择一个作为直径参数
                    var paramter = GetParameterWithPiameterParam(ele, "直径");
                    if (paramter != null && paramter.Count != 0) {
                        foreach (var item in paramter) {
                            piameter += float.Parse(item.AsValueString());
                        }
                        piameter = piameter / paramter.Count;
                    } else {
                        //没有直径参数的，利用宽高等参数判断，取中值
                        var width = GetParameterWithPiameterParam(ele, "风管宽度").FirstOrDefault(); ;
                        var height = GetParameterWithPiameterParam(ele, "风管高度").FirstOrDefault();

                        if (width != null && height != null) {
                            var widthValue = float.Parse(width.AsValueString());
                            var heightValue = float.Parse(height.AsValueString());
                            piameter = (widthValue + heightValue) / 2;
                        } else {
                            //利用尺寸200x100,200x100200x100,只取前后两位计算
                            var size = MtCommon.GetOneParameter(ele, "尺寸");
                            if (!string.IsNullOrEmpty(size)) {
                                List<string> values = new List<string>();
                                if (size.Contains("×")) {
                                    values = size.Split('×').ToList();
                                    piameter = (int.Parse(values[0]) + int.Parse(values[values.Count - 1])) / 2;
                                } else if (size.Contains("-")) {
                                    values = size.Split('-').ToList();
                                    piameter = (int.Parse(values[0]) + int.Parse(values[values.Count - 1])) / 2;
                                } else {
                                    //正则表达式提取数字
                                    MatchCollection mc0 = Regex.Matches(size, @"/d+(/./d+)?");
                                    double sum = 0;
                                    foreach (var item in mc0) {
                                        sum += Convert.ToDouble(item);
                                    }
                                    piameter = (int)(sum / mc0.Count);
                                }
                            } else {
                                piameter = 0f;
                            }
                        }
                    }
                }

                var campus = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(Parameters.Campus));
                var build = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(Parameters.Building));
                var level = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(Parameters.Level));
                var pipeId = campus + "-" + build + "-" + level + "_" + m_SystemCode + "_" + ele.Id;

                if (!m_dic.ContainsKey(pipeId)) {
                    m_dic.Add(pipeId, piameter);

                    eles.Add(ele);

                    //MtCommon.SetOneParameter(ele, "空间编码", piameter.ToString());
                }
            }

            m_sqlite = new MtSQLite(m_sqliteFilePath);
            List<string> quarays = new List<string>();
            foreach (var item in m_dic) {
                //quarays.Add($"Update Pipe Set diameter = '{item.Value}' where code = '{item.Key}'");
                quarays.Add($"Insert into Pipe (code,diameter) values ('{item.Key}','{item.Value}')");
            }
            m_sqlite.ExecuteNoneQuery(quarays.ToArray());

            MtCommon.HideElements(m_uIDocument.Document, eles);
        }

        List<Parameter> GetParameterWithPiameterParam(Element ele, string symble) {
            if (ele == null) return null;
            List<Parameter> parameters = new List<Parameter>();
            foreach (Parameter item in ele.Parameters) {
                if (item.Definition.Name.Contains(symble)) {
                    parameters.Add(item);
                }
            }
            return parameters;
        }
        #endregion
    }
}
