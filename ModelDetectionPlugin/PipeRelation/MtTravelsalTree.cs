using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;

namespace ModelDetectionPlugin {

    public class TreeNode {

        #region Properties
        private ElementId m_Id;

        private string m_familyName;

        private string m_typeName;

        private FlowDirectionType m_direction;

        private TreeNode m_parent;

        private Connector m_inputConnector;

        private List<TreeNode> m_childNodes;

        private string m_info;

        private int m_isValue;

        public string FamilyName {
            get { return m_familyName; }
            set { m_familyName = value; }
        }

        public string TypeName {
            get { return m_typeName; }
            set { m_typeName = value; }
        }

        public string Info {
            get {
                return m_info;
            }
            set {
                m_info = value;
            }
        }

        public int IsValve {
            get {
                return m_isValue;
            }
            set {
                m_isValue = value;
            }
        }

        public ElementId Id {
            get {
                return m_Id;
            }
        }

        public FlowDirectionType Direction {
            get {
                return m_direction;
            }
            set {
                m_direction = value;
            }
        }

        public TreeNode Parent {
            get {
                return m_parent;
            }
            set {
                m_parent = value;
            }
        }

        public List<TreeNode> ChildNodes {
            get {
                return m_childNodes;
            }
            set {
                m_childNodes = value;
            }
        }

        public Connector InputConnector {
            get {
                return m_inputConnector;
            }
            set {
                m_inputConnector = value;
            }
        }
        #endregion

        public TreeNode(Autodesk.Revit.DB.ElementId id) {
            m_Id = id;
            m_childNodes = new List<TreeNode>();
        }
    }

    public class MtTravelsalTree {

        #region Member variables

        private Document m_document;

        private MEPSystem m_system;

        private TreeNode m_startingElementNode;

        private List<TreeNode> m_allTreeNodeList;

        private MtSQLite m_sqlite;

        private string m_message = null;
        public string Message {
            get {
                return m_message;
            }
        }

        private bool m_isHide;
        public bool IsHide {
            get {
                return m_isHide;
            }
        }
        #endregion

        #region Methods
        public MtTravelsalTree(Document activeDocument, MEPSystem system) {
            m_document = activeDocument;
            m_system = system;
            m_allTreeNodeList = new List<TreeNode>();
        }

        public void TraversePipe(Element selectEle, bool isSameSystemName = true, string multiSystem = null) {
            m_startingElementNode = GetStartingElementNode(selectEle);
            Traverse(m_startingElementNode, isSameSystemName, multiSystem);
        }

        public void SaveIntoDB(string sqlitePath, string param, bool isPositive, bool isWaterReturn) {
            string[] m_params = param.Split(',');
            string systemName = m_params[0];
            string subSystemName = m_params[1];
            string tunnelName = m_params[2];
            string tableName = m_params[3];
            string columnName = m_params[4];

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            DumpIntoDB2(sqlitePath, systemName, subSystemName, tunnelName, tableName, columnName, isPositive, isWaterReturn);
            //DumpIntoDB(sqlitePath, systemName, subSystemName, tunnelName, tableName, columnName, isPositive, isWaterReturn);
            stopwatch.Stop();
            //TaskDialog.Show("Msg", stopwatch.ElapsedMilliseconds.ToString());
        }

