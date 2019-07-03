using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ModelDetectionPlugin {

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Journaling(Autodesk.Revit.Attributes.JournalingMode.NoCommandData)]
    public class Ribbon : IExternalApplication {

        MainPanel m_modelDetectionPanel;
        public MainPanel ModelDetectionPanel {
            get { return m_modelDetectionPanel; }
        }
        internal static Ribbon instance = null;

        public Ribbon() {

        }
        public Result OnStartup(UIControlledApplication application) {
            instance = this;

            application.CreateRibbonTab(MtGlobals.DiagnosticTabName);
            RibbonPanel modelDectionPanel = application.CreateRibbonPanel(MtGlobals.DiagnosticTabName, MtGlobals.DiagnosticPanelName);
            modelDectionPanel.AddSeparator();


            PushButtonData pushButtonData = new PushButtonData(MtGlobals.ModelDetection, MtGlobals.ModelDetection,
            @"E:\5_RevitProject\ModelDetectionPlugin\ModelDetectionPlugin\bin\Debug\ModelDetectionPlugin.dll", "ModelDetectionPlugin.MtModelDectection");
            PushButton setCarportNum = modelDectionPanel.AddItem(pushButtonData) as PushButton;

            string m_mainPageGUID = "9202DA7D-2BFA-4445-A621-904EDB479DD8";
            m_modelDetectionPanel = new MainPanel();
            Guid guid = new Guid(m_mainPageGUID);

            DockablePaneId dockablePaneId = new DockablePaneId(guid);
            application.RegisterDockablePane(dockablePaneId, MtGlobals.ApplicationName, m_modelDetectionPanel as IDockablePaneProvider);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application) {

            
                

            return Result.Succeeded;
        }

        public MainPanel GetMainPanel() {
            if (!IsMainPanelAvailable())
                throw new InvalidOperationException("Main Window not Constructed!");
            return m_modelDetectionPanel;
        }

        public bool IsMainPanelAvailable() {
            if (m_modelDetectionPanel == null)
                return false;

            bool isAvailable = true;
            try {
                bool isVisible = m_modelDetectionPanel.IsVisible;
            } catch (Exception) {
                isAvailable = false;
            }
            return isAvailable;
        }

    }
}
