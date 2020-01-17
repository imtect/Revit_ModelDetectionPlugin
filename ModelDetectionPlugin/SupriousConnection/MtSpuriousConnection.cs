 using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ModelDetectionPlugin {

    public class MtSpuriousConnection : IExternalEventHandler {

        UIApplication m_uiApp;
        UIDocument m_uIDocument;
        //IList<SpuriousConnectionError> m_ErrorListView;
        Dictionary<string, SpuriousConnectionError> m_dicErrorList;

        MtGlobals.SpuriousConnectionMethods m_selMethod;
        public MtGlobals.SpuriousConnectionMethods SelMethod {
            set { m_selMethod = value; }
        }

        public MtSpuriousConnection() {
            //m_ErrorListView = new List<SpuriousConnectionError>();
            m_dicErrorList = new Dictionary<string, SpuriousConnectionError>();
        }

        bool m_isRemoveFan;
        public bool IsRemoveFan {
            get { return m_isRemoveFan; }
            set { m_isRemoveFan = value; }
        }

        bool m_isRemoveCondensorPipe;
        public bool IsRemoveCondensorPipe {
            get { return m_isRemoveCondensorPipe; }
            set { m_isRemoveCondensorPipe = value; }
        }
        bool m_isRemoveAirDuct;
        public bool IsRemoveAirDust {
            get { return m_isRemoveAirDuct; }
            set { m_isRemoveAirDuct = value; }
        }

        public void Execute(UIApplication uiapp) {
            m_uiApp = uiapp;
            m_uIDocument = m_uiApp.ActiveUIDocument;

            Transaction trans = new Transaction(m_uIDocument.Document, "SpuriousConnection");
            trans.Start();

            switch (m_selMethod) {
                case MtGlobals.SpuriousConnectionMethods.None:

                    break;
                case MtGlobals.SpuriousConnectionMethods.TestSpuriousConnection:
                    TravelAllPipe();
                    break;
                default:
                    break;
            }
            trans.Commit();
        }

        public string GetName() {
            return "SpuriousConnection";
        }


        #region SpuriousConnection
        public void TestOneElement() {
            //ICollection<ElementId> elementids = m_selection.GetElementIds();
            //IList<ElementId> notCompleteConnected = null;
            //foreach (var eleId in elementids) {
            //    Element ele = m_uidoc.Document.GetElement(eleId);
            //    bool isCompleteConnected = EleConnectorIsComplete(ele);
            //    if (isCompleteConnected)
            //        notCompleteConnected.Add(eleId);
            //}
            //IsolateElement(notCompleteConnected);
        }

        public void TravelAllPipe() {
            ClearDic();

            //设置全部管道的基本参数
            ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter hostFilter = new ElementClassFilter(typeof(HostObject));
            LogicalOrFilter andFilter = new LogicalOrFilter(instanceFilter, hostFilter);

            FilteredElementCollector collector = new FilteredElementCollector(m_uIDocument.Document);
            collector.WherePasses(andFilter);

            IList<ElementId> notCompleteConnected = new List<ElementId>();

            //设置剔除条件

            foreach (var item in collector) {
                //按条件剔除Element
                string familyName = MtCommon.GetElementFamilyName(m_uIDocument.Document, item);
                string typeName = MtCommon.GetElementType(m_uIDocument.Document, item);
                Parameter param = item.LookupParameter("系统名称");
                string systemName = string.Empty;
                if (param != null) {
                    systemName = param.AsString();
                }

                if (IsRemoveFan && ((!string.IsNullOrEmpty(systemName) && systemName.Contains("风盘")) 
                    || familyName.Contains("风机盘管") || familyName.Contains("风盘")
                    || typeName.Contains("风机盘管") || typeName.Contains("风盘")))
                    continue;

                if (IsRemoveCondensorPipe && (IsRejectFromCondition(item, "冷凝水") || MtCommon.GetElementFamilyName(m_uIDocument.Document, item).Contains("冷凝水")))
                    continue;

                if (IsRemoveAirDust && (item.Category.Name.Contains(MtGlobals.DustCategory)
                    || item.Category.Name.Contains("风管附件")
                    || item.Category.Name.Contains("风管管件")
                    || item.Category.Name.Contains("风道末端")))
                    continue;

                bool isCompleteConnected = EleConnectorIsComplete(item);
                if (!isCompleteConnected) {
                    notCompleteConnected.Add(item.Id);
                    AddListViewErrorData(item);
                }
            }

            IList<SpuriousConnectionError> m_ErrorListView = m_dicErrorList.Select(v => v.Value).ToList();
            m_ErrorListView = m_ErrorListView.OrderBy(v => (v.FamilyName + v.TypeName)).ToList();
            SetErrorListView(m_ErrorListView);
        }

        public void SetErrorListView(ICollection<SpuriousConnectionError> elementIds) {
            if (elementIds != null && elementIds.Count != 0) {
                ListView spuriousConnectListView = Ribbon.instance.ModelDetectionPanel.SpuriousConnectionListView;
                if (spuriousConnectListView != null) {
                    spuriousConnectListView.ItemsSource = elementIds;
                    SetListViewMsg("总数为：" + elementIds.Count);
                }
            }
        }

        public void SetListViewMsg(string msg) {
            Label listViewMsg = Ribbon.instance.ModelDetectionPanel.SpuriousListViewMsg;
            if (listViewMsg != null) {
                listViewMsg.Content = msg;
            }
        }

        void AddListViewErrorData(Element ele, string errorType = null) {
            string famliyName = MtCommon.GetElementFamilyName(m_uIDocument.Document, ele);
            string typeName = MtCommon.GetElementType(m_uIDocument.Document, ele);
            string message = MtCommon.GetStringValue(ErrorType.NoEndPipe) + errorType;
            SpuriousConnectionError error = CreateBasicInfoError(ele.Id.ToString(), famliyName, typeName, message);

            if (!m_dicErrorList.ContainsKey(ele.Id.ToString())) {
                m_dicErrorList.Add(ele.Id.ToString(), error);
            }
        }

        public SpuriousConnectionError CreateBasicInfoError(string id, string familyName, string typeName, string errorMsg) {
            SpuriousConnectionError error = new SpuriousConnectionError();
            error.ID = id;
            error.FamilyName = familyName;
            error.TypeName = typeName;
            error.ErrorMsg = errorMsg;
            return error;
        }

        void ClearDic() {
            m_dicErrorList.Clear();
        }


        bool IsRejectFromCondition(Element ele, string condition) {

            string FilterCondition = "系统名称";

            Parameter param = ele.LookupParameter(FilterCondition);
            string paramStr = string.Empty;
            if (param != null) {
                paramStr = param.AsString();
            }

            if ((!string.IsNullOrEmpty(paramStr) && paramStr.Contains(condition)) ||
                MtCommon.GetElementFamilyName(m_uIDocument.Document, ele).Contains(condition))
                return true;
            else
                return false;
        }

        bool EleConnectorIsComplete(Element ele) {
            ConnectorSet connectors = MtCommon.GetAllConnectors(ele);

            if (connectors != null) {
                foreach (Connector item in connectors) {
                    if (item.Domain == Domain.DomainElectrical) //去除ElectricalConnector
                        continue;

                    if (!item.IsConnected)
                        return false;
                }
            }
            return true;
        }


        #endregion
    }
}
