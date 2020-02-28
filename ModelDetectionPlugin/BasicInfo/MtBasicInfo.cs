using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows.Controls;

namespace ModelDetectionPlugin {
    public class MtBasicInfo : IExternalEventHandler {

        UIApplication m_uiApp;
        UIDocument m_uIDocument;
        Selection m_selection;

        MtGlobals.BasicInfoMethods m_selMethod;
        public MtGlobals.BasicInfoMethods SelMethod {
            get { return m_selMethod; }
            set { m_selMethod = value; }
        }

        #region Varables
        private string m_district;
        private string m_building;
        private bool m_isEnVersion;
        private bool m_isMarkPipeInfos;

        public string m_sqliteFilePath;
        MtSQLite m_sqlite;
        public string m_tableName;
        Dictionary<string, string> m_floorInfo = new Dictionary<string, string>();
        public Autodesk.Revit.DB.Color m_floorColor;
        public Autodesk.Revit.DB.Color m_corridorColor;
        public bool m_isClassifyColorByDep;

        Dictionary<ElementId, BasicInfoError> m_ltBasicInfoErrors;

        public string District {
            get {
                return m_district;
            }
            set {
                m_district = value;
            }
        }

        public string Building {
            get {
                return m_building;
            }
            set {
                m_building = value;
            }
        }

        public bool IsMarkPipeInfos {
            get {
                return m_isMarkPipeInfos;
            }
            set {
                m_isMarkPipeInfos = value;
            }
        }
        #endregion

        public MtBasicInfo() {
            m_ltBasicInfoErrors = new Dictionary<ElementId, BasicInfoError>();
        }

        public void Execute(UIApplication uiApplication) {
            m_uiApp = uiApplication;
            m_uIDocument = m_uiApp.ActiveUIDocument;

            Transaction trans = new Transaction(m_uIDocument.Document, "BasicInfo");
            trans.Start();

            switch (m_selMethod) {
                case MtGlobals.BasicInfoMethods.CheckBasicInfo:
                    CheckBasicInfos();
                    break;



                case MtGlobals.BasicInfoMethods.MarkBasicInfo:
                    SetBasicInfos();
                    break;
                case MtGlobals.BasicInfoMethods.WriteFloorInfo:
                    SetData(m_uIDocument.Document);
                    break;
                case MtGlobals.BasicInfoMethods.MarkFloorInfo:
                    MarkFloorInfo(m_uIDocument.Document);
                    break;
                case MtGlobals.BasicInfoMethods.SetDepColor:
                    SetFloorColor(m_uIDocument.Document);
                    break;
                case MtGlobals.BasicInfoMethods.MarkFloorTag:
                    CreateLevelFloorTags(m_uIDocument.Document);
                    break;
                case MtGlobals.BasicInfoMethods.AddFloorTag:
                    AddFloorTags(m_uIDocument.Document);
                    break;
                default:
                    break;
            }
            trans.Commit();
        }

        public string GetName() {
            return string.Empty;
        }

        #region CheckBasicInfo

        public void CheckBasicInfos() {
            m_ltBasicInfoErrors.Clear();
            //if (m_isMarkPipeInfos) {
            CheckPipeBasicInfo();
            //} else {
            //    CheckBuildingBasicInfo();
            //}
            IList<BasicInfoError> errorList = m_ltBasicInfoErrors.Select(v => v.Value).ToList().OrderBy(v => (v.FamilyName + v.TypeName)).ToList();
            SetErrorListView(errorList);
        }


