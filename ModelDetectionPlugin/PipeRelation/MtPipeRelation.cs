using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace ModelDetectionPlugin {
    public class MtPipeRelation : IExternalEventHandler {

        UIApplication m_uiApp;
        UIDocument m_uIDocument;
        //MEPSystem m_system;
        //Element m_selectedElement;
        MtGlobals.PipeRelationMethods m_selMethod;
        public MtGlobals.PipeRelationMethods SelMethod {
            set { m_selMethod = value; }
        }

        #region Varables
        private string m_dbfilepath;
        private string m_systemName;
        private string m_subsystemName;
        private string m_tunnelName;
        private string m_tableName;
        private string m_columnName;

        private string m_multiSystem;

        private bool m_isPositiveDir;
        private bool m_isWaterReturnPipe;
        private bool m_isSameSystem;
        private bool m_isIsolatedElments;

        public string DBFilePath {
            get {
                return m_dbfilepath;
            }
            set {
                m_dbfilepath = value;
            }
        }

        public string SystemName {
            get {
                return m_systemName;
            }
            set {
                m_systemName = value;
            }
        }

        public string SubSystemName {
            get {
                return m_subsystemName;
            }
            set {
                m_subsystemName = value;
            }
        }

        public string TableName {
            get {
                return m_tableName;
            }
            set {
                m_tableName = value;
            }
        }

        public string TunnelName {
            get {
                return m_tunnelName;
            }
            set {
                m_tunnelName = value;
            }
        }

        public string ColumnName {
            get {
                return m_columnName;
            }
            set {
                m_columnName = value;
            }
        }

        public string MultiSystem {
            get {
                return m_multiSystem;
            }
            set {
                m_multiSystem = value;
            }
        }

        public bool IsPositiveDir {
            get {
                return m_isPositiveDir;
            }
            set {
                m_isPositiveDir = value;
            }
        }

        public bool IsWaterReturnPipe {
            get {
                return m_isWaterReturnPipe;
            }
            set {
                m_isWaterReturnPipe = value;
            }
        }

        public bool IsSameSystem {
            get { return m_isSameSystem; }
            set { m_isSameSystem = value; }
        }

        public bool IsIsolatedElements {
            get { return m_isIsolatedElments; }
            set { m_isIsolatedElments = value; }
        }

        #endregion

        Dictionary<string, PipeRelationError> m_dicPipeRelationError;

        List<StartElementData> m_startElements;

        public MtPipeRelation() {
            m_dicPipeRelationError = new Dictionary<string, PipeRelationError>();
            m_startElements = new List<StartElementData>();
        }

        public void Execute(UIApplication uiapp) {
            m_uiApp = uiapp;
            m_uIDocument = m_uiApp.ActiveUIDocument;

            Transaction trans = new Transaction(m_uIDocument.Document, "Level");
            trans.Start();

            switch (m_selMethod) {
                case MtGlobals.PipeRelationMethods.None:
                    break;
                case MtGlobals.PipeRelationMethods.CheckPipeRelation:
                    TestMultiStartPipe();
                    break;
                case MtGlobals.PipeRelationMethods.GetPipeRelation:
                    GetPipeRelationShip();
                    break;
                default:
                    break;
            }
            trans.Commit();
        }

        public string GetName() {
            return "PipeRelation";
        }

        public void GetPipeRelationShip() {
            GetElementSystem();
            foreach (var item in m_startElements) {
                OnTraversalTree(item);
            }
        }

        public void OnTraversalTree(StartElementData data, bool isSaveIntoDB = true) {
            try {
                MtTravelsalTree tree = new MtTravelsalTree(m_uIDocument.Document, data.System);
                tree.TraversePipe(data.StartElement, m_isSameSystem, m_multiSystem);
                if (isSaveIntoDB) {
                    string colName = string.Join(",", new string[] { m_systemName, m_subsystemName, m_tunnelName, m_tableName, m_columnName });
                    tree.SaveIntoDB(m_dbfilepath, colName, m_isPositiveDir, m_isWaterReturnPipe);
                    if (tree.IsHide) {
                        Document doc = m_uIDocument.Document;
                        tree.HideTraverseElement(doc);
                    }
                } else {
                    Document doc = m_uIDocument.Document;
                    tree.HideTraverseElement(doc);
                }
            } catch (Exception) {
                TaskDialog.Show("Error", "不明所以！");
            }
        }

        public void TestMultiStartPipe() {
            GetElementSystem();
            if (m_startElements.Count == 0) return;
            List<Element> totalSelectElements = new List<Element>();
            var index = 0;
            foreach (var item in m_startElements) {
                TestLoopCircuit(item, (eles) => {
                    totalSelectElements = totalSelectElements.Union(eles).ToList();
                    ++index;
                    if (index == m_startElements.Count) {
                        if (m_isIsolatedElments) MtCommon.IsolateElements(m_uIDocument.Document, totalSelectElements);
                        else MtCommon.HideElements(m_uIDocument.Document, totalSelectElements);
                    }
                });
            }
        }

        void TestLoopCircuit(StartElementData data, Action<List<Element>> totalElments = null) {
            ClearErrorListView();

            MtTravelsalTree tree = new MtTravelsalTree(m_uIDocument.Document, data.System);
            List<Element> eles = tree.TestCircuit(data.StartElement, m_isSameSystem, m_multiSystem, IsIsolatedElements, totalElments);

            if (eles != null && eles.Count != 0) {
                foreach (var ele in eles) {
                    AddListViewErrorData(ele, MtCommon.GetStringValue(ErrorType.Circuit));
                }
            }
            IList<PipeRelationError> pipeRelationErrors = m_dicPipeRelationError.Select(v => v.Value).ToList();
            pipeRelationErrors = pipeRelationErrors.OrderBy(v => (v.FamilyName + v.TypeName)).ToList();
            SetErrorListView(pipeRelationErrors);
        }

        public void GetElementSystem() {
            m_startElements.Clear();
            if (m_uIDocument != null) {
                Selection selection = m_uIDocument.Selection;
                ICollection<ElementId> selectionIds = selection.GetElementIds();
                foreach (var eleId in selectionIds) {
                    var startEle = m_uIDocument.Document.GetElement(eleId);
                    StartElementData data = new StartElementData() {
                        StartElement = startEle,
                        System = ExtractMechanicalOrPipingSystem(startEle)
                    };
                    m_startElements.Add(data);
                }
            }
        }

        void ClearErrorListView() {
            m_dicPipeRelationError.Clear();
            IList<PipeRelationError> pipeRelationErrors = m_dicPipeRelationError.Select(v => v.Value).ToList();
            SetErrorListView(pipeRelationErrors);
        }

        public void SetErrorListView(ICollection<PipeRelationError> elementIds) {
            if (elementIds != null && elementIds.Count != 0) {
                ListView pipeRelationListView = Ribbon.instance.ModelDetectionPanel.PipeRelationView;
                if (pipeRelationListView != null) {
                    pipeRelationListView.ItemsSource = elementIds;
                    SetListViewMsg("总数为：" + elementIds.Count);
                }
            }
        }

        public void SetListViewMsg(string msg) {
            Label listViewMsg = Ribbon.instance.ModelDetectionPanel.PipeRelationListViewMsg;
            if (listViewMsg != null) {
                listViewMsg.Content = msg;
            }
        }

        void AddListViewErrorData(Element ele, string errorType = null) {
            string eleId = ele.Id.ToString();
            string famliyName = MtCommon.GetElementFamilyName(m_uIDocument.Document, ele);
            string typeName = MtCommon.GetElementType(m_uIDocument.Document, ele);
            string message = errorType;
            PipeRelationError error = CreatePipeRelationError(ele.Id.ToString(), famliyName, typeName, message);

            if (!m_dicPipeRelationError.ContainsKey(ele.Id.ToString())) {
                m_dicPipeRelationError.Add(eleId, error);
            }
        }

        public PipeRelationError CreatePipeRelationError(string id, string familyName, string typeName, string errorMsg) {
            PipeRelationError error = new PipeRelationError();
            error.ID = id;
            error.FamilyName = familyName;
            error.TypeName = typeName;
            error.ErrorMsg = errorMsg;
            return error;
        }

        private MEPSystem ExtractMechanicalOrPipingSystem(Element selectedElement) {
            MEPSystem system = null;

            if (selectedElement is MEPSystem) {
                if (selectedElement is MechanicalSystem || selectedElement is PipingSystem) {
                    system = selectedElement as MEPSystem;
                    return system;
                }
            } else {
                FamilyInstance fi = selectedElement as FamilyInstance;

                if (fi != null) {
                    MEPModel mepModel = fi.MEPModel;
                    ConnectorSet connectors = null;
                    try {
                        connectors = mepModel.ConnectorManager.Connectors;
                    } catch (System.Exception) {
                        system = null;
                    }
                    system = ExtractSystemFromConnectors(connectors, selectedElement);
                } else {
                    MEPCurve mepCurve = selectedElement as MEPCurve;
                    if (mepCurve != null) {
                        ConnectorSet connectors = null;
                        connectors = mepCurve.ConnectorManager.Connectors;
                        system = ExtractSystemFromConnectors(connectors, selectedElement);
                    }
                }
            }

            return system;
        }

        private MEPSystem ExtractSystemFromConnectors(ConnectorSet connectors, Element selectEle) {
            MEPSystem system = null;

            if (connectors == null || connectors.Size == 0) {
                return null;
            }

            List<MEPSystem> systems = new List<MEPSystem>();
            foreach (Connector connector in connectors) {
                MEPSystem tmpSystem = connector.MEPSystem;
                if (tmpSystem == null) {
                    continue;
                }

                MechanicalSystem ms = tmpSystem as MechanicalSystem;
                if (ms != null) {
                    systems.Add(tmpSystem);
                } else {
                    PipingSystem ps = tmpSystem as PipingSystem;
                    systems.Add(tmpSystem);
                }
            }
            //if there is more than one system is found, get system contain the selected element;
            foreach (MEPSystem sys in systems) {
                if (sys.GetParameters(selectEle.Name) != null) {
                    system = sys;
                }
            }
            return system;
        }
    }

    public struct StartElementData {
        public Element StartElement;
        public MEPSystem System;
    }
}