        public void DumpIntoDB2(string path, string systemName, string subSystem, string tunnelName,
            string tableName, string columnName, bool isPositiveDir, bool isWaterReturnPipe) {

            if (string.IsNullOrEmpty(path))
                return;
            m_sqlite = new MtSQLite(path);

            string[] _columnKey = columnName.Split(';');

            List<string[]> listColumnValues = new List<string[]>();
            foreach (var Node in m_allTreeNodeList) {
                string info = Node.Info;

                string[] _columnValue = new string[_columnKey.Length];
                _columnValue[0] = ResetPipeId(Node, systemName, Node.Id.ToString());   //ID
                                                                                       //if (string.IsNullOrEmpty(tunnelName))
                                                                                       //    _columnValue[3] = "'" + "Default" + "'"; //TUNNEL 默认为Default
                                                                                       //else
                _columnValue[3] = "'" + GetTunnelName(tunnelName) + "'";

                _columnValue[5] = Node.IsValve.ToString(); //IsValve : 0 ：非Valve  1: 是Valve
                _columnValue[6] = "'" + subSystem + "'"; //SUBSYSTEM : 通过下拉框选择
                _columnValue[7] = "'" + GetSubsystemCode(subSystem) + "'"; //subsystemCode
                _columnValue[8] = "'" + (IsEncodeDevice(info) ? 1 : 0).ToString() + "'"; //设备是否有编码
                _columnValue[9] = "'" + "0" + "'"; //是否是末端管道

                if (isPositiveDir) {
                    _columnValue[4] = "1"; //DIRECTION 1:正向 0：逆向

                    if (Node.Parent == null) {
                        _columnValue[1] = "'" + string.Empty + "'";

                        foreach (var child in Node.ChildNodes) {
                            //引用问题，所以用了临时变量
                            string[] tempColumnValue = CopyData(_columnValue);
                            tempColumnValue[2] = ResetPipeId(child, systemName, child.Id.ToString()); //DOWNSTREAM 
                            listColumnValues.Add(tempColumnValue);
                        }
                    } else {
                        _columnValue[1] = ResetPipeId(Node.Parent, systemName, Node.Parent.Id.ToString());  //UPSTREAM
                        if (Node.ChildNodes != null && Node.ChildNodes.Count != 0) {
                            foreach (var child in Node.ChildNodes) {
                                string[] tempColumnValue = CopyData(_columnValue);
                                tempColumnValue[2] = ResetPipeId(child, systemName, child.Id.ToString()); //DOWNSTREAM
                                listColumnValues.Add(tempColumnValue);
                            }
                        } else {
                            _columnValue[2] = "'" + string.Empty + "'";
                            _columnValue[9] = "'" + "1" + "'"; //是否是末端管道 1：是 0：不是
                            listColumnValues.Add(_columnValue);  //加入没有下游的记录
                        }
                    }
                } else {
                    _columnValue[4] = "0"; //DIRECTION 1:正向 0：逆向

                    if (Node.Parent == null) {
                        _columnValue[2] = "'" + string.Empty + "'";

                        foreach (var child in Node.ChildNodes) {
                            string[] tempColumnValue = CopyData(_columnValue);
                            tempColumnValue[1] = ResetPipeId(child, systemName, child.Id.ToString()); //加入逆向的初始管道
                            listColumnValues.Add(tempColumnValue);
                        }
                    } else {
                        _columnValue[2] = ResetPipeId(Node.Parent, systemName, Node.Parent.Id.ToString());  //DOWNSTREAM

                        if (Node.ChildNodes != null && Node.ChildNodes.Count != 0) {
                            foreach (var child in Node.ChildNodes) {
                                string[] tempColumnValue = CopyData(_columnValue);
                                tempColumnValue[1] = ResetPipeId(child, systemName, child.Id.ToString()); //UPSTREAM
                                listColumnValues.Add(tempColumnValue);
                            }
                        } else {
                            if (!isWaterReturnPipe) //回水的末端不加入数据库中，因为供水的管道已经写入
                            {
                                _columnValue[1] = "'" + string.Empty + "'";
                                _columnValue[9] = "'" + "1" + "'";
                                listColumnValues.Add(_columnValue);  //加入没有上游的记录
                            }
                        }
                    }
                }
            }
            m_sqlite.InsertIntoList(tableName, _columnKey, listColumnValues);
            m_isHide = true;
        }

        string[] CopyData(string[] data) {
            string[] tempdata = new string[data.Length];
            for (int i = 0; i < data.Length; i++) {
                tempdata[i] = data[i];
            }
            return tempdata;
        }

