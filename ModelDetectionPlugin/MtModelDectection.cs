using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ModelDetectionPlugin {

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Automatic)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Journaling(Autodesk.Revit.Attributes.JournalingMode.NoCommandData)]
    public class MtModelDectection : IExternalCommand {

        UIApplication m_UIApp;
        UIDocument m_UIDoc;
        public virtual Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) {

            m_UIApp = commandData.Application;
            m_UIDoc = m_UIApp.ActiveUIDocument;

            MainPanel mainpanel = Ribbon.instance.GetMainPanel();
            mainpanel.Init(m_UIApp);

            string modelDectionGUID = "9202DA7D-2BFA-4445-A621-904EDB479DD8";
            Guid retval = Guid.Empty;

            try {
                retval = new Guid(modelDectionGUID);
            } catch (Exception) {
                throw;
            }

            DockablePaneId dockablePaneId = new DockablePaneId(retval);
            DockablePane dockablePane = m_UIApp.GetDockablePane(dockablePaneId);
            dockablePane.Show();

            return Result.Succeeded;
        }
    }
}
