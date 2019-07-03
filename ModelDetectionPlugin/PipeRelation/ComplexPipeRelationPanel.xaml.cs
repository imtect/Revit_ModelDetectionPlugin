using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace ModelDetectionPlugin {
    /// <summary>
    /// ComplexPipeRelationPanel.xaml 的交互逻辑
    /// </summary>
    public partial class ComplexPipeRelationPanel : Window {

        UIDocument m_UIDoc;
        List<PipeRelationItem> m_pipeItem;
        public List<PipeRelationItem> PipeRelationItems {
            get { return m_pipeItem; }
        }


        public ComplexPipeRelationPanel() {
            InitializeComponent();
            m_pipeItem = new List<PipeRelationItem>();
            DataBinding();
        }

        public void InitDocument(UIDocument uidoc) {
            m_UIDoc = uidoc;
        }

        private void DataBinding() {

            for (int i = 0; i < 5; i++) {
                PipeRelationItem item = CreateEmptyPipeRelationItem(i);
                PipeRelationItems.Add(item);
            }
            SettingListView.ItemsSource = PipeRelationItems;
        }


        private void OKBtn_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e) {

        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e) {

        }

        PipeRelationItem CreateEmptyPipeRelationItem(int index ) {
            PipeRelationItem pipeItem = new PipeRelationItem();
            pipeItem.ID = index.ToString();
            pipeItem.Code = "Null";
            pipeItem.SubSystem = string.Empty;
            pipeItem.Direction = string.Empty;
            return pipeItem;
        }

        private void OperateBtn_Click(object sender, RoutedEventArgs e) {

            Button button = sender as Button;

            TaskDialog.Show("Msg", button.Uid);

            Selection selection = m_UIDoc.Selection;
            ICollection<ElementId> selectionIds = selection.GetElementIds();
            if (selectionIds.Count != 0 && selectionIds.Count == 1) {
                Element ele = null;
                foreach (var item in selectionIds) {
                    ele = m_UIDoc.Document.GetElement(item);
                }

            }
        }

        private void SubSysteCombox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }

        private void DirectionCombox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }
    }

    public class PipeRelationItem {
        public string ID;
        public string Code;
        public string SubSystem;
        public string Direction;
    }

    public enum PipeDirection {
        [StringValue("反向")]
        Negative,
        [StringValue("正向")]
        Positive,
    }

}