        //效率较低
        public void DumpIntoDB(string path, string systemName, string subSystem, string tunnelName,
            string tableName, string columnName, bool isPositiveDir, bool isWaterReturnPipe) {
            if (string.IsNullOrEmpty(path))
                return;
            m_sqlite = new MtSQLite(path);

            string[] _columnKey = columnName.Split(';'); //id,upstream,downstream
            string[] _columnValue = new string[10];

            foreach (var Node in m_allTreeNodeList) {
                string info = Node.Info;

                _columnValue[0] = ResetPipeId(Node, systemName, Node.Id.ToString());   //ID

                if (string.IsNullOrEmpty(tunnelName))
                    _columnValue[3] = "'" + "Default" + "'"; //TUNNEL 默认为Default
                else
                    _columnValue[3] = "'" + tunnelName + "'";

                _columnValue[5] = Node.IsValve.ToString(); //IsValve : 0 ：非Valve  1: 是Valve
                _columnValue[6] = "'" + subSystem + "'"; //SUBSYSTEM : 通过下拉框选择
                _columnValue[7] = "'" + GetSubsystemCode(subSystem) + "'"; //subsystemCode
                _columnValue[8] = "'" + (IsEncodeDevice(info) ? 1 : 0).ToString() + "'"; //设备是否有编码
                _columnValue[9] = "'" + "0" + "'"; //是否是末端管道
                if (isPositiveDir) {
                    _columnValue[4] = "1"; //DIRECTION 1:正向 0：逆向

                    if (Node.Parent == null) {
                        _columnValue[1] = "'" + string.Empty + "'";

                        foreach (var child in Node.ChildNodes) {
                            _columnValue[2] = ResetPipeId(child, systemName, child.Id.ToString()); //DOWNSTREAM
                            m_sqlite.InsertInto(tableName, _columnKey, _columnValue);
                        }
                    } else {
                        _columnValue[1] = ResetPipeId(Node.Parent, systemName, Node.Parent.Id.ToString());  //UPSTREAM
                        if (Node.ChildNodes != null && Node.ChildNodes.Count != 0) {
                            foreach (var child in Node.ChildNodes) {
                                _columnValue[2] = ResetPipeId(child, systemName, child.Id.ToString()); //DOWNSTREAM
                                m_sqlite.InsertInto(tableName, _columnKey, _columnValue);
                            }
                        } else {
                            _columnValue[2] = "'" + string.Empty + "'";
                            _columnValue[9] = "'" + "1" + "'"; //是否是末端管道 1：是 0：不是
                            m_sqlite.InsertInto(tableName, _columnKey, _columnValue); //加入没有下游的记录
                        }
                    }
                } else {
                    _columnValue[4] = "0"; //DIRECTION 1:正向 0：逆向

                    if (Node.Parent == null) {
                        _columnValue[2] = "'" + string.Empty + "'";

                        foreach (var child in Node.ChildNodes) {
                            _columnValue[1] = ResetPipeId(child, systemName, child.Id.ToString()); //加入逆向的初始管道
                            m_sqlite.InsertInto(tableName, _columnKey, _columnValue);
                        }
                    } else {
                        _columnValue[2] = ResetPipeId(Node.Parent, systemName, Node.Parent.Id.ToString());  //DOWNSTREAM

                        if (Node.ChildNodes != null && Node.ChildNodes.Count != 0) {
                            foreach (var child in Node.ChildNodes) {
                                _columnValue[1] = ResetPipeId(child, systemName, child.Id.ToString()); //UPSTREAM
                                m_sqlite.InsertInto(tableName, _columnKey, _columnValue);
                            }
                        } else {
                            if (!isWaterReturnPipe) //回水的末端不加入数据库中，因为供水的管道已经写入
                            {
                                _columnValue[1] = "'" + string.Empty + "'";
                                _columnValue[9] = "'" + "1" + "'";
                                m_sqlite.InsertInto(tableName, _columnKey, _columnValue); //加入没有上游的记录
                            }
                        }
                    }
                }
            }
            m_isHide = true;
        }

        public void HideTraverseElement(Document doc) {
            if (m_allTreeNodeList.Count == 0 || m_allTreeNodeList == null)
                return;
            foreach (var treenode in m_allTreeNodeList) {
                Element ele = MtCommon.GetElementById(doc, treenode.Id.ToString());
                MtCommon.HideElementTemporary(doc, ele);
            }
            m_allTreeNodeList.Clear();
        }

