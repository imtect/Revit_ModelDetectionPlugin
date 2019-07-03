using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ModelDetectionPlugin {
    public class MtLevel : IExternalEventHandler {
        UIApplication m_uiApp;
        UIDocument m_uIDocument;

        MtGlobals.LevelMethods m_selMethod;
        public MtGlobals.LevelMethods SelMethod {
            set { m_selMethod = value; }
        }

        Dictionary<string, double> m_dicLevelOffset;
        List<LevelOffset> m_listLevelOffset;
        Dictionary<string, LevelError> m_dicLevelErrors;
        Dictionary<string, Element> m_dicCorrectLevelPipeOrDust;
        Dictionary<string, Level> m_levels;

        public MtLevel() {
            m_dicLevelOffset = new Dictionary<string, double>();
            m_listLevelOffset = new List<LevelOffset>();
            m_dicLevelErrors = new Dictionary<string, LevelError>();
            m_dicCorrectLevelPipeOrDust = new Dictionary<string, Element>();
            m_levels = new Dictionary<string, Level>();
        }

        public void Execute(UIApplication uiapp) {
            m_uiApp = uiapp;
            m_uIDocument = m_uiApp.ActiveUIDocument;

            Transaction trans = new Transaction(m_uIDocument.Document, "Level");
            trans.Start();

            switch (m_selMethod) {
                case MtGlobals.LevelMethods.None:
                    break;
                case MtGlobals.LevelMethods.MarkVerticalPipe:
                    MarkVerticalPipe();
                    break;
                case MtGlobals.LevelMethods.CheckLevel:
                    CheckPipeLevel();
                    break;
                case MtGlobals.LevelMethods.AutoAdjustLevel:
                    AutoAdjustLevel();
                    //SetLevel();
                    break;
                case MtGlobals.LevelMethods.CheckNoStandardSystemName:
                    CheckNoStandardSystemName();
                    break;
                case MtGlobals.LevelMethods.CheckInConsistentSystemName:
                    CheckInConsistentSystemName();
                    break;
                default:
                    break;
            }
            trans.Commit();
        }

        public string GetName() {
            return "Level";
        }

        #region Level
        public void MarkVerticalPipe() {

            GetALLLevelOffset();

            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            foreach (var ele in collector) {
                string levelName = GetPipeLevel(ele);
                string offset = GetPipeOffset(ele);
                string category = ele.Category.Name;

                if (category.Equals(MtGlobals.PipeCategory) || category.Equals(MtGlobals.DustCategory)) {

                    if (ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.VerticalPipe)) == null) continue;

                    string startoffset = GetPipeStartOffset(ele);
                    string endoffset = GetPipeEndOffset(ele);
                    bool isVerticalPipe = CheckIsVerticalPipe(ele, startoffset, endoffset);
                    if (isVerticalPipe) {
                        MarkLongVerticalPipeOrDust(ele, levelName, startoffset, endoffset);
                    } else {
                        MtCommon.SetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.VerticalPipe), "0");
                    }
                }
            }
        }

        public void CheckPipeLevel() {
            ClearDic();
            GetALLLevelOffset();

            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            foreach (var ele in collector) {
                string levelName = GetPipeLevel(ele);
                string offset = GetPipeOffset(ele);

                string category = ele.Category.Name;
                if (category.Equals(MtGlobals.PipeCategory) || category.Equals(MtGlobals.DustCategory)) {
                    PipeOrDuctDetection(ele, levelName, offset);
                } else {
                    NonPipeOrDuctDetection(ele, levelName, offset);
                }
            }

            IList<LevelError> levelErrors = m_dicLevelErrors.Select(v => v.Value).ToList();
            levelErrors = levelErrors.OrderBy(v => v.FamilyName).ToList();
            SetErrorListView(levelErrors);
        }

        public void AutoAdjustLevel() {
            GetALLLevelOffset();

            foreach (var item in m_dicLevelErrors) {
                Element ele = MtCommon.GetElementById(m_uIDocument.Document, item.Key);

                if (ele != null) {

                    string offset = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Offset));
                    string startOffset = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.StartOffset));
                    string endOffset = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.EndOffset));

                    string _level = string.Empty;
                    string level = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Level));
                    if (string.IsNullOrEmpty(level) || level.Equals(MtGlobals.Parameters.NoParam.ToString()))
                        _level = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.ReferenceLevel));
                    else
                        _level = level;

                    string correctLevel = string.Empty;
                    double offsetValue = double.Parse(offset);
                    double correctOffsetValue = offsetValue;

                    if (offsetValue > 0) {
                        bool isFirst = false;
                        for (int i = 0; i < m_listLevelOffset.Count; i++) {
                            LevelOffset leveloffset = m_listLevelOffset[i];
                            if (leveloffset.levelName != _level && !isFirst) continue;
                            else {
                                isFirst = true;

                                if (correctOffsetValue >= leveloffset.levelOffsetValue) {
                                    correctOffsetValue -= leveloffset.levelOffsetValue;
                                } else {
                                    correctLevel = leveloffset.levelName;
                                    break; //获得正确标高跳出
                                }
                            }
                        }
                    } else {
                        bool isFirst = false;
                        correctOffsetValue = Math.Abs(offsetValue);
                        for (int i = m_listLevelOffset.Count - 1; i >= 0; i--) {
                            LevelOffset leveloffset = m_listLevelOffset[i];
                            if (leveloffset.levelName != _level && !isFirst) continue;
                            else {
                                isFirst = true;

                                if (correctOffsetValue >= leveloffset.levelOffsetValue) {
                                    correctOffsetValue -= leveloffset.levelOffsetValue;
                                } else {
                                    correctLevel = leveloffset.levelName;
                                    break;
                                }
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(level) || level.Equals(MtGlobals.Parameters.NoParam.ToString())) {
                        if (m_levels != null && m_levels.ContainsKey(correctLevel)) {
                            Parameter param = ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.ReferenceLevel));
                            if (param != null)
                                param.Set(m_levels[correctLevel].LevelId);
                        }
                    } else {
                        if (m_levels != null && m_levels.ContainsKey(correctLevel)) {
                            ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.Offset)).Set(correctOffsetValue);
                            Parameter param = ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.Level));
                        }
                    }
                }
            }

            CheckPipeLevel();
        }

        public void MarkLongVerticalPipeOrDust(Element ele, string levelName, string startoffset, string endoffset) {
            if (ValideParams(ele, levelName, startoffset, endoffset)) {
                double LevelOffset = m_dicLevelOffset[levelName];
                double startoffsetV = double.Parse(startoffset);
                double endoffsetV = double.Parse(endoffset);

                if (Math.Abs(startoffsetV - endoffsetV) > LevelOffset) { //只有跨楼层的竖管才标记
                    MtCommon.SetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.VerticalPipe), "1-1");
                } else {
                    MtCommon.SetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.VerticalPipe), "1");
                }
            }
        }

        //检测管道和风管
        public void PipeOrDuctDetection(Element ele, string levelName, string offset) {
            string errorMsg = string.Empty;
            if (!IsLevelCorrect(ele, levelName, offset, out errorMsg)) {
                if (!string.IsNullOrEmpty(errorMsg))
                    AddListViewErrorData(ele, errorMsg);
            }
        }

        //检测非管道或风管
        public void NonPipeOrDuctDetection(Element ele, string levelName, string offset) {
            string errorMsg = string.Empty;
            if (!IsLevelCorrect(ele, levelName, offset, out errorMsg)) {
                if (!string.IsNullOrEmpty(errorMsg))
                    AddListViewErrorData(ele, errorMsg);
            }
        }

        bool ValideParams(Element ele, string levelName, string startoffset, string endoffset) {
            if (ele != null && !string.IsNullOrEmpty(levelName) &&
                !string.IsNullOrEmpty(startoffset) && !string.IsNullOrEmpty(endoffset)) {
                return true;
            } else
                return false;
        }

        bool IsLevelCorrect(Element ele, string level, string offset, out string errorMsg) {
            bool isLevelCorrect = false;
            string errorMessage = string.Empty;
            if (!string.IsNullOrEmpty(level) && !string.IsNullOrEmpty(offset)) {
                double levelOffset = 0;
                if (m_dicLevelOffset.ContainsKey(level)) {
                    levelOffset = m_dicLevelOffset[level];
                    double offsetValue = double.Parse(offset);

                    if (offsetValue < 0) {
                        //将给排水中的排水管道排除,排水的管道位于本层标高的下方，该排水管道的标高属于本层
                        string systemName = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.SystemName));
                        if (!string.IsNullOrEmpty(systemName) && (systemName.Contains("排水") || systemName.Contains("废水"))) {
                            if (offsetValue < 0 && Math.Abs(offsetValue) < levelOffset)
                                isLevelCorrect = true;
                            else {
                                isLevelCorrect = false;
                                errorMessage = MtCommon.GetStringValue(ErrorType.NegLevelOffset);
                            }
                        } else {
                            isLevelCorrect = false;
                            errorMessage = MtCommon.GetStringValue(ErrorType.NegLevelOffset);
                        }
                    } else if (offsetValue > 0 && offsetValue > levelOffset) {
                        isLevelCorrect = false;
                        errorMessage = MtCommon.GetStringValue(ErrorType.PosLevelOffset);
                    } else {
                        isLevelCorrect = true;
                    }
                }
            }
            errorMsg = errorMessage;
            return isLevelCorrect;
        }

        public bool CheckIsVerticalPipe(Element ele, string startOffset, string endOffset) {
            bool isVertical = false;
            if (ele != null && !string.IsNullOrEmpty(startOffset)
                && !string.IsNullOrEmpty(endOffset)) {

                if (double.Parse(startOffset) != double.Parse(endOffset))
                    isVertical = true;
                else
                    isVertical = false;
            }
            return isVertical;
        }

        public void SetErrorListView(ICollection<LevelError> elementIds) {
            ListView levelListView = Ribbon.instance.ModelDetectionPanel.LevelListView;
            if (elementIds != null && levelListView != null) {
                if (levelListView != null) {
                    levelListView.ItemsSource = elementIds;
                    SetListViewMsg("总数为：" + elementIds.Count);
                }
            }
        }

        public void SetListViewMsg(string msg) {
            Label listViewMsg = Ribbon.instance.ModelDetectionPanel.LevelListViewMsg;
            if (listViewMsg != null) {
                listViewMsg.Content = msg;
            }
        }

        void AddListViewErrorData(Element ele, string errorType = null) {
            string eleId = ele.Id.ToString();
            string famliyName = MtCommon.GetElementFamilyName(m_uIDocument.Document, ele);
            string typeName = MtCommon.GetElementType(m_uIDocument.Document, ele);
            string message = errorType;
            LevelError error = CreateBasicInfoError(ele.Id.ToString(), famliyName, typeName, message);

            if (!m_dicLevelErrors.ContainsKey(ele.Id.ToString())) {
                m_dicLevelErrors.Add(eleId, error);
            }
        }

        public LevelError CreateBasicInfoError(string id, string familyName, string typeName, string errorMsg) {
            LevelError error = new LevelError();
            error.ID = id;
            error.FamilyName = familyName;
            error.TypeName = typeName;
            error.ErrorMsg = errorMsg;
            return error;
        }

        private void GetALLLevelOffset() {
            m_listLevelOffset.Clear();
            m_levels.Clear();
            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            ICollection<Element> collection = collector.OfClass(typeof(Level)).ToElements();

            List<double> levelValues = new List<double>();
            Dictionary<double, string> levelValueToNameDic = new Dictionary<double, string>();
            foreach (var item in collection) {
                Level level = item as Level;
                if (null != level) {
                    string levelName = level.Name;
                    m_levels.Add(levelName, level);
                    double levelValue = level.Elevation * MtGlobals.InchToMillimeter;//单位转化：英寸转化为毫米
                    if (!levelValues.Contains(levelValue)) {
                        levelValues.Add(levelValue);
                    }
                    if (!levelValueToNameDic.ContainsKey(levelValue))
                        levelValueToNameDic.Add(levelValue, levelName);
                }
            }

            levelValues.Sort(); //排序

            for (int i = 0; i < levelValues.Count - 1; i++) {
                double levelOffSet = levelValues[i + 1] - levelValues[i];
                string levelName = levelValueToNameDic[levelValues[i]];
                if (!m_dicLevelOffset.ContainsKey(levelName))
                    m_dicLevelOffset.Add(levelName, levelOffSet);

                AddLevelOffset(levelName, levelOffSet);
            }

            string lastLevelName = levelValueToNameDic[levelValues[levelValues.Count - 1]];//加入最后一层标高
            if (!m_dicLevelOffset.ContainsKey(lastLevelName))
                m_dicLevelOffset.Add(lastLevelName, MtGlobals.DefaultLevelOffset);

            AddLevelOffset(lastLevelName, MtGlobals.DefaultLevelOffset);
        }

        //用于自动调整正确标高
        class LevelOffset {
            public string levelName;
            public double levelOffsetValue;
        }

        void AddLevelOffset(string levelname, double value) {
            LevelOffset levelOffset = new LevelOffset();
            levelOffset.levelName = levelname;
            levelOffset.levelOffsetValue = value;
            m_listLevelOffset.Add(levelOffset);
        }

        void ClearDic() {
            m_dicLevelErrors.Clear();
            m_dicCorrectLevelPipeOrDust.Clear();
            m_dicLevelOffset.Clear();
            m_listLevelOffset.Clear();
        }
        #endregion

        #region GetParamters

        private string GetPipeOffset(Element ele) {
            return MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Offset));
        }
        private string GetPipeStartOffset(Element ele) {
            return MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.StartOffset));
        }
        private string GetPipeEndOffset(Element ele) {
            return MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.EndOffset));
        }
        private string GetPipeLevel(Element ele) {
            string referenceLevel = string.Empty;
            referenceLevel = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.ReferenceLevel));
            if (string.IsNullOrEmpty(referenceLevel))
                referenceLevel = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Level));
            return referenceLevel;
        }
        #endregion

        #region SystemName
        List<string> m_standardSystemNames = new List<string>();
        Dictionary<string, List<string>> m_dicPipeSystemNames = new Dictionary<string, List<string>>(); //eleID, eleSystemName

        void CheckNoStandardSystemName() {
            m_dicLevelErrors.Clear();
            ClearSystemNameDic();

            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            m_standardSystemNames = GetSelectedSystemNames();

            foreach (var ele in collector) {
                IsStandardSystemName(ele);
            }

            IList<LevelError> levelErrors = m_dicLevelErrors.Select(v => v.Value).ToList();
            levelErrors = levelErrors.OrderBy(v => v.FamilyName).ToList();
            SetErrorListView(levelErrors);
        }

        void CheckInConsistentSystemName() {
            m_dicLevelErrors.Clear();

            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            try {
                foreach (var ele in collector) {

                    IsConsistenSystemName(ele);
                }

                IList<LevelError> levelErrors = m_dicLevelErrors.Select(v => v.Value).ToList();
                levelErrors = levelErrors.OrderBy(v => (v.FamilyName + v.TypeName)).ToList();
                SetErrorListView(levelErrors);

            } catch (Exception e) {

                throw new SystemException(e.Message);
            }

        }

        void IsStandardSystemName(Element ele) {
            string systemName = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.SystemName));

            if (!string.IsNullOrEmpty(systemName)) {
                List<string> eleSystemNames = MtCommon.RemoveNumInComplexString(systemName); //可能包含多个系统名称

                foreach (var name in eleSystemNames) {
                    if (!m_standardSystemNames.Contains(name)) {
                        AddListViewErrorData(ele, MtCommon.GetStringValue(ErrorType.NotStandardSystemName) + name); //不符合标准
                    } else {
                        string eleId = ele.Id.ToString();
                        if (!m_dicPipeSystemNames.ContainsKey(eleId)) {
                            m_dicPipeSystemNames.Add(ele.Id.ToString(), new List<string>());
                            m_dicPipeSystemNames[eleId].Add(name);
                        } else {
                            if (!m_dicPipeSystemNames[eleId].Contains(name))
                                m_dicPipeSystemNames[eleId].Add(name);
                        }
                    }
                }
            } else {
                AddListViewErrorData(ele, MtCommon.GetStringValue(ErrorType.NoParameter) +
                    MtCommon.GetStringValue(MtGlobals.Parameters.SystemName) + " 或系统名称为空"); //Element可能不存在系统名称参数
            }
        }

        void IsConsistenSystemName(Element ele) {
            bool isconsistent = CheckConnectedElementsIsSameSystemName(ele);
            if (!isconsistent) {
                AddListViewErrorData(ele, MtCommon.GetStringValue(ErrorType.InConsistentSystemName));
            }
        }
        //int index = 0;
        bool CheckConnectedElementsIsSameSystemName(Element ele) {

            //string filepath = "C:/Users/1/Desktop/total_id.txt";
            //if (!File.Exists(filepath))
            //    File.Create(filepath).Close();

            //string content = File.ReadAllText(filepath);
            //content += index++ + ":   " + ele.Id.ToString() + "\r\n";
            //File.WriteAllText(filepath, content);




            List<string> eleSystemNames = GetOnePipeSystemNames(ele);

            //系统名称为空
            if (eleSystemNames.Count == 0) {
                AddListViewErrorData(ele, MtCommon.GetStringValue(ErrorType.ParameterIsNull) +
                   MtCommon.GetStringValue(MtGlobals.Parameters.SystemName));
            }


            List<Element> elements = GetConnectElements(ele);
            if (elements.Count == 0) {
                AddListViewErrorData(ele, "连接的元素为空");
            }

            bool isSameSystemName = true;

            foreach (var item in elements) {
                List<string> connSystemNames = GetOnePipeSystemNames(item);
                bool isSame = isHaveSameSystemName(eleSystemNames, connSystemNames);
                if (!isSame) {
                    isSameSystemName = false;
                    break;
                }
            }
            return isSameSystemName;



            //List<string> sysNames = new List<string>();
            //if (m_dicPipeSystemNames.ContainsKey(ele.Id.ToString())) {
            //    sysNames = m_dicPipeSystemNames[ele.Id.ToString()];
            //} else {
            //    //字典中不包含
            //}

            //List<Element> elements = GetConnectElements(ele);
            //bool isSameSystemName = true;
            //foreach (var item in elements) {
            //    string eleId = item.Id.ToString();
            //    if (m_dicPipeSystemNames.ContainsKey(eleId)) {
            //        bool isSame = isHaveSameSystemName(sysNames, m_dicPipeSystemNames[eleId]);
            //        if (!isSame) {
            //            isSameSystemName = false;
            //            break;
            //        }
            //    } else {
            //        //字典中不包含
            //    }
            //}
            //return isSameSystemName;
        }

        List<string> GetOnePipeSystemNames(Element ele) {
            string systemName = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.SystemName));
            List<string> eleSystemNames = new List<string>();
            if (!string.IsNullOrEmpty(systemName)) {
                eleSystemNames = MtCommon.RemoveNumInComplexString(systemName); //可能包含多个系统名称
            }
            return eleSystemNames;
        }

        bool isHaveSameSystemName(List<string> eleSysNames, List<string> connEleSysNames) {

            bool isSame = false;
            if (eleSysNames != null && eleSysNames.Count != 0
                && connEleSysNames != null && connEleSysNames.Count != 0) {

                foreach (var item in eleSysNames) {
                    if (eleSysNames.Count == 1) { //若Element仅有一个系统名称
                        if (connEleSysNames.Contains(item))
                            isSame = true;
                    } else {
                        if (connEleSysNames.Count == 1) { //若Element有多个系统名称，并且与之相连的只有一个系统名称
                            if (eleSysNames.Contains(connEleSysNames[0]))
                                isSame = true;
                        }
                    }
                }
            }
            return isSame;
        }

        List<Element> GetConnectElements(Element ele) {
            List<Element> list = new List<Element>();
            try {
                ConnectorSet connectorSet = MtCommon.GetAllConnectors(ele);

                if (connectorSet != null && connectorSet.Size != 0) {
                    foreach (Connector item in connectorSet) {
                        if (item.Domain != Domain.DomainElectrical) {
                            Connector connector = MtCommon.GetConnectedConnector(item);

                            if (connector != null) {
                                Element element = connector.Owner;
                                list.Add(element);
                            }
                        }
                    }
                } else {
                    //模型自身没有连接点
                    AddListViewErrorData(ele, "模型自身没有连接点");
                }
            } catch (Exception e) {

                throw new Exception(e.Message);
            }
            return list;
        }

        List<string> GetSelectedSystemNames() {
            List<string> names = new List<string>();

            foreach (System.Windows.Controls.CheckBox item in Ribbon.instance.ModelDetectionPanel.StandardSysList.Items) {
                if (item.IsChecked == true)
                    names.Add(item.Content.ToString());
            }
            return names;
        }

        void ClearSystemNameDic() {
            m_standardSystemNames.Clear();
            m_dicPipeSystemNames.Clear();
        }
        #endregion
    }
}