        public void CheckPipeBasicInfo() {
            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            foreach (var ele in collector) {

                CheckPipeParameters(ele);//是否有基础信息

                string category = ele.Category.Name;
                if (category.Equals(MtGlobals.PipeCategory) || category.Equals(MtGlobals.DustCategory)) {
                    GetParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.VerticalPipe)); //检测竖管是否有参数
                } else if (category.Equals(MtGlobals.EquipmentCategory)) {
                    if (ele.Name.Contains("风盘") || ele.Name.Contains("风机盘管")) continue;
                    GetParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.EquipmentCode)); //设备是否有编码
                }
            }
            //MtCommon.IsolateElements(m_uIDocument.Document, m_ltBasicInfoErrors.Select(k => k.Key).ToList());
        }

        private void CheckBuildingBasicInfo() {

        }

        private void CheckPipeParameters(Element ele) {
            GetParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Campus));
            GetParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Building));
            GetParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.MtLevel));
            GetParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.SubDistrict));
        }

        private void GetParameter(Element ele, string paramName) {
            if (ele == null || string.IsNullOrEmpty(paramName)) return;
            Parameter param = ele.LookupParameter(paramName);
            string errorType = string.Empty;
            if (null != param) {
                if (param.AsString() == null) {
                    errorType = MtCommon.GetStringValue(ErrorType.ParameterIsNull) + paramName;
                    AddListViewErrorData(ele, errorType);
                }
            } else {
                errorType = MtCommon.GetStringValue(ErrorType.NoParameter) + paramName;
                AddListViewErrorData(ele, errorType);
            }
        }


        #endregion



















        public void SetBasicInfos() {
            m_ltBasicInfoErrors.Clear();
            if (m_isMarkPipeInfos) {
                SetPipeBasicInfo();
            } else {
                SetBuildingBasicInfo();
            }
            IList<BasicInfoError> errorList = m_ltBasicInfoErrors.Select(v => v.Value).ToList().OrderBy(v => (v.FamilyName + v.TypeName)).ToList();
            SetErrorListView(errorList);
        }


        public void SetBuildingBasicInfo() {
            //仅设置楼板的基本参数
            ElementCategoryFilter elementCategoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            FilteredElementCollector collectors = new FilteredElementCollector(m_uIDocument.Document);
            IList<Element> elementLists = collectors.WherePasses(elementCategoryFilter).WhereElementIsNotElementType().ToElements();
            foreach (var floor in elementLists) {
                SetParameters(floor, false, m_isEnVersion);
            }
        }

        public void SetPipeBasicInfo() {
            //设置全部管道的基本参数
            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);
            foreach (var item in collector) {
                SetParameters(item, true, m_isEnVersion);
            }
        }

        public void SetErrorListView(ICollection<BasicInfoError> elementIds) {
            ListView basicInfoView = Ribbon.instance.ModelDetectionPanel.BasicInfoListView;
            if (basicInfoView != null) {
                basicInfoView.Items.Clear();
                if (elementIds != null && elementIds.Count != 0) {
                    basicInfoView.ItemsSource = elementIds;
                    SetListViewMsg("总数为：" + elementIds.Count);
                }
            }
        }

        public void SetListViewMsg(string msg) {
            Label listViewMsg = Ribbon.instance.ModelDetectionPanel.BasicListViewMsg;
            if (listViewMsg != null) {
                listViewMsg.Content = msg;
            }
        }

        #region SetParameter
        private void SetParameters(Element ele, bool isPipe = false, bool isEnVersion = false) {
            m_isEnVersion = MtCommon.IsEnglishVersion(m_uiApp);
            SetDistrictParameter(ele, m_district);
            SetBuildingParameter(ele, m_building);
            SetSubRegionParameter(ele);
            SetLevelParameter(ele, isPipe, isEnVersion);
        }
        //院区
        private void SetDistrictParameter(Element ele, string district) {
            if (null == ele || string.IsNullOrEmpty(district))
                return;

            Parameter param = ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.Campus));
            if (null != param) {
                bool successed = param.Set(district);
                if (!successed) {
                    string errorType = MtCommon.GetStringValue(ErrorType.SetParamterFailed) + MtCommon.GetStringValue(MtGlobals.Parameters.Campus);
                    AddListViewErrorData(ele, errorType);
                }
            } else {
                string errorType = MtCommon.GetStringValue(ErrorType.NoParameter) + MtCommon.GetStringValue(MtGlobals.Parameters.Campus);
                AddListViewErrorData(ele, errorType);
            }
        }
        //建筑
        private void SetBuildingParameter(Element ele, string buildingName) {
            if (null == ele || string.IsNullOrEmpty(buildingName))
                return;

            Parameter param = ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.Building));
            if (null != param) {
                bool successed = param.Set(buildingName);
                if (!successed) {
                    string errorType = MtCommon.GetStringValue(ErrorType.SetParamterFailed) + MtCommon.GetStringValue(MtGlobals.Parameters.Building);
                    AddListViewErrorData(ele, errorType);
                }
            } else {
                string errorType = MtCommon.GetStringValue(ErrorType.NoParameter) + MtCommon.GetStringValue(MtGlobals.Parameters.Building);
                AddListViewErrorData(ele, errorType);
            }
        }
        //分区
        private void SetSubRegionParameter(Element ele) {
            if (null == ele)
                return;

            Parameter param = ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.SubDistrict));
            if (null != param) {
                if (string.IsNullOrEmpty(param.AsString())) {
                    bool successed = param.Set("A");
                    if (!successed) {
                        string errorType = MtCommon.GetStringValue(ErrorType.SetParamterFailed) + MtCommon.GetStringValue(MtGlobals.Parameters.SubDistrict);
                        AddListViewErrorData(ele, errorType);
                    }
                }
            } else {
                string errorType = MtCommon.GetStringValue(ErrorType.NoParameter) + MtCommon.GetStringValue(MtGlobals.Parameters.SubDistrict);
                AddListViewErrorData(ele, errorType);
            }
        }

        private void SetLevelParameter(Element ele, bool isPipe = false, bool isEnVersion = false) {
            if (ele == null) return;
            string level = string.Empty;
            if (isEnVersion) {
                if (isPipe) {
                    level = GetPipeLevelParamEN(ele);
                } else {
                    level = GetBuildingLevelParamEN(ele);
                }
            } else {
                if (isPipe) {
                    level = GetPipeLevelParamCN(ele);
                } else {
                    level = GetBuildingLevelParamCN(ele);
                }
            }

            //无法获取Element标高或参照标高参数
            if (string.IsNullOrEmpty(level)) {
                string errorType = MtCommon.GetStringValue(ErrorType.NoParameter) + MtCommon.GetStringValue(MtGlobals.Parameters.MtLevel) + "或"
                    + MtCommon.GetStringValue(MtGlobals.Parameters.ReferenceLevel);
                AddListViewErrorData(ele, errorType);
            }

            Parameter param = ele.LookupParameter(MtCommon.GetStringValue(MtGlobals.Parameters.MtLevel));
            if (null != param) {
                bool success = param.Set(level);
                if (!success) {
                    //楼层参数未设置成功
                    string errorType = MtCommon.GetStringValue(ErrorType.SetParamterFailed) + MtCommon.GetStringValue(MtGlobals.Parameters.MtLevel);
                    AddListViewErrorData(ele, errorType);
                }
            } else {
                //无法获取Element楼层参数
                string errorType = MtCommon.GetStringValue(ErrorType.NoParameter) + MtCommon.GetStringValue(MtGlobals.Parameters.MtLevel);
                AddListViewErrorData(ele, errorType);
            }
        }

        void AddListViewErrorData(Element ele, string errorType) {
            string famliyName = MtCommon.GetElementFamilyName(m_uIDocument.Document, ele);
            string typeName = MtCommon.GetElementType(m_uIDocument.Document, ele);
            string message = errorType;
            BasicInfoError error = CreateBasicInfoError(ele.Id.ToString(), famliyName, typeName, message);

            if (!m_ltBasicInfoErrors.ContainsKey(ele.Id)) {
                m_ltBasicInfoErrors.Add(ele.Id, error);
            }
        }

        public BasicInfoError CreateBasicInfoError(string id, string familyName, string typeName, string errorMsg) {
            BasicInfoError error = new BasicInfoError();
            error.ID = id;
            error.FamilyName = familyName;
            error.TypeName = typeName;
            error.ErrorMsg = errorMsg;
            return error;
        }
        #endregion

        #region GetParameter

        string GetOneParameter(Element ele, string _param) {
            string paramValue = string.Empty;
            Parameter param;
            if (ele != null) {
                param = ele.LookupParameter(_param);
                if (param != null) {
                    paramValue = param.AsString();

                    if (string.IsNullOrEmpty(paramValue))
                        paramValue = param.AsValueString();
                } else {
                    Console.WriteLine("The element has no " + _param + "parameter.");
                }
            } else {
                Console.WriteLine("The element is null!");
            }
            return paramValue;
        }

        string GetPipeLevelParamCN(Element ele) {
            string Level = string.Empty;
            Level = GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Level));
            if (string.IsNullOrEmpty(Level))
                Level = GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.ReferenceLevel));
            return Level;
        }

        string GetBuildingLevelParamCN(Element ele) {
            string Level = string.Empty;
            Level = GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Level));
            if (string.IsNullOrEmpty(Level))
                Level = GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.ReferenceLevel));
            return Level;
        }

        string GetPipeLevelParamEN(Element ele) {
            string Level = string.Empty;
            Level = GetOneParameter(ele, MtGlobals.Parameters.Level.ToString());
            if (string.IsNullOrEmpty(Level))
                Level = GetOneParameter(ele, MtGlobals.Parameters.ReferenceLevel.ToString());
            return Level;
        }

        string GetBuildingLevelParamEN(Element ele) {
            string Level = string.Empty;
            Level = GetOneParameter(ele, MtGlobals.Parameters.Level.ToString());
            if (string.IsNullOrEmpty(Level))
                Level = GetOneParameter(ele, MtGlobals.Parameters.ReferenceLevel.ToString());
            return Level;
        }
        #endregion

        #region FloorTag

        #region SetParameter
        public void SetData(Document doc) {

            ElementCategoryFilter elementCategoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            FilteredElementCollector collectors = new FilteredElementCollector(doc);
            IList<Element> elementLists = collectors.WherePasses(elementCategoryFilter).WhereElementIsNotElementType().ToElements();

            if (!string.IsNullOrEmpty(m_sqliteFilePath))
                ReadDataFromDB(m_sqliteFilePath);
            foreach (var item in elementLists) {
                SetParameters(item);
            }
        }

        public void ReadDataFromDB(string sqliteFilePath) {
            m_floorInfo.Clear();
            if (sqliteFilePath == null)
                TaskDialog.Show("Error", "没有指定DB文件");
            m_sqlite = new MtSQLite(sqliteFilePath);
            JArray jarr = new JArray();
            string quarySql = "SELECT ID,空间,空间编码 FROM " + "'" + m_tableName + "'";
            // string quarySql = "SELECT 序号,科室名称,用途 FROM " + "'" + m_tableName + "'";
            jarr = m_sqlite.ExecuteQuery(quarySql);

            foreach (var item in jarr) {
                //string roomNum = item.Value<string>("序号").Trim();
                string id = item.Value<string>("ID").Trim();
                string space = item.Value<string>("空间").Trim();
                string spaceNum = item.Value<string>("空间编码").Trim();
                //string dep = item.Value<string>("科室名称").Trim();
                //string use = item.Value<string>("用途").Trim();


                //string floorId = level + "-" + roomNum;
                //string floorId = roomNum;
                //string info = dep + "*" + use;

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(space) || string.IsNullOrEmpty(spaceNum))
                    continue;

                string floorID = id;
                string info = space + "*" + spaceNum;
                if (!m_floorInfo.ContainsKey(floorID)) {
                    m_floorInfo.Add(floorID, info);
                }
            }
        }

        public void SetParameters(Element ele) {
            if (ele != null) {
                //Parameter dep = ele.LookupParameter("科室");
                //Parameter usage = ele.LookupParameter("用途");
                // Parameter area = ele.LookupParameter("房间面积");
                Parameter space = ele.LookupParameter("空间");
                Parameter spaceN = ele.LookupParameter("空间编码");
                //string floorId = GetFloorCode(ele);
                string floorId = ele.Id.ToString();

                if (!string.IsNullOrEmpty(floorId) && m_floorInfo.ContainsKey(floorId)) {
                    string[] infos = m_floorInfo[floorId].Split('*');
                    space.Set(infos[0]);
                    spaceN.Set(infos[1]);
                    //dep.Set(infos[0]);
                    // usage.Set(infos[1]);
                    // string _area = (ele.LookupParameter("面积").AsDouble() / 10.7639104f).ToString("F2") + "m²";
                    // area.Set(_area);
                }
            }
        }

        private string GetFloorCode(Element floor) {
            if (floor == null) return string.Empty;

            //string distribute = floor.LookupParameter("院区").AsString();
            //string build = floor.LookupParameter("建筑").AsString();
            //string level = floor.LookupParameter("楼层").AsString();
            //string space = floor.LookupParameter("空间").AsString();

            string markNum = GetOneParameter(floor, "标记");

            return markNum;
        }

        public void MarkFloorInfo(Document doc) {
            Autodesk.Revit.DB.View view = doc.ActiveView;
            Element Level = GetCurrentLevel(doc, view.Name);

            ElementCategoryFilter elementCategoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            ElementLevelFilter elementLevelFilter = new ElementLevelFilter(Level.Id);
            FilteredElementCollector collectors = new FilteredElementCollector(m_uIDocument.Document);
            IList<Element> elementLists = collectors.WherePasses(elementCategoryFilter).WhereElementIsNotElementType().WherePasses(elementLevelFilter).ToElements();

            foreach (var floor in elementLists) {
                string dep = floor.LookupParameter("科室").AsString();
                string usage = floor.LookupParameter("用途").AsString();
                string area = floor.LookupParameter("房间面积").AsString();
                //string number = floor.LookupParameter("空间-新").AsString();
                string labelContent = dep + "\n" + usage + "\n" + area;
                CreateOneFloorIndependentTag(doc, floor as Floor, labelContent);
            }

        }
        #endregion

        #region CreateFloorTags
        public void AddFloorTags(Document doc) {
            m_selection = m_uIDocument.Selection;
            ICollection<ElementId> selectedIds = m_selection.GetElementIds();

            if (selectedIds == null || selectedIds.Count == 0)
                TaskDialog.Show("Error", "必须选择一个楼板实例！");

            foreach (var item in selectedIds) {
                Element ele = doc.GetElement(item);
                Element level = doc.GetElement(ele.LevelId); //获得选择楼板的LEVEL;
                int tagIndex = GetCurrentLevelMaxFloorTags(level) + 1;
                string Label = RenameLabel(ele, tagIndex);
                CreateOneFloorIndependentTag(doc, ele as Floor, Label);
            }
        }

        private Element GetCurrentLevel(Document doc, string viewName) {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> collections = collector.OfClass(typeof(Level)).ToElements();

            foreach (var level in collections) {
                if (level.Name == viewName)
                    return level;
            }
            return null;
        }

        public void CreateLevelFloorTags(Document doc) {
            Autodesk.Revit.DB.View view = doc.ActiveView;
            Element Level = GetCurrentLevel(doc, view.Name);

            ElementCategoryFilter elementCategoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            ElementLevelFilter elementLevelFilter = new ElementLevelFilter(Level.Id);
            FilteredElementCollector collectors = new FilteredElementCollector(m_uIDocument.Document);
            IList<Element> elementLists = collectors.WherePasses(elementCategoryFilter).WhereElementIsNotElementType().WherePasses(elementLevelFilter).ToElements();

            int index = 1;
            foreach (var floor in elementLists) {
                string labelContent = RenameLabel(floor, index);
                CreateOneFloorIndependentTag(doc, floor as Floor, labelContent);
                index++;
            }

            //ICollection<ElementId> elementids = m_selection.GetElementIds();

            //int index = 1;
            //foreach (var floorId in elementids)
            //{
            //    Element floor = doc.GetElement(floorId);
            //    string labelContent = RenameLabel(floor, index);
            //    CreateOneFloorIndependentTag(doc, floor as Floor, labelContent);
            //    index++;
            //}
        }

        //参考 https://stackoverflow.com/questions/25457886/c-sharp-revit-api-createindependenttag-method-failing-to-add-tag-to-ceilings-e
        public IndependentTag CreateOneFloorIndependentTag(Document document, Floor floor, string labelName) {
            Autodesk.Revit.DB.View view = document.ActiveView;

            TagMode tagMode = TagMode.TM_ADDBY_CATEGORY;
            TagOrientation tagOri = TagOrientation.Horizontal;

            //Revit elements can be located by a point(most family instance),a line(walls, line based components)
            //or sketch(celling, floors etc);
            //Simply answer is to find the boundling of box of the floor and calculate the center of the 
            //if the floor is a large L shape or something the boundling box center may not be over the floor at all
            //need some other algorithm to find the center point;

            //calculate the center of mark
            XYZ centerPoint = CalculateCenterOfMark(floor, view);

            IndependentTag newTag = document.Create.NewTag(view, floor, false, tagMode, tagOri, centerPoint);

            if (null == newTag) {
                throw new Exception("Create IndependentTag Failed!");
            }
            //NewTag.tagText is read-only, so use the othter parameters to set the tag text;
            SetTagText(floor, labelName);
            return newTag;
        }

        private XYZ CalculateCenterOfMark(Floor floor, Autodesk.Revit.DB.View view) {
            BoundingBoxXYZ boundingBoxXYZ = floor.get_BoundingBox(view);
            XYZ min = boundingBoxXYZ.Min;
            XYZ max = boundingBoxXYZ.Max;

            XYZ centerPoint = min + (max - min) / 2;

            Options opt = m_uiApp.Application.Create.NewGeometryOptions();
            List<XYZ> points = GetFloorBoundaryPolygons(floor, opt);
            if (!IsInPolygon(centerPoint, points)) {
                //centerPoint = ResetTagCenterPoint(points);
                centerPoint = TestInteralPoint(points, centerPoint);
            }
            return centerPoint;
        }

        private void SetTagText(Floor floor, string labelName) {
            Parameter foundParam = floor.LookupParameter("标记");
            bool result = foundParam.Set(labelName);
        }

        private string RenameLabel(Element floor, int index) {
            if (floor == null) return string.Empty;

            string level = floor.LookupParameter("楼层").AsString();
            string zone = floor.LookupParameter("分区").AsString();
            return level + zone + "-" + AddZeroToFloorIndex(index);
        }

        private string AddZeroToFloorIndex(int index) {
            string floorNum = string.Empty;
            switch (index.ToString().Length) {
                case 1:
                    floorNum = "00" + index.ToString();
                    break;
                case 2:
                    floorNum = "0" + index.ToString();
                    break;
                case 3:
                    floorNum = index.ToString();
                    break;
            }
            return floorNum;
        }

        private int GetCurrentLevelMaxFloorTags(Element level) {
            ElementCategoryFilter elementCategoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            ElementLevelFilter elementLevelFilter = new ElementLevelFilter(level.Id);
            FilteredElementCollector collectors = new FilteredElementCollector(m_uIDocument.Document);
            IList<Element> elementLists = collectors.WherePasses(elementCategoryFilter).WhereElementIsNotElementType().WherePasses(elementLevelFilter).ToElements();

            List<int> tagLabels = new List<int>();

            int max = 0;

            foreach (var floor in elementLists) {
                string tagName = floor.LookupParameter("标记").AsString();
                if (!string.IsNullOrEmpty(tagName)) {
                    int current = int.Parse(tagName.Split('-')[1]);
                    if (current > max)
                        max = current;
                }
            }

            TaskDialog.Show("Message", "该层最大标记值为： " + max.ToString());
            return max;
        }

        #endregion

        #region GetFloorBoundary
        //参考 判断点是否在多边形内部 ： https://blog.csdn.net/u011722133/article/details/52813374
        bool IsInPolygon(XYZ centerPoint, List<XYZ> points) {
            //比较xy,判断centerpoint的xy是否在floor的边界内
            int crossing = 0;

            for (int i = 0; i < points.Count - 1; i++) {
                double slope = (points[i + 1].Y - points[i].Y) / (points[i + 1].X - points[i].X);
                bool cond1 = (points[i].X <= centerPoint.X) && (centerPoint.X < points[i + 1].X);
                bool cond2 = (points[i + 1].X <= centerPoint.X) && (centerPoint.X < points[i].X);
                bool above = (centerPoint.Y < slope * (centerPoint.X - points[i].X) + points[i].Y);
                if ((cond1 || cond2) && above)
                    crossing++;
            }
            return (crossing % 2 != 0);
        }

        List<XYZ> GetFloorBoundaryPolygons(Floor floor, Options opt) {
            List<XYZ> polygons = new List<XYZ>();
            GeometryElement geometryElement = floor.get_Geometry(opt);
            foreach (GeometryObject obj in geometryElement) {
                Solid solid = obj as Solid;
                if (null != solid) {
                    GetBoundary(polygons, solid);
                }
            }
            return polygons;
        }

        bool GetBoundary(List<XYZ> ploygons, Solid solid) {
            PlanarFace heightest = null;
            FaceArray faceArrays = solid.Faces;
            heightest = faceArrays.get_Item(0) as PlanarFace;
            foreach (Face face in faceArrays) {
                //比较表面原点的Z轴确定最高点
                PlanarFace pf = face as PlanarFace;
                if (null != pf && IsHorizontal(pf)) {
                    if (null == heightest && pf.Origin.Z > heightest.Origin.Z) {
                        heightest = pf;
                    }
                }
            }

            if (null != heightest) {
                EdgeArrayArray loops = heightest.EdgeLoops;
                foreach (EdgeArray loop in loops) {
                    foreach (Edge edge in loop) {
                        IList<XYZ> points = edge.Tessellate();
                        foreach (var point in points) {
                            bool isEqual = false;
                            foreach (var item in ploygons) //去除相同的顶点
                            {
                                isEqual = IsEqualXYZ(item, point);
                            }
                            if (!isEqual)
                                ploygons.Add(ResetPoint(point));
                        }
                    }
                }
            }
            return null != heightest;
        }

        bool IsHorizontal(PlanarFace pf) {
            XYZ up = new XYZ(0, 1, 0);
            if (pf.FaceNormal.DotProduct(up) == 0)
                return false;
            else
                return true;
        }

        XYZ ResetPoint(XYZ point) {
            double x = double.Parse(point.X.ToString("F2"));
            double y = double.Parse(point.Y.ToString("F2"));
            double z = double.Parse(point.Z.ToString("F2"));
            return new XYZ(x, y, z);
        }

        bool IsEqualXYZ(XYZ point1, XYZ point2) {
            if ((int)point1.X == (int)point2.X && (int)point1.Y == (int)point2.Y && (int)point1.Z == (int)point1.Z)
                return true;
            else
                return false;
        }

        #region 内心算法
        //简单的内心算法，从中心点出发绘制两条垂直的线，垂直线与两条边相交，记录相交点

        XYZ TestInteralPoint(List<XYZ> polygons, XYZ centerPoint) {
            //过centerPoint做垂直水平方向上的直线，计算两条直线与其他相邻顶点的相交点；
            //并分别计算同一侧相邻点的距离，距离最大的，取其中心点作为CenterPoint
            XYZ horizontal = new XYZ(0, 1, -centerPoint.Y);
            XYZ vertical = new XYZ(1, 0, -centerPoint.X);

            //horizontal
            List<XYZ> hori_interset_points = intersectPoints(polygons, horizontal);
            XYZ hor_point1 = new XYZ();
            XYZ hor_point2 = new XYZ();
            GetMaxLengthPoints(hori_interset_points, centerPoint, out hor_point1, out hor_point2);


            //vertical 
            List<XYZ> vertical_interset_points = intersectPoints(polygons, vertical);
            XYZ ver_point1 = new XYZ();
            XYZ ver_point2 = new XYZ();
            GetMaxLengthPoints(vertical_interset_points, centerPoint, out ver_point1, out ver_point2);

            if (GetLength(hor_point1, hor_point2) > GetLength(ver_point1, ver_point2)) {
                return (hor_point1 + hor_point2) / 2;
            } else {
                return (ver_point1 + ver_point2) / 2;
            }
        }

        List<XYZ> intersectPoints(List<XYZ> polygons, XYZ lineParas) {
            if (polygons == null && polygons.Count == 0) return null;
            XYZ axis = new XYZ(lineParas.Y, lineParas.X, 0);
            List<XYZ> intersectpoints = new List<XYZ>();
            for (int i = 0; i < polygons.Count - 1; i++) {
                //计算交点时，只计算与水平方向和垂直方向接近垂直的两个点组成的直线；即两个向量的夹角大于60小于120
                XYZ vector1 = (polygons[i] - polygons[i + 1]).Normalize();
                double cosValue = vector1.DotProduct(axis);
                if (cosValue > -0.5f && cosValue < 0.5f) {
                    XYZ temppoint = Intersect(polygons[i], polygons[i + 1], lineParas);
                    if (temppoint != null)
                        intersectpoints.Add(temppoint);
                }
            }
            return intersectpoints;
        }

        XYZ Intersect(XYZ point1, XYZ point2, XYZ lineParas) {
            double A1 = point2.Y - point1.Y;
            double B1 = point1.X - point2.X;
            double C1 = point2.X * point1.Y - point1.X * point2.Y;

            double A2 = lineParas.X;
            double B2 = lineParas.Y;
            double C2 = lineParas.Z;

            double x = 0, y = 0;
            double temp = A1 * B2 - A2 * B1;
            if (temp != 0) {
                x = (B1 * C2 - B2 * C1) / temp;
                y = (A1 * C2 - A2 * C1) / -temp;
            }

            XYZ intersectPoint = new XYZ(x, y, point1.Z);

            if (IsOnSegment(point1, point2, intersectPoint)) //是否在线段上
                return intersectPoint;
            else
                return null;
        }

        bool IsOnSegment(XYZ p1, XYZ p2, XYZ p) {
            int maxX = p1.X >= p2.X ? (int)p1.X : (int)p2.X;
            int minX = p1.X <= p2.X ? (int)p1.X : (int)p2.X;
            int maxY = p1.Y >= p2.Y ? (int)p1.Y : (int)p2.Y;
            int minY = p1.Y <= p2.Y ? (int)p1.Y : (int)p2.Y;
            if ((int)p.X >= minX && (int)p.X <= maxX &&
                (int)p.Y >= minY && (int)p.Y <= maxY)
                return true;
            else
                return false;
        }

        void GetMaxLengthPoints(List<XYZ> intersectPoints, XYZ lineParas, out XYZ point1, out XYZ point2) {
            double maxLength = 0;
            XYZ temp1 = new XYZ(0, 0, 0);
            XYZ temp2 = new XYZ(0, 0, 0);
            for (int i = 0; i < intersectPoints.Count - 1; i++) {
                if (IsInSameSide(intersectPoints[i], intersectPoints[i + 1], lineParas)) {
                    double length = GetLength(intersectPoints[i], intersectPoints[i + 1]);
                    if (maxLength > length)
                        continue;
                    else {
                        maxLength = length;
                        temp1 = intersectPoints[i];
                        temp2 = intersectPoints[i + 1];
                    }
                }
            }
            point1 = temp1;
            point2 = temp2;
        }

        double GetLength(XYZ point1, XYZ point2) {
            return Math.Sqrt((point1.X - point2.X) * (point1.X - point2.X) + (point1.Y - point2.Y) * (point1.Y - point2.Y));
        }

        bool IsInSameSide(XYZ point1, XYZ point2, XYZ lineparas) {
            if (point1.Y == point2.Y && (point1.X <= lineparas.X && point2.X <= lineparas.X) ||
                point1.Y == point2.Y && (point1.X >= lineparas.X && point2.X >= lineparas.X) ||
                point1.X == point2.X && (point1.Y <= lineparas.Y && point2.Y <= lineparas.Y) ||
                point1.X == point2.X && (point1.Y >= lineparas.Y && point2.Y >= lineparas.Y)) {
                return true;
            } else
                return false;
        }

        #endregion

        #region Obsolete
        //参考 求任意多边形内点的算法 https://blog.csdn.net/yujinqiong/article/details/4465910
        //定理1：每个多边形至少有一个凸顶点
        //定理2：顶点数>=4的简单多边形至少有一条对角线
        //结论： x坐标最大，最小的点肯定是凸顶点
        //y坐标最大，最小的点肯定是凸顶点

        XYZ ResetTagCenterPoint(List<XYZ> points) {
            XYZ lowestPoint, leftPoint, rightPoint;  //三个点组成多边形
            int pointsCount = points.Count;
            lowestPoint = points[0];
            int index = 0;
            for (int i = 1; i < pointsCount - 1; i++) {
                if (points[i].Y < lowestPoint.Y)  //最低点一定是个凸点
                {
                    lowestPoint = points[i];
                    index++;
                }
            }
            leftPoint = points[(index - 1 + pointsCount) % pointsCount];
            rightPoint = points[(index + 1) % pointsCount];


            XYZ[] tri = new XYZ[3];
            XYZ q = new XYZ();
            tri[0] = lowestPoint; tri[1] = leftPoint; tri[2] = rightPoint;
            double md = 1000000f;
            int in1 = index;
            bool bin = false;

            for (int i = 0; i < pointsCount; i++)                                 //寻找在三角形avb内且离顶点v最近的顶点q   
            {
                if (i == index) continue;
                if (i == (index - 1 + pointsCount) % pointsCount) continue;
                if (i == (index + 1) % pointsCount) continue;
                if (!InsideConvexPolygon(3, tri, points[i])) continue;
                bin = true;
                if ((lowestPoint - points[i]).GetLength() < md) {
                    q = points[i];
                    md = (lowestPoint - q).GetLength();
                }
            }
            if (!bin)                                                         //没有顶点在三角形avb内，返回线段ab中点   
            {
                double x1 = (leftPoint.X + rightPoint.X) / 2;
                double y1 = (leftPoint.Y + rightPoint.Y) / 2;
                return new XYZ(x1, y1, lowestPoint.Z);
            }

            double x = (lowestPoint.X + q.X) / 2;
            double y = (lowestPoint.Y + q.Y) / 2;
            return new XYZ(x, y, lowestPoint.Z);
        }

        bool InsideConvexPolygon(int vcount, XYZ[] polygon, XYZ q) //   可用于三角形！   
        {
            double x = 0, y = 0, z = 0;
            XYZ m, n;
            int i;
            for (i = 0; i < vcount; i++)         //   寻找一个肯定在多边形polygon内的点p：多边形顶点平均值   
            {
                x += polygon[i].X;
                y += polygon[i].Y;
                z = polygon[i].Z;
            }
            x /= vcount;
            y /= vcount;
            XYZ p = new XYZ(x, y, z);

            for (i = 0; i < vcount; i++) {
                m = polygon[i]; n = polygon[(i + 1) % vcount];
                if (multiply(p, m, n) * multiply(q, m, n) < 0)     /*   点p和点q在边l的两侧，说明点q肯定在多边形外         */
                    break;
            }
            return (i == vcount);
        }

        //参考 https://www.cnblogs.com/onegarden/p/5622166.html
        double multiply(XYZ pt1, XYZ pt2, XYZ p0) {
            double mult = (pt1.X - p0.X) * (pt2.Y - p0.Y) - (pt2.X - p0.X) * (pt1.Y - p0.Y);
            return mult;

        }
        #endregion

        #endregion

        #region ChageFloorColor

        public void SetFloorColor(Document doc) {
            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            //获取实体填充  
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            ElementCategoryFilter elementCategoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            FilteredElementCollector collectors = new FilteredElementCollector(m_uIDocument.Document);
            IList<Element> elementLists = collectors.WherePasses(elementCategoryFilter).WhereElementIsNotElementType().ToElements();

            foreach (var item in elementLists) {
                if (m_isClassifyColorByDep) {
                    string name = item.LookupParameter("科室").AsString();

                    if (name != null && m_ColorMappingDic.ContainsKey(name))
                        SetOneFloorColor(doc, fillPatternElement, item.Id, m_ColorMappingDic[name]);
                } else {
                    string name = item.LookupParameter("用途").AsString();
                    if (name != null && (name.Contains("走廊") || name.Equals("走廊") || name.Contains("通道"))) {
                        SetOneFloorColor(doc, fillPatternElement, item.Id, m_corridorColor);
                    } else if (!string.IsNullOrEmpty(name)) {
                        SetOneFloorColor(doc, fillPatternElement, item.Id, m_floorColor);
                    }
                }
            }
        }

        void SetOneFloorColor(Document doc, FillPatternElement fillPatternElement, ElementId floorId, Autodesk.Revit.DB.Color color) {
            OverrideGraphicSettings OverrideGraphicSettings = new OverrideGraphicSettings();
            OverrideGraphicSettings = doc.ActiveView.GetElementOverrides(floorId);
            OverrideGraphicSettings.SetProjectionFillPatternId(fillPatternElement.Id);

            OverrideGraphicSettings.SetProjectionFillColor(color);
            doc.ActiveView.SetElementOverrides(floorId, OverrideGraphicSettings);
        }

        private Dictionary<string, Autodesk.Revit.DB.Color> m_ColorMappingDic = new Dictionary<string, Autodesk.Revit.DB.Color>()
        {
            {"内科",new Color(135,206,250)},
            {"外科",new Color(138,138,186)},
            {"妇产科",new Color(133,193,167)},
            {"儿科",new Color(176,224,230)},
            {"小儿外科",new Color(176,224,230)},
            {"发育儿科",new Color(176,224,230)},
            {"神经内科",new Color(125,170,89)},
            {"感染科",new Color(238,225,202)},
            {"中医科",new Color(178,163,127)},
            {"老年病科",new Color(244,178,152)},
            {"眼科",new Color(242,240,156)},
            {"耳鼻咽喉科",new Color(175,221,175)},
            {"口腔科",new Color(109,160,143)},
            {"肿瘤科",new Color(142,199,237)},
            {"疼痛病房",new Color(115,115,155)},
            {"介入病房",new Color(176,196,222)},
            {"康复中心",new Color(98,173,196)},
            {"内镜诊治中心",new Color(163,209,206)},
            {"宁养病房",new Color(152,165,111)},
            {"睡眠医学中心",new Color(234,220,214)},
            {"其他科室",new Color(232,206,158)},
            {"计算机中心",new Color(239,173,163)},
            {"其他部门",new Color(193,150,120)},
            {"未知",new Color(247,165,129)},
            {"公共区域",new Color(193,193,193)},
            {"总服务台",new Color(114,183,179)},
            {"机电-空调",new Color(130,158,216)},
            {"明喆物业",new Color(88,178,220)},
            {"中国电信",new Color(186,206,175)},
            {"消防监控中心",new Color(141,182,219)},
            {"健身中心",new Color(101,170,101)},
            {"财务处",new Color(234,195,155)},
        };

        #endregion

        #endregion
    }
}