        public List<Element> TestCircuit(Element ele, bool isSameSystemName = true, string multiSystem = null, bool isIsolateElemnt = true) {
            m_startingElementNode = GetStartingElementNode(ele);
            return NoRecursionTravalPipe(m_startingElementNode, isSameSystemName, multiSystem, isIsolateElemnt);
        }


        private List<Element> NoRecursionTravalPipe(TreeNode eleNode, bool isSameSystemName = true, string multiSystem = null, bool isIsolateElemnt = true) {
            List<Element> errorListElemnts = new List<Element>();
            Queue<TreeNode> queue = new Queue<TreeNode>();
            queue.Enqueue(eleNode);
            Dictionary<string, TreeNode> m_dicTree = new Dictionary<string, TreeNode>();
            Element m_errorEle = null;

            List<string> systemNames = multiSystem.Split(';').ToList();

            while (queue.Count != 0) {
                //string content = File.ReadAllText(filepath);
                //content += index++ + ":   ";
                //foreach (var que in queue) {
                //    content += que.Id.ToString() + ", ";
                //}
                //content += "\r\n";
                //MtCommon.WriteIntText(content);

                TreeNode treeNode = queue.Dequeue();

                List<TreeNode> childNodes = treeNode.ChildNodes;
                Element selele = m_document.GetElement(treeNode.Id);

                m_dicTree.Add(selele.Id.ToString(), treeNode);

                ConnectorSet connectorSet = MtCommon.GetAllConnectors(selele);

                foreach (Connector connector in connectorSet) {
                    MEPSystem mepSystem = connector.MEPSystem;

                    if (isSameSystemName) {
                        Element ele = m_document.GetElement(m_startingElementNode.Id);
                        if (ele.Category.Name.Equals("风道末端") || ele.Category.Name.Equals("风管管件") ||
                            ele.Category.Name.Equals("风管") || ele.Category.Name.Equals("风管附件")) { //风管中可以存在设备两端系统名称中数字不一致的情况
                            if (mepSystem == null || !MtCommon.RemoveNumInString(mepSystem.Name).Equals(MtCommon.RemoveNumInString(m_system.Name))) continue;
                        } else {//管道的判断，需根据id判断，避免造成供回水，冷凝水全部检测到
                            if (mepSystem == null || !mepSystem.Id.IntegerValue.Equals(m_system.Id.IntegerValue)) continue;
                        }
                    } else { //此种情况是风管中不同系统用相同管道，排风排烟，新风送风，使用相同管道，但是流向是连续的。
                        if (multiSystem != null && multiSystem.Length != 0) {
                            if (mepSystem == null || !systemNames.Contains(MtCommon.RemoveNumInString(mepSystem.Name))) continue;
                        }
                        //if (mepSystem == null || (mepSystem is MechanicalSystem) && (m_system is PipingSystem) || (mepSystem is PipingSystem) && (m_system is MechanicalSystem))
                        //    continue;
                    }

                    if (treeNode.Parent == null) {
                        if (connector.IsConnected) {
                            treeNode.Direction = connector.Direction;
                        }
                    } else {
                        if (connector.IsConnectedTo(treeNode.InputConnector)) {
                            treeNode.Direction = connector.Direction;
                            continue;
                        }
                    }

                    Connector connectedConnector = MtCommon.GetConnectedConnector(connector);
                    if (connectedConnector != null) {
                        TreeNode node = new TreeNode(connectedConnector.Owner.Id);
                        node.InputConnector = connector;
                        node.Parent = treeNode;
                        node.Info = GetElementInfo(m_document.GetElement(node.Id));
                        node.IsValve = IsValve(m_document.GetElement(node.Id)) ? 1 : 0;
                        childNodes.Add(node);
                    }
                }

                childNodes.Sort(delegate (TreeNode t1, TreeNode t2) {
                    return t1.Id.IntegerValue > t2.Id.IntegerValue ? 1 : (t1.Id.IntegerValue < t2.Id.IntegerValue ? -1 : 0);
                }
                );

                foreach (TreeNode item in childNodes) {
                    if (!m_dicTree.ContainsKey(item.Id.ToString())) {  //队列中包含该节点 ，则说明重复了
                        if (!isSameNumber(queue, item))
                            queue.Enqueue(item);
                    } else {
                        m_errorEle = m_document.GetElement(item.Id);
                        if (!errorListElemnts.Contains(m_errorEle))
                            errorListElemnts.Add(m_errorEle);
                    }
                }
            }
            m_isHide = true;

            List<Element> isolatedElemets = new List<Element>();
            foreach (TreeNode item in m_dicTree.Values) {
                isolatedElemets.Add(m_document.GetElement(item.Id));
            }
            if (isIsolateElemnt) //将元素隔离
                IsolateElements(isolatedElemets);
            else {
                HideElements(isolatedElemets);
            }
            return errorListElemnts;
        }

        bool isSameNumber(Queue<TreeNode> queue, TreeNode tn) {
            if (queue != null && queue.Count != 0 && tn != null) {
                foreach (var item in queue) {
                    if (item.Id.ToString().Equals(tn.Id.ToString()))
                        return true;
                }
            }
            return false;
        }

        #endregion

        #region private
        private TreeNode GetStartingElementNode(Element selectElement) {
            TreeNode startingElementNode = null;

            startingElementNode = new TreeNode(selectElement.Id);
            startingElementNode.FamilyName = MtCommon.GetElementFamilyName(m_document, selectElement);
            startingElementNode.TypeName = MtCommon.GetElementType(m_document, selectElement);
            startingElementNode.Parent = null;
            startingElementNode.InputConnector = null;
            startingElementNode.Info = GetElementInfo(selectElement);
            startingElementNode.IsValve = IsValve(selectElement) ? 1 : 0;
            return startingElementNode;
        }

        private void Traverse(TreeNode elementNode, bool isSameSystemName = true, string multiSystem = null) {
            m_allTreeNodeList.Add(elementNode);

            AppendChildren(elementNode, isSameSystemName, multiSystem);
            foreach (TreeNode node in elementNode.ChildNodes) {
                Traverse(node, isSameSystemName, multiSystem);
            }
        }

        private void AppendChildren(TreeNode elementNode, bool isSameSystemName = true, string multiSystem = null) {
            List<TreeNode> nodes = elementNode.ChildNodes;

            Element element = m_document.GetElement(elementNode.Id);
            ConnectorSet connectors = MtCommon.GetAllConnectors(element);

            List<string> systemNames = multiSystem.Split(';').ToList();

            foreach (Connector connector in connectors) {
                MEPSystem mepSystem = connector.MEPSystem;

                if (isSameSystemName) {
                    Element ele = m_document.GetElement(m_startingElementNode.Id);
                    if (ele.Category.Name.Equals("风道末端") || ele.Category.Name.Equals("风管管件") ||
                        ele.Category.Name.Equals("风管") || ele.Category.Name.Equals("风管附件")) { //风管中可以存在设备两端系统名称中数字不一致的情况
                        if (mepSystem == null || !MtCommon.RemoveNumInString(mepSystem.Name).Equals(MtCommon.RemoveNumInString(m_system.Name))) continue;
                    } else {//管道的判断，需根据id判断，避免造成供回水，冷凝水全部检测到
                        if (mepSystem == null || !mepSystem.Id.IntegerValue.Equals(m_system.Id.IntegerValue)) continue;
                    }
                } else { //此种情况是风管中不同系统用相同管道，排风排烟，新风送风，使用相同管道，但是流向是连续的。

                    if (multiSystem != null && multiSystem.Length != 0) {
                        if (mepSystem == null || !systemNames.Contains(MtCommon.RemoveNumInString(mepSystem.Name))) continue;
                    }

                    //if (mepSystem == null || (mepSystem is MechanicalSystem) && (m_system is PipingSystem) || (mepSystem is PipingSystem) && (m_system is MechanicalSystem))
                    //    continue;
                }

                if (elementNode.Parent == null) {
                    if (connector.IsConnected) {
                        elementNode.Direction = connector.Direction;
                    }
                } else {
                    if (connector.IsConnectedTo(elementNode.InputConnector)) {
                        elementNode.Direction = connector.Direction;
                        continue;
                    }
                }

                Connector connectedConnector = MtCommon.GetConnectedConnector(connector);
                if (connectedConnector != null) {
                    TreeNode node = new TreeNode(connectedConnector.Owner.Id);
                    node.FamilyName = MtCommon.GetElementFamilyName(m_document, connectedConnector.Owner);
                    node.TypeName = MtCommon.GetElementType(m_document, connectedConnector.Owner);
                    node.InputConnector = connector;
                    node.Parent = elementNode;
                    node.Info = GetElementInfo(m_document.GetElement(node.Id));
                    node.IsValve = IsValve(m_document.GetElement(node.Id)) ? 1 : 0;
                    nodes.Add(node);
                }
            }

            nodes.Sort(delegate (TreeNode t1, TreeNode t2) {
                return t1.Id.IntegerValue > t2.Id.IntegerValue ? 1 : (t1.Id.IntegerValue < t2.Id.IntegerValue ? -1 : 0);
            }
            );
        }

        private string GetSubsystemCode(string subsystem) {
            return ((int)MtCommon.GetEnumValueByString<MtGlobals.EPSystem>(subsystem)).ToString();
        }

        private string GetTunnelName(string tunnelName) {
            return (MtCommon.GetEnumValueByString<MtGlobals.Tunnel>(tunnelName)).ToString();
        }

        private bool IsEncodeDevice(string info) {
            if (string.IsNullOrEmpty(info)) return false;
            if (!info.Split('*')[1].Equals("Null"))
                return true;
            else
                return false;
        }

        private string ResetPipeId(TreeNode node, string systemName, string id) {
            if (node == null || string.IsNullOrEmpty(systemName) || string.IsNullOrEmpty(id))
                return string.Empty;

            //string[] infos = info.Split('*'); //[0] : 院区+建筑+楼层  [1]:设备Code
            //if (!infos[1].Equals("Null")) {
            //    return "'" + infos[1] + "'";  //设备编码不为空，则将设备编码作为该管道（其实是设备）的Code;
            //} else {
            //    return "'" + infos[0] + "_" + systemName + "_" + id + "'"; //普通管道则编码为院区-建筑-楼层_系统_ID;
            //}


            string[] infos = node.Info.Split('*'); //[0] : 院区+建筑+楼层  [1]:是否是设备，0：不是，1是
            string buildName = infos[0].Substring(0, 10);
            string equipName = buildName + "-" + node.FamilyName + " " + node.TypeName + " [" + node.Id + "]";

            if (infos[1].Equals("1")) {
                return "'" + equipName + "'";
            } else {
                return "'" + infos[0] + "_" + systemName + "_" + id + "'";
            }
        }

        private string GetElementInfo(Element ele) {
            if (ele == null) return string.Empty;

            string area = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Distribute));
            string building = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.Building));
            string level = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.MtLevel));
            //string equipmentCode = MtCommon.GetOneParameter(ele, MtCommon.GetStringValue(MtGlobals.Parameters.EquipmentCode));

            string isEquip = "0";

            if (ele.Category.Name.Equals("机械设备")) {
                string familyName = MtCommon.GetElementFamilyName(m_document, ele);
                if (familyName.Contains("空调机组") || familyName.Contains("新风机组") || familyName.Contains("分集水器")) {
                    isEquip = "1";
                }
            }
            return area + "-" + building + "-" + level + "*" + isEquip;
        }

        private bool IsValve(Element ele) {
            if (ele == null) return false;
            string familyName = MtCommon.GetElementFamilyName(m_document, ele);
            if (!string.IsNullOrEmpty(familyName) && familyName.Contains("阀"))
                return true;
            else
                return false;
        }

        private void IsolateElements(ICollection<Element> elements) {
            List<ElementId> eleIds = new List<ElementId>();
            foreach (var item in elements) {
                eleIds.Add(item.Id);
            }
            MtCommon.IsolateElements(m_document, eleIds);
        }

        private void HideElements(ICollection<Element> elements) {
            List<ElementId> eleIds = new List<ElementId>();
            foreach (var item in elements) {
                eleIds.Add(item.Id);
            }
            MtCommon.HideElementsTemporary(m_document, eleIds);
        }
        #endregion
    }
}
